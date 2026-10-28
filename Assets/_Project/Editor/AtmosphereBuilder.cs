using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de editor: arma la atmosfera base de Callejones con un clic.
/// Menu: Callejones > Construir atmosfera (todo).
///
/// Que hace, en orden:
/// 1. Crea los materiales de la paleta en Assets/_Project/Materials.
/// 2. Genera las mallas low-poly (farol y persiana) y el prefab Farol.
/// 3. Decora el prefab Modulo: materiales, persianas, faroles, cables, toldo, cartel.
/// 4. Aplica los materiales al ModuloSalida de la escena.
/// 5. Iluminacion nocturna: sin sol ni cielo, niebla y fondo del mismo marron oscuro.
/// 6. Post-procesado: crea el perfil CallejonesVolume y le agrega FearPostFX al Global Volume.
/// 7. Estilo PS1 (si esta activo): shader Callejones/PS1 Lit, escalado Point, sin SSAO y
///    RetroSettings en la camara. Se cambia con Callejones > Estilo PS1 / Estilo moderno.
/// 8. Sonido: sintetiza los clips y arma ambiente, pasos, faroles y torii (AtmosphereBuilder.Audio.cs).
/// 9. Laberinto con giros: piezas Esquina, Cruce, Fondo y Tapa, y el deposito (AtmosphereBuilder.Maze.cs).
///
/// Se puede ejecutar varias veces: reemplaza lo que genero antes en vez de duplicarlo.
/// Esta carpeta se llama Editor a proposito: Unity no incluye este codigo en el juego final.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string Root = "Assets/_Project";
    private const string MaterialsDir = Root + "/Materials";
    private const string MeshesDir = Root + "/Meshes";
    private const string SettingsDir = Root + "/Settings";
    private const string ModuloPath = Root + "/Prefabs/Modulo.prefab";
    private const string FarolPath = Root + "/Prefabs/Farol.prefab";
    private const string VolumePath = SettingsDir + "/CallejonesVolume.asset";

    // Medidas del modulo (ver Paso 2): 3 m de ancho, 3 m de alto, 6 m de largo.
    private const float WallInnerX = 1.4f;   // cara interior de las paredes
    private const float ShutterWidth = 2.4f;
    private const float ShutterHeight = 2.4f;

    // Paleta: ver la seccion "Direccion de arte" del doc.
    private static readonly Color FogColor = Hex("#1D0C07");
    private static readonly Color AmbientColor = Hex("#22160F");
    private static readonly Color LanternEmission = new Color(1f, 0.2f, 0.05f) * 2.2f;
    // Ambar y no rojo: la luz se multiplica por el color de cada superficie, y si las dos
    // son rojizas el resultado queda rojo puro (sin verde ni azul).
    private static readonly Color LanternLightColor = new Color(1f, 0.62f, 0.35f);

    // Estilo PS1: se recuerda entre sesiones del editor. Por defecto, activado.
    private const string PS1PrefKey = "Callejones.EstiloPS1";
    private const string PS1ShaderName = "Callejones/PS1 Lit";
    private const string LitShaderName = "Universal Render Pipeline/Lit";

    private static bool UsePS1
    {
        get => EditorPrefs.GetBool(PS1PrefKey, true);
        set => EditorPrefs.SetBool(PS1PrefKey, value);
    }

    private class Mats
    {
        public Material pared, piso, persiana, metal, toldo, cable, cartel, farolTela, farolTapa;
        public Material teja, madera, maceta, planta, balde, ventanaLuz, ventanaOscura, cartelRojo;
        public Material torii, soga, papel, resplandor;
    }

    // ------------------------------------------------------------------ menus

    [MenuItem("Callejones/Construir atmosfera (todo)", priority = 0)]
    public static void BuildAll()
    {
        if (!CanRun()) return;

        Mats mats = BuildMaterials();
        GameObject farol = BuildLanternPrefab(mats);
        DecorateModule(mats, farol);
        DecorateExitModule(mats);
        BuildMaze(mats, farol);
        SetupNightLighting();
        SetupPostProcessing();
        SetupRetro(UsePS1);
        SetupSound(BuildSounds()); // despues del torii: le agrega su sonido

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Callejones] Atmosfera construida en la escena '" + SceneManager.GetActiveScene().name +
                  "' (estilo " + (UsePS1 ? "PS1" : "moderno") + "). Guarda con Ctrl+S.");
    }

    [MenuItem("Callejones/Estilo PS1 (activar)", priority = 40)]
    public static void EnablePS1()
    {
        if (!CanRun()) return;
        UsePS1 = true;
        BuildMaterials();
        SetupRetro(true);
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Estilo PS1 activado. Guarda con Ctrl+S.");
    }

    [MenuItem("Callejones/Estilo moderno (sin PS1)", priority = 41)]
    public static void DisablePS1()
    {
        if (!CanRun()) return;
        UsePS1 = false;
        BuildMaterials();
        SetupRetro(false);
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Estilo moderno activado. Guarda con Ctrl+S.");
    }

    [MenuItem("Callejones/Solo iluminacion nocturna", priority = 20)]
    public static void LightingOnly()
    {
        if (!CanRun()) return;
        SetupNightLighting();
        Debug.Log("[Callejones] Iluminacion nocturna aplicada. Guarda con Ctrl+S.");
    }

    [MenuItem("Callejones/Solo post-procesado", priority = 21)]
    public static void PostOnly()
    {
        if (!CanRun()) return;
        SetupPostProcessing();
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Post-procesado aplicado. Guarda con Ctrl+S.");
    }

    private static bool CanRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Callejones", "Sal del modo Play antes de usar esta herramienta.", "OK");
            return false;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModuloPath) == null)
        {
            EditorUtility.DisplayDialog("Callejones", "No encuentro el prefab en " + ModuloPath + ".", "OK");
            return false;
        }
        if (Shader.Find(LitShaderName) == null)
        {
            EditorUtility.DisplayDialog("Callejones", "No encuentro el shader de URP Lit. El proyecto tiene que usar URP.", "OK");
            return false;
        }
        if (UsePS1 && Shader.Find(PS1ShaderName) == null)
        {
            EditorUtility.DisplayDialog("Callejones",
                "No encuentro el shader " + PS1ShaderName + ". Revisa que Assets/_Project/Shaders/PS1Lit.shader " +
                "exista y que la consola no muestre errores de shader.", "OK");
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ 1. materiales

    private static Mats BuildMaterials()
    {
        EnsureFolder(MaterialsDir);
        Textures tex = BuildTextures();
        Color paper = Hex("#DCAD78");
        return new Mats
        {
            // Con textura, el color base funciona como tinte: blanco = la textura tal cual.
            pared = Mat("Pared", new Color(0.85f, 0.8f, 0.78f), 0.15f, baseMap: tex.ladrillo),
            piso = Mat("PisoMojado", new Color(0.75f, 0.75f, 0.75f), 0.85f, 0f, new Color(0.6f, 0.58f, 0.55f), baseMap: tex.piedra),
            persiana = Mat("Persiana", Hex("#5E5A55"), 0.45f, 0.55f, new Color(0.4f, 0.4f, 0.38f)),
            metal = Mat("MetalOscuro", Hex("#4A4744"), 0.35f, 0.3f),
            toldo = Mat("Toldo", Hex("#5A1A10"), 0.2f),
            cable = Mat("Cable", Hex("#0A0504"), 0.3f),
            cartel = Mat("Cartel", Color.white, 0.3f, 0f, null, paper * 0.9f, tex.kanbanPapel, tex.kanbanPapel),
            cartelRojo = Mat("CartelRojo", Color.white, 0.3f, 0f, null, new Color(1f, 0.35f, 0.25f) * 1.3f, tex.kanbanRojo, tex.kanbanRojo),
            farolTela = Mat("FarolTela", Hex("#C84E25"), 0.2f, 0f, null, LanternEmission),
            farolTapa = Mat("FarolTapa", Hex("#120806"), 0.3f),
            teja = Mat("Teja", Hex("#3A2A24"), 0.3f),
            madera = Mat("Madera", Color.white, 0.2f, baseMap: tex.madera),
            maceta = Mat("Maceta", Hex("#8A4A30"), 0.2f),
            planta = Mat("Planta", Hex("#2E3A22"), 0.25f),
            // Acento frio de la paleta: pocos objetos azules para que el rojo resalte.
            balde = Mat("Balde", Hex("#2F4A6A"), 0.5f, 0f, new Color(0.3f, 0.3f, 0.3f)),
            ventanaLuz = Mat("VentanaLuz", Hex("#E3B060"), 0.6f, 0f, null, Hex("#E3B060") * 1.1f),
            ventanaOscura = Mat("VentanaOscura", Hex("#101418"), 0.9f, 0f, new Color(0.5f, 0.5f, 0.5f)),
            torii = Mat("Torii", Hex("#B8301E"), 0.25f, 0f, null, new Color(0.5f, 0.06f, 0.03f) * 0.6f),
            soga = Mat("Soga", Hex("#8C6A3C"), 0.1f),
            papel = Mat("PapelShide", Hex("#E8E2D0"), 0.1f, 0f, null, Hex("#E8E2D0") * 0.55f),
            resplandor = Mat("Resplandor", paper, 0.1f, 0f, null, paper * 1.3f),
        };
    }

    /// <summary>
    /// Crea o actualiza un material con el shader del estilo activo.
    /// specular: color del brillo para el shader PS1 (null = sin brillo).
    /// En el estilo moderno (URP Lit) el brillo sale de smoothness y metallic.
    /// </summary>
    private static Material Mat(string name, Color baseColor, float smoothness, float metallic = 0f,
        Color? specular = null, Color? emission = null, Texture2D baseMap = null, Texture2D emissionMap = null)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        bool ps1 = UsePS1;
        Shader shader = Shader.Find(ps1 ? PS1ShaderName : LitShaderName);

        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader)
        {
            m.shader = shader;
        }

        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Smoothness", smoothness);
        m.SetTexture("_BaseMap", baseMap);
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetTexture("_EmissionMap", emissionMap);

        if (ps1)
        {
            // Simple Lit: el brillo es Blinn-Phong. _SpecularHighlights 0 = con brillo, 1 = sin brillo.
            m.DisableKeyword("_SPECGLOSSMAP");
            if (specular.HasValue)
            {
                Color s = specular.Value;
                m.SetFloat("_SpecularHighlights", 0f);
                m.SetColor("_SpecColor", new Color(s.r, s.g, s.b, smoothness));
                m.EnableKeyword("_SPECULAR_COLOR");
            }
            else
            {
                m.SetFloat("_SpecularHighlights", 1f);
                m.DisableKeyword("_SPECULAR_COLOR");
            }
        }
        else
        {
            // URP Lit usa la misma propiedad al reves: 1 = con brillo.
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_SpecularHighlights", 1f);
            m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.DisableKeyword("_SPECULAR_COLOR");
        }

        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            // Importante: el inspector de URP apaga _EMISSION si el material no esta
            // marcado como emisivo para la iluminacion global. Con None, la emision se
            // borraba sola cada vez que Unity validaba el material.
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ 2. farol

    // Perfil del farol: (radio, altura) de arriba hacia abajo. Se gira 8 veces
    // alrededor del eje Y; con caras planas queda el look low-poly.
    private static readonly Vector2[] LanternBodyProfile =
    {
        new Vector2(0.10f, 0.00f),
        new Vector2(0.19f, -0.06f),
        new Vector2(0.235f, -0.17f),
        new Vector2(0.245f, -0.25f),
        new Vector2(0.235f, -0.33f),
        new Vector2(0.19f, -0.44f),
        new Vector2(0.10f, -0.50f),
    };

    private static readonly Vector2[] LanternCapProfile =
    {
        new Vector2(0.11f, 0.02f),
        new Vector2(0.11f, -0.02f),
    };

    private static GameObject BuildLanternPrefab(Mats m)
    {
        EnsureFolder(MeshesDir);
        Mesh body = GetOrCreateMesh("Farol_Cuerpo");
        BuildLathe(body, LanternBodyProfile, 8);
        Mesh cap = GetOrCreateMesh("Farol_Tapa");
        BuildLathe(cap, LanternCapProfile, 8);

        // El pivote es el punto de donde cuelga: todo queda por debajo.
        var root = new GameObject("Farol");
        try
        {
            BoxChild(root.transform, "Cuerda", m.cable, new Vector3(0f, -0.135f, 0f), new Vector3(0.012f, 0.27f, 0.012f), Vector3.zero);
            MeshChild(root.transform, "TapaSuperior", cap, m.farolTapa, new Vector3(0f, -0.27f, 0f));
            GameObject bodyGo = MeshChild(root.transform, "Cuerpo", body, m.farolTela, new Vector3(0f, -0.27f, 0f));
            MeshChild(root.transform, "TapaInferior", cap, m.farolTapa, new Vector3(0f, -0.77f, 0f));
            BoxChild(root.transform, "Borla", m.toldo, new Vector3(0f, -0.87f, 0f), new Vector3(0.03f, 0.14f, 0.03f), Vector3.zero);

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.52f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LanternLightColor;
            light.range = 4.5f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;

            root.AddComponent<LanternLight>().Setup(light, bodyGo.GetComponent<MeshRenderer>());

            return PrefabUtility.SaveAsPrefabAsset(root, FarolPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ------------------------------------------------------------------ 3. modulo

    private static void DecorateModule(Mats m, GameObject farolPrefab)
    {
        Mesh shutterMesh = GetOrCreateMesh("Persiana");
        BuildShutter(shutterMesh, ShutterWidth, ShutterHeight, 0.12f, 0.04f);

        // LoadPrefabContents abre el prefab "en memoria", como el modo prefab,
        // y SaveAsPrefabAsset guarda los cambios en todas las copias de la escena.
        GameObject root = PrefabUtility.LoadPrefabContents(ModuloPath);
        try
        {
            ApplyStructureMaterials(root.transform, m);

            // Paredes y piso con UV en metros, para que la textura no se estire.
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "ParedIzq" || t.name == "ParedDer" || t.name == "Bloqueo" || t.name == "Piso")
                    ConvertToMetric(t);

            // Decorado compartido por todas las variantes (arriba, lejos del camino).
            DestroyChild(root.transform, "Decorado");
            Transform deco = new GameObject("Decorado").transform;
            deco.SetParent(root.transform, false);

            var farolA = (GameObject)PrefabUtility.InstantiatePrefab(farolPrefab, deco);
            farolA.name = "FarolA";
            farolA.transform.localPosition = new Vector3(-0.4f, 3.15f, -1.5f);

            var farolB = (GameObject)PrefabUtility.InstantiatePrefab(farolPrefab, deco);
            farolB.name = "FarolB";
            farolB.transform.localPosition = new Vector3(0.7f, 3.15f, 1.5f);

            BoxChild(deco, "CableA", m.cable, new Vector3(-0.4f, 3.15f, 0f), new Vector3(0.025f, 0.025f, 6f), Vector3.zero);
            BoxChild(deco, "CableB", m.cable, new Vector3(0.7f, 3.15f, 0f), new Vector3(0.025f, 0.025f, 6f), Vector3.zero);
            BoxChild(deco, "CableCruzado", m.cable, new Vector3(0f, 3.0f, 0.6f), new Vector3(3f, 0.02f, 0.02f), new Vector3(0f, 0f, 3f));
            BoxChild(deco, "Toldo", m.toldo, new Vector3(-1.08f, 2.8f, 0f), new Vector3(0.7f, 0.04f, 5.8f), new Vector3(0f, 0f, -18f));
            BuildDecorSets(deco, m);

            // Persianas: van dentro de cada variante porque dependen de sus paredes.
            foreach (string variant in new[] { "Abierto", "Cerrado" })
            {
                Transform v = root.transform.Find(variant);
                if (v == null) continue;
                DestroyChild(v, "Persianas");
                Transform group = new GameObject("Persianas").transform;
                group.SetParent(v, false);
                CreateShutter(group, "PersianaIzq", new Vector3(-WallInnerX, 0f, -1.5f), false, shutterMesh, m);
                CreateShutter(group, "PersianaDer", new Vector3(WallInnerX, 0f, 1.5f), true, shutterMesh, m);
            }

            PrefabUtility.SaveAsPrefabAsset(root, ModuloPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplyStructureMaterials(Transform root, Mats m)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            var r = t.GetComponent<MeshRenderer>();
            if (r == null) continue;
            switch (t.name)
            {
                case "Piso": r.sharedMaterial = m.piso; break;
                case "ParedIzq":
                case "ParedDer":
                case "Bloqueo": r.sharedMaterial = m.pared; break;
            }
        }
    }

    private static void CreateShutter(Transform parent, string name, Vector3 wallPoint, bool rightWall, Mesh mesh, Mats m)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = wallPoint;
        // La malla mira hacia +X (pared izquierda). La de la derecha se gira 180 grados.
        go.transform.localRotation = rightWall ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        MeshChild(go.transform, "Panel", mesh, m.persiana, Vector3.zero);
        BoxChild(go.transform, "Caja", m.metal, new Vector3(0.11f, ShutterHeight + 0.13f, 0f), new Vector3(0.22f, 0.26f, ShutterWidth + 0.1f), Vector3.zero);
        BoxChild(go.transform, "GuiaA", m.metal, new Vector3(0.04f, ShutterHeight * 0.5f, -ShutterWidth * 0.5f - 0.03f), new Vector3(0.08f, ShutterHeight, 0.06f), Vector3.zero);
        BoxChild(go.transform, "GuiaB", m.metal, new Vector3(0.04f, ShutterHeight * 0.5f, ShutterWidth * 0.5f + 0.03f), new Vector3(0.08f, ShutterHeight, 0.06f), Vector3.zero);
    }

    // ------------------------------------------------------------------ 4. modulo de salida

    private static void DecorateExitModule(Mats m)
    {
        Transform exit = FindInScene<Transform>(t => t.name == "ModuloSalida");
        if (exit == null) return;
        Undo.RegisterFullObjectHierarchyUndo(exit.gameObject, "Materiales de la salida");
        ApplyStructureMaterials(exit, m);
        BuildExitTorii(exit, m);
    }

    // ------------------------------------------------------------------ 5. iluminacion

    private static void SetupNightLighting()
    {
        // Niebla y fondo del MISMO color: lo lejano se funde con el vacio sin un corte visible.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogStartDistance = 4f;
        RenderSettings.fogEndDistance = 16f;

        // Sin cielo: la luz ambiente es un marron muy oscuro y parejo.
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientColor;
        RenderSettings.reflectionIntensity = 0.25f;
        DynamicGI.UpdateEnvironment();

        // Sin sol: toda la luz sale de los faroles.
        foreach (Light l in FindAllInScene<Light>())
        {
            if (l.type != LightType.Directional || !l.gameObject.activeSelf) continue;
            Undo.RecordObject(l.gameObject, "Apagar luz direccional");
            l.gameObject.SetActive(false);
        }

        Camera cam = Camera.main != null ? Camera.main : FindInScene<Camera>(null);
        if (cam != null)
        {
            Undo.RecordObject(cam, "Fondo de camara");
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            Undo.RecordObject(data, "Post-procesado de camara");
            data.renderPostProcessing = true;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ------------------------------------------------------------------ 6. post-procesado

    private static void SetupPostProcessing()
    {
        EnsureFolder(SettingsDir);
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);
        }

        // Brillo alrededor de los faroles.
        Bloom bloom = Comp<Bloom>(profile);
        Set(bloom.threshold, 0.8f);
        Set(bloom.intensity, 1.2f);
        Set(bloom.scatter, 0.75f);
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(1f, 0.8f, 0.65f);
        bloom.highQualityFiltering.overrideState = true;
        bloom.highQualityFiltering.value = true;

        // Neutral y no ACES: en una escena tan oscura, ACES aplasta las sombras
        // y se come el verde y el azul (todo quedaba rojo puro).
        Tonemapping tone = Comp<Tonemapping>(profile);
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.Neutral;

        ColorAdjustments color = Comp<ColorAdjustments>(profile);
        Set(color.postExposure, -0.1f);
        Set(color.contrast, 18f);
        Set(color.saturation, 10f);
        color.colorFilter.overrideState = true;
        color.colorFilter.value = new Color(1f, 0.96f, 0.92f);

        WhiteBalance white = Comp<WhiteBalance>(profile);
        Set(white.temperature, 5f);

        Vignette vignette = Comp<Vignette>(profile);
        Set(vignette.intensity, 0.32f);
        Set(vignette.smoothness, 0.45f);
        vignette.color.overrideState = true;
        vignette.color.value = Color.black;

        FilmGrain grain = Comp<FilmGrain>(profile);
        grain.type.overrideState = true;
        grain.type.value = FilmGrainLookup.Medium1;
        Set(grain.intensity, 0.25f);
        Set(grain.response, 0.8f);

        ChromaticAberration chromatic = Comp<ChromaticAberration>(profile);
        Set(chromatic.intensity, 0f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // Asignar el perfil al Global Volume de la escena (o crearlo si no existe).
        Volume volume = FindInScene<Volume>(v => v.isGlobal);
        if (volume == null)
        {
            var go = new GameObject("Global Volume");
            Undo.RegisterCreatedObjectUndo(go, "Crear Global Volume");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
        }
        Undo.RecordObject(volume, "Perfil de post-procesado");
        volume.sharedProfile = profile;

        FearPostFX fx = volume.GetComponent<FearPostFX>();
        if (fx == null) fx = Undo.AddComponent<FearPostFX>(volume.gameObject);

        FearMeter fear = FindInScene<FearMeter>(null);
        if (fear != null)
        {
            var so = new SerializedObject(fx);
            so.FindProperty("fear").objectReferenceValue = fear;
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static T Comp<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(false);
            // Cada efecto se guarda DENTRO del archivo del perfil.
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        component.active = true;
        EditorUtility.SetDirty(component);
        return component;
    }

    private static void Set(VolumeParameter<float> parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    // ------------------------------------------------------------------ 7. estilo PS1

    /// <summary>
    /// Prende o apaga lo que el estilo PS1 necesita fuera de los materiales:
    /// - Escalado Point en el asset de URP: al agrandar la imagen de baja resolucion,
    ///   cada pixel queda como un cuadrado duro en vez de borroso.
    /// - Oclusion ambiental (SSAO) apagada: la PS1 no tenia y ensucia el dithering.
    /// - RetroSettings en la camara: resolucion baja, temblor de vertices y colores.
    /// </summary>
    private static void SetupRetro(bool on)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            Undo.RecordObject(urp, "Estilo PS1");
            urp.upscalingFilter = on ? UpscalingFilterSelection.Point : UpscalingFilterSelection.Auto;
            EditorUtility.SetDirty(urp);

            foreach (ScriptableRendererData data in urp.rendererDataList)
            {
                if (data == null) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                    feature.SetActive(!on);
                    EditorUtility.SetDirty(feature);
                }
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }
        }

        Camera cam = Camera.main != null ? Camera.main : FindInScene<Camera>(null);
        if (cam != null)
        {
            RetroSettings retro = cam.GetComponent<RetroSettings>();
            if (retro == null && on) retro = Undo.AddComponent<RetroSettings>(cam.gameObject);
            if (retro != null)
            {
                Undo.RecordObject(retro, "Estilo PS1");
                retro.enabled = on;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ------------------------------------------------------------------ mallas low-poly

    private static Mesh GetOrCreateMesh(string name)
    {
        string path = MeshesDir + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
        }
        return mesh;
    }

    /// <summary>
    /// Torno: gira un perfil (radio, altura) alrededor del eje Y.
    /// Cada cara tiene sus propios vertices y una sola normal: sombreado plano (flat shading).
    /// Cierra los extremos con tapas.
    /// </summary>
    private static void BuildLathe(Mesh mesh, Vector2[] profile, int sides)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides;
            float a1 = (s + 1) * Mathf.PI * 2f / sides;
            var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 outward = (d0 + d1).normalized;

            for (int i = 0; i < profile.Length - 1; i++)
            {
                Vector2 p = profile[i];
                Vector2 q = profile[i + 1];
                AddQuad(verts, norms, tris,
                    d0 * p.x + Vector3.up * p.y,
                    d1 * p.x + Vector3.up * p.y,
                    d1 * q.x + Vector3.up * q.y,
                    d0 * q.x + Vector3.up * q.y,
                    outward);
            }

            Vector2 top = profile[0];
            Vector2 bottom = profile[profile.Length - 1];
            AddTriangle(verts, norms, tris, Vector3.up * top.y, d0 * top.x + Vector3.up * top.y, d1 * top.x + Vector3.up * top.y, Vector3.up);
            AddTriangle(verts, norms, tris, Vector3.up * bottom.y, d0 * bottom.x + Vector3.up * bottom.y, d1 * bottom.x + Vector3.up * bottom.y, Vector3.down);
        }

        Apply(mesh, verts, norms, tris);
    }

    /// <summary>
    /// Persiana metalica: bandas en zigzag que miran hacia +X.
    /// Ocupa x de 0 a depth, y de 0 a height, z de -width/2 a width/2.
    /// </summary>
    private static void BuildShutter(Mesh mesh, float width, float height, float bandHeight, float depth)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        int bands = Mathf.Max(1, Mathf.RoundToInt(height / bandHeight));
        float bh = height / bands;
        float hz = width * 0.5f;

        for (int b = 0; b < bands; b++)
        {
            float y0 = b * bh;
            float ym = y0 + bh * 0.5f;
            float y1 = y0 + bh;

            AddQuad(verts, norms, tris,
                new Vector3(0f, y0, -hz), new Vector3(0f, y0, hz),
                new Vector3(depth, ym, hz), new Vector3(depth, ym, -hz),
                Vector3.right);
            AddQuad(verts, norms, tris,
                new Vector3(depth, ym, -hz), new Vector3(depth, ym, hz),
                new Vector3(0f, y1, hz), new Vector3(0f, y1, -hz),
                Vector3.right);
        }

        Apply(mesh, verts, norms, tris);
    }

    // En Unity, una cara se ve desde el lado hacia el que apunta Cross(b - a, c - a).
    // Si apunta al reves de 'outward', se invierte el orden de los vertices.
    private static void AddQuad(List<Vector3> v, List<Vector3> n, List<int> t,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        if (normal.sqrMagnitude < 1e-12f) normal = Vector3.Cross(c - a, d - a);
        bool flip = Vector3.Dot(normal, outward) < 0f;
        normal = (flip ? -normal : normal).normalized;

        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);

        if (!flip) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
    }

    private static void AddTriangle(List<Vector3> v, List<Vector3> n, List<int> t,
        Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        bool flip = Vector3.Dot(normal, outward) < 0f;
        normal = (flip ? -normal : normal).normalized;

        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        n.Add(normal); n.Add(normal); n.Add(normal);

        if (!flip) { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
        else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
    }

    private static void Apply(Mesh mesh, List<Vector3> verts, List<Vector3> norms, List<int> tris)
    {
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
    }

    // ------------------------------------------------------------------ utilidades

    private static GameObject MeshChild(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>
    /// Cubo decorativo SIN collider: no frena al jugador y no tapa la linea de vision
    /// que usa VisibilityChecker para descubrir cambios.
    /// </summary>
    private static GameObject BoxChild(Transform parent, string name, Material mat, Vector3 localPos, Vector3 scale, Vector3 euler)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static T FindInScene<T>(System.Predicate<T> match) where T : Component
    {
        foreach (T c in FindAllInScene<T>())
            if (match == null || match(c)) return c;
        return null;
    }

    private static List<T> FindAllInScene<T>() where T : Component
    {
        var result = new List<T>();
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            result.AddRange(go.GetComponentsInChildren<T>(true));
        return result;
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
