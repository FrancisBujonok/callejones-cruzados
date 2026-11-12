using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 31: LA FALSA SALIDA. Una luz calida de feria al fondo de un ramal, que parece el camino
/// hacia la fiesta, y no lo es.
///
/// 1. Cada tanto se la pide al laberinto (CorridorTreadmill.RequestFalseExit). El proximo cruce
///    de adelante recibe, en uno de sus lados, un recto que termina en un callejon sin salida.
/// 2. En ese callejon se pega el "Montaje": dos guirnaldas de farolitos encendidos, dos luces
///    naranjas (una ilumina la pared de arriba, asi el resplandor se ve por encima del callejon) y
///    el murmullo de una feria, detras de la pared del fondo y con un pasa bajos, como si la
///    fiesta estuviera ahi nomas. Si el altar esta guardado, se pone en la pared del fondo.
/// 3. Al acercarse a menos de revealDistance de la pared, el murmullo se corta de golpe y los
///    farolitos se apagan uno por uno, de afuera hacia adentro. El farol del callejon se corta un
///    instante: queda solo la vela del altar. Suma miedo.
/// 4. A veces (figureChance), cuando te das vuelta para salir, la Figura esta parada en el cruce
///    por el que entraste. Se la pone apenas no estas mirando hacia ahi (espera hasta 8 s).
///
/// Nunca encierra: el otro lado del cruce sigue. Como mucho maxPerGame veces por partida, y si
/// el jugador fue por el otro lado y el ramal se recicla sin que se acerque, se vuelve a pedir
/// mas adelante (retryAfter).
///
/// Va en el objeto "FalsaSalida" (raiz de la escena). Lo arma Callejones > Falsa salida.
/// Para probar: F7 la pide ya (aparece en el proximo cruce que se arme adelante).
/// </summary>
public class FalseExit : MonoBehaviour
{
    public enum State { Idle, Requested, Lit, Revealing, Dark }

    [Header("Referencias")]
    [SerializeField] private CorridorTreadmill paths;
    [SerializeField] private LandmarkDirector landmarks;
    [SerializeField] private Transform player;
    [SerializeField] private FearMeter fear;
    [Tooltip("Si se deja vacio, la busca en la escena.")]
    [SerializeField] private FigurePresence figure;

    [Header("El montaje (lo arma la herramienta)")]
    [Tooltip("Guirnaldas, luces y sonido, armados en coordenadas del callejon (Fondo). Guardado y apagado mientras no se usa.")]
    [SerializeField] private Transform rig;
    [SerializeField] private Renderer[] lanterns;
    [SerializeField] private Light[] lights;
    [SerializeField] private Material litMaterial;
    [SerializeField] private Material darkMaterial;
    [Tooltip("Detras de la pared del fondo, con pasa bajos.")]
    [SerializeField] private AudioSource murmur;
    [SerializeField] private AudioClip[] crowdClips;
    [SerializeField] private AudioClip[] drumClips;

    [Header("Cuando")]
    [Tooltip("Segundos de juego antes de la primera.")]
    [SerializeField] private float firstAfter = 100f;
    [Tooltip("Segundos despues de una (vista hasta el final) antes de pedir otra.")]
    [SerializeField] private float cooldown = 210f;
    [Tooltip("Si el jugador fue por otro lado y el callejon se recicla sin que se acerque.")]
    [SerializeField] private float retryAfter = 45f;
    [SerializeField] private int maxPerGame = 3;

    [Header("Al acercarse")]
    [Tooltip("Metros (en el plano) desde la pared del fondo a los que se apaga todo.")]
    [SerializeField] private float revealDistance = 5f;
    [SerializeField] private float fearOnReveal = 10f;
    [Tooltip("Probabilidad de que la Figura este atras cuando te das vuelta.")]
    [SerializeField, Range(0f, 1f)] private float figureChance = 0.5f;
    [Tooltip("Segundos despues del apagon desde los que se la pone atras.")]
    [SerializeField] private float figureDelay = 1.6f;
    [Tooltip("Segundos que el farol del callejon queda cortado despues del apagon.")]
    [SerializeField] private float farolOff = 7f;

    [Header("Sonido")]
    [Tooltip("Cada cuantos segundos arranca otro pedazo del murmullo (los clips duran 7 s y se enciman).")]
    [SerializeField] private float crowdEvery = 6.2f;
    [SerializeField, Range(0f, 1f)] private float drumChance = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;
    [Tooltip("Prototipo: F7 la pide ya.")]
    [SerializeField] private bool forceKey = true;

    public State Current { get; private set; } = State.Idle;
    public int Revealed { get; private set; }
    public string LastEvent { get; private set; } = "-";
    /// <summary>El callejon donde esta puesta (null si no hay).</summary>
    public CorridorSegment Host => host;

