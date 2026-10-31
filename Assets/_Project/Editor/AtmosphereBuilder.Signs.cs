using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Parte de AtmosphereBuilder: carteles con texto de verdad (Paso 16).
///
/// Los PNG los genera Herramientas/generar_carteles.py (chino tradicional, japones y coreano)
/// y quedan en Assets/_Project/Textures/Carteles. El nombre de cada archivo dice como usarlo:
///     {formato}_{idioma}_{numero}_{estilo}.png      por ejemplo  V5_tw_03_rojo.png
/// Formatos: V5 (pared, vertical), V2 (colgante), H5 (pancarta), P (afiche), T (placa del torii).
/// Paso 18: S (caja de luz saliente o neon vertical), N (neon de letras sueltas, sin fondo),
/// D (placa de direccion azul), F (flecha de salida verde) y A (papelitos pegados, sin fondo).
/// Los N y los A tienen fondo transparente: su material recorta (alpha clipping) y solo quedan
/// las letras o los papeles, que se ven contra el cielo o la pared.
///
/// Aca se arma un material por PNG y se agrupan por formato. Cada cartel de la escena es un
/// plano (Quad) con un SignRandomizer que sortea entre los de su formato; los de Taiwan pesan
/// el doble para que predominen.
///
/// Por que un Quad y no un cubo: el cubo de Unity pone la textura espejada o girada en algunas
/// de sus caras. Con letras inventadas no se notaba; con letras de verdad se leerian al reves.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string SignsDir = Root + "/Textures/Carteles";
    private const float TaiwanWeight = 2f;

    // Tamano de cada formato en el mundo: la misma proporcion que su PNG, asi nada se estira.
    private static readonly Vector2 SignV5Size = new Vector2(0.255f, 1.2f);   // PNG 28 x 132
    private static readonly Vector2 SignV2Size = new Vector2(0.42f, 0.81f);   // PNG 28 x 54
    private static readonly Vector2 SignH5Size = new Vector2(1.9f, 0.403f);   // PNG 132 x 28
    private static readonly Vector2 SignPSize = new Vector2(0.45f, 0.6f);     // PNG 54 x 72
    private static readonly Vector2 SignTSize = new Vector2(0.22f, 0.424f);   // PNG 28 x 54
    private static readonly Vector2 SignSSize = new Vector2(0.32f, 1.21f);    // PNG 28 x 106
    private static readonly Vector2 SignNSize = new Vector2(1.9f, 0.403f);    // PNG 132 x 28
    private static readonly Vector2 SignPlacaSize = new Vector2(0.36f, 0.193f); // PNG 56 x 30 (D y F)
    private static readonly Vector2 SignASize = new Vector2(0.45f, 0.45f);    // PNG 48 x 48

    // Cuanto brilla el neon: pasa con ganas el umbral del bloom (0.8).
    private const float NeonGlow = 2.2f;

    /// <summary>Los carteles de un formato, con su peso.</summary>
    private class SignSet
    {
        public readonly List<Material> mats = new List<Material>();
        public readonly List<float> weights = new List<float>();
    }

    /// <summary>Lee la carpeta de carteles y arma un material por PNG.</summary>
    private static void BuildSigns(Mats m)
    {
        EnsureFolder(MaterialsDir + "/Carteles");
        m.signsV5 = new SignSet();
        m.signsV2 = new SignSet();
        m.signsH5 = new SignSet();
        m.signsP = new SignSet();
        m.signsS = new SignSet();
        m.signsN = new SignSet();
        m.signsPlaca = new SignSet();
        m.signsA = new SignSet();
        m.signTorii = null;
        if (!AssetDatabase.IsValidFolder(SignsDir))
        {
            Debug.LogWarning("[Callejones] No hay carteles en " + SignsDir + ". Corre Herramientas/generar_carteles.py.");
            return;
        }

        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SignsDir }))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.Ordinal); // siempre el mismo orden

        foreach (string path in paths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string[] parts = name.Split('_');
            if (parts.Length < 4) continue;

            SetupSignImport(path);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Material mat = Mat("Carteles/" + name, Color.white, 0.3f, 0f, null, SignEmission(parts[3]), tex, tex);
            // Neones sueltos y papelitos: sin fondo. El resto es un rectangulo lleno.
            SetCutout(mat, parts[0] == "N" || parts[0] == "A");
            float weight = parts[1] == "tw" ? TaiwanWeight : 1f;

            SignSet set = parts[0] switch
            {
                "V5" => m.signsV5,
                "V2" => m.signsV2,
                "H5" => m.signsH5,
                "P" => m.signsP,
                "S" => m.signsS,
                "N" => m.signsN,
                "D" => m.signsPlaca,
                "F" => m.signsPlaca, // las flechas de salida comparten lugar con las placas
                "A" => m.signsA,
                _ => null,
            };
            if (parts[0] == "T") m.signTorii = mat;
            if (set == null) continue;
            set.mats.Add(mat);
            set.weights.Add(weight);
        }
    }

    /// <summary>
    /// Cuanto brilla cada estilo. La emision se multiplica por la misma textura, asi cada pixel
    /// brilla con su propio color: en el neon brillan las letras y el fondo negro sigue negro.
    /// </summary>
    private static Color SignEmission(string style)
    {
        if (style == "rojo") return new Color(1f, 0.35f, 0.25f) * 1.3f;
        if (style == "negro") return new Color(0.85f, 0.7f, 0.35f) * 0.9f;
        if (style.StartsWith("neon")) return Color.white * NeonGlow;
        if (style.StartsWith("caja")) return Color.white;                  // caja de luz
        if (style.StartsWith("placa") || style.StartsWith("flecha")) return Color.white * 0.55f; // chapa sin luz propia
        if (style == "pegado") return Hex("#DCAD78") * 0.6f;               // papel, mas apagado que los carteles
        return Hex("#DCAD78") * 0.9f;                                      // papel
    }

    /// <summary>
    /// Recorte por transparencia (alpha clipping): los pixeles con alfa menor a 0.5 no se dibujan.
    /// PS1 Lit y URP Lit usan las mismas propiedades: _AlphaClip, _Cutoff y la palabra _ALPHATEST_ON.
    /// </summary>
    private static void SetCutout(Material mat, bool on)
    {
        mat.SetFloat("_AlphaClip", on ? 1f : 0f);
        mat.SetFloat("_Cutoff", 0.5f);
        if (on)
        {
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        else
        {
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "");
            mat.renderQueue = -1; // el que diga el shader
        }
        EditorUtility.SetDirty(mat);
    }

    /// <summary>Pixeles duros, sin mipmaps ni compresion: asi se ven las letras como en la PS1.</summary>
    private static void SetupSignImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
            importer.textureCompression == TextureImporterCompression.Uncompressed &&
            importer.npotScale == TextureImporterNPOTScale.None && importer.wrapMode == TextureWrapMode.Clamp)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None; // los PNG no miden potencias de 2
        importer.wrapMode = TextureWrapMode.Clamp;
        // Los neones y los papelitos traen fondo transparente: el alfa tiene que llegar al material.
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = importer.DoesSourceTextureHaveAlpha();
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Una cara de cartel: un plano que mira hacia 'yaw' (0 = hacia -Z, 90 = hacia -X,
    /// -90 = hacia +X, 180 = hacia +Z). El Quad de Unity se ve desde -Z y con las letras derechas.
    /// </summary>
    private static MeshRenderer SignFace(Transform parent, string name, Vector3 localPos, Vector2 size, float yaw, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        return renderer;
    }

    /// <summary>Le pone al cartel su sorteo de texto. Todas sus caras dicen lo mismo.</summary>
    private static void AddSignPick(GameObject owner, SignSet set, params Renderer[] faces)
    {
        if (set == null || set.mats.Count == 0) return;
        foreach (Renderer face in faces) face.sharedMaterial = set.mats[0];
        owner.AddComponent<SignRandomizer>().Setup(faces, set.mats.ToArray(), set.weights.ToArray());
    }

    private static Material FirstOr(SignSet set, Material fallback) =>
        set != null && set.mats.Count > 0 ? set.mats[0] : fallback;

    /// <summary>Cartel vertical pegado a la pared derecha (mira hacia -X).</summary>
    private static void CreateWallSign(Transform parent, string name, Vector3 center, Mats m)
    {
        Transform t = Group(parent, name);
        t.localPosition = center;
        BoxChild(t, "Marco", m.cable, new Vector3(0.015f, 0f, 0f), new Vector3(0.03f, SignV5Size.y + 0.03f, SignV5Size.x + 0.03f), Vector3.zero);
        MeshRenderer face = SignFace(t, "Cara", new Vector3(-0.002f, 0f, 0f), SignV5Size, 90f, FirstOr(m.signsV5, m.cartel));
        AddSignPick(t.gameObject, m.signsV5, face);
    }

    /// <summary>Afiche pegado a una fachada (mira hacia +X, como las fachadas).</summary>
    private static void CreatePoster(Transform parent, string name, Vector3 wallPoint, Mats m)
    {
        MeshRenderer face = SignFace(parent, name, wallPoint, SignPSize, -90f, FirstOr(m.signsP, m.cartel));
        AddSignPick(face.gameObject, m.signsP, face);
    }

    /// <summary>
    /// Placa chica pegada a una pared lateral: direccion azul o flecha verde de salida (Paso 18).
    /// wallPoint: en la cara de la pared. rightWall: pared derecha o E (mira hacia -X).
    /// </summary>
    private static void CreatePlate(Transform parent, string name, Vector3 wallPoint, bool rightWall, Mats m)
    {
        Transform t = Group(parent, name);
        t.localPosition = wallPoint;
        t.localRotation = Quaternion.Euler(0f, rightWall ? 90f : -90f, 0f); // su -Z queda mirando al pasillo
        BoxChild(t, "Chapa", m.cable, new Vector3(0f, 0f, -0.005f), new Vector3(SignPlacaSize.x + 0.02f, SignPlacaSize.y + 0.02f, 0.01f), Vector3.zero);
        // 1 cm por delante de la chapa: el temblor de vertices PS1 no llega a cruzarlas.
        MeshRenderer face = SignFace(t, "Cara", new Vector3(0f, 0f, -0.02f), SignPlacaSize, 0f, FirstOr(m.signsPlaca, m.cartel));
        AddSignPick(t.gameObject, m.signsPlaca, face);
    }

    /// <summary>Papelitos pegados (destapaciones, prestamos, cerrajero...) en una fachada que mira hacia +X.</summary>
    private static void CreateFlyers(Transform parent, string name, Vector3 wallPoint, Mats m)
    {
        MeshRenderer face = SignFace(parent, name, wallPoint, SignASize, -90f, FirstOr(m.signsA, m.cartel));
        AddSignPick(face.gameObject, m.signsA, face);
    }

    /// <summary>Placa del torii (mira hacia -Z, hacia donde llega el jugador). Siempre la misma.</summary>
    private static void BuildToriiPlaque(Transform torii, Mats m)
    {
        DestroyChild(torii, "Placa");
        DestroyChild(torii, "PlacaLetras");
        BoxChild(torii, "Placa", m.cable, new Vector3(0f, 2.75f, -0.07f), new Vector3(SignTSize.x + 0.08f, SignTSize.y + 0.08f, 0.04f), Vector3.zero);
        SignFace(torii, "PlacaLetras", new Vector3(0f, 2.75f, -0.091f), SignTSize, 0f, m.signTorii != null ? m.signTorii : m.cartel);
    }

    /// <summary>Para el menu: cambia solo la placa del torii que ya esta en la escena (sin tocar su sonido).</summary>
    private static void UpdateToriiPlaque(Mats m)
    {
        Transform exit = FindInScene<Transform>(t => t.name == "ModuloSalida");
        Transform torii = exit != null ? exit.Find("Torii") : null;
        if (torii == null) return;
        Undo.RegisterFullObjectHierarchyUndo(torii.gameObject, "Placa del torii");
        BuildToriiPlaque(torii, m);
    }
}
