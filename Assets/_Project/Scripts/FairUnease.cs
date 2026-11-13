using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 36: "algo raro de a poco". Casi nada al principio; hacia el final, cosas chicas que no
/// rompen la calma pero preparan el capitulo 2.
///
/// Un nivel de 0 a 1 que sube por dos lados, el que llegue primero:
/// - el tiempo: nada los primeros 2 minutos; llega a 1 a los 10 minutos de feria;
/// - el gato: cada aparicion que el jugador pasa lo sube (la ultima, la del callejon, es 1).
///
/// Lo que pasa segun el nivel (cada cosa la hace su propio script; este solo les dice cuanto):
///   0.15  alguna persona mira al jugador de mas (FairPerson.StareShare), cada vez mas gente.
///   0.30  el musico y el tambor repiten el mismo clip, cada vez mas grave (FairAmbience).
///         Algun grupo de farolitos o bombitas se apaga un instante.
///   0.45  los carteles de algunos puestos cambian por uno de "no mires atras" cuando el jugador
///         no los esta mirando (uno por vez, cada tanto).
///   0.50  cada tanto, una parte de la gente que pasea se queda quieta y se da vuelta a mirar al
///         jugador unos segundos (FairWalker.Freeze).
///   0.65  el murmullo de la gente se va apagando (FairAmbience.CrowdFade).
///   0.85  algunos de los que pasean desaparecen cuando nadie los mira (FairWalker.VanishShare).
///   final (gato en el callejon): los vendedores miran todos al jugador y casi no hay murmullo.
///
/// La primera vez que el jugador ve algo raro de cerca, el personaje lo piensa (ThoughtText).
/// Pruebas (solo en el editor): F10 sube el nivel 0.25; con F1 se ve el nivel.
///
/// Va en el objeto "LoRaro". Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class FairUnease : MonoBehaviour
{
    [Header("Cuando")]
    [Tooltip("Segundos de feria antes de que empiece (por tiempo).")]
    [SerializeField] private float startAfter = 120f;
    [Tooltip("Segundos de feria en los que llega a 1 (por tiempo).")]
    [SerializeField] private float fullAt = 600f;
    [SerializeField] private CatTrail catTrail;

    [Header("Carteles que cambian")]
    [SerializeField] private Renderer[] stallSigns;
    [SerializeField] private Material[] warningSigns;

    [Header("Luces que titilan")]
    [SerializeField] private Renderer[] lights;

    [Header("Pruebas")]
    [SerializeField] private bool debugKeys = true;

    private static readonly string[] SignLines =
    {
        "¿Ese cartel no era otro?",
        "Juraría que ese cartel era distinto.",
    };
    private const string FrozenLine = "¿Por qué se quedaron todos quietos?";
    private const string QuietLine = "Se hizo mucho silencio de golpe.";

    private float playTime, boost, nextSign, nextFreeze, nextFlicker;
    private int swapped;
    private readonly List<Renderer> changed = new List<Renderer>();
    private bool sawSign, sawFrozen, saidQuiet;
    private float checkFrozenAt = -1f;
    private Camera cam;
    private Transform player;

    public float Level { get; private set; }
    private float timeFactor, catFactor;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(CatTrail trail, Renderer[] signs, Material[] warnings, Renderer[] flickerLights)
    {
        catTrail = trail;
        stallSigns = signs;
        warningSigns = warnings;
        lights = flickerLights;
    }

    private void Awake() => ResetAll();
    private void OnDestroy() => ResetAll();

    private static void ResetAll()
    {
        FairPerson.StareShare = 0f;
        FairPerson.VendorsStare = false;
        FairWalker.ResetUnease();
        FairAmbience.RepeatAmount = 0f;
        FairAmbience.CrowdFade = 0f;
    }

    private void Start()
    {
        cam = Camera.main;
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        nextSign = nextFreeze = nextFlicker = 0f;
    }

    private void Update()
    {
        bool playing = GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;
        if (playing) playTime += Time.deltaTime;

#if UNITY_EDITOR
        if (debugKeys && BoostPressed()) boost = Mathf.Min(1f, boost + 0.25f);
#endif
        timeFactor = Mathf.Clamp01((playTime - startAfter) / Mathf.Max(1f, fullAt - startAfter));
        bool final = false;
        catFactor = 0f;
        if (catTrail != null && catTrail.Count > 1)
        {
            int at = Mathf.Clamp(catTrail.Current, 0, catTrail.Count - 1);
            catFactor = at / (float)(catTrail.Count - 1);
            final = catTrail.Current >= catTrail.Count - 1;
        }
        Level = Mathf.Clamp01(Mathf.Max(Mathf.Max(timeFactor, catFactor), boost));
        if (boost >= 1f) final = true;

        // Lo que hacen los otros scripts.
        FairPerson.StareShare = Ramp(0.15f, 1f) * 0.75f;
        FairPerson.VendorsStare = final;
        FairAmbience.RepeatAmount = Ramp(0.3f, 1f);
        FairAmbience.CrowdFade = final ? 0.95f : Ramp(0.65f, 1f) * 0.8f;
        FairWalker.VanishShare = Ramp(0.85f, 1f) * 0.45f;

        if (!playing) return;
        float t = Time.time;

        // Farolitos que se apagan un instante.
        if (Level >= 0.3f && t >= nextFlicker && lights != null && lights.Length > 0)
        {
            nextFlicker = t + Random.Range(5f, 12f) / (0.5f + Level);
            StartCoroutine(Flicker(lights[Random.Range(0, lights.Length)]));
        }

        // Carteles que cambian (sin que se vea el cambio).
        if (Level >= 0.45f && t >= nextSign) TrySwapSign(t);
        if (!sawSign) CheckChangedSignSeen();

        // La gente que se queda quieta mirando.
        if (Level >= 0.5f && t >= nextFreeze)
        {
            nextFreeze = t + Random.Range(35f, 60f) * (1.3f - Level * 0.5f);
            FairWalker.Freeze(Random.Range(2.5f, 4f), 0.35f + 0.5f * Ramp(0.5f, 1f));
            checkFrozenAt = t + 1.2f;
        }
        if (!sawFrozen && checkFrozenAt > 0f && t >= checkFrozenAt)
        {
            checkFrozenAt = -1f;
            if (FairWalker.FrozenInView >= 2) { sawFrozen = true; ThoughtText.Show(FrozenLine, 3.6f); }
        }

        if (final && !saidQuiet && t > 0f)
        {
            saidQuiet = true;
            StartCoroutine(SayLater(QuietLine, 6f));
        }
    }

    /// <summary>0 por debajo de 'from', 1 en 'to', lineal en el medio.</summary>
    private float Ramp(float from, float to) => Mathf.Clamp01((Level - from) / Mathf.Max(0.001f, to - from));

    private IEnumerator Flicker(Renderer r)
    {
        if (r == null || !r.enabled) yield break;
        int blinks = Random.value < 0.4f ? 2 : 1;
        for (int i = 0; i < blinks; i++)
        {
            r.enabled = false;
            yield return new WaitForSeconds(Random.Range(0.06f, 0.22f));
            r.enabled = true;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
        }
    }

    private IEnumerator SayLater(string line, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        ThoughtText.Show(line, 3.6f);
    }

    private void TrySwapSign(float t)
    {
        nextSign = t + 4f;
        if (stallSigns == null || warningSigns == null || warningSigns.Length == 0) return;
        int allowed = Mathf.CeilToInt(Ramp(0.45f, 1f) * 4f) + 1; // hasta 5 carteles cambiados
        if (swapped >= allowed) return;
        // Uno que no se este viendo y que no este muy cerca.
        for (int tries = 0; tries < 6; tries++)
        {
            Renderer r = stallSigns[Random.Range(0, stallSigns.Length)];
            if (r == null || changed.Contains(r) || Visible(r.transform.position, 90f) || Near(r.transform.position, 8f)) continue;
            r.sharedMaterial = warningSigns[Random.Range(0, warningSigns.Length)];
            changed.Add(r);
            swapped++;
            nextSign = t + Random.Range(25f, 45f);
            return;
        }
    }

    private void CheckChangedSignSeen()
    {
        foreach (Renderer r in changed)
        {
            if (r == null || !Near(r.transform.position, 9f)) continue;
            if (!Visible(r.transform.position, 40f, 0.25f)) continue;
            sawSign = true;
            ThoughtText.Show(SignLines[Random.Range(0, SignLines.Length)], 3.6f);
            return;
        }
    }

    private bool Near(Vector3 p, float meters) => player != null && Vector3.Distance(player.position, p) < meters;

    /// <summary>Esta en pantalla (margin = cuanto del centro: 0 = toda la pantalla, 0.25 = el centro).</summary>
    private bool Visible(Vector3 p, float maxDistance, float margin = -0.1f)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.z > 0f && v.z < maxDistance && v.x > margin && v.x < 1f - margin && v.y > margin && v.y < 1f - margin;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool BoostPressed() => Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame;
#else
    private static bool BoostPressed() => Input.GetKeyDown(KeyCode.F10);
#endif

    private void OnGUI()
    {
        if (!DebugOverlay.Visible) return;
        GUI.Label(new Rect(10, 380, 500, 20), "raro: nivel " + Level.ToString("0.00") + " (tiempo " + timeFactor.ToString("0.00") +
            ", gato " + catFactor.ToString("0.00") + (boost > 0f ? ", F10 " + boost.ToString("0.00") : "") + ") · carteles " + swapped);
    }
}