    private CorridorSegment host;
    private Transform storage;
    private float playTime, nextAt, revealT, nextCrowd;
    private float[] baseIntensity, offAt;
    private bool figurePending, farolCut, altarPlaced;
    private int lastCrowd = -1;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(CorridorTreadmill treadmill, LandmarkDirector director, Transform playerTransform, FearMeter fearMeter,
        FigurePresence figurePresence, Transform rigRoot, Renderer[] lanternRenderers, Light[] rigLights,
        Material lit, Material dark, AudioSource murmurSource, AudioClip[] crowd, AudioClip[] drums)
    {
        paths = treadmill;
        landmarks = director;
        player = playerTransform;
        fear = fearMeter;
        figure = figurePresence;
        rig = rigRoot;
        lanterns = lanternRenderers;
        lights = rigLights;
        litMaterial = lit;
        darkMaterial = dark;
        murmur = murmurSource;
        crowdClips = crowd;
        drumClips = drums;
    }

    private void Awake()
    {
        if (paths == null || player == null || rig == null)
        {
            Debug.LogWarning("[FalseExit] Faltan referencias. Ejecuta Callejones > Falsa salida.", this);
            enabled = false;
            return;
        }
        if (figure == null) figure = FindAnyObjectByType<FigurePresence>();
        storage = transform;
        baseIntensity = new float[lights != null ? lights.Length : 0];
        for (int i = 0; i < baseIntensity.Length; i++) baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
        offAt = new float[lanterns != null ? lanterns.Length : 0];
        nextAt = firstAfter;
        Store();
    }

    private void OnEnable()
    {
        if (paths == null) return;
        paths.FalseExitPlaced += OnPlaced;
        paths.PieceRecycled += OnRecycled;
    }

    private void OnDisable()
    {
        if (paths == null) return;
        paths.FalseExitPlaced -= OnPlaced;
        paths.PieceRecycled -= OnRecycled;
    }

    private static bool Playing() =>
        GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    private void Update()
    {
        if (!Playing()) return;
        playTime += Time.deltaTime;
        if (forceKey && ForcePressed() && (Current == State.Idle || Current == State.Requested)) Request(true);

        switch (Current)
        {
            case State.Idle:
                if (playTime >= nextAt && Revealed < maxPerGame && !paths.ExitRequested) Request(false);
                break;
            case State.Requested:
                if (paths.ExitRequested) Cancel("se pidio la salida de verdad");
                break;
            case State.Lit:
                UpdateLit();
                break;
            case State.Revealing:
                UpdateReveal(Time.deltaTime);
                break;
        }
    }

    // ------------------------------------------------------------------ pedir y poner

    private void Request(bool forced)
    {
        Current = State.Requested;
        paths.RequestFalseExit(true);
        LastEvent = forced ? "pedida con F7" : "pedida";
        Debug.Log($"[FalseExit] {LastEvent}: va en el proximo cruce de adelante.");
    }

    private void Cancel(string reason)
    {
        paths.RequestFalseExit(false);
        Current = State.Idle;
        nextAt = playTime + retryAfter;
        LastEvent = reason;
    }

    /// <summary>El laberinto armo el callejon: se le pega el montaje y, si se puede, el altar.</summary>
    private void OnPlaced(CorridorSegment segment)
    {
        if (Current != State.Requested) return;
        host = segment;
        rig.SetParent(segment.transform, false);
        rig.localPosition = Vector3.zero;
        rig.localRotation = Quaternion.identity;
        SetAllLit(true);
        rig.gameObject.SetActive(true);
        altarPlaced = landmarks != null && landmarks.PlaceAt(segment, "Altar");
        Current = State.Lit;
        nextCrowd = 0f;
        farolCut = false;
        LastEvent = altarPlaced ? "encendida (con el altar)" : "encendida";
        Debug.Log($"[FalseExit] {LastEvent} en '{segment.name}'.");
    }

    /// <summary>El callejon vuelve al deposito: el montaje se guarda.</summary>
    private void OnRecycled(CorridorSegment segment)
    {
        if (segment != host) return;
        bool seen = Current == State.Revealing || Current == State.Dark;
        Store();
        Current = State.Idle;
        nextAt = playTime + (seen ? cooldown : retryAfter);
        if (!seen) LastEvent = "se reciclo sin que te acercaras";
    }

    private void Store()
    {
        if (murmur != null) murmur.Stop();
        rig.SetParent(storage, false);
        rig.gameObject.SetActive(false);
        SetAllLit(true);
        host = null;
    }

    // ------------------------------------------------------------------ encendida

    private void UpdateLit()
    {
        if (host == null) return;

        if (murmur != null && Time.time >= nextCrowd)
        {
            PlayCrowd();
            nextCrowd = Time.time + crowdEvery * Random.Range(0.9f, 1.1f);
        }

        // Los farolitos de papel respiran un poco.
        for (int i = 0; i < baseIntensity.Length; i++)
            if (lights[i] != null)
                lights[i].intensity = baseIntensity[i] * (0.9f + 0.1f * Mathf.PerlinNoise(i * 7.3f, Time.time * 1.4f));

        Vector3 wall = host.transform.TransformPoint(new Vector3(0f, 0f, 1.2f));
        Vector3 d = wall - player.position;
        d.y = 0f;
        if (d.magnitude < revealDistance) StartReveal();
    }

