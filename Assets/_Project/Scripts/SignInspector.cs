using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 25: MIRAR DE CERCA. Con un cartel, afiche o papelito cerca y en el centro de la vista,
/// abajo aparece "E · mirar". Con E el cartel se ve grande, tal cual (sin traducir), y abajo
/// aparece lo que piensa el personaje, letra por letra (SignCatalog.Line).
///
/// Mientras se mira, el jugador no se mueve ni gira, pero el tiempo sigue: lo que esta a la
/// espalda sigue siendo punto ciego y el laberinto puede cambiar. Se cierra con E, Esc o un clic.
///
/// Que carteles se pueden mirar: los de SignRandomizer.Active (los de las piezas prendidas),
/// a menos de maxDistance, a menos de maxAngle del centro de la vista, de frente y sin una
/// pared en el medio. Los carteles no tienen collider, por eso no se usa un Raycast.
///
/// La interfaz se arma sola al empezar, dentro del Canvas de GameUI (misma fuente y escala que
/// los menus) y detras de todos sus paneles, asi el fundido a negro la tapa.
/// Va en el Player. La pone la herramienta Callejones > Pistas y mirar de cerca.
///
/// Paso 28: tambien se pueden mirar objetos (Inspectable): latas, jaulas, ventanas, la
/// persiana, el telefono... Con un objeto no se muestra una imagen: el fondo se oscurece apenas,
/// el mundo sigue a la vista y abajo aparece la linea. Las lineas de los objetos estan en
/// Dialogos/Objetos.txt (ObjectLines); las de los carteles, en SignCatalog.cs.
/// Si un cartel y un objeto estan cerca, gana el que este mas cerca del centro de la vista.
///
/// Paso 30: las notas escritas a mano (carteles M_) dicen lo que esta en Dialogos/Notas.txt, y
/// cada vez que se abre un cartel se avisa con SignOpened (lo usa PosterEchoes: algunos afiches
/// hacen sonar algo cerca).
/// </summary>
public class SignInspector : MonoBehaviour
{
    /// <summary>true mientras se esta mirando un cartel. GameFlow no pausa con Esc mientras tanto.</summary>
    public static bool IsOpen { get; private set; }
    /// <summary>El cuadro en que se cerro: ese mismo Esc tampoco tiene que pausar.</summary>
    public static int ClosedFrame { get; private set; } = -1;
    /// <summary>Paso 30: se abrio un cartel (el cartel y el nombre de su material, sin "(Instance)").</summary>
    public static event System.Action<SignRandomizer, string> SignOpened;

    [Header("Referencias")]
    [Tooltip("La camara del jugador. Si se deja vacia, usa la Main Camera.")]
    [SerializeField] private Camera view;
    [Tooltip("Si se deja vacio, usa el de este mismo objeto.")]
    [SerializeField] private FirstPersonController controller;
    [Tooltip("Paso 28: las lineas de los objetos (Dialogos/Objetos.txt).")]
    [SerializeField] private TextAsset objectLinesFile;
    [Tooltip("Paso 30: lo que dicen las notas escritas a mano (Dialogos/Notas.txt).")]
    [SerializeField] private TextAsset notesFile;
    [Tooltip("Paso 28: con miedo alto, los objetos dicen otras cosas. Si se deja vacio, usa el de este objeto.")]
    [SerializeField] private FearMeter fear;

    [Header("Que se puede mirar")]
    [Tooltip("Metros maximos desde los ojos hasta el centro del cartel.")]
    [SerializeField] private float maxDistance = 3f;
    [Tooltip("Grados maximos entre el centro de la vista y el cartel.")]
    [SerializeField] private float maxAngle = 13f;

    [Header("Como se ve")]
    [Tooltip("Lo maximo que ocupa el cartel, en unidades de la interfaz (640 x 360).")]
    [SerializeField] private Vector2 maxImageSize = new Vector2(340f, 236f);
    [Tooltip("Letras por segundo de la linea del personaje.")]
    [SerializeField] private float charsPerSecond = 40f;
    [Tooltip("Segundos del fundido al abrir y al cerrar.")]
    [SerializeField] private float fadeTime = 0.15f;
    [Tooltip("Paso 28: que tan oscuro queda el fondo al mirar un objeto (con un cartel es 0.93).")]
    [SerializeField, Range(0f, 1f)] private float objectDim = 0.25f;

    public string LastLine { get; private set; } = "";
    public SignRandomizer Target { get; private set; }
    /// <summary>Paso 28: el objeto que se miraria con E ahora (si gana contra los carteles).</summary>
    public Inspectable TargetObject { get; private set; }

