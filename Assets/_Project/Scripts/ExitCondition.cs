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
/// Paso 24 (ritmo de la partida):
/// - Ademas del modo, hace falta un minimo de pasos totales (minTotalSteps). Un jugador que gira
///   sin parar ya no sale en 15-20 s: la ciudad lo retiene al menos unos 300 m.
/// - Descubrimientos espaciados: para la salida, un descubrimiento cuenta solo si se caminaron
///   minMetersBetween metros desde el ultimo que conto. Una mirada atras que descubre tres piezas
///   juntas cuenta una vez. El miedo y el sonido no cambian: siguen sumando por cada uno.
/// - Escucha VisibilityChecker.onChangeDiscovered, asi que la trampa de la plaza tambien cuenta.
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
    [Tooltip("Cantidad a alcanzar segun el modo: cambios descubiertos (que cuentan), pasos totales o pasos sin mirar atras.")]
    [SerializeField] private int target = 4;
    [Tooltip("Paso 24: pasos totales minimos para que se abra la salida, aunque ya se cumpla lo del modo (375 pasos = 300 m).")]
    [SerializeField] private int minTotalSteps = 375;
    [Tooltip("Paso 24: metros a caminar despues de un descubrimiento que conto para que el siguiente tambien cuente.")]
    [SerializeField] private float minMetersBetween = 25f;

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
    /// <summary>Descubrimientos que contaron para la salida (los espaciados).</summary>
    public int CountedDiscoveries { get; private set; }
    /// <summary>Descubrimientos que no contaron por estar demasiado cerca del anterior.</summary>
    public int SkippedDiscoveries { get; private set; }
    /// <summary>Metros que faltan para que el proximo descubrimiento cuente (0 = ya cuenta).</summary>
    public float MetersUntilNextCounts =>
        CountedDiscoveries == 0 ? 0f : Mathf.Max(0f, lastCountedAt + minMetersBetween - Walked());
    public bool StepsReached => steps == null || steps.TotalSteps >= minTotalSteps;

    private StepCounter steps;
    private ParanoiaSensor paranoia;
    private int calmSteps;
    private float lastCountedAt;

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

    // GameFlow apaga este componente durante la caminata del titulo: apagado no escucha nada,
    // asi que lo que pase en el titulo no cuenta.
    private void OnEnable()
    {
        if (steps != null) steps.onStep.AddListener(OnStep);
        if (paranoia != null) paranoia.onLookBack.AddListener(OnLookBack);
        if (visibility != null) visibility.onChangeDiscovered.AddListener(OnDiscovery);
    }

    private void OnDisable()
    {
        if (steps != null) steps.onStep.RemoveListener(OnStep);
        if (paranoia != null) paranoia.onLookBack.RemoveListener(OnLookBack);
        if (visibility != null) visibility.onChangeDiscovered.RemoveListener(OnDiscovery);
    }

    private void OnStep(int streak) => calmSteps++;
    private void OnLookBack() => calmSteps = 0;

    /// <summary>Metros caminados en total, segun los pasos.</summary>
    private float Walked() => steps != null ? steps.TotalSteps * steps.StepLength : 0f;

    /// <summary>Cada cambio descubierto (modulo o plaza). Solo cuenta si esta lejos del anterior.</summary>
    private void OnDiscovery()
    {
        if (Done) return;
        float walked = Walked();
        if (CountedDiscoveries > 0 && walked - lastCountedAt < minMetersBetween)
        {
            SkippedDiscoveries++;
            Debug.Log($"[ExitCondition] Descubrimiento que no cuenta para la salida: faltaban {lastCountedAt + minMetersBetween - walked:0} m.");
            return;
        }
        CountedDiscoveries++;
        lastCountedAt = walked;
        Debug.Log($"[ExitCondition] Descubrimiento {CountedDiscoveries}/{target} para la salida (a los {walked:0} m).");
    }

    private void Update()
    {
        if (!Done && mode != Mode.Manual && Progress() >= target && StepsReached) Trigger();
    }

    private int Progress() => mode switch
    {
        Mode.Discoveries => CountedDiscoveries,
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
        GUI.Label(new Rect(12, 122, 800, 22), DebugText());
    }

    private string DebugText()
    {
        if (Done) return "Salida: abierta";
        if (mode == Mode.Manual) return "Salida: modo manual";

        int totalSteps = steps != null ? steps.TotalSteps : 0;
        string stepsText = $"pasos {Mathf.Min(totalSteps, minTotalSteps)}/{minTotalSteps}";
        if (mode != Mode.Discoveries)
            return $"Salida ({mode}): {Mathf.Min(Progress(), target)}/{target} · {stepsText}";

        float wait = MetersUntilNextCounts;
        string next = CountedDiscoveries >= target ? "listo"
            : wait > 0f ? $"el proximo cuenta en {wait:0} m" : "el proximo cuenta";
        return $"Salida: descubrimientos {Mathf.Min(CountedDiscoveries, target)}/{target} ({next}) · {stepsText}";
    }
}
