using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decide que modulos del laberinto estan en punto ciego para la camara del jugador.
/// Va en el objeto padre "Laberinto"; encuentra solo a los CorridorSegment hijos.
///
/// Reglas:
/// - Protegido: el jugador esta adentro o a menos de minDistance. Nunca se cambia.
/// - Visible: la caja del modulo (agrandada por safetyMargin) toca el frustum de la camara.
/// - Punto ciego: todo lo demas.
/// </summary>
public class VisibilityChecker : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La camara del jugador (Main Camera, hija de Player).")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("El objeto Player.")]
    [SerializeField] private Transform player;

    [Header("Reglas")]
    [Tooltip("Metros extra alrededor de cada modulo: margen para la vision periferica y los giros rapidos.")]
    [SerializeField] private float safetyMargin = 1f;
    [Tooltip("Los modulos a menos de esta distancia del jugador nunca se consideran cambiables.")]
    [SerializeField] private float minDistance = 4f;

    [Header("Debug")]
    [Tooltip("Evalua todos los modulos cada frame para pintar los gizmos en la vista Scene.")]
    [SerializeField] private bool updateDebugEveryFrame = true;

    private readonly List<CorridorSegment> segments = new List<CorridorSegment>();
    private readonly Plane[] frustumPlanes = new Plane[6];

    public IReadOnlyList<CorridorSegment> Segments => segments;

    private void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;

        if (playerCamera == null || player == null)
        {
            Debug.LogError("[VisibilityChecker] Falta asignar Player Camera o Player en el Inspector.", this);
            enabled = false;
            return;
        }

        // true = incluye hijos inactivos
        GetComponentsInChildren(true, segments);
        Debug.Log($"[VisibilityChecker] {segments.Count} modulos encontrados.");
    }

    // LateUpdate corre despues de todos los Update: para este momento la camara
    // ya giro en este frame, asi evaluamos con la orientacion final.
    private void LateUpdate()
    {
        if (!updateDebugEveryFrame) return;

        RefreshFrustum();
        foreach (CorridorSegment s in segments)
        {
            s.DebugState = Evaluate(s);
        }
    }

    private void RefreshFrustum()
    {
        GeometryUtility.CalculateFrustumPlanes(playerCamera, frustumPlanes);
    }

    // Usa los planos ya calculados. Llamar a RefreshFrustum antes.
    private SegmentVisibility Evaluate(CorridorSegment segment)
    {
        Bounds b = segment.GetWorldBounds();

        // SqrDistance devuelve 0 si el punto esta dentro de la caja.
        if (b.SqrDistance(player.position) < minDistance * minDistance)
            return SegmentVisibility.Protected;

        b.Expand(safetyMargin * 2f); // Expand suma al tamano total, por eso x2

        return GeometryUtility.TestPlanesAABB(frustumPlanes, b)
            ? SegmentVisibility.Visible
            : SegmentVisibility.BlindSpot;
    }

    /// <summary>Consulta puntual para un solo modulo.</summary>
    public bool IsInBlindSpot(CorridorSegment segment)
    {
        RefreshFrustum();
        return Evaluate(segment) == SegmentVisibility.BlindSpot;
    }

    /// <summary>
    /// Devuelve los modulos en punto ciego. Con onlyBehind = true, solo los que
    /// quedan a la espalda de la camara (producto punto negativo). Lo usara el Paso 3.
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
            if (Evaluate(s) != SegmentVisibility.BlindSpot) continue;

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
