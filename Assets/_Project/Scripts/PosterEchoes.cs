using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 30: AFICHES CON CONSECUENCIAS. Algunos afiches de locales (los K) hacen sonar algo
/// cerca despues de mirarlos con E, o a veces solo al pasar al lado:
/// - Mahjong: fichas que chocan detras de una ventana, en un piso de arriba.
/// - Karaoke: alguien que canta, lejos y arriba.
/// - Perro buscado: un perro que ladra, mas cerca que los de siempre.
/// - Fuegos artificiales: un estallido lejano. Una sola vez por partida (el personaje dice que
///   no escucho ninguno en toda la noche).
/// - Fiesta de verano: un tambor de festival, muy lejos.
/// - Recital: alguien que practica guitarra del otro lado de la pared.
/// - Mercado nocturno: el murmullo de una feria, lejos.
/// - Adivina: un cuenco que suena justo del otro lado de la pared.
/// Los demas (fideos, te, alquiler, faroles, carnaval, liquidacion) no hacen nada.
///
/// Para que no se gaste:
/// - Cada tipo de afiche tiene varios sonidos y los va usando sin repetir hasta agotarlos.
/// - Despues de sonar, ese tipo espera styleCooldown segundos (y cualquiera, globalCooldown).
/// - La probabilidad empieza en la de cada tipo y se divide por dos cada vez que sono.
/// - Como mucho maxPerGame veces por partida (los fuegos, una).
/// - Nunca encima de una pista (LookBackLures).
///
/// Suena en un punto detras del afiche (adentro del edificio) o arriba, con un pasa bajos: del
/// otro lado de la pared. Va en el objeto "EcosDeAfiches". Lo arma Callejones > Detalles cotidianos.
/// </summary>
public class PosterEchoes : MonoBehaviour
{
    [System.Serializable]
    public class Echo
    {
        [Tooltip("El estilo del afiche: la ultima parte del nombre (mahjong, karaoke, perro...).")]
        public string style;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float chance = 0.7f;
        public int maxPerGame = 3;
        [Tooltip("Metros detras del afiche (adentro del edificio): minimo y maximo.")]
        public Vector2 depth = new Vector2(3f, 5f);
        [Tooltip("Metros por encima del afiche.")]
        public float height = 2f;
        [Tooltip("Pasa bajos: mas bajo, mas apagado y lejano.")]
        public float cutoff = 2000f;
        [Range(0f, 1f)] public float volume = 0.7f;
        public float maxDistance = 20f;
    }

    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private LookBackLures lures;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioLowPassFilter filter;

    [Header("Que suena con cada afiche")]
    [SerializeField] private Echo[] echoes;

    [Header("Para que no se gaste")]
    [SerializeField] private float styleCooldown = 75f;
    [SerializeField] private float globalCooldown = 20f;
    [Tooltip("Cada vez que suena un tipo, su probabilidad se multiplica por esto.")]
    [SerializeField, Range(0f, 1f)] private float chanceDecay = 0.5f;
    [SerializeField] private Vector2 delay = new Vector2(0.8f, 2f);

    [Header("Al pasar al lado (sin mirarlo)")]
    [SerializeField] private float passDistance = 2.2f;
    [Tooltip("Se multiplica por la probabilidad del tipo. Cada afiche se tira una sola vez por aparicion.")]
    [SerializeField, Range(0f, 1f)] private float passChance = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;

    public string LastEvent { get; private set; } = "-";

    private class StyleState { public float last = -999f; public int times; public readonly List<int> bag = new List<int>(); public int lastClip = -1; }

