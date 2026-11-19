using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 40: lo que el jugador se trajo de la feria cambia los callejones (capitulo 2). Lee
/// FairPocket y FairPrizes, que sobreviven al cambio de escena.
///
/// 1. La frase de la adivina se cumple:
///    - "Cuando las calles cambien, no te des vuelta para comprobarlo": cada mirada atras suma
///      miedo de mas.
///    - "El gato sabe donde esta la salida": cuando aparece la salida, hay un gato sentado ahi que
///      maulla cada tanto; se lo oye de lejos y guia.
///    - "Vas a pasar tres veces por el mismo lugar. A la tercera, mira arriba": en la tercera
///      pasada de un bucle (CorridorTreadmill), arriba del cruce aparece el gato en una cornisa.
///      Verlo calma un poco.
///    - "Lo que te sigue no camina. Espera": la Figura no da pasos y casi siempre espera adelante.
/// 2. La mascara puesta (MaskView): la Figura aparece mucho menos (no te reconoce) y los carteles
///    de "no mires atras" muestran otra cosa: una patita de gato.
/// 3. La patita de la tarjeta: tambien el gato de la salida. Haber hablado con la duena: al final,
///    el personaje piensa en avisarle.
/// 4. Lo que se llevo (el peluche, las mascaras, el pez, la bolita, el papelito, el afiche) aparece
///    cada tanto tirado en un callejon de adelante, como si alguien hubiera pasado antes.
///
/// Pruebas (solo en el editor): F11 da todo lo de la feria y pasa a la frase siguiente; F1 muestra
/// en que esta. Va en el objeto "EcosDeLaFeria". Lo arma Callejones > Ecos de la feria (capitulo 2).
/// </summary>
public class FairEchoes : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CorridorTreadmill treadmill;
    [SerializeField] private FigurePresence figure;
    [SerializeField] private ParanoiaSensor paranoia;
    [SerializeField] private FearMeter fear;

    [Header("Objetos (escondidos hasta que hacen falta)")]
    [Tooltip("Ids: Peluche, Mascara, MascaraGato, Pez, Bolita, Papelito, AficheGato.")]
    [SerializeField] private string[] itemIds;
    [SerializeField] private GameObject[] itemModels;
    [Tooltip("El gato de la salida y el de la cornisa (con su repisa).")]
    [SerializeField] private GameObject exitCat;
    [SerializeField] private GameObject ledgeCat;
    [SerializeField] private AudioSource exitMeow;
    [SerializeField] private AudioSource ledgeMeow;
    [SerializeField] private AudioClip[] meows;
    [Tooltip("Los carteles con patitas (lo que muestran los de 'no mires atras' con la mascara): vertical y horizontal.")]
    [SerializeField] private Material pawSignV;
    [SerializeField] private Material pawSignH;

    [Header("Ecos")]
    [SerializeField] private float firstEchoAfter = 70f;
    [SerializeField] private Vector2 echoEvery = new Vector2(90f, 150f);
    [SerializeField] private float lookBackFear = 3f;

    [Header("Pruebas")]
    [SerializeField] private bool debugKeys = true;

    private enum Fortune { Ninguna = -1, NoDarseVuelta = 0, GatoSalida = 1, TresVeces = 2, NoCamina = 3 }

    private static readonly Dictionary<string, string> EchoLines = new Dictionary<string, string>
    {
        { "Peluche", "Un peluche como el que gané, tirado en el piso. ¿El mío no lo tenía yo?" },
        { "Mascara", "Una máscara de zorro, igual a la de la feria. Boca arriba, mirando el cielo." },
        { "MascaraGato", "Una máscara de gato en el piso. Como la que compré." },
        { "Pez", "Una bolsita con un pez naranja. Todavía nada." },
        { "Bolita", "Una bolita de vidrio. Como la de la ramune." },
        { "Papelito", "Un papelito escrito. Es la letra de la adivina." },
        { "AficheGato", "El afiche del gato, pegado acá también." },
    };

    private Transform player;
    private Camera cam;
    private Fortune fortune = Fortune.Ninguna;
    private Vector3 lastPos;
    private float walked, nextEchoAt;
    private int echoIndex;
    private readonly Dictionary<CorridorSegment, GameObject> placed = new Dictionary<CorridorSegment, GameObject>();
    private readonly HashSet<string> seenEchoes = new HashSet<string>();
    private readonly Dictionary<Renderer, Material> swappedSigns = new Dictionary<Renderer, Material>();
    private float nextSignCheck, nextExitMeow, nextLedgeMeow;
    private CorridorSegment ledgeSegment;
    private bool saidLookBack, saidExitCat, saidLedge, saidFigure, saidMaskSign, saidEnding;
    private int figureAppearances;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(CorridorTreadmill maze, FigurePresence fig, ParanoiaSensor sensor, FearMeter fearMeter,
        string[] ids, GameObject[] models, GameObject catAtExit, GameObject catOnLedge, AudioSource exitVoice,
        AudioSource ledgeVoice, AudioClip[] catClips, Material pawVertical, Material pawHorizontal)
    {
        treadmill = maze; figure = fig; paranoia = sensor; fear = fearMeter;
        itemIds = ids; itemModels = models; exitCat = catAtExit; ledgeCat = catOnLedge;
        exitMeow = exitVoice; ledgeMeow = ledgeVoice; meows = catClips; pawSignV = pawVertical; pawSignH = pawHorizontal;
    }

    private void Awake()
    {
        FigurePresence.ChanceScale = 1f;
        FigurePresence.NoSteps = false;
        FigurePresence.PeekShareOverride = -1f;
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        cam = Camera.main;
        if (player != null) lastPos = player.position;
        nextEchoAt = firstEchoAfter;
        foreach (GameObject m in itemModels) if (m != null) m.SetActive(false);
        if (exitCat != null) exitCat.SetActive(false);
        if (ledgeCat != null) ledgeCat.SetActive(false);

        if (paranoia != null) paranoia.onLookBack.AddListener(OnLookBack);
        if (treadmill != null)
        {
            treadmill.onExitPlaced.AddListener(OnExitPlaced);
            treadmill.PiecePlaced += OnPiecePlaced;
            treadmill.PieceRecycled += OnPieceRecycled;
            treadmill.LoopCrossReached += OnLoopCross;
        }
        ReadFortune();
    }

    private void OnDestroy()
    {
        FigurePresence.ChanceScale = 1f;
        FigurePresence.NoSteps = false;
        FigurePresence.PeekShareOverride = -1f;
        if (treadmill != null)
        {
            treadmill.PiecePlaced -= OnPiecePlaced;
            treadmill.PieceRecycled -= OnPieceRecycled;
            treadmill.LoopCrossReached -= OnLoopCross;
        }
    }

    /// <summary>Cual de las cuatro frases le dio la adivina (por las palabras).</summary>
    private void ReadFortune()
    {
        string f = FairPocket.Fortune ?? "";
        fortune = f.Contains("des vuelta") ? Fortune.NoDarseVuelta
            : f.Contains("gato sabe") ? Fortune.GatoSalida
            : f.Contains("tres veces") ? Fortune.TresVeces
            : f.Contains("no camina") ? Fortune.NoCamina
            : Fortune.Ninguna;
        FigurePresence.NoSteps = fortune == Fortune.NoCamina;
        FigurePresence.PeekShareOverride = fortune == Fortune.NoCamina ? 0.85f : -1f;
    }

    private static bool Playing() => GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    private void Update()
    {
#if UNITY_EDITOR
        if (debugKeys && DebugPressed()) DebugGiveAll();
#endif
        if (GameFlow.Instance != null && GameFlow.Instance.Current == GameFlow.State.Ending && !saidEnding) SayEnding();
        if (!Playing() || player == null) return;

        Vector3 d = player.position - lastPos;
        d.y = 0f;
        if (d.magnitude < 2f) walked += d.magnitude;
        lastPos = player.position;

        // La mascara: la Figura casi no la encuentra.
        FigurePresence.ChanceScale = MaskView.On ? 0.35f : 1f;
        if (Time.time >= nextSignCheck) { nextSignCheck = Time.time + 0.4f; UpdateMaskSigns(); }

        // "Lo que te sigue no camina. Espera."
        if (figure != null && figure.Appearances > figureAppearances)
        {
            figureAppearances = figure.Appearances;
            if (fortune == Fortune.NoCamina && !saidFigure)
            {
                saidFigure = true;
                ThoughtText.Show("No camina. Espera. Como dijo la adivina.", 4f);
            }
        }

        UpdateExitCat();
        UpdateLedgeCat();
        CheckEchoesSeen();
    }

    // ------------------------------------------------------------------ 1. la frase

    private void OnLookBack()
    {
        if (fortune != Fortune.NoDarseVuelta || !Playing()) return;
        if (fear != null) fear.AddFear(lookBackFear);
        if (!saidLookBack)
        {
            saidLookBack = true;
            ThoughtText.Show("«No te des vuelta para comprobarlo.» Eso me dijo la adivina.", 4.5f);
        }
    }

    private bool WantsExitCat => fortune == Fortune.GatoSalida || FairPocket.PawStamp;

    private void OnExitPlaced()
    {
        if (!WantsExitCat || exitCat == null || treadmill.ExitSegment == null) return;
        CorridorSegment seg = treadmill.ExitSegment;
        // En la entrada de la salida (debajo del torii), mirando al jugador.
        Vector3 inside = seg.transform.TransformPoint(seg.LocalCellCenter(0));
        if (!FloorAt(inside, out Vector3 floor)) floor = new Vector3(inside.x, player.position.y - 1f, inside.z);
        exitCat.transform.position = floor;
        Vector3 toPlayer = player.position - floor;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f) exitCat.transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
        exitCat.SetActive(true);
        nextExitMeow = Time.time + 2f;
        if (!saidExitCat)
        {
            saidExitCat = true;
            ThoughtText.Show(fortune == Fortune.GatoSalida
                ? "Un gato maúlla, lejos. «El gato sabe dónde está la salida.»"
                : "Un gato maúlla, lejos. Como la patita de la tarjeta.", 4.5f);
        }
    }

    private void UpdateExitCat()
    {
        if (exitCat == null || !exitCat.activeSelf) return;
        if (treadmill == null || treadmill.ExitSegment == null) { exitCat.SetActive(false); return; }
        if (Time.time >= nextExitMeow) { PlayMeow(exitMeow); nextExitMeow = Time.time + Random.Range(6f, 10f); }
    }

    private void OnLoopCross(int pass, CorridorSegment cross)
    {
        if (fortune != Fortune.TresVeces || pass != 2 || ledgeCat == null || cross == null) return;
        // Arriba del cruce, contra la pared mas cercana: el gato en una cornisa.
        Vector3 center = cross.transform.TransformPoint(cross.LocalCellCenter(0));
        if (!FloorAt(center, out Vector3 floor)) return;
        Vector3 from = floor + Vector3.up * 1.5f;
        float best = 99f; RaycastHit wall = default;
        foreach (Vector3 dir in new[] { cross.transform.right, -cross.transform.right, cross.transform.forward, -cross.transform.forward })
            if (Physics.Raycast(from, dir, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore) && hit.distance < best)
            {
                best = hit.distance; wall = hit;
            }
        if (best > 6f) return;
        Vector3 normal = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
        ledgeCat.transform.position = new Vector3(wall.point.x, floor.y + 3.1f, wall.point.z) + normal * 0.2f;
        ledgeCat.transform.rotation = Quaternion.LookRotation(normal);
        ledgeCat.SetActive(true);
        ledgeSegment = cross;
        nextLedgeMeow = Time.time + 1.5f;
    }

    private void UpdateLedgeCat()
    {
        if (ledgeCat == null || !ledgeCat.activeSelf) return;
        Vector3 cat = ledgeCat.transform.position + Vector3.up * 0.3f;
        float dist = Vector3.Distance(player.position, cat);
        if (dist < 9f && Time.time >= nextLedgeMeow) { PlayMeow(ledgeMeow); nextLedgeMeow = Time.time + Random.Range(7f, 12f); }
        if (!saidLedge && dist < 14f && Visible(cat, 0.15f))
        {
            saidLedge = true;
            ThoughtText.Show("Arriba, en una cornisa: el gato. Me está mirando.", 4.5f);
            if (fear != null) fear.Relieve(15f);
        }
    }

    // ------------------------------------------------------------------ 2. la mascara

    private void UpdateMaskSigns()
    {
        if (pawSignV == null || pawSignH == null) return;
        if (MaskView.On)
        {
            foreach (SignRandomizer s in SignRandomizer.Active)
            {
                if (s == null || s.Faces == null) continue;
                foreach (Renderer face in s.Faces)
                {
                    if (face == null || IsPaw(face.sharedMaterial) || !SignCatalog.IsWarning(face.sharedMaterial != null ? face.sharedMaterial.name : "")) continue;
                    swappedSigns[face] = face.sharedMaterial;
                    Vector3 size = face.transform.lossyScale;
                    face.sharedMaterial = Mathf.Abs(size.y) >= Mathf.Abs(size.x) ? pawSignV : pawSignH;
                }
            }
            if (!saidMaskSign)
                foreach (var kv in swappedSigns)
                    if (kv.Key != null && Vector3.Distance(kv.Key.transform.position, player.position) < 7f && Visible(kv.Key.transform.position, 0.2f))
                    {
                        saidMaskSign = true;
                        ThoughtText.Show("Con la máscara, el cartel muestra otra cosa: una patita.", 4.5f);
                        break;
                    }
        }
        else if (swappedSigns.Count > 0)
        {
            foreach (var kv in swappedSigns)
                if (kv.Key != null && IsPaw(kv.Key.sharedMaterial)) kv.Key.sharedMaterial = kv.Value;
            swappedSigns.Clear();
        }
    }

    private bool IsPaw(Material m) => m != null && (m == pawSignV || m == pawSignH);

    // ------------------------------------------------------------------ 3. el final

    private void SayEnding()
    {
        saidEnding = true;
        if (FairPocket.HasFlag("DuenoGato")) ThoughtText.Show("Mañana le aviso a la señora del arco: el gato está bien.", 6f);
        else if (FairPocket.PawStamp) ThoughtText.Show("La patita de la tarjeta sigue ahí. No se borra.", 6f);
    }

    // ------------------------------------------------------------------ 4. los ecos

    private List<string> Owned()
    {
        var list = new List<string>();
        foreach (string id in itemIds)
        {
            bool has = id == "Peluche" || id == "Mascara" || id == "Pez" ? FairPrizes.Has(id)
                : id == "AficheGato" ? FairPocket.HasFlag("DuenoGato")
                : FairPocket.HasItem(id);
            if (has) list.Add(id);
        }
        return list;
    }

    private void OnPiecePlaced(CorridorSegment seg)
    {
        if (!Playing() || walked < nextEchoAt || seg == null || placed.ContainsKey(seg)) return;
        if (seg.Kind == SegmentKind.Salida) return;
        List<string> owned = Owned();
        if (owned.Count == 0) return;
        string id = owned[echoIndex % owned.Count];
        GameObject model = Model(id);
        if (model == null || model.activeSelf) return;

        Vector3 center = seg.transform.TransformPoint(seg.LocalCellCenter(seg.CellCount / 2));
        if (!FloorAt(center, out Vector3 floor)) return;
        // Contra una pared (a 25 cm), mirando al pasillo.
        Vector3 from = floor + Vector3.up * 0.4f;
        Vector3 side = Random.value < 0.5f ? seg.transform.right : -seg.transform.right;
        if (!Physics.Raycast(from, side, out RaycastHit wall, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            side = -side;
            if (!Physics.Raycast(from, side, out wall, 4f, ~0, QueryTriggerInteraction.Ignore)) return;
        }
        Vector3 normal = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
        if (id == "AficheGato")
        {
            model.transform.position = new Vector3(wall.point.x, floor.y + 1.45f, wall.point.z) + normal * 0.02f;
            model.transform.rotation = Quaternion.LookRotation(-normal); // el Quad se ve desde -Z
        }
        else
        {
            model.transform.position = new Vector3(wall.point.x, floor.y, wall.point.z) + normal * 0.25f;
            model.transform.rotation = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f);
        }
        model.SetActive(true);
        placed[seg] = model;
        echoIndex++;
        nextEchoAt = walked + Random.Range(echoEvery.x, echoEvery.y);
    }

    private void OnPieceRecycled(CorridorSegment seg)
    {
        if (seg != null && placed.TryGetValue(seg, out GameObject model))
        {
            if (model != null) model.SetActive(false);
            placed.Remove(seg);
        }
        if (seg != null && seg == ledgeSegment && ledgeCat != null) { ledgeCat.SetActive(false); ledgeSegment = null; }
    }

    private void CheckEchoesSeen()
    {
        foreach (var kv in placed)
        {
            GameObject m = kv.Value;
            if (m == null || !m.activeSelf) continue;
            string id = IdOf(m);
            if (id == null || seenEchoes.Contains(id)) continue;
            if (Vector3.Distance(player.position, m.transform.position) > 5f || !Visible(m.transform.position + Vector3.up * 0.1f, 0.2f)) continue;
            seenEchoes.Add(id);
            if (EchoLines.TryGetValue(id, out string line)) ThoughtText.Show(line, 4.5f);
            return;
        }
    }

    private GameObject Model(string id)
    {
        for (int i = 0; i < itemIds.Length && i < itemModels.Length; i++) if (itemIds[i] == id) return itemModels[i];
        return null;
    }

    private string IdOf(GameObject model)
    {
        for (int i = 0; i < itemModels.Length && i < itemIds.Length; i++) if (itemModels[i] == model) return itemIds[i];
        return null;
    }

    // ------------------------------------------------------------------ ayudas

    private void PlayMeow(AudioSource source)
    {
        if (source == null || meows == null || meows.Length == 0) return;
        source.pitch = Random.Range(0.95f, 1.08f);
        source.PlayOneShot(meows[Random.Range(0, meows.Length)]);
    }

    private static bool FloorAt(Vector3 around, out Vector3 floor)
    {
        floor = around;
        if (!Physics.Raycast(around + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return false;
        floor = hit.point;
        return true;
    }

    private bool Visible(Vector3 p, float margin)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(p);
        if (v.z <= 0f || v.x < margin || v.x > 1f - margin || v.y < margin || v.y > 1f - margin) return false;
        // Que no haya una pared en el medio.
        Vector3 eye = cam.transform.position;
        return !Physics.Linecast(eye, p, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) || Vector3.Distance(hit.point, p) < 0.4f;
    }

#if UNITY_EDITOR
    /// <summary>Prueba: todo lo de la feria y la frase siguiente.</summary>
    private void DebugGiveAll()
    {
        FairPocket.DebugFillCard();
        FairPocket.AddItem("MascaraGato");
        FairPocket.AddItem("Bolita");
        FairPocket.SetFlag("DuenoGato");
        FairPrizes.Add("Peluche"); FairPrizes.Add("Mascara"); FairPrizes.Add("Pez");
        string[] list =
        {
            "«Cuando las calles cambien, no te des vuelta para comprobarlo.»",
            "«El gato sabe dónde está la salida. Vos todavía no.»",
            "«Vas a pasar tres veces por el mismo lugar. A la tercera, mirá arriba.»",
            "«Lo que te sigue no camina. Espera.»",
        };
        FairPocket.SetFortune(list[((int)fortune + 1 + list.Length) % list.Length]);
        FairPocket.SetPaw();
        ReadFortune();
        nextEchoAt = walked; // el proximo callejon ya trae algo
    }
#endif

#if ENABLE_INPUT_SYSTEM
    private static bool DebugPressed() => Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame;
#else
    private static bool DebugPressed() => Input.GetKeyDown(KeyCode.F11);
#endif

    private void OnGUI()
    {
        if (!DebugOverlay.Visible) return;
        GUI.Label(new Rect(10, 398, 700, 20), "feria: frase " + fortune + (MaskView.On ? " · máscara" : "") +
            " · lleva " + string.Join(",", Owned()) + " · próximo eco en " + Mathf.Max(0f, nextEchoAt - walked).ToString("0") + " m");
    }
}
