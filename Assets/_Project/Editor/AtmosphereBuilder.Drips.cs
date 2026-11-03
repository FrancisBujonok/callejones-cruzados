using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Parte de AtmosphereBuilder: el goteo y el arreglo de los carteles que parpadeaban (Paso 21).
/// Menu: Callejones > Goteo, ventanas y carteles (tambien corre con "Edificios, techados y patio"
/// y con "Construir atmosfera (todo)").
///
/// Goteo:
/// - Puntos de goteo (DripSource) en el borde de los toldos del recto, en los aires de los pisos
///   5 a 7 (Buildings), en el techado y en la ropa del patio, cada uno con su charco abajo.
/// - DripSystem en la Main Camera: gotas, ondas y sonido (Shaders/Agua.shader, Audio/Goteo_*).
/// - Texturas y sonidos: Herramientas/generar_goteo.py.
///
/// Carteles que desaparecian: los vertices de PS1 Lit saltan a una grilla de pantalla, y una cara
/// pegada a 2 mm de su marco (o 1 cm de una pared) quedaba a ratos detras de el, sobre todo de
/// lejos y de costado. Ahora los materiales de carteles, placas y vidrios usan _PS1DepthOffset
/// (se dibujan apenas por delante de lo que tienen detras) y las caras mas pegadas se separaron.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string DripTexDir = Root + "/Textures/Goteo";
    private const string AguaShaderName = "Callejones/Agua";
    // Corrimiento de profundidad de lo que va pegado a otra cosa. Se mide en "pendientes de
    // pixel": cubre el salto de los vertices PS1 a 1080p sin que el cartel tape lo que tiene
    // delante (solo gana contra lo que esta a unos centimetros detras).
    private const float DecalOffset = -4f;

    [MenuItem("Callejones/Goteo, ventanas y carteles", priority = 29)]
    public static void DripsMenu()
    {
        if (!CanRun()) return;
        GameObject farol = AssetDatabase.LoadAssetAtPath<GameObject>(FarolPath);
        if (farol == null)
        {
            EditorUtility.DisplayDialog("Callejones", "Falta el prefab Farol. Ejecuta primero Callejones > Construir atmosfera (todo).", "OK");
            return;
        }
        Mats m = BuildMaterials();  // carteles con corrimiento y fachadas con ventanas que cambian
        DecorateModule(m, farol);   // el recto: toldos, aires y techado que gotean
        BuildMaze(m, farol);        // piezas de 3 x 3 y patio
        SetupNightLighting();       // DripSystem en la Main Camera
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Goteo, ventanas y carteles listos. Guarda con Ctrl+S.");
    }

    // ------------------------------------------------------------------ carteles

    /// <summary>El material se dibuja apenas por delante de lo que tiene detras (ver PS1Lit.shader).</summary>
    private static void SetDecalOffset(Material mat, float offset = DecalOffset)
    {
        if (mat == null || !mat.HasProperty("_PS1DepthOffset")) return;
        mat.SetFloat("_PS1DepthOffset", offset);
        EditorUtility.SetDirty(mat);
    }

    /// <summary>Todos los carteles (tambien los mensajes escondidos), y los vidrios de las ventanas de abajo.</summary>
    private static void ApplyDecalOffsets(Mats m)
    {
        foreach (SignSet set in new[] { m.signsV5, m.signsV2, m.signsH5, m.signsP, m.signsS, m.signsN, m.signsPlaca, m.signsA, m.signsXS, m.signsXN })
        {
            if (set == null) continue;
            foreach (Material mat in set.mats) SetDecalOffset(mat);
        }
        SetDecalOffset(m.signTorii);
        SetDecalOffset(m.cartel);
        SetDecalOffset(m.cartelRojo);
        SetDecalOffset(m.ventanaLuz);
        SetDecalOffset(m.ventanaOscura);
    }

    // ------------------------------------------------------------------ goteo: piezas

    /// <summary>
    /// Recto: una gota en el borde de cada toldo. El toldo izquierdo baja hacia el pasillo y su
    /// borde queda en x = -0.75, a 2.69 m; el tejadillo derecho, en x = 0.87, a 2.87 m.
    /// </summary>
    private static void BuildRectoDrips(Transform deco)
    {
        DestroyChild(deco, "Goteo");
        Transform group = Group(deco, "Goteo");
        var rng = new System.Random(8800);
        float zl = -2.3f + 4.6f * (float)rng.NextDouble();
        AddDrip(group, "ToldoIzq", new Vector3(-0.75f, 2.66f, zl), new Vector2(3f, 8f), null, 0f);
        AddPuddle(group, "CharcoIzq", new Vector3(-0.75f, 0f, zl), 0.6f, (float)rng.NextDouble() * 360f);
        float zr = -2.3f + 4.6f * (float)rng.NextDouble();
        AddDrip(group, "TejadilloDer", new Vector3(0.875f, 2.83f, zr), new Vector2(3.5f, 9f), null, 0f);
        AddPuddle(group, "CharcoDer", new Vector3(0.875f, 0f, zr), 0.55f, (float)rng.NextDouble() * 360f);
    }

    /// <summary>Un punto de goteo. roof: techo que puede haber debajo (o null).</summary>
    private static void AddDrip(Transform parent, string name, Vector3 localPos, Vector2 interval, GameObject roof, float roofHeight)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<DripSource>().Setup(interval, roof, roofHeight);
    }

    /// <summary>Un charco en el piso (y = 0 de la pieza), con su forma irregular girada.</summary>
    private static void AddPuddle(Transform parent, string name, Vector3 floorPoint, float size, float yaw)
    {
        Material mat = GetPuddleMaterial();
        if (mat == null) return;
        GameObject go = MeshChild(parent, name, GetDripQuad(), mat, new Vector3(floorPoint.x, 0.004f, floorPoint.z));
        go.transform.localRotation = Quaternion.Euler(90f, yaw, 0f); // el cuadrado mira hacia arriba
        go.transform.localScale = new Vector3(size, size, 1f);
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------ goteo: recursos

    /// <summary>Cuadrado de 1 x 1 m centrado, que mira hacia -Z (como el Quad de Unity).</summary>
    private static Mesh GetDripQuad()
    {
        Mesh mesh = GetOrCreateMesh("Goteo_Cuadrado");
        if (mesh.vertexCount == 4) return mesh;
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        AddQuad(v, n, t, new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), Vector3.back);
        var uv = new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        Apply(mesh, v, n, t, uv);
        return mesh;
    }

    private static Texture2D LoadDripTexture(string name)
    {
        string path = DripTexDir + "/" + name + ".png";
        if (!File.Exists(path)) return null;
        SetupSignImport(path); // pixeles duros, sin mipmaps (como los carteles)
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>
    /// Charco: oscuro y muy brillante (refleja los faroles), con la forma de Charco.png. Brilla
    /// apenas en violeta, como si reflejara el cielo: sin eso, en la penumbra no se distinguia de
    /// las manchas del piso.
    /// </summary>
    private static Material GetPuddleMaterial()
    {
        Texture2D tex = LoadDripTexture("Charco");
        if (tex == null)
        {
            Debug.LogWarning("[Callejones] Falta " + DripTexDir + "/Charco.png. Corre Herramientas/generar_goteo.py.");
            return null;
        }
        Material mat = Mat("Charco", Color.white, 0.92f, 0f, new Color(0.75f, 0.77f, 0.82f), new Color(1.4f, 1.3f, 2.2f), tex, tex);
        SetCutout(mat, true);
        SetDecalOffset(mat);
        return mat;
    }

    private static Material GetAguaMaterial(string name, Texture2D tex, Color color, float cutoff, float offset)
    {
        Shader shader = Shader.Find(AguaShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[Callejones] No encuentro el shader " + AguaShaderName + ": no hay gotas.");
            return null;
        }
        string path = MaterialsDir + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Cutoff", cutoff);
        mat.SetFloat("_Offset", offset);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static AudioClip[] LoadClips(string prefix, int count)
    {
        var clips = new List<AudioClip>();
        for (int i = 0; i < count; i++)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + prefix + i + ".wav");
            if (clip != null) clips.Add(clip);
        }
        return clips.ToArray();
    }

    /// <summary>DripSystem en la Main Camera (lo llama SetupSkyDirector).</summary>
    private static void SetupDrips(Camera cam)
    {
        DripSystem drips = cam.GetComponent<DripSystem>();
        if (drips == null) drips = Undo.AddComponent<DripSystem>(cam.gameObject);
        Undo.RecordObject(drips, "Goteo");
        // La gota brilla un poco (pasa el umbral del bloom): asi se ve contra lo oscuro.
        Material drop = GetAguaMaterial("Gota", null, new Color(1.1f, 1.2f, 1.35f, 1f), 0.5f, 0f);
        Material ripple = GetAguaMaterial("Onda", LoadDripTexture("Onda"), new Color(0.75f, 0.82f, 0.9f, 1f), 0.3f, DecalOffset);
        AudioClip[] floor = LoadClips("Goteo_", 4);
        AudioClip[] metal = LoadClips("Goteo_Chapa_", 2);
        if (floor.Length == 0) Debug.LogWarning("[Callejones] Faltan los sonidos del goteo. Corre Herramientas/generar_goteo.py.");
        drips.Setup(FindInScene<FearMeter>(null), FindInScene<CorridorTreadmill>(null), cam, GetDripQuad(), drop, ripple, floor, metal);
        EditorUtility.SetDirty(drips);
    }
}
