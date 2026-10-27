using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cuenta "pasos" segun la distancia horizontal real recorrida por este objeto.
/// - Un paso = cada stepLength metros.
/// - Racha continua: si el jugador se queda quieto mas de pauseToReset segundos, la racha vuelve a 0.
/// - Al llegar a stepsToTrigger pasos seguidos, dispara onStreakReached y la racha reinicia.
/// No sabe nada del controlador: solo mira la posicion. Por eso sirve con cualquier forma de moverse.
/// </summary>
public class StepCounter : MonoBehaviour
{
    [Header("Que es un paso")]
    [Tooltip("Metros recorridos que cuentan como un paso.")]
    [SerializeField] private float stepLength = 0.8f;
    [Tooltip("Velocidad minima (m/s) para considerar que el jugador esta caminando.")]
    [SerializeField] private float minSpeedToCount = 0.5f;
    [Tooltip("Segundos quieto que rompen la racha.")]
    [SerializeField] private float pauseToReset = 0.75f;
    [Tooltip("Si en un solo frame se mueve mas que esto (teletransporte), ese frame se ignora.")]
    [SerializeField] private float teleportThreshold = 1.5f;

    [Header("Disparador")]
    [Tooltip("Pasos seguidos necesarios para disparar onStreakReached.")]
    [SerializeField] private int stepsToTrigger = 12;

    [Header("Eventos")]
    [Tooltip("Cada paso. Envia el numero de paso dentro de la racha actual.")]
    public UnityEvent<int> onStep;
    [Tooltip("Se completo la racha: aca se conectara el director del laberinto.")]
    public UnityEvent onStreakReached;
    [Tooltip("El jugador se detuvo y perdio la racha.")]
    public UnityEvent onStreakBroken;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;

    public int CurrentStreak { get; private set; }
    public int TotalSteps { get; private set; }
    public int StepsToTrigger => stepsToTrigger;
    /// <summary>Velocidad horizontal de este frame, en m/s. La usa CameraFeel.</summary>
    public float Speed { get; private set; }
    /// <summary>
    /// Cuanto se avanzo hacia el proximo paso: 0 justo al dar un paso, casi 1 justo antes del siguiente.
    /// CameraFeel lo usa para que el balanceo de la cabeza caiga en el mismo instante que el sonido.
    /// </summary>
    public float StepProgress => stepLength > 0f ? distanceAccumulator / stepLength : 0f;

    private Vector3 lastPosition;
    private float distanceAccumulator;
    private float stillTimer;

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        // 1. Cuanto se movio en horizontal desde el frame anterior.
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;

        float distance = delta.magnitude;

        // 2. Un salto enorme en un frame es un teletransporte, no una caminata.
        if (distance > teleportThreshold)
        {
            Speed = 0f;
            return;
        }

        float speed = distance / Mathf.Max(Time.deltaTime, 0.0001f);
        Speed = speed;

        // 3. Quieto: contar cuanto tiempo; si pasa el limite, se rompe la racha.
        if (speed < minSpeedToCount)
        {
            stillTimer += Time.deltaTime;
            if (stillTimer >= pauseToReset && (CurrentStreak > 0 || distanceAccumulator > 0f))
            {
                BreakStreak();
            }
            return;
        }

        // 4. Caminando: acumular distancia y convertirla en pasos.
        stillTimer = 0f;
        distanceAccumulator += distance;

        // while y no if: en un frame lento podrian haberse recorrido dos pasos.
        while (distanceAccumulator >= stepLength)
        {
            distanceAccumulator -= stepLength;
            RegisterStep();
        }
    }

    private void RegisterStep()
    {
        CurrentStreak++;
        TotalSteps++;
        onStep?.Invoke(CurrentStreak);

        if (CurrentStreak >= stepsToTrigger)
        {
            Debug.Log($"[StepCounter] Racha de {stepsToTrigger} pasos alcanzada. Total: {TotalSteps}");
            onStreakReached?.Invoke();
            CurrentStreak = 0;
        }
    }

    private void BreakStreak()
    {
        bool hadSteps = CurrentStreak > 0;
        CurrentStreak = 0;
        distanceAccumulator = 0f;

        if (hadSteps)
        {
            Debug.Log("[StepCounter] Racha rota: el jugador se detuvo.");
            onStreakBroken?.Invoke();
        }
    }

    /// <summary>Reinicia la racha a mano (util despues de un evento de guion).</summary>
    public void ResetStreak()
    {
        CurrentStreak = 0;
        distanceAccumulator = 0f;
        stillTimer = 0f;
        lastPosition = transform.position;
    }

    // Texto en pantalla sin necesidad de armar un Canvas. Solo para el prototipo.
    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible) return;
        GUI.Label(new Rect(12, 12, 400, 22), $"Racha: {CurrentStreak} / {stepsToTrigger}");
        GUI.Label(new Rect(12, 34, 400, 22), $"Pasos totales: {TotalSteps}");
    }
}
