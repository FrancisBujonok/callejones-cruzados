using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: sensacion de cuerpo, flujo de juego y ejecutable.
///
/// Menus:
/// - Callejones > Sensacion de cuerpo (camara): crea "Cabeza" entre Player y Main Camera y le pone CameraFeel.
/// - Callejones > Flujo de juego (titulo, pausa, final): crea el Canvas "Interfaz", el EventSystem
///   y el objeto "Juego" con GameFlow. Agrega la escena a Build Settings.
/// - Callejones > Compilar ejecutable (Windows): guarda la escena y arma Builds/Windows/CallejonesCruzados.exe.
///
/// Importante: si "Interfaz" ya existe, NO se reemplaza (para no borrar cambios hechos a mano).
/// Para regenerarla desde cero, borrala de la jerarquia y volve a ejecutar el menu.
/// Por eso este menu tampoco corre dentro de "Construir atmosfera (todo)".
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string BuildPath = "Builds/Windows/CallejonesCruzados.exe";

    // Paleta de la interfaz: papel de los carteles y el rojo anaranjado de los faroles.
    private static readonly Color UiPaper = Hex("#E8D6B0");
    private static readonly Color UiDim = Hex("#9C8A74");
    private static readonly Color UiAccent = Hex("#FF6A35");
    private static readonly Color UiPressed = Hex("#B8301E");

    // ------------------------------------------------------------------ menus

    [MenuItem("Callejones/Sensacion de cuerpo (camara)", priority = 23)]
    public static void CameraFeelMenu()
    {
        if (!CanRun()) return;
        SetupCameraFeel();
        Debug.Log("[Callejones] Sensacion de cuerpo lista. Guarda con Ctrl+S.");
    }

    [MenuItem("Callejones/Flujo de juego (titulo, pausa, final)", priority = 24)]
    public static void GameFlowMenu()
    {
        if (!CanRun()) return;
        SetupGameFlow();
    }

    [MenuItem("Callejones/Compilar ejecutable (Windows)", priority = 60)]
    public static void BuildWindows()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Callejones] Sal del modo Play antes de compilar.");
            return;
        }

        // El ejecutable usa la escena GUARDADA: lo que no se guardo no entra.
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EnsureSceneInBuild();

        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.enabled) scenes.Add(s.path);

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
            Debug.Log($"[Callejones] Ejecutable listo en {BuildPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0} s).");
        else
            Debug.LogError($"[Callejones] La compilacion termino con resultado {summary.result} y {summary.totalErrors} errores. Mira la consola.");
    }

    // ------------------------------------------------------------------ 1. sensacion de cuerpo

    private static void SetupCameraFeel()
    {
        StepCounter steps = FindInScene<StepCounter>(null);
        FearMeter fear = FindInScene<FearMeter>(null);
        Camera cam = Camera.main != null ? Camera.main : FindInScene<Camera>(null);
        if (steps == null || cam == null)
        {
            Debug.LogWarning("[Callejones] No encuentro el Player (StepCounter) o la Main Camera.");
            return;
        }

        Transform player = steps.transform;
        Transform head = cam.transform.parent;

        // Player > Main Camera  pasa a ser  Player > Cabeza > Main Camera.
        // La Cabeza queda donde estaba la camara, y la camara queda en (0,0,0) dentro de ella.
        if (head == null || head.name != "Cabeza")
        {
            var go = new GameObject("Cabeza");
            Undo.RegisterCreatedObjectUndo(go, "Crear Cabeza");
            head = go.transform;
            head.SetParent(player, false);
            head.localPosition = cam.transform.localPosition;
            head.localRotation = Quaternion.identity;
            head.SetSiblingIndex(0);
            Undo.SetTransformParent(cam.transform, head, "Camara dentro de Cabeza");
            cam.transform.localPosition = Vector3.zero;
        }

        CameraFeel feel = head.GetComponent<CameraFeel>();
        if (feel == null) feel = Undo.AddComponent<CameraFeel>(head.gameObject);
        Undo.RecordObject(feel, "Sensacion de cuerpo");
        feel.Setup(steps, fear, player);
        EditorUtility.SetDirty(feel);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ------------------------------------------------------------------ 2. flujo de juego

    private static void SetupGameFlow()
    {
        // TextMeshPro necesita sus recursos (fuente por defecto y configuracion).
        // Se importan solos, pero la importacion termina despues: hay que volver a ejecutar el menu.
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.LogWarning("[Callejones] Importando los recursos de TextMeshPro. Cuando termine, ejecuta este menu otra vez.");
            return;
        }

        Camera cam = Camera.main != null ? Camera.main : FindInScene<Camera>(null);
        if (cam == null)
        {
            Debug.LogWarning("[Callejones] No encuentro la Main Camera.");
            return;
        }

        EnsureSceneInBuild();
        PlayerSettings.productName = "Callejones Cruzados";
        EnsureEventSystem();

        GameUI ui = FindInScene<GameUI>(null);
        if (ui == null) ui = BuildInterface(cam);
        else Debug.Log("[Callejones] \"Interfaz\" ya existe: no la reemplazo. Para regenerarla, borrala y ejecuta el menu otra vez.");

        // Objeto "Juego" con GameFlow (las reglas).
        GameFlow flow = FindInScene<GameFlow>(null);
        if (flow == null)
        {
            var go = new GameObject("Juego");
            Undo.RegisterCreatedObjectUndo(go, "Crear Juego");
            flow = go.AddComponent<GameFlow>();
        }

        StepCounter steps = FindInScene<StepCounter>(null);
        SetRefs(flow,
            ("ui", ui),
            ("controller", FindInScene<FirstPersonController>(null)),
            ("steps", steps),
            ("visibility", FindInScene<VisibilityChecker>(null)),
            ("loopExit", FindInScene<LoopExit>(null)),
            ("exitCondition", FindInScene<ExitCondition>(null)));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Callejones] Flujo de juego listo. Guarda con Ctrl+S.");
    }

    /// <summary>La escena actual primero en Build Settings; se quita la SampleScene de la plantilla.</summary>
    private static void EnsureSceneInBuild()
    {
        string active = SceneManager.GetActiveScene().path;
        var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(active, true) };
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path != active && !s.path.EndsWith("SampleScene.unity")) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>
    /// El EventSystem reparte los clics y las teclas entre los botones. Sin el, los botones se ven
    /// pero no responden. El proyecto usa el Input System nuevo, asi que lleva su modulo.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (FindInScene<EventSystem>(null) != null) return;
        var go = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(go, "Crear EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        module.AssignDefaultActions();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    // ------------------------------------------------------------------ la interfaz

    /// <summary>
    /// Canvas en modo "Screen Space - Camera": se dibuja DENTRO de la camara, asi que la interfaz
    /// tambien sale a 240 lineas (pixelada como el juego) y le llegan el grano y la vineta.
    /// En modo "Overlay" se dibujaria nitida encima de todo, y desentonaria con el estilo PS1.
    /// </summary>
    private static GameUI BuildInterface(Camera cam)
    {
        var root = new GameObject("Interfaz", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Crear Interfaz");

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        // Apenas detras del plano cercano de la camara: asi ninguna pared se mete por delante.
        canvas.planeDistance = cam.nearClipPlane + 0.02f;

        var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Resolucion de referencia chica: los tamanos se piensan "en pixeles de PS1".
        scaler.referenceResolution = new Vector2(640f, 360f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Orden = orden de dibujo: lo de abajo en la jerarquia tapa a lo de arriba.
        // El fundido negro va arriba de titulo y pausa, y el final arriba del fundido.

        // --- Titulo
        CanvasGroup title = Panel(root.transform, "Titulo");
        Shade(title.transform, 0.45f);
        Label(title.transform, "Nombre", "CALLEJONES CRUZADOS", 40, UiPaper, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 60f), 8f);
        Transform titleButtons = ButtonColumn(title.transform, "Botones", new Vector2(0f, -40f));
        var play = MenuButton(titleButtons, "BotonJugar", "Jugar");
        var titleQuit = MenuButton(titleButtons, "BotonSalir", "Salir");
        Label(title.transform, "Ayuda", "WASD caminar  ·  Mouse mirar  ·  Esc pausa", 12, UiDim, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(600f, 20f), 1f);

        // --- Pausa
        CanvasGroup pause = Panel(root.transform, "Pausa");
        Shade(pause.transform, 0.65f);
        Label(pause.transform, "Texto", "PAUSA", 32, UiPaper, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(400f, 50f), 6f);
        Transform pauseButtons = ButtonColumn(pause.transform, "Botones", new Vector2(0f, -30f));
        var resume = MenuButton(pauseButtons, "BotonContinuar", "Continuar");
        var backToTitle = MenuButton(pauseButtons, "BotonTitulo", "Volver al título");
        var pauseQuit = MenuButton(pauseButtons, "BotonSalir", "Salir");

        // --- Fundido (negro a pantalla completa; no recibe clics)
        CanvasGroup fade = Panel(root.transform, "Fundido");
        fade.blocksRaycasts = false;
        fade.interactable = false;
        var black = fade.gameObject.AddComponent<UnityEngine.UI.Image>();
        black.color = Color.black;
        black.raycastTarget = false;

        // --- Final
        CanvasGroup ending = Panel(root.transform, "Final");
        Label(ending.transform, "Texto", "Saliste.", 36, UiPaper, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(500f, 50f), 4f);
        TMPro.TMP_Text stats = Label(ending.transform, "Estadisticas", "", 16, UiDim, new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(400f, 80f), 1f);
        TMPro.TMP_Text hint = Label(ending.transform, "Indicacion", "Presioná cualquier tecla para volver al título", 12, UiDim, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(600f, 20f), 1f);
        CanvasGroup hintGroup = hint.gameObject.AddComponent<CanvasGroup>();

        var ui = root.AddComponent<GameUI>();
        SetRefs(ui,
            ("titlePanel", title), ("pausePanel", pause), ("endingPanel", ending), ("fade", fade),
            ("playButton", play), ("titleQuitButton", titleQuit), ("resumeButton", resume),
            ("backToTitleButton", backToTitle), ("pauseQuitButton", pauseQuit),
            ("endingStats", stats), ("endingHint", hintGroup));

        // Fuera de Play solo se ve el titulo; los otros paneles quedan con alpha 0 para no
        // encimarse. Para editar uno, subile el Alpha de su Canvas Group a 1 (y volvelo a 0).
        // Al empezar el juego, GameUI oculta todo y GameFlow muestra lo que corresponde.
        pause.alpha = 0f;
        fade.alpha = 0f;
        ending.alpha = 0f;
        hintGroup.alpha = 0f;
        return ui;
    }

    private static CanvasGroup Panel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        return go.AddComponent<CanvasGroup>();
    }

    /// <summary>Velo negro semitransparente detras del texto, para que se lea sobre el callejon.</summary>
    private static void Shade(Transform parent, float alpha)
    {
        var go = new GameObject("Velo", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0f, 0f, 0f, alpha);
        image.raycastTarget = false;
    }

    private static TMPro.TMP_Text Label(Transform parent, string name, string text, float size, Color color,
        Vector2 anchor, Vector2 position, Vector2 boxSize, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = boxSize;

        var label = go.AddComponent<TMPro.TextMeshProUGUI>();
        label.font = TMPro.TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.characterSpacing = spacing;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>Columna centrada: el Vertical Layout Group acomoda los botones uno debajo del otro.</summary>
    private static Transform ButtonColumn(Transform parent, string name, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(240f, 110f);

        var layout = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return go.transform;
    }

    /// <summary>
    /// Boton de solo texto: el mismo texto es el grafico que cambia de color
    /// (papel en reposo, rojo anaranjado al pasar el mouse o elegirlo con el teclado).
    /// </summary>
    private static UnityEngine.UI.Button MenuButton(Transform column, string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(column, false);
        go.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 28f;

        var label = go.AddComponent<TMPro.TextMeshProUGUI>();
        label.font = TMPro.TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = 22;
        label.color = Color.white; // blanco: el color real lo pone el tinte del boton
        label.characterSpacing = 2f;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.raycastTarget = true; // el texto recibe el clic

        var button = go.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = label;
        UnityEngine.UI.ColorBlock colors = button.colors;
        colors.normalColor = UiDim;
        colors.highlightedColor = UiAccent;
        colors.selectedColor = UiAccent;
        colors.pressedColor = UiPressed;
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        go.AddComponent<SelectOnHover>();
        return button;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Asigna campos privados [SerializeField] por nombre, como arrastrarlos en el Inspector.</summary>
    private static void SetRefs(Object target, params (string field, Object value)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in refs)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogWarning("[Callejones] " + target.GetType().Name + " no tiene el campo " + field);
            else p.objectReferenceValue = value;
        }
        so.ApplyModifiedProperties();
    }
}
