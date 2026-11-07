using UnityEngine;

/// <summary>
/// Paso 25: PISTAS QUE TE HACEN DARTE VUELTA. Para el jugador que camina sin mirar atras
/// (y por eso nunca descubre los cambios ni llega a la salida). Ninguna dice "mira atras":
/// pasan algo a tu espalda y dan ganas de mirar.
///
/// - Gato: un maullido atras, a ras del piso. A veces dos.
/// - Campanillas: alguien que camina con cascabeles, cada vez mas cerca. Se callan de golpe
///   cuando te das vuelta.
/// - Luz: un tubo se prende atras tuyo y tu sombra se estira por el piso, adelante. Se apaga
///   en cuanto la miras.
/// - Telefono: suena el telefono publico que dejaste atras (si esta). Si no, uno de una casa,
///   amortiguado detras de una pared. Se corta en cuanto miras hacia ahi.
/// - Carteles: mientras no te das vuelta, los carteles de "no mires atras" salen cada vez mas
///   seguido en las piezas nuevas (SignRandomizer.WarningBoost). Esa no es una pista suelta:
///   va creciendo sola con la calma.
///
/// Calma = segundos desde la ultima vez que el jugador miro atras: un giro rapido
/// (ParanoiaSensor.onLookBack) o mirar mas de medio segundo para el lado contrario al que camina.
/// Desde firstCueAfter segundos de calma, cada gapMin a gapMax segundos suena una pista (nunca
/// la misma dos veces seguidas). Cada pista seguida sin darse vuelta acorta la espera y acerca
/// el sonido. Al mirar atras, todo vuelve a empezar.
///
/// Todas suman un poco de miedo. Ninguna hace perder.
/// Va en el objeto "Pistas" (raiz de la escena). Lo arma Callejones > Pistas y mirar de cerca.
/// </summary>
public class LookBackLures : MonoBehaviour
{
    public enum Cue { None, Cat, Bells, Light, Phone }

    [Header("Referencias")]
    [SerializeField] private Transform player;
    [Tooltip("Si se deja vacia, usa la Main Camera.")]
    [SerializeField] private Camera view;
    [SerializeField] private ParanoiaSensor paranoia;
    [SerializeField] private FearMeter fear;

    [Header("Fuentes (las arma la herramienta)")]
    [SerializeField] private AudioSource catSource;
    [SerializeField] private AudioSource bellsSource;
    [SerializeField] private AudioSource phoneSource;
    [Tooltip("Pasa bajos del telefono: amortigua el de la casa, detras de la pared.")]
    [SerializeField] private AudioLowPassFilter phoneFilter;
    [SerializeField] private Light backLight;
    [Tooltip("Zumbido del tubo, en la misma posicion que la luz.")]
    [SerializeField] private AudioSource lightHum;
    [Tooltip("El cuerpo del jugador que solo proyecta sombra. Se prende solo con la luz.")]
    [SerializeField] private Renderer[] shadowBody;
    [Tooltip("Intensidad del tubo de atras. Con menos de 30, la sombra casi no se ve entre la niebla.")]
    [SerializeField] private float lightIntensity = 40f;

    [Header("Clips")]
    [SerializeField] private AudioClip meow;
    [SerializeField] private AudioClip phoneRing;

    [Header("Cuando")]
    [Tooltip("Segundos sin mirar atras antes de la primera pista.")]
    [SerializeField] private float firstCueAfter = 18f;
    [SerializeField] private float gapMin = 14f;
    [SerializeField] private float gapMax = 22f;
    [Tooltip("Segundos que se acorta la espera por cada pista seguida sin darse vuelta.")]
    [SerializeField] private float gapShrinkPerCue = 2.5f;
    [SerializeField] private float minGap = 8f;

    [Header("Que pista (pesos)")]
    [SerializeField] private float catWeight = 3f;
    [SerializeField] private float bellsWeight = 2f;
    [SerializeField] private float lightWeight = 2f;
    [SerializeField] private float phoneWeight = 1.2f;
    [Tooltip("Peso del telefono cuando el telefono publico quedo atras: casi seguro que suena ese.")]
    [SerializeField] private float phoneNearbyWeight = 6f;

    [Header("Miedo")]
    [SerializeField] private float fearPerCue = 2.5f;

    [Header("Carteles de \"no mires atras\"")]
    [Tooltip("Segundos de calma desde los que empiezan a salir mas.")]
    [SerializeField] private float warningFrom = 10f;
    [Tooltip("Cuanto sube su peso por segundo de calma.")]
    [SerializeField] private float warningPerSecond = 0.125f;
    [SerializeField] private float maxWarningBoost = 8f;

