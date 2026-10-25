using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Convierte los modulos del Laberinto en una "cinta" infinita.
/// Cuando quedan menos de segmentsAhead modulos delante del jugador, toma el modulo
/// de mas atras (solo si esta en punto ciego), lo reinicia y lo coloca al frente.
/// La niebla oculta el extremo del pasillo, asi el jugador nunca ve aparecer nada.
///
/// El final: cuando alguien llama a RequestExit(), la cinta deja de reciclar y coloca
/// el modulo de salida a continuacion del ultimo modulo. Quien decide CUANDO se llama
/// es otro script (ExitCondition), para poder cambiar la regla sin tocar la cinta.
///
/// Va en el objeto "Laberinto", junto a VisibilityChecker y MazeDirector.
/// Por ahora el pasillo es recto, a lo largo del eje Z (azul) del Laberinto.
/// </summary>
public class CorridorTreadmill : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El objeto Player.")]
    [SerializeField] private Transform player;
    [Tooltip("Si se deja vacio, usa el VisibilityChecker de este mismo objeto.")]
    [SerializeField] private VisibilityChecker visibility;
    [Tooltip("Modulo de salida. Va FUERA de Laberinto; este script lo apaga al empezar.")]
    [SerializeField] private GameObject exitModule;
    [Tooltip("Largo del modulo de salida en metros.")]
    [SerializeField] private float exitModuleLength = 6f;

    [Header("Cinta")]
    [Tooltip("Modulos que siempre tiene que haber delante del modulo donde esta el jugador.")]
    [SerializeField] private int segmentsAhead = 4;

    [Header("Eventos")]
    [Tooltip("Cada vez que un modulo se recicla hacia adelante.")]
    public UnityEvent onSegmentRecycled;
    [Tooltip("Cuando la salida queda colocada al final del pasillo.")]
    public UnityEvent onExitPlaced;

    public bool ExitRequested { get; private set; }
    public bool ExitPlaced { get; private set; }
    public int RecycledCount { get; private set; }

    // Los modulos ordenados de atras (indice 0) hacia adelante (ultimo indice).
    private readonly List<CorridorSegment> ordered = new List<CorridorSegment>();
    private Vector3 axis;

    // Start y no Awake: asi VisibilityChecker ya lleno su lista de modulos en su Awake.
    private void Start()
    {
        if (visibility == null) visibility = GetComponent<VisibilityChecker>();

        if (player == null || visibility == null)
        {
            Debug.LogError("[CorridorTreadmill] Falta asignar Player o Visibility en el Inspector.", this);
            enabled = false;
            return;
        }

        axis = transform.forward;
        axis.y = 0f;
        axis.Normalize();

        ordered.AddRange(visibility.Segments);
        ordered.Sort((a, b) => Along(a.transform.position).CompareTo(Along(b.transform.position)));

        if (exitModule != null) exitModule.SetActive(false);

        Debug.Log($"[CorridorTreadmill] Cinta lista con {ordered.Count} modulos.");
    }

    // Que tan adelante esta un punto, medido sobre el eje del pasillo (producto punto).
    private float Along(Vector3 point) => Vector3.Dot(point - transform.position, axis);

    private void Update()
    {
        if (ExitPlaced || ordered.Count < 2) return;

        if (ExitRequested)
        {
            PlaceExit();
            return;
        }

        int playerIndex = FindPlayerIndex();
        int ahead = ordered.Count - 1 - playerIndex;
        if (ahead >= segmentsAhead) return;

        // Solo se mueve el de mas atras si el jugador no lo esta viendo.
        // Si esta mirando hacia atras, la cinta espera.
        CorridorSegment rear = ordered[0];
        if (rear.State != SegmentVisibility.BlindSpot) return;

        Recycle(rear);
    }

    private int FindPlayerIndex()
    {
        float playerAlong = Along(player.position);
        int best = 0;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < ordered.Count; i++)
        {
            float d = Mathf.Abs(Along(ordered[i].transform.position) - playerAlong);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        return best;
    }

    private void Recycle(CorridorSegment segment)
    {
        CorridorSegment front = ordered[ordered.Count - 1];

        // Centro a centro: medio modulo del de adelante + medio modulo del que llega.
        float offset = (front.Length + segment.Length) * 0.5f;
        segment.transform.SetPositionAndRotation(
            front.transform.position + axis * offset,
            front.transform.rotation);

        segment.ResetForReuse();

        ordered.RemoveAt(0);
        ordered.Add(segment);

        RecycledCount++;
        Debug.Log($"[CorridorTreadmill] '{segment.name}' reciclado al frente (reciclados: {RecycledCount}).");
        onSegmentRecycled?.Invoke();
    }

    /// <summary>
    /// Pide terminar el loop. La cinta deja de reciclar y coloca la salida
    /// despues del ultimo modulo, mas alla de la niebla.
    /// </summary>
    public void RequestExit()
    {
        if (ExitRequested) return;
        ExitRequested = true;
        Debug.Log("[CorridorTreadmill] Salida solicitada.");
    }

    private void PlaceExit()
    {
        ExitPlaced = true;

        if (exitModule == null)
        {
            Debug.LogWarning("[CorridorTreadmill] Se pidio la salida pero no hay Exit Module asignado.", this);
            return;
        }

        CorridorSegment front = ordered[ordered.Count - 1];
        float offset = (front.Length + exitModuleLength) * 0.5f;
        exitModule.transform.SetPositionAndRotation(
            front.transform.position + axis * offset,
            front.transform.rotation);
        exitModule.SetActive(true);

        Debug.Log("[CorridorTreadmill] Salida colocada al final del pasillo.");
        onExitPlaced?.Invoke();
    }
}