    private void PlayCrowd()
    {
        if (crowdClips == null || crowdClips.Length == 0) return;
        int i = Random.Range(0, crowdClips.Length);
        if (crowdClips.Length > 1 && i == lastCrowd) i = (i + 1) % crowdClips.Length;
        lastCrowd = i;
        murmur.pitch = Random.Range(0.97f, 1.03f);
        murmur.PlayOneShot(crowdClips[i], 0.9f);
        if (drumClips != null && drumClips.Length > 0 && Random.value < drumChance)
            murmur.PlayOneShot(drumClips[Random.Range(0, drumClips.Length)], 0.45f);
    }

    // ------------------------------------------------------------------ el apagon

    private void StartReveal()
    {
        if (murmur != null) murmur.Stop(); // silencio de golpe
        Current = State.Revealing;
        revealT = 0f;
        Revealed++;

        // De afuera hacia adentro: los de las puntas primero.
        var order = new List<int>();
        for (int i = 0; i < lanterns.Length; i++) order.Add(i);
        order.Sort((a, b) => Mathf.Abs(LocalX(b)).CompareTo(Mathf.Abs(LocalX(a))));
        for (int rank = 0; rank < order.Count; rank++)
            offAt[order[rank]] = 0.25f + rank * 0.09f + Random.Range(0f, 0.05f);

        if (fear != null) fear.AddFear(fearOnReveal);
        figurePending = figure != null && Random.value < figureChance;
        LastEvent = "se apago" + (altarPlaced ? " (quedo el altar)" : "");
        Debug.Log($"[FalseExit] {LastEvent}.");
    }

    private float LocalX(int i) => lanterns[i] != null ? lanterns[i].transform.localPosition.x : 0f;

    private void UpdateReveal(float dt)
    {
        revealT += dt;
        int on = 0;
        for (int i = 0; i < lanterns.Length; i++)
        {
            if (lanterns[i] == null) continue;
            float t = revealT - offAt[i];
            // Antes de apagarse, dos parpadeos cortos.
            bool lit = t < 0f || (t < 0.1f && Mathf.FloorToInt(t / 0.035f) % 2 == 1);
            Material want = lit ? litMaterial : darkMaterial;
            if (lanterns[i].sharedMaterial != want) lanterns[i].sharedMaterial = want;
            if (lit) on++;
        }
        float k = lanterns.Length > 0 ? on / (float)lanterns.Length : 0f;
        for (int i = 0; i < baseIntensity.Length; i++)
            if (lights[i] != null) lights[i].intensity = baseIntensity[i] * k * k;

        // Cuando se apaga el ultimo, el farol del callejon se corta un instante. La vela del
        // altar (tambien es un LanternLight) queda prendida.
        if (on == 0 && !farolCut)
        {
            farolCut = true;
            foreach (LanternLight lantern in host.GetComponentsInChildren<LanternLight>())
                if (lantern.GetComponentInParent<Landmark>() == null) lantern.Dropout(farolOff);
        }

        // La Figura, en el cruce por el que entraste (un poco hacia el otro lado, para que quede
        // a mas de 5.5 m). Se pone apenas ese punto queda fuera de la imagen.
        if (figurePending && revealT >= figureDelay)
        {
            float back = 1.5f + 6f * paths.FalseExitStraights + 1.5f + 0.7f;
            Vector3 spot = host.transform.TransformPoint(new Vector3(0f, 0f, -back));
            if (figure.AppearAt(spot, "estaba en el cruce de la falsa salida"))
            {
                figurePending = false;
                LastEvent += ", y la Figura quedo en el cruce";
            }
            else if (revealT > figureDelay + 8f) figurePending = false;
        }

        if (on == 0 && !figurePending && revealT > figureDelay) Current = State.Dark;
    }

    private void SetAllLit(bool lit)
    {
        if (lanterns != null)
            foreach (Renderer r in lanterns)
                if (r != null) r.sharedMaterial = lit ? litMaterial : darkMaterial;
        if (lights != null && baseIntensity != null)
            for (int i = 0; i < lights.Length && i < baseIntensity.Length; i++)
                if (lights[i] != null) lights[i].intensity = lit ? baseIntensity[i] : 0f;
    }

    // ------------------------------------------------------------------ pruebas

    /// <summary>Para las pruebas: la pide ya.</summary>
    public void Force() => Request(true);

#if ENABLE_INPUT_SYSTEM
    private static bool ForcePressed() => Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame;
#else
    private static bool ForcePressed() => Input.GetKeyDown(KeyCode.F7);
#endif

    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible || !Playing()) return;
        string state = Current == State.Idle
            ? (Revealed >= maxPerGame ? "no hay mas esta partida" : $"proxima en {Mathf.Max(0f, nextAt - playTime):0} s")
            : Current.ToString();
        GUI.Label(new Rect(12, 342, 900, 22), $"Falsa salida: {state}, vistas {Revealed}/{maxPerGame}, ultimo: {LastEvent} (F7 la pide)");
    }
}
