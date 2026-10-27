using UnityEngine;

/// <summary>
/// "Sensacion de cuerpo": pequenos movimientos de la cabeza que hacen sentir que hay
/// una persona caminando, y no una camara flotando.
/// - Balanceo al caminar: la cabeza baja en cada paso y se mece a izquierda y derecha.
///   Usa el mismo contador que los pasos (StepCounter), asi el punto mas bajo coincide
///   con el sonido del pie.
/// - Respiracion: sube y baja despacio; con miedo, mas rapido y mas marcado.
/// - Inclinacion al girar: al girar rapido, la cabeza se inclina un poco hacia ese lado.
/// - Temblor: con miedo alto, un temblor chico e irregular.
///
/// Va en el objeto "Cabeza", entre Player y Main Camera:
///   Player (gira izquierda/derecha) > Cabeza (este script) > Main Camera (mira arriba/abajo)
/// Asi no se pisa con FirstPersonController, que ya maneja la rotacion de la camara.
/// </summary>
public class CameraFeel : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private StepCounter steps;
    [SerializeField] private FearMeter fear;
    [Tooltip("El cuerpo que gira (Player). Se usa para medir la velocidad de giro.")]
    [SerializeField] private Transform body;

    [Header("Balanceo al caminar")]
    [Tooltip("Cuanto baja la cabeza en cada paso, en metros.")]
    [SerializeField] private float bobHeight = 0.04f;
    [Tooltip("Cuanto se mece a los costados, en metros.")]
    [SerializeField] private float bobSide = 0.025f;
    [Tooltip("Velocidad (m/s) a la que el balanceo llega al maximo.")]
    [SerializeField] private float fullBobSpeed = 2f;
    [Tooltip("Que tan rapido aparece y desaparece el balanceo al empezar o frenar.")]
    [SerializeField] private float bobBlend = 6f;

    [Header("Respiracion")]
    [SerializeField] private float breathHeight = 0.006f;
    [Tooltip("Respiraciones por segundo sin miedo.")]
    [SerializeField] private float calmBreathRate = 0.22f;
    [Tooltip("Respiraciones por segundo con el miedo al maximo.")]
    [SerializeField] private float fearBreathRate = 0.6f;
    [Tooltip("Cuanto mas grande es la respiracion con miedo maximo (x veces).")]
    [SerializeField] private float fearBreathScale = 2.5f;

    [Header("Inclinacion al girar")]
    [Tooltip("Grados de inclinacion por cada grado/segundo de giro.")]
    [SerializeField] private float tiltPerTurnSpeed = 0.006f;
    [SerializeField] private float maxTilt = 1.5f;
    [SerializeField] private float tiltBlend = 8f;

    [Header("Temblor con miedo")]
    [Tooltip("Miedo a partir del cual empieza el temblor.")]
    [SerializeField, Range(0f, 1f)] private float tremorFrom = 0.6f;
    [Tooltip("Grados maximos de temblor.")]
    [SerializeField] private float tremorAngle = 0.35f;
    [SerializeField] private float tremorSpeed = 9f;

    private Vector3 basePosition;
    private float walkWeight;
    private float breathPhase;
    private float lastYaw;
    private float tilt;
    private float seed;

    /// <summary>Lo usa la herramienta de editor al armar la escena.</summary>
    public void Setup(StepCounter stepCounter, FearMeter fearMeter, Transform bodyTransform)
    {
        steps = stepCounter;
        fear = fearMeter;
        body = bodyTransform;
    }

    private void Awake()
    {
        basePosition = transform.localPosition;
        if (body == null) body = transform.parent;
        if (body != null) lastYaw = body.eulerAngles.y;
        seed = Random.value * 100f;
    }

    // LateUpdate: despues de que el jugador se movio y giro en este frame.
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // en pausa (timeScale 0) la cabeza queda quieta donde estaba

        float f = fear != null ? fear.Normalized : 0f;

        // 1. Balanceo. walkWeight va de 0 (quieto) a 1 (caminando): suaviza el arranque y el frenado.
        float targetWeight = steps != null ? Mathf.Clamp01(steps.Speed / fullBobSpeed) : 0f;
        walkWeight += (targetWeight - walkWeight) * (1f - Mathf.Exp(-bobBlend * dt));

        float progress = steps != null ? steps.StepProgress : 0f;
        int stepCount = steps != null ? steps.TotalSteps : 0;
        // down = 1 justo al apoyar el pie (progress 0) y 0 a mitad del paso.
        float down = (1f + Mathf.Cos(2f * Mathf.PI * progress)) * 0.5f;
        // Cada paso cambia de pie: el seno pasa de positivo a negativo en pasos alternos.
        float side = Mathf.Sin(Mathf.PI * (stepCount + progress));
        Vector3 bob = new Vector3(side * bobSide, -down * bobHeight, 0f) * walkWeight;

        // 2. Respiracion.
        float rate = Mathf.Lerp(calmBreathRate, fearBreathRate, f);
        breathPhase += rate * dt * 2f * Mathf.PI;
        float breath = Mathf.Sin(breathPhase) * breathHeight * Mathf.Lerp(1f, fearBreathScale, f);

        // 3. Inclinacion: velocidad de giro del cuerpo, en grados por segundo.
        float yaw = body != null ? body.eulerAngles.y : 0f;
        float turnSpeed = Mathf.DeltaAngle(lastYaw, yaw) / dt;
        lastYaw = yaw;
        float targetTilt = Mathf.Clamp(-turnSpeed * tiltPerTurnSpeed, -maxTilt, maxTilt);
        tilt += (targetTilt - tilt) * (1f - Mathf.Exp(-tiltBlend * dt));

        // 4. Temblor: ruido Perlin (suave pero irregular), solo con miedo alto.
        float tremor = Mathf.InverseLerp(tremorFrom, 1f, f) * tremorAngle;
        float t = Time.time * tremorSpeed;
        float tremorX = (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f * tremor;
        float tremorY = (Mathf.PerlinNoise(seed + 31.7f, t) - 0.5f) * 2f * tremor;

        transform.localPosition = basePosition + bob + Vector3.up * breath;
        transform.localRotation = Quaternion.Euler(tremorX, tremorY, tilt);
    }
}
