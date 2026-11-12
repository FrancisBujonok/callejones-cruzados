using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: dos prototipos del Paso 27.
/// Menu: Callejones > Figura y trote (prototipos) (tambien corre dentro de "Construir atmosfera (todo)").
///
/// 1. La Figura: "Figura" (raiz de la escena) con FigurePresence y un cuerpo low-poly hecho de
///    cajas y dos piezas de torno. Proporciones apenas mal: 2.16 m de alto, hombros angostos,
///    brazos que llegan a las rodillas, dedos largos, cuello largo y cabeza chica, sin cara.
///    Material casi negro y mate: de lejos es solo una silueta dentro de la niebla.
///    Sonido: Figura_Respiracion (sintetizado), una respiracion cada 6.5 s.
/// 2. El trote: el componente Stamina en el Player, conectado a FirstPersonController.
/// </summary>
public static partial class AtmosphereBuilder
{
    // Perfiles de torno (radio, altura), de arriba hacia abajo.
    private static readonly Vector2[] FigureTorsoProfile =
    {
        new Vector2(0.05f, 0.64f), new Vector2(0.14f, 0.58f), new Vector2(0.13f, 0.42f),
        new Vector2(0.105f, 0.18f), new Vector2(0.11f, 0f),
    };
    private static readonly Vector2[] FigureHeadProfile =
    {
        new Vector2(0f, 0.25f), new Vector2(0.06f, 0.235f), new Vector2(0.09f, 0.16f),
        new Vector2(0.085f, 0.07f), new Vector2(0.05f, 0.01f), new Vector2(0f, 0f),
    };

    [MenuItem("Callejones/Figura y trote (prototipos)", priority = 29)]
    public static void PrototypesMenu()
    {
        if (!CanRun()) return;
        SetupPrototypes();
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Figura y trote listos. Guarda con Ctrl+S.");
    }

