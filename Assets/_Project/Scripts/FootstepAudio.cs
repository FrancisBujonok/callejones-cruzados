using UnityEngine;

/// <summary>
/// Pasos del jugador. Escucha StepCounter.onStep: el sonido sale justo cuando el contador
/// cuenta un paso, asi lo que se oye y lo que el juego mide son la misma cosa
/// (12 pasos oidos = 12 pasos de la racha).
///
/// Eco: con miedo alto, a veces suena un paso de mas DETRAS del jugador, un instante despues
/// del suyo. No hay nadie. Pero invita a darse vuelta, y darse vuelta seguido es paranoia.
/// Como el eco llega despues, al frenar puede oirse "un paso de mas".
///
/// Va en el Player, junto a StepCounter.
/// </summary>
public class FootstepAudio : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private StepCounter steps;
    [SerializeField] private FearMeter fear;
    [Tooltip("Fuente 2D para los pasos propios.")]
    [SerializeField] private AudioSource source;
    [Tooltip("Fuente 3D hija del Player, ubicada detras de el, para el eco.")]
    [SerializeField] private AudioSource echoSource;
    [SerializeField] private AudioClip[] clips;

    [Header("Pasos")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
    [Tooltip("Variacion de tono al azar en cada paso (0.08 = mas o menos 8%).")]
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.08f;

    [Header("Eco detras del jugador")]
    [Tooltip("Miedo a partir del cual puede aparecer el eco.")]
    [SerializeField, Range(0f, 1f)] private float echoFromFear = 0.35f;
    [Tooltip("Probabilidad de eco por paso con el miedo al maximo.")]
    [SerializeField, Range(0f, 1f)] private float echoChanceAtMaxFear = 0.35f;
    [SerializeField] private float echoDelayMin = 0.3f;
    [SerializeField] private float echoDelayMax = 0.5f;
    [Tooltip("Metros detras del jugador.")]
    [SerializeField] private float echoDistance = 4f;
    [SerializeField, Range(0f, 1f)] private float echoVolume = 0.45f;

    private int lastClip = -1;
    private float echoTimer = -1f;

    /// <summary>Lo usa la herramienta de editor al armar la escena.</summary>
    public void Setup(StepCounter stepCounter, FearMeter fearMeter, AudioSource stepSource, AudioSource echo, AudioClip[] stepClips)
    {
        steps = stepCounter;
        fear = fearMeter;
        source = stepSource;
        echoSource = echo;
        clips = stepClips;
    }

    private void Awake()
    {
        if (steps == null) steps = GetComponent<StepCounter>();
        if (fear == null) fear = GetComponent<FearMeter>();
    }

    private void OnEnable()
    {
        if (steps != null) steps.onStep.AddListener(OnStep);
    }

    private void OnDisable()
    {
        if (steps != null) steps.onStep.RemoveListener(OnStep);
    }

    private void OnStep(int streak)
    {
        Play(source, volume);
        TryScheduleEcho();
    }

    private void TryScheduleEcho()
    {
        if (echoSource == null || fear == null || echoTimer > 0f) return;

        float t = Mathf.InverseLerp(echoFromFear, 1f, fear.Normalized);
        if (t <= 0f || Random.value > echoChanceAtMaxFear * t) return;

        echoTimer = Random.Range(echoDelayMin, echoDelayMax);
    }

    private void Update()
    {
        if (echoTimer <= 0f) return;

        echoTimer -= Time.deltaTime;
        if (echoTimer > 0f) return;

        // Hija del Player: -Z local siempre es "detras", gire hacia donde gire.
        echoSource.transform.localPosition = new Vector3(Random.Range(-0.6f, 0.6f), -0.8f, -echoDistance);
        Play(echoSource, echoVolume);
    }

    private void Play(AudioSource target, float vol)
    {
        if (target == null || clips == null || clips.Length == 0) return;

        // Nunca el mismo clip dos veces seguidas: la repeticion exacta delata al sonido.
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == lastClip) i = (i + 1) % clips.Length;
        lastClip = i;

        target.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        target.PlayOneShot(clips[i], vol);
    }
}
