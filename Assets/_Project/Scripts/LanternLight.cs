using UnityEngine;

/// <summary>
/// Farol de papel. Hace dos cosas:
/// - Parpadeo: la luz y el brillo del papel varian suavemente (ruido Perlin).
///   Con miedo alto el parpadeo es mas fuerte y aparecen cortes breves.
/// - Balanceo: el farol se mece un poco, colgado de su punto de enganche.
/// El pivote de este objeto es el punto donde cuelga (arriba de la cuerda).
/// Lee el miedo de FearMeter si hay uno en la escena; si no, parpadea en modo calma.
/// </summary>
public class LanternLight : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La luz puntual dentro del farol.")]
    [SerializeField] private Light lanternLight;
    [Tooltip("El cuerpo de papel del farol (su material tiene emision).")]
    [SerializeField] private Renderer glowRenderer;

    [Header("Brillo del papel")]
    [SerializeField] private Color emissionColor = new Color(1f, 0.2f, 0.05f);
    [SerializeField] private float emissionIntensity = 2.2f;

    [Header("Parpadeo")]
    [Tooltip("Cuanto varia la luz sin miedo (0 = fija, 1 = llega a apagarse).")]
    [SerializeField, Range(0f, 1f)] private float calmFlicker = 0.08f;
    [Tooltip("Cuanto varia la luz con el miedo al maximo.")]
    [SerializeField, Range(0f, 1f)] private float fearFlicker = 0.55f;
    [Tooltip("Velocidad del parpadeo.")]
    [SerializeField] private float flickerSpeed = 3f;
    [Tooltip("A partir de este miedo (0 a 1) aparecen cortes breves de luz.")]
    [SerializeField, Range(0f, 1f)] private float dropoutFromFear = 0.6f;
    [Tooltip("Cortes por segundo, en promedio, con el miedo al maximo.")]
    [SerializeField] private float dropoutsPerSecondAtMax = 0.25f;

    [Header("Balanceo")]
    [Tooltip("Grados maximos de balanceo.")]
    [SerializeField] private float swayAngle = 2.5f;
    [SerializeField] private float swaySpeed = 0.7f;

    // Compartido entre todos los faroles: hay un solo medidor de miedo.
    private static FearMeter fearMeter;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock block;
    private float baseIntensity;
    private Quaternion baseRotation;
    private float seed;
    private float dropoutTimer;

    /// <summary>Lo usa la herramienta de editor al armar el prefab.</summary>
    public void Setup(Light light, Renderer glow)
    {
        lanternLight = light;
        glowRenderer = glow;
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (lanternLight != null) baseIntensity = lanternLight.intensity;
        baseRotation = transform.localRotation;
        seed = Random.value * 100f; // cada farol parpadea distinto
    }

    private void Start()
    {
        if (fearMeter == null) fearMeter = FindAnyObjectByType<FearMeter>();
    }

    private void Update()
    {
        float fear = fearMeter != null ? fearMeter.Normalized : 0f;

        // 1. Parpadeo continuo: PerlinNoise da valores suaves entre 0 y 1.
        float amount = Mathf.Lerp(calmFlicker, fearFlicker, fear);
        float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
        float factor = 1f - amount * noise;

        // 2. Cortes breves: solo con miedo alto, y cada vez mas seguidos.
        if (dropoutTimer > 0f)
        {
            dropoutTimer -= Time.deltaTime;
            factor *= 0.05f;
        }
        else if (fear > dropoutFromFear)
        {
            float t = Mathf.InverseLerp(dropoutFromFear, 1f, fear);
            if (Random.value < dropoutsPerSecondAtMax * t * Time.deltaTime)
                dropoutTimer = Random.Range(0.05f, 0.18f);
        }

        if (lanternLight != null) lanternLight.intensity = baseIntensity * factor;

        // MaterialPropertyBlock cambia el brillo de ESTE farol sin crear
        // una copia del material para cada uno.
        if (glowRenderer != null)
        {
            glowRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, emissionColor * (emissionIntensity * factor));
            glowRenderer.SetPropertyBlock(block);
        }

        // 3. Balanceo: dos senos con frecuencias distintas para que no se vea mecanico.
        float s = Time.time * swaySpeed + seed;
        transform.localRotation = baseRotation * Quaternion.Euler(
            Mathf.Sin(s) * swayAngle,
            0f,
            Mathf.Sin(s * 0.73f + 1.3f) * swayAngle * 0.6f);
    }
}
