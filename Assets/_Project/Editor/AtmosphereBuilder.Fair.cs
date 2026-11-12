using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: el CAPITULO 1, la feria de dia (Paso 32).
/// Menu: Callejones > Feria de dia (capitulo 1).
///
/// Arma de cero la escena Assets/_Project/Scenes/Feria.unity (la reemplaza si ya existe) y vuelve a
/// la escena que estaba abierta (la guarda antes). La feria esta en una calle peatonal, delante de
/// los callejones, al atardecer:
///
///   z = 12   fachadas del lado de la feria, con la entrada al callejon (x de 7 a 10)
///   z = 4..6 fila de puestos que mira hacia el sur
///   z = -1..4 el pasillo de la feria (guirnaldas, banderines, nobori), arco de entrada en x = -30
///   z = -3..-1 fila de puestos que mira hacia el norte
///   z = -6.5 cordon: bolardos, faroles de calle, parada de colectivo, arboles
///   z = -6.5..-14.5 la calle (dos carriles, para los autos de mas adelante)
///   z = -17.5 fachadas de enfrente
///
/// El jugador arranca en x = -33.5, debajo del arco, mirando el pasillo. Se puede caminar entre
/// x = -34 y 34 (vallas de obra en las puntas) y de la vereda hasta las fachadas. El capitulo
/// termina entrando al callejon (ChapterExit), detras del gato (LeadingCat).
///
/// Lo que se ve (calle, edificios, puestos, decoracion) esta en AtmosphereBuilder.FairWorld.cs.
/// Aca: la escena, la luz del atardecer, el jugador, los menus y el flujo, el sonido y la salida.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string FairScenePath = Root + "/Scenes/Feria.unity";
    private const string FairSceneName = "Feria";
    private const string FairVolumePath = SettingsDir + "/FeriaVolume.asset";
    private const string SunsetSkyPath = MaterialsDir + "/CieloAtardecer.mat";

    // Donde arranca el jugador y donde esta la entrada al callejon.
    private static readonly Vector3 FairStart = new Vector3(-33.5f, 1.1f, 1.5f);
    private const float AlleyX0 = 7f, AlleyX1 = 10f, AlleyDepth = 10f;
    private const float FairBackZ = 12f;

    [MenuItem("Callejones/Feria de dia (capitulo 1)", priority = 32)]
    public static void FairMenu()
    {
        if (!CanRun()) return;
        Scene previous = SceneManager.GetActiveScene();
        string previousPath = previous.path;
        if (!string.IsNullOrEmpty(previousPath)) EditorSceneManager.SaveScene(previous);

        Mats m = BuildMaterials();
        EnsureFolder(Root + "/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            BuildFairScene(m);
            EditorSceneManager.SaveScene(scene, FairScenePath);
            EnsureChapterScenesInBuild(previousPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Callejones] Feria lista en " + FairScenePath + ".");
        }
        finally
        {
            if (!string.IsNullOrEmpty(previousPath)) EditorSceneManager.OpenScene(previousPath);
        }
    }

    /// <summary>Prototipo (con el inicio) primero, la Feria segunda, el resto despues.</summary>
    private static void EnsureChapterScenesInBuild(string titleScenePath)
    {
        var list = new List<EditorBuildSettingsScene>();
        string title = string.IsNullOrEmpty(titleScenePath) || titleScenePath == FairScenePath
            ? Root + "/Scenes/Prototipo.unity" : titleScenePath;
        list.Add(new EditorBuildSettingsScene(title, true));
        list.Add(new EditorBuildSettingsScene(FairScenePath, true));
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path != title && s.path != FairScenePath && !s.path.EndsWith("SampleScene.unity")) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }

    private static void BuildFairScene(Mats m)
    {
        FairMats f = BuildFairMaterials(m);

        // 1. Luz del atardecer, cielo y niebla.
        Light sun = SetupSunsetLighting();

        // 2. Post-procesado: una copia del perfil de la noche, mas suave.
        SetupFairVolume();

        // 3. El jugador y la camara.
        Camera cam = BuildFairPlayer(out FirstPersonController controller, out StepCounter steps);

        // 4. Menus y flujo (sin inicio: empieza directo, con la tarjeta del capitulo 1).
        SetupFairFlow(cam, controller, steps);

        // 5. Lo que se ve: calle, edificios, puestos y decoracion (AtmosphereBuilder.FairWorld.cs).
        Transform world = new GameObject("Feria").transform;
        BuildFairWorld(world, m, f);

        // 6. La salida: el callejon, el gato y el fin del capitulo.
        BuildFairExit(world, m, f, controller.transform);

        // 7. Sonido.
        SetupFairSound(world);

        // 8. Para mas adelante: carriles de autos y lugares para la gente (solo se ven en Scene).
        BuildFairMarkers(world);

        if (sun != null) sun.transform.SetAsFirstSibling();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ------------------------------------------------------------------ luz

    private static Light SetupSunsetLighting()
    {
        Material sky = BuildSunsetSkyMaterial();
        RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Hex("#C98B6B");
        RenderSettings.fogStartDistance = 30f;
        RenderSettings.fogEndDistance = 140f;

        // Ambiente en tres tonos: arriba el violeta del cielo, al medio el naranja, abajo marron.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Hex("#7A6386");
        RenderSettings.ambientEquatorColor = Hex("#A8705A");
        RenderSettings.ambientGroundColor = Hex("#34261F");
        RenderSettings.reflectionIntensity = 0.4f;

        // El sol, bajo, desde el fondo de la calle (-X) y un poco desde la calle (-Z): ilumina las
        // fachadas del lado de la feria y deja sombras largas hacia adelante.
        var go = new GameObject("Sol");
        Light sun = go.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.68f, 0.43f);
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.Hard;
        sun.shadowStrength = 0.75f;
        go.transform.rotation = Quaternion.LookRotation(new Vector3(0.86f, -0.2f, 0.47f));
        RenderSettings.sun = sun;
        return sun;
    }

    /// <summary>
    /// El cielo del atardecer: el mismo shader que el de la noche, con otros colores. El sol no se
    /// dibuja (la luna del shader tiene manchas): el resplandor del horizonte hace de sol. Se ve una
    /// luna palida alta, pocas estrellas arriba y la vuelta al mundo hacia el fondo de la calle.
    /// </summary>
    private static Material BuildSunsetSkyMaterial()
    {
        Shader shader = Shader.Find(SkyShaderName);
        if (shader == null) return null;
        Material s = AssetDatabase.LoadAssetAtPath<Material>(SunsetSkyPath);
        if (s == null)
        {
            s = new Material(shader);
            AssetDatabase.CreateAsset(s, SunsetSkyPath);
        }
        else if (s.shader != shader) s.shader = shader;

        s.SetColor("_HorizonColor", Hex("#E39A62"));
        s.SetColor("_GlowColor", Hex("#F0B472"));
        s.SetColor("_MidColor", Hex("#B26E84"));
        s.SetColor("_ZenithColor", Hex("#3A3F7A"));
        s.SetFloat("_HorizonHold", 4f);
        s.SetFloat("_GlowHeight", 16f);
        s.SetFloat("_MidHeight", 42f);
        s.SetColor("_StarColor", Hex("#F4EEDC"));
        s.SetFloat("_StarDensity", 0.0015f);
        s.SetFloat("_StarMinHeight", 55f);
        s.SetColor("_MoonColor", Hex("#F2E6D2"));
        s.SetFloat("_MoonAzimuth", -60f);
        s.SetFloat("_MoonElevation", 38f);
        s.SetFloat("_MoonSize", 2f);
        s.SetFloat("_MoonHalo", 0.08f);
        s.SetColor("_CloudColor", Hex("#E6A07E"));
        s.SetFloat("_CloudAmount", 0.5f);
        s.SetFloat("_CloudScale", 1.4f);
        s.SetColor("_CityColor", Hex("#6E4C5E"));
        s.SetColor("_WindowColor", Hex("#F0B060"));
        s.SetFloat("_CityHeight", 10f);
        s.SetFloat("_WindowAmount", 0.03f);
        s.SetFloat("_Wheel", 1f);
        s.SetFloat("_WheelAzimuth", 80f);
        s.SetFloat("_WheelElevation", 13f);
        s.SetFloat("_HazeBottom", 4f);
        s.SetFloat("_HazeTop", 9f);
        EditorUtility.SetDirty(s);
        return s;
    }

    private static void SetupFairVolume()
    {
        VolumeProfile night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (night == null) SetupPostProcessing();
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(FairVolumePath) == null)
            AssetDatabase.CopyAsset(VolumePath, FairVolumePath);
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FairVolumePath);
        if (profile != null)
        {
            if (profile.TryGet(out Bloom bloom)) { Set(bloom.threshold, 0.95f); Set(bloom.intensity, 0.8f); }
            if (profile.TryGet(out Vignette vignette)) Set(vignette.intensity, 0.24f);
            if (profile.TryGet(out FilmGrain grain)) Set(grain.intensity, 0.18f);
            if (profile.TryGet(out ColorAdjustments color)) { Set(color.postExposure, 0f); Set(color.saturation, 6f); }
            EditorUtility.SetDirty(profile);
        }

        var go = new GameObject("Global Volume");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    // ------------------------------------------------------------------ jugador

    /// <summary>
    /// Como el de los callejones, pero sin lo del laberinto: cuerpo con CharacterController, pasos,
    /// trote, miedo (lo usan la camara y el trote; aca queda en 0) y la cabeza con CameraFeel.
    /// </summary>
    private static Camera BuildFairPlayer(out FirstPersonController controller, out StepCounter steps)
    {
        var player = new GameObject("Player") { tag = "Player" };
        player.transform.position = FairStart;
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.08f;

        var head = new GameObject("Cabeza");
        head.transform.SetParent(player.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(head.transform, false);
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
        camGo.AddComponent<RetroSettings>();
        camGo.AddComponent<SkyFog>();
        camGo.AddComponent<FairSky>();

        steps = player.AddComponent<StepCounter>();
        FearMeter fear = player.AddComponent<FearMeter>();
        player.AddComponent<Stamina>();
        controller = player.AddComponent<FirstPersonController>();
        SetRefs(controller, ("cameraPivot", camGo.transform));
        SetRefs(player.GetComponent<Stamina>(), ("fear", fear));
        head.AddComponent<CameraFeel>().Setup(steps, fear, player.transform);

        // Pasos: los mismos sonidos que en los callejones (sin eco: aca es abierto).
        var clips = new List<AudioClip>();
        for (int i = 0; i < 8; i++)
        {
            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Paso_" + i + ".wav");
            if (c != null) clips.Add(c);
        }
        if (clips.Count > 0)
        {
            AudioSource src = NewSource(player.transform, "Pasos", null, false, 50);
            AudioSource echo = NewSource(player.transform, "PasosEco", null, false, 60);
            echo.volume = 0f;
            player.AddComponent<FootstepAudio>().Setup(steps, fear, src, echo, clips.ToArray());
        }
        return cam;
    }

    // ------------------------------------------------------------------ menus y flujo

    private static void SetupFairFlow(Camera cam, FirstPersonController controller, StepCounter steps)
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            Debug.LogWarning("[Callejones] Faltan los recursos de TextMeshPro: corre antes Callejones > Menus.");
        EnsureEventSystem();
        GameUI ui = BuildMenus(cam);
        MenuCamera menuCamera = SetupMenuCamera(cam, ui);

        var go = new GameObject("Juego");
        GameFlow flow = go.AddComponent<GameFlow>();
        SetRefs(flow, ("ui", ui), ("controller", controller), ("steps", steps), ("menuCamera", menuCamera));
        var so = new SerializedObject(flow);
        so.FindProperty("hasTitle").boolValue = false;
        so.FindProperty("titleScene").stringValue = "Prototipo";
        so.FindProperty("firstChapterScene").stringValue = FairSceneName;
        so.FindProperty("secondChapterScene").stringValue = "Prototipo";
        so.FindProperty("chapterNumber").stringValue = "CAPÍTULO 1";
        so.FindProperty("chapterName").stringValue = "Delante de la feria";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ sonido

    private static void SetupFairSound(Transform world)
    {
        var go = new GameObject("SonidoFeria");
        go.transform.SetParent(world, false);
        var layers = new List<FairAmbience.Layer>();
        void Layer(string name, string prefix, Vector2 every, float volume, Vector3 center, Vector3 size, float maxDistance)
        {
            AudioClip[] clips = LoadRealSounds(prefix);
            if (clips.Length == 0) return;
            layers.Add(new FairAmbience.Layer
            {
                name = name, clips = clips, every = every, volume = volume,
                center = center, size = size, maxDistance = maxDistance,
            });
        }
        // El murmullo: clips de 7 s que arrancan cada 2-3.5 s en cualquier punto del pasillo.
        Layer("Gente", "Feria", new Vector2(2.2f, 3.5f), 0.5f, new Vector3(0f, 1.6f, 1.5f), new Vector3(52f, 0f, 5f), 26f);
        Layer("Tambor", "Tambor", new Vector2(8f, 14f), 0.6f, new Vector3(31f, 1.2f, 1.5f), Vector3.zero, 48f);
        Layer("Campanillas", "Campanillas", new Vector2(10f, 18f), 0.35f, FortuneStallSpot, Vector3.zero, 14f);
        Layer("Musico", "Guitarra", new Vector2(9f, 14f), 0.3f, new Vector3(-12f, 1.2f, -4.6f), Vector3.zero, 18f);
        Layer("Perros", "Perro", new Vector2(25f, 45f), 0.3f, new Vector3(0f, 3f, 30f), new Vector3(120f, 0f, 12f), 60f);
        go.AddComponent<FairAmbience>().Setup(layers.ToArray());
    }

    // ------------------------------------------------------------------ marcas para mas adelante

    /// <summary>
    /// Carriles para los autos (dos, uno por sentido) y lugares para la gente: vendedores detras de
    /// cada mostrador y un recorrido por el pasillo. Son objetos vacios: FairMarkers los dibuja en
    /// la vista Scene. Cuando se agreguen autos y NPC, van a leer estas posiciones.
    /// </summary>
    private static void BuildFairMarkers(Transform world)
    {
        Transform root = Group(world, "ParaMasAdelante");
        root.gameObject.AddComponent<FairMarkers>();
        Transform east = Group(root, "CarrilHaciaElEste");
        Transform west = Group(root, "CarrilHaciaElOeste");
        for (int i = 0; i <= 11; i++)
        {
            float x = -110f + i * 20f;
            Group(east, "Punto" + i).localPosition = new Vector3(x, 0f, -8.5f);
            Group(west, "Punto" + i).localPosition = new Vector3(-x, 0f, -12.5f);
        }
        Transform walk = Group(root, "PaseoPorElPasillo");
        float[] xs = { -31f, -20f, -8f, 4f, 16f, 26f, 16f, 4f, -8f, -20f };
        float[] zs = { 1.5f, 0.6f, 2.4f, 0.8f, 2.2f, 1.5f, 2.6f, 0.4f, 1.8f, 2.8f };
        for (int i = 0; i < xs.Length; i++) Group(walk, "Punto" + i).localPosition = new Vector3(xs[i], 0f, zs[i]);
        Transform vendors = Group(root, "Vendedores");
        foreach (Vector3 p in FairVendorSpots) Group(vendors, "Vendedor").localPosition = p;
    }
}
