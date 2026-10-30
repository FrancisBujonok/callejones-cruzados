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
///   Si "Interfaz" ya existe, NO se reemplaza (para no borrar cambios hechos a mano).
/// - Callejones > Menus (inicio, pausa, opciones y final): lo mismo, pero SIEMPRE rearma
///   "Interfaz" con el diseno del Paso 17 (ver AtmosphereBuilder.Menus.cs).
/// - Callejones > Compilar ejecutable (Windows): guarda la escena y arma Builds/Windows/CallejonesCruzados.exe.
///
/// Ninguno corre dentro de "Construir atmosfera (todo)".
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
        SetupGameFlow(false);
    }

    [MenuItem("Callejones/Menus (inicio, pausa, opciones y final)", priority = 25)]
    public static void MenusMenu()
    {
        if (!CanRun()) return;
        SetupGameFlow(true);
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

    /// <param name="rebuildMenus">true = borra "Interfaz" y la vuelve a armar.</param>
    private static void SetupGameFlow(bool rebuildMenus)
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
        if (ui != null && rebuildMenus)
        {
            Undo.DestroyObjectImmediate(ui.gameObject);
            ui = null;
        }
        if (ui == null) ui = BuildMenus(cam);
        else Debug.Log("[Callejones] \"Interfaz\" ya existe: no la reemplazo. Para rearmarla usa Callejones > Menus.");

        // La camara de los menus y la caminata del inicio (Paso 17).
        MenuCamera menuCamera = SetupMenuCamera(cam, ui);
        FirstPersonController controller = FindInScene<FirstPersonController>(null);
        TitleWalker walker = SetupTitleWalker(controller, menuCamera);

        // Objeto "Juego" con GameFlow (las reglas).
        GameFlow flow = FindInScene<GameFlow>(null);
        if (flow == null)
        {
            var go = new GameObject("Juego");
            Undo.RegisterCreatedObjectUndo(go, "Crear Juego");
            flow = go.AddComponent<GameFlow>();
        }

        SetRefs(flow,
            ("ui", ui),
            ("controller", controller),
            ("steps", FindInScene<StepCounter>(null)),
            ("visibility", FindInScene<VisibilityChecker>(null)),
            ("loopExit", FindInScene<LoopExit>(null)),
            ("exitCondition", FindInScene<ExitCondition>(null)),
            ("titleWalker", walker),
            ("menuCamera", menuCamera));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Callejones] Flujo de juego y menus listos. Guarda con Ctrl+S.");
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