    private CanvasGroup panel, prompt;
    private RawImage picture;
    private Image background, shadow;
    private ObjectLines objectLines;
    private ObjectLines notes;
    private readonly Dictionary<string, int> objectsSeen = new Dictionary<string, int>();
    private RectTransform pictureRect, shadowRect;
    private TMPro.TMP_Text line;
    private float openTime, panelAlpha, promptAlpha;
    private readonly Dictionary<string, int> timesSeen = new Dictionary<string, int>();
    private int warningsSeen;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Camera playerCamera, FirstPersonController firstPerson)
    {
        view = playerCamera;
        controller = firstPerson;
    }

    /// <summary>Paso 28: lo usa la herramienta de editor.</summary>
    public void SetupObjects(TextAsset linesFile, FearMeter fearMeter)
    {
        objectLinesFile = linesFile;
        fear = fearMeter;
    }

    /// <summary>Paso 30: lo usa la herramienta de editor.</summary>
    public void SetupNotes(TextAsset file) => notesFile = file;

    private void Awake()
    {
        if (view == null) view = Camera.main;
        if (controller == null) controller = GetComponent<FirstPersonController>();
        if (fear == null) fear = GetComponent<FearMeter>();
        objectLines = new ObjectLines(objectLinesFile);
        notes = new ObjectLines(notesFile);
        IsOpen = false;
        BuildInterface();
    }

    private void OnDisable()
    {
        if (IsOpen) Close();
    }

    private static bool Playing() =>
        GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    private void Update()
    {
        if (panel == null || view == null) return;

        if (!Playing())
        {
            if (IsOpen) Close();
            Target = null;
            TargetObject = null;
        }
        else if (IsOpen)
        {
            UpdateOpen();
        }
        else
        {
            // Paso 28: el cartel y el objeto mas centrados; gana el mas cerca del centro.
            SignRandomizer sign = FindTarget(out float signAngle);
            Inspectable thing = FindObjectTarget(out float thingAngle);
            if (sign != null && thing != null)
            {
                // Paso 29: los carteles tienen 3 grados de ventaja. Lo que se quiere leer casi
                // siempre es el cartel, no lo que esta al lado.
                if (signAngle <= thingAngle + 3f) thing = null;
                else sign = null;
            }
            Target = sign;
            TargetObject = thing;
            if (InspectPressed())
            {
                if (Target != null) Open(Target);
                else if (TargetObject != null) Open(TargetObject);
            }
        }

        // Fundidos con tiempo sin escala: la pausa pone el tiempo en 0.
        float dt = Time.unscaledDeltaTime;
        float step = fadeTime > 0f ? dt / fadeTime : 1f;
        panelAlpha = Mathf.MoveTowards(panelAlpha, IsOpen ? 1f : 0f, step);
        panel.alpha = panelAlpha;
        bool hasTarget = Target != null || TargetObject != null;
        promptAlpha = Mathf.MoveTowards(promptAlpha, !IsOpen && hasTarget ? 0.75f : 0f, step);
        prompt.alpha = promptAlpha;
        if (IsOpen)
        {
            // El cartel se acerca un poco al abrir: de 96 % a 100 %.
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - openTime) / 0.25f));
            float s = Mathf.Lerp(0.96f, 1f, k);
            pictureRect.localScale = new Vector3(s, s, 1f);
            shadowRect.localScale = pictureRect.localScale;
        }
    }

    // ------------------------------------------------------------------ abrir y cerrar

    private void Open(SignRandomizer sign)
    {
        Material mat = sign.CurrentMaterial;
        if (mat == null && sign.Faces != null && sign.Faces.Length > 0 && sign.Faces[0] != null)
            mat = sign.Faces[0].sharedMaterial;
        if (mat == null) return;

        Texture tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture;
        if (tex == null) return;

        picture.texture = tex;
        ShowPicture(true);
        // Lo mas grande que entre. Con 2 o mas, escala entera (pixeles parejos, como en la PS1).
        float fit = Mathf.Min(maxImageSize.x / tex.width, maxImageSize.y / tex.height);
        float scale = fit >= 2f ? Mathf.Floor(fit) : fit;
        Vector2 size = new Vector2(tex.width * scale, tex.height * scale);
        pictureRect.sizeDelta = size;
        shadowRect.sizeDelta = size;

        string key = SignCatalog.Clean(mat.name);
        timesSeen.TryGetValue(key, out int seen);
        // Paso 30: las notas a mano dicen lo que esta en Notas.txt; el resto, lo de SignCatalog.
        LastLine = key.StartsWith("M_") && notes != null && notes.Count > 0
            ? notes.Line(key, seen, fear != null ? fear.Normalized : 0f)
            : SignCatalog.Line(key, seen, warningsSeen);
        timesSeen[key] = seen + 1;
        if (SignCatalog.IsWarning(key)) warningsSeen++;

        line.text = LastLine;
        line.maxVisibleCharacters = 0;
        openTime = Time.unscaledTime;
        IsOpen = true;
        if (controller != null) controller.enabled = false;
        Debug.Log($"[SignInspector] Mirando '{key}': {LastLine}");
        SignOpened?.Invoke(sign, key);
    }

    /// <summary>Paso 28: mirar un objeto. Sin imagen: el mundo sigue a la vista, un poco mas oscuro.</summary>
    private void Open(Inspectable thing)
    {
        ShowPicture(false);
        objectsSeen.TryGetValue(thing.Key, out int seen);
        LastLine = objectLines.Line(thing.Key, seen, fear != null ? fear.Normalized : 0f);
        objectsSeen[thing.Key] = seen + 1;

        line.text = LastLine;
        line.maxVisibleCharacters = 0;
        openTime = Time.unscaledTime;
        IsOpen = true;
        if (controller != null) controller.enabled = false;
        Debug.Log($"[SignInspector] Mirando objeto '{thing.Key}': {LastLine}");
    }

    /// <summary>Con un cartel: imagen grande y fondo casi negro. Con un objeto: sin imagen y fondo apenas oscuro.</summary>
    private void ShowPicture(bool value)
    {
        picture.enabled = value;
        shadow.enabled = value;
        Color c = background.color;
        c.a = value ? 0.93f : objectDim;
        background.color = c;
    }

    private void UpdateOpen()
    {
        float t = Time.unscaledTime - openTime;
        line.maxVisibleCharacters = Mathf.Min(line.text.Length, Mathf.FloorToInt(t * charsPerSecond));
        // Un instante de gracia: el mismo E que abrio no tiene que cerrar.
        if (t > 0.2f && ClosePressed()) Close();
    }

    private void Close()
    {
        IsOpen = false;
        ClosedFrame = Time.frameCount;
        // Si el juego sigue (no se cerro por una pausa o el final), el jugador vuelve a moverse.
        if (controller != null && Playing()) controller.enabled = true;
    }

    // ------------------------------------------------------------------ que se esta mirando

    private SignRandomizer FindTarget(out float bestAngle)
    {
        Transform cam = view.transform;
        Vector3 eye = cam.position;
        float best = maxAngle;
        SignRandomizer found = null;

        foreach (SignRandomizer sign in SignRandomizer.Active)
        {
            if (sign == null || sign.Faces == null) continue;
            foreach (Renderer face in sign.Faces)
            {
                if (face == null || !face.isVisible) continue;
                Vector3 center = face.bounds.center;
                Vector3 to = center - eye;
                float dist = to.magnitude;
                if (dist > maxDistance || dist < 0.05f) continue;

                float angle = Vector3.Angle(cam.forward, to);
                if (angle >= best) continue;

                // El Quad se ve desde su -Z: esa es la cara con las letras.
                Vector3 normal = -face.transform.forward;
                if (Vector3.Dot(normal, to / dist) > -0.25f) continue; // de costado o de espaldas

                // Sin pared en el medio. Lo que este pegado al cartel (su marco, la pared donde
                // esta) queda a menos de 15 cm del final de la linea: eso no tapa.
                Vector3 front = center + normal * 0.06f;
                float reach = Vector3.Distance(eye, front);
                if (Physics.Linecast(eye, front, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.distance < reach - 0.15f)
                    continue;

                best = angle;
                found = sign;
            }
        }
        bestAngle = best;
        return found;
    }

    /// <summary>
    /// Paso 28: el objeto mas centrado en la vista, a menos de su distancia maxima, sin una pared
    /// en el medio. Lo que la linea choca del propio objeto (una maceta tiene collider) no cuenta.
    /// </summary>
    private Inspectable FindObjectTarget(out float bestAngle)
    {
        Transform cam = view.transform;
        Vector3 eye = cam.position;
        float best = float.MaxValue; // aca se compara el puntaje (angulo + tamano), no solo el angulo
        Inspectable found = null;

        foreach (Inspectable thing in Inspectable.Active)
        {
            if (thing == null || !thing.TryGetVisibleBounds(out Bounds box)) continue;
            Vector3 center = box.center;
            Vector3 to = center - eye;
            float dist = to.magnitude;
            if (dist > thing.MaxDistance || dist < 0.05f) continue;

            // El angulo hasta el punto de la caja mas cercano a la linea de la mirada: una
            // persiana grande se puede mirar por cualquier parte, una lata solo de cerca del centro.
            Vector3 aim = eye + cam.forward * dist;
            Vector3 nearest = box.ClosestPoint(aim);
            float angle = Vector3.Angle(cam.forward, nearest - eye);
            if (angle >= maxAngle) continue;
            // Los objetos grandes suman 1.5 grados por metro (hasta 5): asi, mirando un papelito
            // pegado en la persiana, gana el papelito; mirando la sal de la puerta, gana la sal y
            // no la entrada. Mirando la persiana lejos del papelito, gana la persiana.
            float score = angle + Mathf.Min(5f, 1.5f * box.size.magnitude);
            if (score >= best) continue;

            if (Physics.Linecast(eye, center, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(thing.transform) && hit.distance < dist - box.extents.magnitude - 0.05f)
                continue;

            best = score;
            found = thing;
        }
        bestAngle = best;
        return found;
    }

    // ------------------------------------------------------------------ la interfaz

    private void BuildInterface()
    {
        GameUI ui = FindAnyObjectByType<GameUI>(FindObjectsInactive.Include);
        Canvas canvas = ui != null ? ui.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            Debug.LogWarning("[SignInspector] No hay Canvas de GameUI: no se puede mirar de cerca.", this);
            enabled = false;
            return;
        }

        TMPro.TMP_Text sample = ui.GetComponentInChildren<TMPro.TMP_Text>(true);
        Color paper = new Color(0.91f, 0.84f, 0.69f, 1f);

        // El panel: fondo oscuro, la sombra del cartel, el cartel y la linea.
        RectTransform root = NewRect("MirarDeCerca", canvas.transform);
        Stretch(root);
        root.SetSiblingIndex(0); // detras de los paneles y del fundido a negro
        panel = root.gameObject.AddComponent<CanvasGroup>();
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;

        RectTransform bg = NewRect("Fondo", root);
        Stretch(bg);
        background = bg.gameObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.01f, 0.01f, 0.93f);

        shadowRect = NewRect("Sombra", root);
        shadowRect.anchoredPosition = new Vector2(5f, 21f);
        shadow = shadowRect.gameObject.AddComponent<Image>();
        shadow.color = new Color(0f, 0f, 0f, 0.55f);

        pictureRect = NewRect("Cartel", root);
        pictureRect.anchoredPosition = new Vector2(0f, 26f);
        picture = pictureRect.gameObject.AddComponent<RawImage>();
        picture.raycastTarget = false;

        RectTransform lineRect = NewRect("Linea", root);
        lineRect.anchorMin = lineRect.anchorMax = new Vector2(0.5f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.anchoredPosition = new Vector2(0f, 18f);
        lineRect.sizeDelta = new Vector2(560f, 62f); // Paso 30: tres renglones (las notas son largas)
        line = NewText(lineRect, sample, 14f, paper);

        // La indicacion: aparte, para que se vea sin el panel.
        RectTransform hint = NewRect("MirarIndicacion", canvas.transform);
        hint.SetSiblingIndex(1);
        hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0f);
        hint.pivot = new Vector2(0.5f, 0f);
        hint.anchoredPosition = new Vector2(0f, 74f);
        hint.sizeDelta = new Vector2(200f, 18f);
        prompt = hint.gameObject.AddComponent<CanvasGroup>();
        prompt.alpha = 0f;
        prompt.interactable = false;
        prompt.blocksRaycasts = false;
        NewText(hint, sample, 11f, paper).text = "E · mirar";
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TMPro.TMP_Text NewText(RectTransform rect, TMPro.TMP_Text sample, float size, Color color)
    {
        var text = rect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        if (sample != null) text.font = sample.font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.textWrappingMode = TMPro.TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    // ------------------------------------------------------------------ teclas

#if ENABLE_INPUT_SYSTEM
    private static bool InspectPressed() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;

    private static bool ClosePressed()
    {
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        return (k != null && (k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)) ||
               (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame));
    }
#else
    private static bool InspectPressed() => Input.GetKeyDown(KeyCode.E);

    private static bool ClosePressed() =>
        Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) ||
        Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
#endif
}
