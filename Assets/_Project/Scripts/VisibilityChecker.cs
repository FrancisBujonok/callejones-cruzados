using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Evalua cada frame que modulos del laberinto ve el jugador.
/// Va en el objeto padre "Laberinto"; encuentra solo a los CorridorSegment hijos.
///
/// Reglas para CAMBIAR un modulo (conservadoras):
/// - Protegido: el jugador esta adentro o a menos de minDistance. Nunca se cambia.
/// - Visible: la caja del modulo, agrandada por safetyMargin, toca el frustum de la camara.
/// - Punto ciego: todo lo demas.
///
/// Regla para DESCUBRIR un cambio (estricta):
/// - La caja real del modulo toca el frustum Y hay linea de vision directa a su centro.
/// </summary>
public class VisibilityChecker : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La camara del jugador (Main Camera, hija de Player).")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("El objeto Player.")]
    [SerializeField] private Transform player;

    [Header("Reglas para cambiar")]
    [Tooltip("Metros extra alrededor de cada modulo: margen para la vision periferica y los giros rapidos.")]
    [SerializeField] private float safetyMargin = 1f;
    [Tooltip("Los modulos a menos de esta distancia del jugador nunca se consideran cambiables.")]
    [SerializeField] private float minDistance = 4f;

    [Header("Descubrimiento")]
    [Tooltip("Exige linea de vision directa al centro del modulo para contar un cambio como descubierto.")]
    [SerializeField] private bool requireLineOfSight = true;
    [Tooltip("Se dispara cuando el jugador ve un modulo que cambio a sus espaldas.")]
    public UnityEvent onChangeDiscovered;

    public int DiscoveredCount { get; private set; }
    public IReadOnlyList<CorridorSegment> Segments => segments;

    private readonly List<CorridorSegment> segments = new List<CorridorSegment>();
    private readonly Plane[] frustumPlanes = new Plane[6];

    private void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;

        if (playerCamera == null || player == null)
        {
            Debug.LogError("[VisibilityChecker] Falta asignar Player Camera o Player en el Inspector.", this);
            enabled = false;
            return;
        }

        GetComponentsInChildren(true, segments); // true = incluye hijos inactivos
        Debug.Log($"[VisibilityChecker] {segments.Count} modulos encontrados.");
    }

    // LateUpdate corre despues de todos los Update: la camara ya giro en este frame.
    private void LateUpdate()
    {
        RefreshFrustum();

        foreach (CorridorSegment s in segments)
        {
            // Las piezas guardadas (apagadas) por el generador no cuentan.
            if (!s.gameObject.activeInHierarchy)
            {
                s.State = SegmentVisibility.Unknown;
                continue;
            }

            s.State = Evaluate(s, out bool inView);

            if (s.HasUnseenChange && inView && HasLineOfSight(s))
            {
                s.MarkDiscovered();
                DiscoveredCount++;
                Debug.Log($"[VisibilityChecker] Cambio descubierto en '{s.name}'.");
                onChangeDiscovered?.Invoke();
            }
        }
    }

    private void RefreshFrustum()
    {
        GeometryUtility.CalculateFrustumPlanes(playerCamera, frustumPlanes);
    }

    /// <summary>
    /// Devuelve el estado para decidir si se puede cambiar.
    /// inView: la caja REAL (sin margen) toca el frustum. Se usa para el descubrimiento.
    /// Usa los planos ya calculados: llamar a RefreshFrustum antes.
    /// </summary>
    private SegmentVisibility Evaluate(CorridorSegment segment, out bool inView)
    {
        Bounds b = segment.GetWorldBounds();
        inView = GeometryUtility.TestPlanesAABB(frustumPlanes, b);

        // SqrDistance devuelve 0 si el punto esta dentro de la caja.
        if (b.SqrDistance(player.position) < minDistance * minDistance)
            return SegmentVisibility.Protected;

        Bounds safe = b;
        safe.Expand(safetyMargin * 2f); // Expand suma al tamano total, por eso x2

        return GeometryUtility.TestPlanesAABB(frustumPlanes, safe)
            ? SegmentVisibility.Visible
            : SegmentVisibility.BlindSpot;
    }

    /// <summary>
    /// Tira una linea invisible desde la camara al centro del modulo.
    /// Si no choca con nada, o lo primero que toca es parte del propio modulo, se ve.
    /// Si choca antes con otra cosa (por ejemplo el Bloqueo de otro modulo), esta tapado.
    /// </summary>
    private bool HasLineOfSight(CorridorSegment segment)
    {
        if (!requireLineOfSight) return true;

        Vector3 from = playerCamera.transform.position;
        Vector3 to = segment.GetWorldBounds().center;

        if (!Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return true;

        return hit.transform.IsChildOf(segment.transform);
    }

    /// <summary>Consulta puntual para un solo modulo.</summary>
    public bool IsInBlindSpot(CorridorSegment segment)
    {
        RefreshFrustum();
        return Evaluate(segment, out _) == SegmentVisibility.BlindSpot;
    }

    /// <summary>
    /// Para el generador de caminos: si una pieza puesta en esa caja podria verse aparecer.
    /// Se ve si esta dentro del campo de vision, mas cerca que maxDistance (despues la niebla
    /// la tapa) y no hay ninguna pared entre la camara y su centro.
    /// </summary>
    public bool CouldBeSeen(Bounds box, float maxDistance)
    {
        RefreshFrustum();
        Bounds safe = box;
        safe.Expand(safetyMargin * 2f);
        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, safe)) return false;

        Vector3 from = playerCamera.transform.position;
        if (box.SqrDistance(from) > maxDistance * maxDistance) return false;

        // Si algo tapa la linea hasta el centro, no se ve.
        return !Physics.Linecast(from, box.center, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Devuelve los modulos en punto ciego. Con onlyBehind = true, solo los que
    /// quedan a la espalda de la camara (producto punto negativo).
    /// </summary>
    public List<CorridorSegment> GetBlindSpotSegments(bool onlyBehind = true)
    {
        RefreshFrustum();

        Vector3 camPos = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        var result = new List<CorridorSegment>();
        foreach (CorridorSegment s in segments)
        {
            if (!s.gameObject.activeInHierarchy) continue;
            if (Evaluate(s, out _) != SegmentVisibility.BlindSpot) continue;

            if (onlyBehind)
            {
                Vector3 toSegment = s.GetWorldBounds().center - camPos;
                toSegment.y = 0f;
                // Dot: 1 = justo adelante, 0 = al costado, -1 = justo atras.
                if (Vector3.Dot(forward, toSegment.normalized) >= 0f) continue;
            }

            result.Add(s);
        }
        return result;
    }
}
