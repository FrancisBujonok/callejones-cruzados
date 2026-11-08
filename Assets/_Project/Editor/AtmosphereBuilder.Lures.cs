using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: las pistas que te hacen darte vuelta y mirar de cerca (Paso 25).
/// Menu: Callejones > Pistas y mirar de cerca (tambien corre dentro de "Construir atmosfera (todo)").
///
/// Sonidos nuevos (sintetizados, como todos; ver AtmosphereBuilder.Audio):
/// - Pista_Gato: un maullido sintetizado. Paso 26: no sonaba a gato; ahora se usan los maullidos
///   grabados de Audio/Reales (Gato_1..4, CC0). El sintetizado queda solo si esos faltan.
/// - Pista_Campanillas (loop): alguien que camina con cascabeles, un paso cada 0.6 s.
/// - Pista_Telefono: un timbre de telefono viejo, dos repiques ("rrring, rrring").
/// La luz de atras usa el zumbido de los faroles (Farol_Zumbido).
///
/// En la escena:
/// - "Pistas" (raiz): LookBackLures, las tres fuentes de sonido y la luz de atras (un foco que
///   proyecta sombras, apagado).
/// - Player/Sombra: un cuerpo hecho de una capsula y una esfera que solo proyecta sombra
///   (Shadows Only). Esta apagado: LookBackLures lo prende mientras dura la luz de atras.
/// - Player: SignInspector (mirar de cerca).
/// </summary>
public static partial class AtmosphereBuilder
{
    private class LureSounds
    {
        public AudioClip gato, campanillas, telefono;
    }

    [MenuItem("Callejones/Pistas y mirar de cerca", priority = 27)]
    public static void LuresMenu()
    {
        if (!CanRun()) return;
        AudioClip hum = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/Farol_Zumbido.wav");
        if (hum == null)
        {
            EditorUtility.DisplayDialog("Callejones", "Falta el sonido de los faroles. Ejecuta primero Callejones > Solo sonido.", "OK");
            return;
        }
        SetupLures(BuildLureSounds(), hum);
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Pistas y mirar de cerca listos. Guarda con Ctrl+S.");
    }

    // ------------------------------------------------------------------ 1. sonidos

    private static LureSounds BuildLureSounds()
    {
        return new LureSounds
        {
            gato = GetOrCreateSound("Pista_Gato", 1.1f, 2501, false, SynthMeow),
            campanillas = GetOrCreateSound("Pista_Campanillas", 4.8f, 2502, true, SynthWalkingBells),
            telefono = GetOrCreateSound("Pista_Telefono", 2.6f, 2503, false, SynthPhoneRing),
        };
    }

