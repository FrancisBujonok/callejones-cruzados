using UnityEngine;

/// <summary>
/// Paso 26: LO COTIDIANO. La novela lo dice: lo raro todo el tiempo cansa; lo que desorienta es
/// que se alterne con lo normal. Cada tanto se oye algo comun, del otro lado de una pared:
/// - Platos: alguien lavando o poniendo la mesa, amortiguado.
/// - Tos: alguien que tose detras de una persiana.
/// - Moto: una moto que pasa por otra calle, de un lado al otro, lejos y detras de las casas.
/// - La maquina expendedora funciona: al pasar cerca, a veces cae una lata. Nadie la compro.
///
/// No suman miedo ni cuentan para nada: son el respiro entre una cosa y otra. Con mucho miedo
/// se oyen menos (el mundo se encierra), y nunca encima de una pista (LookBackLures).
/// Platos, tos y moto son grabaciones de BigSoundBank (CC0, Audio/Reales/LICENCIAS.txt); la lata
/// se sintetiza como el resto.
///
/// Paso 28: tres sonidos grabados mas, tambien de BigSoundBank (CC0):
/// - Persiana: la rejilla de un comercio que baja, a 12-20 m, detras de las casas.
/// - Perro: un perro que ladra lejos, a 18-28 m, en otra cuadra.
/// - Campanillas: campanillas de viento colgadas de una ventana, cerca (3-5 m, arriba).
///
/// Va en el objeto "Cotidiano" (raiz de la escena). Lo arma Callejones > Detalles cotidianos.
/// </summary>
public class MundaneSounds : MonoBehaviour
{
    private enum Kind { None, Dishes, Cough, Scooter, Shutter, Dog, Chimes }

    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private FearMeter fear;
    [Tooltip("Para no sonar encima de una pista.")]
    [SerializeField] private LookBackLures lures;

    [Header("Fuentes (las arma la herramienta)")]
    [Tooltip("3D, con pasa bajos: suena del otro lado de una pared.")]
    [SerializeField] private AudioSource wallSource;
    [SerializeField] private AudioLowPassFilter wallFilter;
    [SerializeField] private AudioSource machineSource;

    [Header("Clips")]
    [SerializeField] private AudioClip[] dishes;
    [SerializeField] private AudioClip[] coughs;
    [SerializeField] private AudioClip[] scooters;
    [SerializeField] private AudioClip canDrop;
    [Tooltip("Paso 28.")]
    [SerializeField] private AudioClip[] shutters;
    [SerializeField] private AudioClip[] dogs;
    [SerializeField] private AudioClip[] chimes;

    [Header("Cuando")]
    [SerializeField] private float firstAfter = 12f;
    [SerializeField] private float gapMin = 22f;
    [SerializeField] private float gapMax = 45f;
    [Tooltip("Con este miedo (0 a 1) o mas, la espera se duplica.")]
    [SerializeField, Range(0f, 1f)] private float quietFromFear = 0.6f;

    [Header("Pesos")]
    [SerializeField] private float dishesWeight = 3f;
    [SerializeField] private float coughWeight = 2f;
    [SerializeField] private float scooterWeight = 2f;
    [SerializeField] private float shutterWeight = 1.5f;
    [SerializeField] private float dogWeight = 1.5f;
    [SerializeField] private float chimesWeight = 1.5f;

    [Header("Maquina expendedora")]
    [Tooltip("A menos de estos metros de la maquina, puede caer una lata.")]
    [SerializeField] private float machineDistance = 3.5f;
    [Tooltip("Probabilidad de que caiga una lata cada vez que la maquina aparece y el jugador pasa cerca.")]
    [SerializeField, Range(0f, 1f)] private float canChance = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;

    public string Last { get; private set; } = "-";