    [Header("Mirar atras despacio")]
    [Tooltip("Mirar mas de estos segundos hacia el lado contrario al que se camina cuenta como mirar atras.")]
    [SerializeField] private float slowLookSeconds = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;

    public float CalmTime { get; private set; }
    public Cue Active { get; private set; }
    public string LastCue { get; private set; } = "-";
    public int CuesPlayed { get; private set; }

    private Cue previous = Cue.None;
    private int inARow;
    private float nextCueAt;
    private float cueTime;
    private Vector3 moveDir = Vector3.forward, lastPos;
    private float slowLookTimer;
    private Vector3 cueSpot;
    private bool secondMeow;
    private int ringsLeft;
    private float nextRingAt;
    private Landmark phoneLandmark;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Transform playerTransform, Camera playerCamera, ParanoiaSensor paranoiaSensor, FearMeter fearMeter,
        AudioSource cat, AudioSource bells, AudioSource phone, AudioLowPassFilter phoneLowPass,
        Light light, AudioSource hum, Renderer[] body, AudioClip meowClip, AudioClip ringClip)
    {
        player = playerTransform;
        view = playerCamera;
        paranoia = paranoiaSensor;
        fear = fearMeter;
        catSource = cat;
        bellsSource = bells;
        phoneSource = phone;
        phoneFilter = phoneLowPass;
        backLight = light;
        lightHum = hum;
        shadowBody = body;
        meow = meowClip;
        phoneRing = ringClip;
    }

    private void Awake()
    {
        if (view == null) view = Camera.main;
        SignRandomizer.WarningBoost = 1f;
        if (player == null || view == null)
        {
            Debug.LogWarning("[LookBackLures] Faltan referencias. Ejecuta Callejones > Pistas y mirar de cerca.", this);
            enabled = false;
            return;
        }
        lastPos = player.position;
        // El telefono publico: uno de los lugares (Landmark). Se busca incluso apagado.
        foreach (Landmark l in FindObjectsByType<Landmark>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.Label.StartsWith("Telefono")) phoneLandmark = l;
        StopAll();
    }

    private void OnEnable()
    {
        if (paranoia != null) paranoia.onLookBack.AddListener(LookedBack);
    }

    private void OnDisable()
    {
        if (paranoia != null) paranoia.onLookBack.RemoveListener(LookedBack);
        SignRandomizer.WarningBoost = 1f;
    }

    private static bool Playing() =>
        GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    private void Update()
    {
        if (!Playing())
        {
            if (Active != Cue.None) StopAll();
            CalmTime = 0f;
            SignRandomizer.WarningBoost = 1f;
            lastPos = player.position;
            return;
        }

        TrackWalkingAndSlowLook();
        CalmTime += Time.deltaTime;

        float boost = 1f + Mathf.Max(0f, CalmTime - warningFrom) * warningPerSecond;
        SignRandomizer.WarningBoost = Mathf.Min(boost, maxWarningBoost);

        if (Active != Cue.None)
        {
            cueTime += Time.deltaTime;
            UpdateCue();
            return;
        }

        if (CalmTime >= firstCueAfter && Time.time >= nextCueAt) StartCue(PickCue());
    }

    /// <summary>Hacia donde camina el jugador, y si esta mirando para el otro lado.</summary>
    private void TrackWalkingAndSlowLook()
    {
        Vector3 delta = player.position - lastPos;
        delta.y = 0f;
        lastPos = player.position;
        if (delta.magnitude > 0.4f * Time.deltaTime && delta.magnitude < 1f) // caminando (y no un teletransporte)
            moveDir = Vector3.Slerp(moveDir, delta.normalized, 4f * Time.deltaTime).normalized;

        Vector3 look = Flat(view.transform.forward);
        if (Vector3.Dot(look, moveDir) < -0.3f)
        {
            slowLookTimer += Time.deltaTime;
            if (slowLookTimer >= slowLookSeconds)
            {
                slowLookTimer = 0f;
                LookedBack();
            }
        }
        else slowLookTimer = 0f;
    }

    /// <summary>Miro atras: se apaga lo que este pasando y la calma vuelve a cero.</summary>
    private void LookedBack()
    {
        CalmTime = 0f;
        inARow = 0;
        SignRandomizer.WarningBoost = 1f;
        if (Active != Cue.None)
        {
            Debug.Log($"[LookBackLures] El jugador se dio vuelta con '{LastCue}' sonando.");
            StopAll();
        }
        nextCueAt = Time.time + Random.Range(gapMin, gapMax) * 0.5f;
    }

    // ------------------------------------------------------------------ elegir y empezar

    private Cue PickCue()
    {
        bool phoneBehind = PhoneBehind(out _);
        float wCat = previous == Cue.Cat ? 0f : catWeight;
        float wBells = previous == Cue.Bells || bellsSource == null ? 0f : bellsWeight;
        float wLight = previous == Cue.Light || backLight == null ? 0f : lightWeight;
        float wPhone = previous == Cue.Phone || phoneSource == null || phoneRing == null ? 0f
            : (phoneBehind ? phoneNearbyWeight : phoneWeight);
        if (meow == null || catSource == null) wCat = 0f;

        float total = wCat + wBells + wLight + wPhone;
        if (total <= 0f) return Cue.None;
        float r = Random.value * total;
        if ((r -= wCat) < 0f) return Cue.Cat;
        if ((r -= wBells) < 0f) return Cue.Bells;
        if ((r -= wLight) < 0f) return Cue.Light;
        return Cue.Phone;
    }

    private void StartCue(Cue cue)
    {
        if (cue == Cue.None) return;
        Active = cue;
        previous = cue;
        LastCue = Name(cue);
        cueTime = 0f;
        CuesPlayed++;
        inARow++;
        if (fear != null) fear.AddFear(fearPerCue);
        float closer = Mathf.Min(inARow - 1, 3); // cada pista seguida, un poco mas cerca

        // "A tu espalda" es detras de donde mira el jugador (no de donde camina).
        Vector3 back = -Flat(view.transform.forward);
        Vector3 side = Vector3.Cross(Vector3.up, back);
        Vector3 feet = player.position + Vector3.down * 1f;

        switch (cue)
        {
            case Cue.Cat:
                cueSpot = feet + back * (8f - closer) + side * Random.Range(-1f, 1f) + Vector3.up * 0.35f;
                catSource.transform.position = cueSpot;
                catSource.pitch = Random.Range(0.92f, 1.08f);
                catSource.PlayOneShot(meow, 0.8f);
                secondMeow = Random.value < 0.45f;
                break;

            case Cue.Bells:
                cueSpot = back; // aca se guarda la direccion: las campanillas siguen al jugador
                bellsSource.volume = 0f;
                bellsSource.transform.position = player.position + back * 11f;
                bellsSource.Play();
                break;

            case Cue.Light:
                // Fija en el mundo, atras y arriba: si el jugador sigue caminando, la sombra se alarga.
                cueSpot = player.position + back * 3f + Vector3.up * 1.6f;
                backLight.transform.position = cueSpot;
                backLight.transform.rotation = Quaternion.LookRotation(
                    (feet - back * 4.5f) - cueSpot, Vector3.up);
                backLight.enabled = false;
                if (lightHum != null)
                {
                    lightHum.transform.position = cueSpot;
                    lightHum.volume = 0f;
                    lightHum.Play();
                }
                SetShadowBody(true);
                break;

            case Cue.Phone:
                if (PhoneBehind(out Vector3 phonePos))
                {
                    cueSpot = phonePos;
                    if (phoneFilter != null) phoneFilter.cutoffFrequency = 22000f;
                    phoneSource.volume = 0.75f;
                }
                else
                {
                    // Un telefono de una casa, detras de una pared: lejos y amortiguado.
                    cueSpot = player.position + back * (10f - closer) + side * 1.4f;
                    if (phoneFilter != null) phoneFilter.cutoffFrequency = 1600f;
                    phoneSource.volume = 0.9f;
                }
                phoneSource.transform.position = cueSpot;
                ringsLeft = Random.Range(3, 6);
                nextRingAt = 0f;
                break;
        }
        Debug.Log($"[LookBackLures] Pista: {LastCue} (calma {CalmTime:0} s, {inARow} seguidas).");
    }

    // ------------------------------------------------------------------ mientras suena

    private void UpdateCue()
    {
        switch (Active)
        {
            case Cue.Cat:
                if (secondMeow && cueTime >= 1.9f)
                {
                    secondMeow = false;
                    catSource.pitch = Random.Range(0.85f, 1f);
                    catSource.PlayOneShot(meow, 0.65f);
                }
                if (cueTime >= 4f) Finish();
                break;

            case Cue.Bells:
            {
                // Se acercan de 11 a 3.5 m en 7 s, siempre a la espalda (por donde vino el jugador).
                float d = Mathf.Lerp(11f, 3.5f, Mathf.SmoothStep(0f, 1f, cueTime / 7f));
                bellsSource.transform.position = player.position + cueSpot * d + Vector3.down * 0.3f;
                bellsSource.volume = Mathf.Clamp01(cueTime / 1.5f) * 0.85f;
                if (Facing(bellsSource.transform.position, 70f)) { Debug.Log("[LookBackLures] Las campanillas se callaron."); StopAll(); }
                else if (cueTime >= 9f) Finish();
                break;
            }

            case Cue.Light:
            {
                float on = LightLevel(cueTime);
                backLight.enabled = on > 0.01f;
                backLight.intensity = lightIntensity * on;
                if (lightHum != null) lightHum.volume = 0.35f * on;
                if (cueTime > 0.6f && Facing(cueSpot, 80f)) { Debug.Log("[LookBackLures] La luz se apago al mirarla."); StopAll(); }
                else if (cueTime >= 8f) Finish();
                break;
            }

            case Cue.Phone:
                if (cueTime >= nextRingAt && ringsLeft > 0)
                {
                    phoneSource.Stop();
                    phoneSource.clip = phoneRing;
                    phoneSource.Play();
                    ringsLeft--;
                    nextRingAt = cueTime + 3.6f;
                }
                if (Facing(cueSpot, 70f)) { Debug.Log("[LookBackLures] El telefono dejo de sonar al mirarlo."); StopAll(); }
                else if (ringsLeft == 0 && cueTime >= nextRingAt - 1f) Finish();
                break;
        }
    }

    /// <summary>
    /// El tubo al prenderse: dos o tres parpadeos y despues prendido, con algun bajon. Al final
    /// de la pista se apaga con un parpadeo.
    /// </summary>
    private static float LightLevel(float t)
    {
        if (t < 0.08f) return 1f;
        if (t < 0.2f) return 0f;
        if (t < 0.26f) return 0.7f;
        if (t < 0.45f) return 0f;
        float level = 1f;
        if (Mathf.Repeat(t * 1.7f, 4.3f) < 0.06f) level = 0.25f; // bajones cortos
        if (t > 7.6f) level = t < 7.7f ? 0f : (t < 7.75f ? 0.6f : 0f);
        return level;
    }

    /// <summary>La pista termino sola (sin que el jugador se diera vuelta).</summary>
    private void Finish()
    {
        StopAll();
        float gap = Mathf.Max(minGap, Random.Range(gapMin, gapMax) - gapShrinkPerCue * inARow);
        nextCueAt = Time.time + gap;
    }

    private void StopAll()
    {
        Active = Cue.None;
        if (bellsSource != null) bellsSource.Stop();
        if (phoneSource != null) phoneSource.Stop();
        if (lightHum != null) lightHum.Stop();
        if (backLight != null) backLight.enabled = false;
        SetShadowBody(false);
    }

    private void SetShadowBody(bool on)
    {
        if (shadowBody == null) return;
        foreach (Renderer r in shadowBody)
            if (r != null) r.enabled = on;
    }

    // ------------------------------------------------------------------ ayudas

    /// <summary>El telefono publico esta puesto, quedo atras y no muy lejos.</summary>
    private bool PhoneBehind(out Vector3 position)
    {
        position = Vector3.zero;
        if (phoneLandmark == null || !phoneLandmark.gameObject.activeInHierarchy) return false;
        position = phoneLandmark.Center;
        Vector3 to = position - player.position;
        to.y = 0f;
        float d = to.magnitude;
        return d > 4f && d < 28f && Vector3.Dot(to / d, Flat(view.transform.forward)) < -0.2f;
    }

    /// <summary>El centro de la vista apunta a ese punto (en horizontal) con menos de 'degrees' grados.</summary>
    private bool Facing(Vector3 point, float degrees)
    {
        Vector3 to = Flat(point - view.transform.position);
        return to.sqrMagnitude > 0.01f && Vector3.Angle(Flat(view.transform.forward), to) < degrees;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private static string Name(Cue cue) => cue switch
    {
        Cue.Cat => "gato",
        Cue.Bells => "campanillas",
        Cue.Light => "luz atras",
        Cue.Phone => "telefono",
        _ => "-",
    };

    // ------------------------------------------------------------------ pruebas

    [ContextMenu("Probar pista: gato")] private void TestCat() => StartCue(Cue.Cat);
    [ContextMenu("Probar pista: campanillas")] private void TestBells() => StartCue(Cue.Bells);
    [ContextMenu("Probar pista: luz atras")] private void TestLight() => StartCue(Cue.Light);
    [ContextMenu("Probar pista: telefono")] private void TestPhone() => StartCue(Cue.Phone);

    /// <summary>Para las pruebas: arranca una pista aunque no haya calma.</summary>
    public void Force(Cue cue)
    {
        if (Active != Cue.None) StopAll();
        StartCue(cue);
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible || !Playing()) return;
        string next = Active != Cue.None ? "sonando: " + LastCue
            : CalmTime < firstCueAfter ? $"primera a los {firstCueAfter:0} s"
            : $"proxima en {Mathf.Max(0f, nextCueAt - Time.time):0} s";
        GUI.Label(new Rect(12, 254, 800, 22),
            $"Pistas: calma {CalmTime:0} s, {next}, carteles de no mirar atras x{SignRandomizer.WarningBoost:0.0}, ultima: {LastCue}");
    }
}