    private static void SetupPrototypes()
    {
        FirstPersonController controller = FindInScene<FirstPersonController>(null);
        if (controller == null)
        {
            Debug.LogWarning("[Callejones] No hay Player: no se arman la Figura ni el trote.");
            return;
        }
        SetupStamina(controller);
        SetupFigure(controller);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ------------------------------------------------------------------ 1. trote

    private static void SetupStamina(FirstPersonController controller)
    {
        GameObject player = controller.gameObject;
        Stamina stamina = player.GetComponent<Stamina>();
        if (stamina == null) stamina = Undo.AddComponent<Stamina>(player);

        var so = new SerializedObject(stamina);
        so.FindProperty("fear").objectReferenceValue = player.GetComponent<FearMeter>();
        so.ApplyModifiedProperties();

        var fpc = new SerializedObject(controller);
        fpc.FindProperty("stamina").objectReferenceValue = stamina;
        fpc.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------------ 2. la Figura

    private static void SetupFigure(FirstPersonController controller)
    {
        Transform player = controller.transform;
        Camera cam = player.GetComponentInChildren<Camera>(true);
        FearMeter fear = player.GetComponent<FearMeter>();
        LookBackLures lures = FindInScene<LookBackLures>(null);

        // Casi negro y sin brillo: se lee como un hueco con forma de persona.
        Material mat = Mat("Figura", Hex("#070605"), 0f);
        AudioClip breathClip = GetOrCreateSound("Figura_Respiracion", 13f, 2701, true, SynthSlowBreath);

        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "Figura") Undo.DestroyObjectImmediate(go);
        var root = new GameObject("Figura");
        Undo.RegisterCreatedObjectUndo(root, "Crear Figura");

        Transform body = Group(root.transform, "Cuerpo");
        Transform head = BuildFigureBody(body, mat);

        AudioSource breath = NewSource(body, "Respiracion", breathClip, true, 50);
        breath.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        breath.volume = 0.45f;
        Make3D(breath, 1.5f, 12f);

        FigurePresence presence = root.AddComponent<FigurePresence>();
        presence.Setup(player, cam, fear, lures, body, head, breath);
        // Paso 31: el laberinto, para asomarse en las esquinas de adelante.
        var so = new SerializedObject(presence);
        so.FindProperty("paths").objectReferenceValue = FindInScene<CorridorTreadmill>(null);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Arma el cuerpo mirando hacia +Z, con los pies en y = 0. Devuelve el pivote de la cabeza.</summary>
    private static Transform BuildFigureBody(Transform body, Material mat)
    {
        Mesh torsoMesh = GetOrCreateMesh("Figura_Torso"); BuildLathe(torsoMesh, FigureTorsoProfile, 6);
        Mesh headMesh = GetOrCreateMesh("Figura_Cabeza"); BuildLathe(headMesh, FigureHeadProfile, 6);

        // Piernas largas y finas, pies chicos.
        foreach (float side in new[] { -1f, 1f })
        {
            BoxChild(body, side < 0 ? "PiernaIzq" : "PiernaDer", mat, new Vector3(side * 0.075f, 0.55f, 0f), new Vector3(0.08f, 1.1f, 0.09f), Vector3.zero);
            BoxChild(body, side < 0 ? "PieIzq" : "PieDer", mat, new Vector3(side * 0.075f, 0.02f, 0.03f), new Vector3(0.08f, 0.04f, 0.15f), Vector3.zero);
        }
        BoxChild(body, "Cadera", mat, new Vector3(0f, 1.12f, 0f), new Vector3(0.24f, 0.1f, 0.12f), Vector3.zero);

        GameObject torso = MeshChild(body, "Torso", torsoMesh, mat, new Vector3(0f, 1.1f, 0f));
        torso.transform.localScale = new Vector3(1f, 1f, 0.55f);
        BoxChild(body, "Hombros", mat, new Vector3(0f, 1.71f, 0f), new Vector3(0.36f, 0.05f, 0.09f), Vector3.zero);

        // Brazos que llegan a las rodillas, apenas hacia adelante, con tres dedos largos.
        foreach (float side in new[] { -1f, 1f })
        {
            Transform arm = Group(body, side < 0 ? "BrazoIzq" : "BrazoDer");
            arm.localPosition = new Vector3(side * 0.17f, 1.7f, 0f);
            arm.localRotation = Quaternion.Euler(-5f, 0f, side * 3f);
            BoxChild(arm, "Brazo", mat, new Vector3(0f, -0.52f, 0f), new Vector3(0.045f, 1.04f, 0.05f), Vector3.zero);
            for (int f = 0; f < 3; f++)
            {
                float x = (f - 1) * 0.016f;
                BoxChild(arm, "Dedo" + f, mat, new Vector3(x, -1.11f, 0.005f), new Vector3(0.01f, 0.15f, 0.012f), new Vector3(0f, 0f, (f - 1) * 6f));
            }
        }

        // Cuello largo, apenas inclinado, y la cabeza chica sin cara.
        BoxChild(body, "Cuello", mat, new Vector3(0f, 1.82f, 0.015f), new Vector3(0.04f, 0.2f, 0.045f), new Vector3(8f, 0f, 0f));
        Transform head = Group(body, "Cabeza");
        head.localPosition = new Vector3(0f, 1.91f, 0.03f);
        GameObject skull = MeshChild(head, "Craneo", headMesh, mat, Vector3.zero);
        skull.transform.localScale = new Vector3(0.95f, 1f, 0.85f);

        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }
        return head;
    }

    // Respiracion lenta: dos por loop de 13 s (una cada 6.5 s; una persona tranquila respira cada
    // 4 s). Inspiracion: aire que sube de 700 a 1100 Hz en 2.2 s. Pausa. Espiracion mas larga y
    // grave (450 a 320 Hz, 2.9 s), con un roce de garganta muy bajito: pulsos a unos 70 por segundo
    // que aparecen y se cortan. Todo cae dentro del loop, asi empalma sin clic.
    private static void SynthSlowBreath(float[] o, System.Random r)
    {
        int n = o.Length;
        float loop = n / (float)SampleRate;
        float half = loop * 0.5f;
        var inhale = new Svf();
        var exhale = new Svf();
        var mouth = new Svf();
        mouth.Set(1500f, 2f);
        float fryPhase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float local = t % half;
            float v = 0f;

            // Inspiracion: de 0.3 a 2.5 s.
            if (local >= 0.3f && local < 2.5f)
            {
                float u = (local - 0.3f) / 2.2f;
                inhale.Set(Mathf.Lerp(700f, 1100f, u), 1.2f);
                float env = Mathf.Sin(Mathf.PI * u);
                v += inhale.BandN(Bipolar(r)) * env * env * 0.5f;
            }
            else inhale.BandN(0f);

            // Espiracion: de 3.1 a 6.0 s, ataque rapido y caida lenta.
            if (local >= 3.1f && local < 6.0f)
            {
                float u = (local - 3.1f) / 2.9f;
                exhale.Set(Mathf.Lerp(450f, 320f, u), 1f);
                float env = Mathf.Clamp01(u / 0.08f) * Mathf.Pow(1f - u, 1.6f);
                float noise = Bipolar(r);
                v += exhale.BandN(noise) * env * 0.85f + mouth.BandN(noise) * env * 0.12f;

                // Roce de garganta: pulsos cortos a ~70 Hz, solo en dos tramos de la espiracion.
                float fryGate = Mathf.Clamp01(Mathf.Sin(Mathf.PI * Mathf.Clamp01((u - 0.15f) / 0.5f)));
                fryPhase += (68f + 6f * Mathf.Sin(t * 3f)) / SampleRate;
                if (fryPhase >= 1f) fryPhase -= 1f;
                float pulse = fryPhase < 0.08f ? (1f - fryPhase / 0.08f) : 0f;
                v += pulse * fryGate * env * 0.06f;
            }
            else { exhale.BandN(0f); mouth.BandN(0f); }

            o[i] = v;
        }
    }
}