    private float nextAt;
    private Kind playing;
    private float playTime, playLength;
    private Vector3 scooterFrom, scooterTo;
    private Landmark machine;
    private bool machineWasActive, canDecided;
    private float canAt = -1f;
    private Kind previous;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Transform playerTransform, FearMeter fearMeter, LookBackLures lookBackLures,
        AudioSource wall, AudioLowPassFilter filter, AudioSource machineAudio,
        AudioClip[] dishClips, AudioClip[] coughClips, AudioClip[] scooterClips, AudioClip can)
    {
        player = playerTransform;
        fear = fearMeter;
        lures = lookBackLures;
        wallSource = wall;
        wallFilter = filter;
        machineSource = machineAudio;
        dishes = dishClips;
        coughs = coughClips;
        scooters = scooterClips;
        canDrop = can;
    }

    /// <summary>Paso 28: los sonidos nuevos. Lo usa la herramienta de editor.</summary>
    public void SetupMore(AudioClip[] shutterClips, AudioClip[] dogClips, AudioClip[] chimeClips)
    {
        shutters = shutterClips;
        dogs = dogClips;
        chimes = chimeClips;
    }

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogWarning("[MundaneSounds] Falta el Player. Ejecuta Callejones > Detalles cotidianos.", this);
            enabled = false;
            return;
        }
        foreach (Landmark l in FindObjectsByType<Landmark>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.Label.StartsWith("Maquina")) machine = l;
        nextAt = firstAfter;
    }

    private static bool Playing() =>
        GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    private void Update()
    {
        if (!Playing())
        {
            if (playing != Kind.None && wallSource != null) wallSource.Stop();
            playing = Kind.None;
            return;
        }

        UpdateMachine();

        if (playing != Kind.None)
        {
            playTime += Time.deltaTime;
            if (playing == Kind.Scooter)
                wallSource.transform.position = Vector3.Lerp(scooterFrom, scooterTo, Mathf.Clamp01(playTime / playLength));
            if (playTime >= playLength) playing = Kind.None;
            return;
        }

        nextAt -= Time.deltaTime;
        if (nextAt > 0f) return;
        if (lures != null && lures.Active != LookBackLures.Cue.None)
        {
            nextAt = 3f; // hay una pista sonando: lo cotidiano espera
            return;
        }
        PlayOne(Pick());
        float gap = Random.Range(gapMin, gapMax);
        if (fear != null && fear.Normalized >= quietFromFear) gap *= 2f; // con mucho miedo, el mundo se calla
        nextAt = gap;
    }

    // ------------------------------------------------------------------ del otro lado de la pared

    private Kind Pick()
    {
        float wd = Has(dishes) && previous != Kind.Dishes ? dishesWeight : 0f;
        float wc = Has(coughs) && previous != Kind.Cough ? coughWeight : 0f;
        float ws = Has(scooters) && previous != Kind.Scooter ? scooterWeight : 0f;
        float wsh = Has(shutters) && previous != Kind.Shutter ? shutterWeight : 0f;
        float wdg = Has(dogs) && previous != Kind.Dog ? dogWeight : 0f;
        float wch = Has(chimes) && previous != Kind.Chimes ? chimesWeight : 0f;
        float total = wd + wc + ws + wsh + wdg + wch;
        if (total <= 0f) return Kind.None;
        float r = Random.value * total;
        if ((r -= wd) < 0f) return Kind.Dishes;
        if ((r -= wc) < 0f) return Kind.Cough;
        if ((r -= ws) < 0f) return Kind.Scooter;
        if ((r -= wsh) < 0f) return Kind.Shutter;
        if ((r -= wdg) < 0f) return Kind.Dog;
        return Kind.Chimes;
    }

    private void PlayOne(Kind kind)
    {
        if (kind == Kind.None || wallSource == null) return;
        Vector3 right = player.right;
        Vector3 forward = player.forward;
        float side = Random.value < 0.5f ? -1f : 1f;
        AudioClip clip;

        switch (kind)
        {
            case Kind.Dishes:
                clip = dishes[Random.Range(0, dishes.Length)];
                wallSource.transform.position = player.position + right * side * Random.Range(2.5f, 4.5f) + forward * Random.Range(-3f, 3f);
                SetWall(1700f, 0.55f, 14f);
                Last = "platos";
                break;
            case Kind.Cough:
                clip = coughs[Random.Range(0, coughs.Length)];
                wallSource.transform.position = player.position + right * side * Random.Range(2f, 3.5f) + forward * Random.Range(-2f, 4f);
                SetWall(2200f, 0.75f, 12f);
                Last = "tos";
                break;
            case Kind.Shutter:
                // Una rejilla que baja lejos, detras de las casas: se oye apagada.
                clip = shutters[Random.Range(0, shutters.Length)];
                wallSource.transform.position = player.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(12f, 20f);
                SetWall(1500f, 0.8f, 30f);
                Last = "persiana";
                break;
            case Kind.Dog:
                clip = dogs[Random.Range(0, dogs.Length)];
                wallSource.transform.position = player.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(18f, 28f);
                SetWall(1300f, 0.85f, 40f);
                Last = "perro";
                break;
            case Kind.Chimes:
                // Colgadas de una ventana, arriba y cerca: casi sin pasa bajos.
                clip = chimes[Random.Range(0, chimes.Length)];
                wallSource.transform.position = player.position + right * side * Random.Range(1.2f, 1.4f) + forward * Random.Range(-3f, 5f) + Vector3.up * 1.4f;
                SetWall(6000f, 0.4f, 14f);
                Last = "campanillas";
                break;
            default:
                clip = scooters[Random.Range(0, scooters.Length)];
                // Pasa por una calle paralela, detras de las casas: de un costado al otro.
                Vector3 street = player.position + right * side * 11f;
                scooterFrom = street - forward * 14f;
                scooterTo = street + forward * 14f;
                if (Random.value < 0.5f) { Vector3 t = scooterFrom; scooterFrom = scooterTo; scooterTo = t; }
                wallSource.transform.position = scooterFrom;
                SetWall(1100f, 0.9f, 32f);
                Last = "moto";
                break;
        }

        wallSource.pitch = Random.Range(0.94f, 1.06f);
        wallSource.clip = clip;
        wallSource.Play();
        playing = kind;
        previous = kind;
        playTime = 0f;
        playLength = clip.length / Mathf.Max(0.5f, wallSource.pitch);
        Debug.Log($"[MundaneSounds] {Last}.");
    }

    private void SetWall(float cutoff, float volume, float maxDistance)
    {
        if (wallFilter != null) wallFilter.cutoffFrequency = cutoff;
        wallSource.volume = volume;
        wallSource.maxDistance = maxDistance;
    }

    // ------------------------------------------------------------------ la maquina

    /// <summary>
    /// Cada vez que la maquina aparece en un lugar nuevo se decide una vez si va a caer una lata.
    /// Si toca, cae cuando el jugador pasa cerca, con un instante de demora.
    /// </summary>
    private void UpdateMachine()
    {
        if (machine == null || machineSource == null || canDrop == null) return;
        bool active = machine.gameObject.activeInHierarchy;
        if (active && !machineWasActive) { canDecided = false; canAt = -1f; } // aparecio en otro lado
        machineWasActive = active;
        if (!active || canDecided) return;

        Vector3 to = machine.Center - player.position;
        to.y = 0f;
        if (canAt < 0f)
        {
            if (to.magnitude > machineDistance) return;
            if (Random.value > canChance) { canDecided = true; return; }
            canAt = Time.time + Random.Range(0.3f, 1.2f);
            return;
        }
        if (Time.time < canAt) return;

        canAt = -1f;
        canDecided = true;
        machineSource.transform.position = machine.Center + Vector3.down * 0.6f;
        machineSource.pitch = Random.Range(0.95f, 1.05f);
        machineSource.PlayOneShot(canDrop, 0.8f);
        Last = "lata en la maquina";
        Debug.Log("[MundaneSounds] Cayo una lata en la maquina expendedora.");
    }

    private static bool Has(AudioClip[] clips) => clips != null && clips.Length > 0 && clips[0] != null;

    // ------------------------------------------------------------------ pruebas

    [ContextMenu("Probar: platos")] private void TestDishes() => PlayOne(Kind.Dishes);
    [ContextMenu("Probar: tos")] private void TestCough() => PlayOne(Kind.Cough);
    [ContextMenu("Probar: moto")] private void TestScooter() => PlayOne(Kind.Scooter);

    [ContextMenu("Probar: persiana")] private void TestShutter() => PlayOne(Kind.Shutter);
    [ContextMenu("Probar: perro")] private void TestDog() => PlayOne(Kind.Dog);
    [ContextMenu("Probar: campanillas")] private void TestChimes() => PlayOne(Kind.Chimes);

    /// <summary>Para las pruebas: 0 platos, 1 tos, 2 moto, 3 persiana, 4 perro, 5 campanillas.</summary>
    public void Force(int kind) => PlayOne(kind <= 0 ? Kind.Dishes : (Kind)(kind + 1));

    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible || !Playing()) return;
        GUI.Label(new Rect(12, 276, 800, 22),
            $"Cotidiano: {(playing != Kind.None ? "sonando " + Last : $"proximo en {Mathf.Max(0f, nextAt):0} s")}, ultimo: {Last}");
    }
}