    private readonly Dictionary<string, StyleState> states = new Dictionary<string, StyleState>();
    private readonly HashSet<string> passed = new HashSet<string>();
    private float lastAny = -999f, nextScan;
    private Echo pendingEcho;
    private Vector3 pendingPos;
    private float pendingAt = -1f;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Transform playerTransform, LookBackLures lookBackLures, AudioSource audioSource, AudioLowPassFilter lowPass, Echo[] list)
    {
        player = playerTransform;
        lures = lookBackLures;
        source = audioSource;
        filter = lowPass;
        echoes = list;
    }

    private void OnEnable() => SignInspector.SignOpened += OnSignOpened;
    private void OnDisable() => SignInspector.SignOpened -= OnSignOpened;

    private static bool Playing() =>
        GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    private void OnSignOpened(SignRandomizer sign, string key) => TryTrigger(sign, key, 1f, "lo miraste");

    private void Update()
    {
        if (!Playing() || player == null) return;

        if (pendingAt > 0f && Time.time >= pendingAt)
        {
            pendingAt = -1f;
            Play(pendingEcho, pendingPos);
        }

        // Al pasar al lado: cada afiche (cada aparicion, con lo que dice en ese momento) se tira una vez.
        if (Time.time < nextScan) return;
        nextScan = Time.time + 0.4f;
        foreach (SignRandomizer sign in SignRandomizer.Active)
        {
            if (sign == null || sign.name != "AficheLocal" || sign.CurrentMaterial == null) continue;
            Vector3 to = sign.transform.position - player.position;
            to.y = 0f;
            if (to.sqrMagnitude > passDistance * passDistance) continue;
            string key = SignCatalog.Clean(sign.CurrentMaterial.name);
            string id = sign.GetInstanceID() + key;
            if (!passed.Add(id)) continue;
            TryTrigger(sign, key, passChance, "pasaste al lado");
        }
        if (passed.Count > 400) passed.Clear(); // las piezas se reciclan: no hace falta recordar tanto
    }

    private void TryTrigger(SignRandomizer sign, string key, float chanceScale, string why)
    {
        if (!key.StartsWith("K_") || pendingAt > 0f) return;
        string style = key.Substring(key.LastIndexOf('_') + 1);
        Echo echo = Find(style);
        if (echo == null || echo.clips == null || echo.clips.Length == 0) return;

        StyleState st = State(style);
        if (st.times >= echo.maxPerGame) return;
        if (Time.time - st.last < styleCooldown || Time.time - lastAny < globalCooldown) return;
        if (lures != null && lures.Active != LookBackLures.Cue.None) return;

        float chance = echo.chance * Mathf.Pow(chanceDecay, st.times) * chanceScale;
        if (Random.value > chance) return;

        // Detras del afiche: su forward apunta hacia la pared (la cara se ve desde -Z).
        Transform t = sign.transform;
        Vector3 back = t.forward;
        back.y = 0f;
        back = back.sqrMagnitude > 0.01f ? back.normalized : -player.forward;
        pendingPos = t.position + back * Random.Range(echo.depth.x, echo.depth.y) + Vector3.up * echo.height
                     + Vector3.Cross(Vector3.up, back) * Random.Range(-2f, 2f);
        pendingEcho = echo;
        pendingAt = Time.time + Random.Range(delay.x, delay.y);
        st.last = Time.time; // se cuenta ya, asi no se tira dos veces mientras espera
        lastAny = Time.time;
        LastEvent = $"{style} ({why})";
    }

    private void Play(Echo echo, Vector3 position)
    {
        if (echo == null || source == null) return;
        StyleState st = State(echo.style);
        AudioClip clip = echo.clips[NextClip(st, echo.clips.Length)];
        source.transform.position = position;
        if (filter != null) filter.cutoffFrequency = echo.cutoff;
        source.maxDistance = echo.maxDistance;
        source.pitch = Random.Range(0.96f, 1.04f);
        source.PlayOneShot(clip, echo.volume);
        st.times++;
        LastEvent = $"{echo.style}: {clip.name} (vez {st.times})";
        Debug.Log($"[PosterEchoes] {LastEvent}");
    }

    /// <summary>Bolsa: usa todos los sonidos del tipo en orden al azar antes de repetir; nunca el mismo dos veces seguidas.</summary>
    private static int NextClip(StyleState st, int count)
    {
        if (st.bag.Count == 0)
        {
            for (int i = 0; i < count; i++) st.bag.Add(i);
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (st.bag[i], st.bag[j]) = (st.bag[j], st.bag[i]);
            }
            if (count > 1 && st.bag[0] == st.lastClip) (st.bag[0], st.bag[count - 1]) = (st.bag[count - 1], st.bag[0]);
        }
        int clip = st.bag[0];
        st.bag.RemoveAt(0);
        st.lastClip = clip;
        return clip;
    }

    private Echo Find(string style)
    {
        if (echoes == null) return null;
        foreach (Echo e in echoes) if (e != null && e.style == style) return e;
        return null;
    }

    private StyleState State(string style)
    {
        if (!states.TryGetValue(style, out StyleState st)) { st = new StyleState(); states[style] = st; }
        return st;
    }

    /// <summary>Para las pruebas: hace sonar un tipo ya, sin probabilidad ni espera (cerca del jugador).</summary>
    public void Force(string style)
    {
        Echo echo = Find(style);
        if (echo == null || player == null) return;
        Play(echo, player.position - player.forward * echo.depth.x + Vector3.up * echo.height);
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible || !Playing()) return;
        GUI.Label(new Rect(12, 320, 900, 22), $"Ecos de afiches: {LastEvent}");
    }
}
