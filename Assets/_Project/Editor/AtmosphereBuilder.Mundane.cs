using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: lo cotidiano (Paso 26).
/// Menu: Callejones > Detalles cotidianos (tambien corre dentro de "Construir atmosfera (todo)").
///
/// Sonidos:
/// - Grabados (Audio/Reales, de BigSoundBank, CC0; ver LICENCIAS.txt en esa carpeta): maullidos
///   (Gato_1..4, los usa LookBackLures), platos (Platos_1..3), tos (Tos_1..3) y una moto que
///   pasa (Moto_1). No se sintetizan: el maullido sintetizado no sonaba a gato.
/// - Sintetizado: Evento_Lata, una lata que cae en la maquina expendedora.
/// - Paso 28: mas grabados de BigSoundBank: Persiana_1..2 (rejilla de comercio), Perro_1..4 y
///   Campanillas_1..3 para MundaneSounds, y Gota_1..6 y GotaChapa_1..2 para el goteo
///   (UseRecordedDrips: reemplazan a los sintetizados de DripSystem).
///
/// En la escena: "Cotidiano" (raiz) con MundaneSounds, una fuente "del otro lado de la pared"
/// (3D, con pasa bajos) y otra para la maquina.
/// Las jaulas, plumas y cajas con agujeros son un juego de decoracion mas del recto (SetD, en
/// AtmosphereBuilder.Decor): se arman con Callejones > Fachadas, carteles y lugares.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string RealSoundsDir = AudioDir + "/Reales";

    private class MundaneClips
    {
        public AudioClip[] platos, tos, motos;
        public AudioClip[] persianas, perros, campanillas; // Paso 28
        public AudioClip lata;
    }

    [MenuItem("Callejones/Detalles cotidianos", priority = 28)]
    public static void MundaneMenu()
    {
        if (!CanRun()) return;
        SetupMundane(BuildMundaneSounds());
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Detalles cotidianos listos. Guarda con Ctrl+S.");
    }

    // ------------------------------------------------------------------ 1. sonidos

    private static MundaneClips BuildMundaneSounds()
    {
        return new MundaneClips
        {
            platos = LoadRealSounds("Platos"),
            tos = LoadRealSounds("Tos"),
            motos = LoadRealSounds("Moto"),
            persianas = LoadRealSounds("Persiana"),
            perros = LoadRealSounds("Perro"),
            campanillas = LoadRealSounds("Campanillas"),
            lata = GetOrCreateSound("Evento_Lata", 1.1f, 2601, false, SynthCanDrop),
        };
    }

    /// <summary>Los WAV grabados que empiezan con 'prefix' (Gato_1, Gato_2...), ya importados como los demas.</summary>
    private static AudioClip[] LoadRealSounds(string prefix)
    {
        var clips = new List<AudioClip>();
        if (!AssetDatabase.IsValidFolder(RealSoundsDir)) return clips.ToArray();
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets(prefix + "_ t:AudioClip", new[] { RealSoundsDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileName(path).StartsWith(prefix + "_")) paths.Add(path);
        }
        paths.Sort(System.StringComparer.Ordinal);
        foreach (string path in paths)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM; // como los sintetizados
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        }
        return clips.ToArray();
    }

    // Lata que cae en la maquina: el clac de la tapita de plastico, el golpe seco contra la
    // bandeja (un grave corto y el "tin" del aluminio), un rebote mas chico y la lata que rueda.
    private static void SynthCanDrop(float[] o, System.Random r)
    {
        var band = new Svf();
        band.Set(2600f, 3f);
        // 1. La tapita.
        for (int j = 0; j < Mathf.RoundToInt(0.02f * SampleRate); j++)
        {
            float t = j / (float)SampleRate;
            o[j] += band.BandN(Bipolar(r)) * Mathf.Exp(-t / 0.004f) * 0.6f;
        }
        // 2. Golpes: el primero fuerte, el rebote mas chico.
        CanHit(o, 0.06f, 1f, r);
        CanHit(o, 0.27f, 0.45f, r);
        CanHit(o, 0.40f, 0.2f, r);
        // 3. Rueda: golpecitos cada vez mas juntos y mas suaves.
        float time = 0.5f;
        for (int k = 0; k < 9; k++)
        {
            CanTick(o, time, 0.12f * (1f - k / 10f), r);
            time += 0.055f - 0.003f * k;
        }
    }

    private static void CanHit(float[] o, float start, float amp, System.Random r)
    {
        double[] ring = { 950.0, 2320.0, 3650.0 };
        float[] decay = { 0.10f, 0.06f, 0.035f };
        int s = Mathf.RoundToInt(start * SampleRate);
        int len = Mathf.RoundToInt(0.3f * SampleRate);
        var low = 0f;
        float a = OnePole(500f);
        for (int j = 0; j < len && s + j < o.Length; j++)
        {
            float t = j / (float)SampleRate;
            low += a * (Bipolar(r) - low);
            float v = SineAt(110.0, t) * Mathf.Exp(-t / 0.05f) * 0.8f + low * Mathf.Exp(-t / 0.02f) * 1.2f;
            for (int k = 0; k < ring.Length; k++) v += Mathf.Exp(-t / decay[k]) * SineAt(ring[k], t, k) * 0.35f / (k + 1);
            o[s + j] += v * amp;
        }
    }

    private static void CanTick(float[] o, float start, float amp, System.Random r)
    {
        int s = Mathf.RoundToInt(start * SampleRate);
        int len = Mathf.RoundToInt(0.04f * SampleRate);
        for (int j = 0; j < len && s + j < o.Length; j++)
        {
            float t = j / (float)SampleRate;
            o[s + j] += (SineAt(2320.0 + 80.0 * Bipolar(r), t) * 0.6f + Bipolar(r) * 0.4f) * Mathf.Exp(-t / 0.008f) * amp;
        }
    }

    // ------------------------------------------------------------------ 2. escena

    private static void SetupMundane(MundaneClips c)
    {
        FirstPersonController controller = FindInScene<FirstPersonController>(null);
        if (controller == null)
        {
            Debug.LogWarning("[Callejones] No hay Player: no se arma lo cotidiano.");
            return;
        }
        FearMeter fear = controller.GetComponent<FearMeter>();
        LookBackLures lures = FindInScene<LookBackLures>(null);

        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "Cotidiano") Undo.DestroyObjectImmediate(go);
        var root = new GameObject("Cotidiano");
        Undo.RegisterCreatedObjectUndo(root, "Crear Cotidiano");

        AudioSource wall = NewSource(root.transform, "OtroLado", null, false, 70);
        Make3D(wall, 1f, 14f);
        AudioLowPassFilter filter = wall.gameObject.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = 1800f;
        AudioSource machine = NewSource(root.transform, "Maquina", null, false, 60);
        Make3D(machine, 0.8f, 10f);

        MundaneSounds mundane = root.AddComponent<MundaneSounds>();
        mundane.Setup(controller.transform, fear, lures, wall, filter, machine, c.platos, c.tos, c.motos, c.lata);
        mundane.SetupMore(c.persianas, c.perros, c.campanillas);
        UseRecordedDrips();
        SetupPosterEchoes(controller.transform, lures, c.perros);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ------------------------------------------------------------------ 3. goteo grabado (Paso 28)

    /// <summary>
    /// Si estan las gotas grabadas (Audio/Reales/Gota_*, GotaChapa_*), DripSystem las usa en vez
    /// de las sintetizadas. Sin ellas, queda como estaba.
    /// </summary>
    private static void UseRecordedDrips()
    {
        DripSystem drips = FindInScene<DripSystem>(null);
        if (drips == null) return;
        AudioClip[] floor = LoadRealSounds("Gota");
        AudioClip[] metal = LoadRealSounds("GotaChapa");
        if (floor.Length == 0) return;
        var so = new SerializedObject(drips);
        SetClips(so.FindProperty("floorClips"), floor);
        if (metal.Length > 0) SetClips(so.FindProperty("metalClips"), metal);
        so.ApplyModifiedProperties();
    }

    private static void SetClips(SerializedProperty array, AudioClip[] clips)
    {
        array.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }

    // ------------------------------------------------------------------ 4. ecos de los afiches (Paso 30)

    /// <summary>
    /// "EcosDeAfiches" (raiz de la escena): PosterEchoes con una fuente 3D y un pasa bajos, y lo
    /// que suena con cada tipo de afiche. Los sonidos son grabados (Audio/Reales, CC0):
    /// Fichas, Canto, FuegosLejos, Tambor, Guitarra, Feria y Cuenco; el perro usa los de siempre.
    /// </summary>
    private static void SetupPosterEchoes(Transform player, LookBackLures lures, AudioClip[] dogs)
    {
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "EcosDeAfiches") Undo.DestroyObjectImmediate(go);
        var root = new GameObject("EcosDeAfiches");
        Undo.RegisterCreatedObjectUndo(root, "Crear EcosDeAfiches");

        AudioSource source = NewSource(root.transform, "Fuente", null, false, 60);
        Make3D(source, 1.5f, 30f);
        AudioLowPassFilter filter = source.gameObject.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = 2000f;

        var echoes = new[]
        {
            Echo("mahjong",  LoadRealSounds("Fichas"),      0.7f,  3, 3f, 5f,   2.5f, 2200f, 0.7f,  14f),
            Echo("karaoke",  LoadRealSounds("Canto"),       0.7f,  3, 6f, 10f,  3.5f, 1400f, 0.8f,  22f),
            Echo("perro",    dogs,                          0.6f,  3, 4f, 7f,   0f,   2500f, 0.9f,  25f),
            Echo("fuegos",   LoadRealSounds("FuegosLejos"), 0.6f,  1, 20f, 30f, 15f,  1500f, 1f,    60f),
            Echo("matsuri",  LoadRealSounds("Tambor"),      0.6f,  2, 18f, 26f, 4f,   700f,  1f,    50f),
            Echo("banda",    LoadRealSounds("Guitarra"),    0.65f, 3, 3f, 6f,   3f,   2200f, 0.6f,  16f),
            Echo("feria",    LoadRealSounds("Feria"),       0.6f,  2, 16f, 24f, 2f,   800f,  1f,    45f),
            Echo("adivina",  LoadRealSounds("Cuenco"),      0.5f,  2, 1.2f, 2f, 0.3f, 6000f, 0.55f, 12f),
        };
        root.AddComponent<PosterEchoes>().Setup(player, lures, source, filter, echoes);
    }

    private static PosterEchoes.Echo Echo(string style, AudioClip[] clips, float chance, int max,
        float depthMin, float depthMax, float height, float cutoff, float volume, float maxDistance)
    {
        return new PosterEchoes.Echo
        {
            style = style, clips = clips, chance = chance, maxPerGame = max,
            depth = new Vector2(depthMin, depthMax), height = height, cutoff = cutoff,
            volume = volume, maxDistance = maxDistance,
        };
    }
}
