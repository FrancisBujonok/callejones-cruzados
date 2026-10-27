using UnityEngine;

/// <summary>
/// Decide CUANDO termina el loop. Cuando la condicion se cumple, le pide la salida
/// a CorridorTreadmill. Es una pieza aparte a proposito: la regla del final todavia
/// no esta decidida, y asi se puede cambiar (o reemplazar este script entero)
/// sin tocar la cinta ni la salida.
///
/// Modos de prueba:
/// - Manual: solo con el menu "Forzar salida" o llamando a Trigger() desde un evento.
/// - Discoveries: el jugador descubrio 'target' cambios del laberinto.
/// - TotalSteps: el jugador camino 'target' pasos en total.
/// - CalmSteps: el jugador camino 'target' pasos seguidos SIN mirar atras.
///
/// Va en el objeto "Laberinto".
/// </summary>
public class ExitCondition : MonoBehaviour
{
    public enum Mode
    {
        Manual,
        Discoveries,
        TotalSteps,
        CalmSteps
    }

    [Header("Como termina el loop")]
    [SerializeField] private Mode mode = Mode.Discoveries;
    [Tooltip("Cantidad a alcanzar segun el modo: cambios descubiertos, pasos totales o pasos sin mirar atras.")]
    [SerializeField] private int target = 3;

    [Header("Referencias")]
    [Tooltip("El objeto Player. De ahi se toman StepCounter y ParanoiaSensor.")]
    [SerializeField] private GameObject player;
    [Tooltip("Si se deja vacio, usa el de este mismo objeto.")]
    [SerializeField] private CorridorTreadmill treadmill;
    [Tooltip("Si se deja vacio, usa el de este mismo objeto.")]
    [SerializeField] private VisibilityChecker visibility;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;

    public bool Done { get; private set; }

    private StepCounter steps;
    private ParanoiaSensor paranoia;
    private int calmSteps;

    private void Awake()
    {
        if (treadmill == null) treadmill = GetComponent<CorridorTreadmill>();
        if (visibility == null) visibility = GetComponent<VisibilityChecker>();

        if (player != null)
        {
            steps = player.GetComponent<StepCounter>();
            paranoia = player.GetComponent<ParanoiaSensor>();
        }

        if (treadmill == null)
        {
            Debug.LogError("[ExitCondition] No hay CorridorTreadmill en este objeto.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (steps != null) steps.onStep.AddListener(OnStep);
        if (paranoia != null) paranoia.onLookBack.AddListener(OnLookBack);
    }

    private void OnDisable()
    {
        if (steps != null) steps.onStep.RemoveListener(OnStep);
        if (paranoia != null) paranoia.onLookBack.RemoveListener(OnLookBack);
    }

    private void OnStep(int streak) => calmSteps++;
    private void OnLookBack() => calmSteps = 0;

    private void Update()
    {
        if (!Done && Progress() >= target && mode != Mode.Manual) Trigger();
    }

    private int Progress() => mode switch
    {
        Mode.Discoveries => visibility != null ? visibility.DiscoveredCount : 0,
        Mode.TotalSteps => steps != null ? steps.TotalSteps : 0,
        Mode.CalmSteps => calmSteps,
        _ => 0
    };

    /// <summary>Cumple la condicion a la fuerza. Tambien aparece en el menu de tres puntos.</summary>
    [ContextMenu("Forzar salida")]
    public void Trigger()
    {
        if (Done || treadmill == null) return;
        Done = true;
        Debug.Log($"[ExitCondition] Condicion cumplida ({mode}). Se abre la salida.");
        treadmill.RequestExit();
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible) return;
        string text = Done
            ? "Salida: abierta"
            : mode == Mode.Manual
                ? "Salida: modo manual"
                : $"Salida ({mode}): {Mathf.Min(Progress(), target)} / {target}";
        GUI.Label(new Rect(12, 122, 400, 22), text);
    }
}