    // Maullido: una voz con muchos armonicos (la frecuencia de la "cuerda" sube de 520 a 780 Hz
    // y baja a 430), pasada por tres formantes que se mueven: "i" (300 y 2300 Hz), "a" (850 y
    // 1300 Hz) y "u" (350 y 800 Hz). Cada armonico suena mas fuerte cuanto mas cerca este de
    // un formante. Un poco de aire (ruido) y vibrato.
    private static void SynthMeow(float[] o, System.Random r)
    {
        int n = o.Length;
        float dur = 0.95f;
        const int harmonics = 14;
        var phase = new double[harmonics + 1];
        var breath = new Svf();
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            if (t > dur) break;
            float u = t / dur;

            float f0 = u < 0.35f
                ? Mathf.Lerp(520f, 780f, Mathf.SmoothStep(0f, 1f, u / 0.35f))
                : Mathf.Lerp(780f, 430f, Mathf.SmoothStep(0f, 1f, (u - 0.35f) / 0.65f));
            f0 *= 1f + 0.012f * Mathf.Sin(2f * Mathf.PI * 6f * t);

            // Formantes: de "i" a "a" en la primera mitad, de "a" a "u" en la segunda.
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.45f));
            float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.55f) / 0.45f));
            float f1 = Mathf.Lerp(Mathf.Lerp(300f, 850f, a), 350f, b);
            float f2 = Mathf.Lerp(Mathf.Lerp(2300f, 1300f, a), 800f, b);

            float v = 0f;
            for (int k = 1; k <= harmonics; k++)
            {
                float fk = f0 * k;
                phase[k] += 2.0 * System.Math.PI * fk / SampleRate;
                float g1 = Mathf.Exp(-Sq((fk - f1) / 160f));
                float g2 = 0.7f * Mathf.Exp(-Sq((fk - f2) / 220f));
                float g3 = 0.25f * Mathf.Exp(-Sq((fk - 3200f) / 400f));
                v += (g1 + g2 + g3 + 0.03f) / k * (float)System.Math.Sin(phase[k]);
            }
            breath.Set(f2, 2f);
            v += breath.BandN(Bipolar(r)) * 0.08f;

            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((dur - t) / 0.18f);
            o[i] = v * env;
        }
    }

    private static float Sq(float x) => x * x;

    // Campanillas: alguien que camina con cascabeles atados (8 pasos en el loop, uno cada 0.6 s).
    // En cada paso suenan tres a cinco cascabeles casi juntos. (Una campanita grave que habia
    // quedaba sonando de fondo todo el loop, como un tono fijo: se saco.)
    private static void SynthWalkingBells(float[] o, System.Random r)
    {
        int n = o.Length;
        int stepsInLoop = 8;
        for (int s = 0; s < stepsInLoop; s++)
        {
            float t0 = s * (n / (float)stepsInLoop) / SampleRate + 0.03f * Bipolar(r);
            int count = 3 + r.Next(3);
            float accent = s % 2 == 0 ? 1f : 0.75f; // un pie pisa mas fuerte que el otro
            for (int j = 0; j < count; j++)
            {
                int start = Mathf.RoundToInt((t0 + 0.06f * Rand(r)) * SampleRate);
                Jingle(o, (start + n) % n, (0.10f + 0.07f * Rand(r)) * accent, r);
            }
        }
    }

    // Timbre de telefono viejo: un martillito golpea dos campanas 20 veces por segundo
    // (alternando una y otra), en dos repiques de 0.85 s con un silencio de 0.3 s entre medio.
    private static void SynthPhoneRing(float[] o, System.Random r)
    {
        double[] bells = { 1180.0, 1460.0 };
        double[] ratios = { 1.0, 2.32, 4.1 };
        float[] amps = { 1f, 0.45f, 0.2f };
        float[] starts = { 0.02f, 1.17f };
        const float ringLen = 0.85f;
        const float strikeRate = 40f; // 20 por campana
        foreach (float s in starts)
        {
            int strikes = Mathf.RoundToInt(ringLen * strikeRate);
            for (int k = 0; k < strikes; k++)
            {
                float t0 = s + k / strikeRate;
                double f = bells[k % 2] * (1.0 + 0.002 * Bipolar(r));
                int start = Mathf.RoundToInt(t0 * SampleRate);
                int len = Mathf.RoundToInt(0.12f * SampleRate);
                for (int j = 0; j < len && start + j < o.Length; j++)
                {
                    float t = j / (float)SampleRate;
                    float v = 0f;
                    for (int p = 0; p < ratios.Length; p++)
                        v += amps[p] * Mathf.Exp(-t / (0.05f / (p + 1))) * SineAt(f * ratios[p], t, p);
                    v += Bipolar(r) * Mathf.Exp(-t / 0.0015f) * 0.4f; // el golpe del martillo
                    o[start + j] += v * 0.3f;
                }
            }
        }
    }

    // ------------------------------------------------------------------ 2. escena

    private static void SetupLures(LureSounds s, AudioClip hum)
    {
        FirstPersonController controller = FindInScene<FirstPersonController>(null);
        if (controller == null)
        {
            Debug.LogWarning("[Callejones] No hay Player con FirstPersonController: no se arman las pistas.");
            return;
        }
        Transform player = controller.transform;
        Camera cam = player.GetComponentInChildren<Camera>(true);
        ParanoiaSensor paranoia = player.GetComponent<ParanoiaSensor>();
        FearMeter fear = player.GetComponent<FearMeter>();

        // 1. El objeto "Pistas" con sus fuentes y la luz.
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "Pistas") Undo.DestroyObjectImmediate(go);
        var root = new GameObject("Pistas");
        Undo.RegisterCreatedObjectUndo(root, "Crear Pistas");

        AudioSource cat = NewSource(root.transform, "Gato", null, false, 45);
        Make3D(cat, 1.5f, 18f);
        AudioSource bells = NewSource(root.transform, "Campanillas", s.campanillas, true, 45);
        Make3D(bells, 1.5f, 16f);
        AudioSource phone = NewSource(root.transform, "Telefono", s.telefono, false, 45);
        Make3D(phone, 2f, 24f);
        AudioLowPassFilter lowPass = phone.gameObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 22000f;

        var lightGo = new GameObject("LuzAtras");
        lightGo.transform.SetParent(root.transform, false);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = Hex("#D6F0E2");   // tubo fluorescente: lo unico frio entre faroles calidos
        light.range = 14f;
        light.spotAngle = 115f;
        light.innerSpotAngle = 70f;
        light.intensity = 40f;
        light.shadows = LightShadows.Hard; // duras, como en la PS1 (que ni tenia)
        light.shadowStrength = 0.9f;
        light.shadowBias = 0.03f;
        light.enabled = false;
        AudioSource lightHum = NewSource(lightGo.transform, "Zumbido", hum, true, 60);
        Make3D(lightHum, 0.5f, 7f);

        // 2. El cuerpo que solo hace sombra. Sin el, la luz de atras no tendria a quien estirar.
        Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Sombra del jugador");
        DestroyChild(player, "Sombra");
        Transform body = Group(player, "Sombra");
        Material any = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/Cable.mat");
        // El Player esta en el centro de su capsula (alto 2): los pies, 1 m mas abajo.
        Renderer torso = ShadowPart(body, "Torso", PrimitiveType.Capsule, new Vector3(0f, -0.3f, 0f), new Vector3(0.44f, 0.7f, 0.3f), any);
        Renderer head = ShadowPart(body, "Cabeza", PrimitiveType.Sphere, new Vector3(0f, 0.6f, 0f), new Vector3(0.24f, 0.27f, 0.24f), any);
        Renderer arms = ShadowPart(body, "Brazos", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.62f, 0.14f, 0.16f), any);

        LookBackLures lures = root.AddComponent<LookBackLures>();
        lures.Setup(player, cam, paranoia, fear, cat, bells, phone, lowPass, light, lightHum,
            new[] { torso, head, arms }, MeowClips(s.gato), s.telefono);

        // 3. Mirar de cerca.
        SignInspector inspector = player.GetComponent<SignInspector>();
        if (inspector == null) inspector = Undo.AddComponent<SignInspector>(player.gameObject);
        inspector.Setup(cam, controller);
        EditorUtility.SetDirty(inspector);
        SetupObjectLines(); // Paso 28

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    /// <summary>Paso 26: los maullidos grabados (Audio/Reales/Gato_*.wav); si no estan, el sintetizado.</summary>
    private static AudioClip[] MeowClips(AudioClip synth)
    {
        AudioClip[] real = LoadRealSounds("Gato");
        return real.Length > 0 ? real : new[] { synth };
    }

    /// <summary>Una parte del cuerpo de sombra: sin collider, invisible, solo proyecta sombra. Apagada.</summary>
    private static Renderer ShadowPart(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<MeshRenderer>();
        if (mat != null) renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        return renderer;
    }
}
