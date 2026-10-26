using UnityEngine;

/// <summary>
/// Mezcla el sonido de fondo segun lo que pasa en el juego.
/// - Ambiente (viento): siempre suena. Con miedo alto se "apaga" con un filtro: el mundo
///   se escucha amortiguado y quedan adelante el drone y el latido (como la vineta en la imagen).
/// - Drone: aparece y sube con el miedo.
/// - Latido: desde cierto miedo, cada vez mas rapido y mas fuerte.
/// - Descubrir un cambio: un golpe metalico.
/// - Cambio a tus espaldas: a veces, un ruido de persiana lejos, en el lugar del cambio.
///   No dice que cambio; solo invita a darse vuelta (y darse vuelta alimenta la paranoia).
/// - Cerca de la salida, el ambiente y el miedo se apagan y se oyen las campanas del torii.
///
/// Va en el objeto "Sonido". La herramienta Callejones > Solo sonido lo arma completo.
/// </summary>
public class AmbienceController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private FearMeter fear;
    [SerializeField] private VisibilityChecker visibility;
    [SerializeField] private MazeDirector director;
    [SerializeField] private LoopExit loopExit;
    [Tooltip("El torii de la salida. Al acercarse, el ambiente y el miedo bajan.")]
    [SerializeField] private Transform exitPoint;
    [Tooltip("El Player: desde donde se mide la distancia a la salida.")]
    [SerializeField] private Transform listener;

    [Header("Fuentes")]
    [SerializeField] private AudioSource bed;
    [SerializeField] private AudioSource drone;
    [Tooltip("Latidos: 2D y sin reverberacion.")]
    [SerializeField] private AudioSource body;
    [SerializeField] private AudioSource stings;
    [Tooltip("3D: se mueve al lugar del cambio antes de sonar.")]
    [SerializeField] private AudioSource distant;

    [Header("Clips")]
    [SerializeField] private AudioClip heartbeat;
    [SerializeField] private AudioClip discovery;
    [SerializeField] private AudioClip distantChange;

    [Header("Ambiente")]
    [SerializeField, Range(0f, 1f)] private float bedVolume = 0.35f;
    [Tooltip("Frecuencia de corte del filtro con miedo 0 (Hz). Mas alta = mas brillante.")]
    [SerializeField] private float bedCutoffCalm = 6000f;
    [Tooltip("Frecuencia de corte con miedo maximo (Hz). Mas baja = mas amortiguado.")]
    [SerializeField] private float bedCutoffFear = 700f;

    [Header("Miedo")]
    [SerializeField, Range(0f, 1f)] private float droneMaxVolume = 0.55f;
    [Tooltip("Miedo a partir del cual empieza a oirse el drone.")]
    [SerializeField, Range(0f, 1f)] private float droneFrom = 0.15f;
    [Tooltip("Miedo a partir del cual se oye el latido.")]
    [SerializeField, Range(0f, 1f)] private float heartbeatFrom = 0.45f;
    [SerializeField] private float heartbeatSlowInterval = 1.1f;
    [SerializeField] private float heartbeatFastInterval = 0.5f;
    [SerializeField, Range(0f, 1f)] private float heartbeatMaxVolume = 0.7f;
    [Tooltip("Cuanto puede cambiar el miedo 'oido' por segundo. Evita saltos bruscos de volumen.")]
    [SerializeField] private float fearFollowSpeed = 1.5f;

    [Header("Eventos")]
    [SerializeField, Range(0f, 1f)] private float discoveryVolume = 0.7f;
    [Tooltip("Probabilidad de oir el cambio lejano cada vez que el laberinto cambia.")]
    [SerializeField, Range(0f, 1f)] private float distantChangeChance = 0.6f;
    [SerializeField, Range(0f, 1f)] private float distantChangeVolume = 0.5f;

    [Header("Salida")]
    [Tooltip("Distancia al torii (m) donde el ambiente empieza a bajar.")]
    [SerializeField] private float exitFadeFar = 20f;
    [Tooltip("Distancia al torii (m) donde el ambiente queda al minimo.")]
    [SerializeField] private float exitFadeNear = 4f;
    [Tooltip("Segundos que tarda en apagarse todo al cruzar la salida.")]
    [SerializeField] private float endFadeTime = 3f;

    private AudioLowPassFilter bedFilter;
    private float heardFear;
    private float heartbeatTimer;
    private float endFade = 1f;
    private bool ended;

    /// <summary>Lo usa la herramienta de editor al armar la escena.</summary>
    public void Setup(FearMeter fearMeter, VisibilityChecker visibilityChecker, MazeDirector mazeDirector,
        LoopExit exit, Transform exitTarget, Transform listenerTransform,
        AudioSource bedSource, AudioSource droneSource, AudioSource bodySource,
        AudioSource stingSource, AudioSource distantSource,
        AudioClip heartbeatClip, AudioClip discoveryClip, AudioClip distantClip)
    {
        fear = fearMeter;
        visibility = visibilityChecker;
        director = mazeDirector;
        loopExit = exit;
        exitPoint = exitTarget;
        listener = listenerTransform;
        bed = bedSource;
        drone = droneSource;
        body = bodySource;
        stings = stingSource;
        distant = distantSource;
        heartbeat = heartbeatClip;
        discovery = discoveryClip;
        distantChange = distantClip;
    }

    private void OnEnable()
    {
        if (visibility != null) visibility.onChangeDiscovered.AddListener(OnChangeDiscovered);
        if (director != null) director.onMutation.AddListener(OnMutation);
        if (loopExit != null) loopExit.onLoopCompleted.AddListener(OnLoopCompleted);
    }

    private void OnDisable()
    {
        if (visibility != null) visibility.onChangeDiscovered.RemoveListener(OnChangeDiscovered);
        if (director != null) director.onMutation.RemoveListener(OnMutation);
        if (loopExit != null) loopExit.onLoopCompleted.RemoveListener(OnLoopCompleted);
    }

    private void Start()
    {
        if (bed != null)
        {
            bedFilter = bed.GetComponent<AudioLowPassFilter>();
            bed.Play();
        }
        if (drone != null)
        {
            drone.volume = 0f;
            drone.Play();
        }
    }

    private void Update()
    {
        float target = fear != null ? fear.Normalized : 0f;
        heardFear = Mathf.MoveTowards(heardFear, target, fearFollowSpeed * Time.deltaTime);

        if (ended) endFade = Mathf.MoveTowards(endFade, 0f, Time.deltaTime / Mathf.Max(0.01f, endFadeTime));

        float nearExit = ExitProximity();
        float calmByExit = 1f - nearExit;

        // 1. Ambiente: baja un poco cerca de la salida y se amortigua con el miedo.
        if (bed != null) bed.volume = bedVolume * (1f - 0.75f * nearExit) * endFade;
        if (bedFilter != null)
        {
            // Interpolacion "en octavas": el oido percibe la frecuencia en escala logaritmica.
            bedFilter.cutoffFrequency = bedCutoffCalm * Mathf.Pow(bedCutoffFear / bedCutoffCalm, heardFear);
        }

        // 2. Drone: curva al cuadrado para que entre despacio y crezca al final.
        if (drone != null)
        {
            float d = Mathf.InverseLerp(droneFrom, 1f, heardFear);
            drone.volume = droneMaxVolume * d * d * calmByExit * endFade;
            drone.pitch = Mathf.Lerp(1f, 1.05f, heardFear);
        }

        // 3. Latido.
        UpdateHeartbeat(Mathf.InverseLerp(heartbeatFrom, 1f, heardFear) * calmByExit * endFade);
    }

    private void UpdateHeartbeat(float intensity)
    {
        if (body == null || heartbeat == null || intensity <= 0f)
        {
            heartbeatTimer = 0f;
            return;
        }

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer > 0f) return;

        body.PlayOneShot(heartbeat, heartbeatMaxVolume * Mathf.Lerp(0.3f, 1f, intensity));
        heartbeatTimer = Mathf.Lerp(heartbeatSlowInterval, heartbeatFastInterval, intensity);
    }

    /// <summary>0 lejos de la salida (o si todavia no aparecio), 1 al llegar al torii.</summary>
    private float ExitProximity()
    {
        if (exitPoint == null || listener == null || !exitPoint.gameObject.activeInHierarchy) return 0f;
        Vector3 d = exitPoint.position - listener.position;
        d.y = 0f;
        return 1f - Mathf.InverseLerp(exitFadeNear, exitFadeFar, d.magnitude);
    }

    private void OnChangeDiscovered()
    {
        if (stings != null && discovery != null) stings.PlayOneShot(discovery, discoveryVolume);
    }

    private void OnMutation()
    {
        if (distant == null || distantChange == null || director == null) return;
        if (Random.value > distantChangeChance) return;

        CorridorSegment segment = director.LastMutated;
        if (segment == null) return;

        distant.transform.position = segment.GetWorldBounds().center;
        distant.pitch = Random.Range(0.9f, 1.1f);
        distant.PlayOneShot(distantChange, distantChangeVolume);
    }

    private void OnLoopCompleted()
    {
        ended = true;
    }
}
