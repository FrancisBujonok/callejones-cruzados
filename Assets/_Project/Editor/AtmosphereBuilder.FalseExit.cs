using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: la falsa salida (Paso 31).
/// Menu: Callejones > Falsa salida.
///
/// Crea "FalsaSalida" (raiz de la escena) con el componente FalseExit y, adentro, el "Montaje"
/// apagado: lo que se pega al callejon sin salida cuando el laberinto arma una. Todo esta en
/// coordenadas del Fondo (entrada en z = -1.5, pared del fondo en z = 1.4, paredes a x = +-1.4):
/// - Dos guirnaldas de farolitos de papel encendidos (el mismo material que las de la plaza),
///   una cerca de la pared del fondo y otra mas adelante y mas alta: desde el cruce se ven
///   una detras de la otra.
/// - Dos luces naranjas: una baja, entre las guirnaldas, y otra arriba de la pared del fondo
///   que ilumina el edificio: el resplandor se ve por encima del callejon, como si la feria
///   estuviera del otro lado.
/// - El murmullo de la feria (Feria_*) y a veces un tambor (Tambor_*), 3 m detras de la pared del
///   fondo y con un pasa bajos.
/// El material apagado de los farolitos es FarolitoApagado.
/// </summary>
public static partial class AtmosphereBuilder
{
    [MenuItem("Callejones/Falsa salida", priority = 30)]
    public static void FalseExitMenu()
    {
        if (!CanRun()) return;
        SetupFalseExit(BuildMaterials());
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Falsa salida lista. Guarda con Ctrl+S.");
    }

    private static void SetupFalseExit(Mats m)
    {
        CorridorTreadmill treadmill = FindInScene<CorridorTreadmill>(null);
        FirstPersonController controller = FindInScene<FirstPersonController>(null);
        if (treadmill == null || controller == null)
        {
            Debug.LogWarning("[Callejones] Faltan el Laberinto o el Player: no se arma la falsa salida.");
            return;
        }

        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "FalsaSalida") Undo.DestroyObjectImmediate(go);
        var root = new GameObject("FalsaSalida");
        Undo.RegisterCreatedObjectUndo(root, "Crear falsa salida");
        Transform rig = Group(root.transform, "Montaje");

        Material lit = m.farolTela;
        Material dark = Mat("FarolitoApagado", Hex("#3A1610"), 0.15f);
        Mesh chochin = GetOrCreateMesh("Chochin");
        BuildLathe(chochin, ChochinProfile, 8);

        // Guirnaldas: (z, altura de los ganchos, cuanto cuelga el cable).
        var spans = new List<Span>();
        var lanterns = new List<Renderer>();
        var rows = new[] { new Vector3(0.9f, 2.85f, 0.35f), new Vector3(-0.5f, 3.05f, 0.4f) };
        foreach (Vector3 row in rows)
        {
            Vector3 a = new Vector3(-1.4f, row.y, row.x), b = new Vector3(1.4f, row.y, row.x);
            spans.Add(new Span(a, b, row.z, 0.012f));
            for (float x = -1.05f; x <= 1.06f; x += 0.35f)
            {
                float t = (x - a.x) / (b.x - a.x);
                float y = row.y - row.z * 4f * t * (1f - t) - 0.4f;
                GameObject lantern = MeshChild(rig, "Farolito", chochin, lit, new Vector3(x, y, row.x));
                lantern.transform.localScale = Vector3.one * 0.8f;
                var r = lantern.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                lanterns.Add(r);
            }
        }
        CablesChild(rig, "FalsaSalida_Guirnaldas", spans, m);

        var lights = new[]
        {
            PointLight(rig, "LuzGuirnaldas", new Vector3(0f, 2.3f, 0.5f), new Color(1f, 0.55f, 0.28f), 6.5f, 3f),
            PointLight(rig, "LuzArriba", new Vector3(0f, 4.3f, 1.0f), new Color(1f, 0.47f, 0.22f), 7.5f, 2.6f),
        };

        AudioSource murmur = NewSource(rig, "Feria", null, false, 60);
        murmur.transform.localPosition = new Vector3(0f, 2.2f, 4.5f);
        murmur.volume = 0.85f;
        Make3D(murmur, 2f, 26f);
        murmur.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1500f;

        AudioClip[] crowd = LoadRealSounds("Feria");
        AudioClip[] drums = LoadRealSounds("Tambor");
        if (crowd.Length == 0) Debug.LogWarning("[Callejones] No estan los sonidos Feria_* en Audio/Reales: la falsa salida va a quedar muda.");

        rig.gameObject.SetActive(false);
        FalseExit falseExit = root.AddComponent<FalseExit>();
        falseExit.Setup(treadmill, treadmill.GetComponent<LandmarkDirector>(), controller.transform,
            controller.GetComponent<FearMeter>(), FindInScene<FigurePresence>(null), rig,
            lanterns.ToArray(), lights, lit, dark, murmur, crowd, drums);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
