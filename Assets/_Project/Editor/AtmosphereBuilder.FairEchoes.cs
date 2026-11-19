using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Paso 40: arma "EcosDeLaFeria" en la escena de los callejones (capitulo 2): el componente
/// FairEchoes y lo que necesita, escondido hasta que hace falta:
/// - los objetos de la feria con textura (peluche, mascaras, pez, bolita, papelito) y el afiche;
/// - el gato de la salida y el gato de la cornisa (con su repisa), con sus maullidos;
/// - los carteles con patitas (vertical y horizontal) que muestran los de "no mires atras" cuando
///   el jugador tiene la mascara puesta.
///
/// Menu: Callejones > Ecos de la feria (capitulo 2), con la escena Prototipo abierta.
/// </summary>
public static partial class AtmosphereBuilder
{
    [MenuItem("Callejones/Ecos de la feria (capitulo 2)", priority = 33)]
    public static void FairEchoesMenu()
    {
        if (!CanRun()) return;
        var treadmill = Object.FindFirstObjectByType<CorridorTreadmill>();
        if (treadmill == null)
        {
            EditorUtility.DisplayDialog("Callejones", "Abri la escena de los callejones (Prototipo) antes de correr este menu.", "OK");
            return;
        }
        BuildFairEchoes(treadmill);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[Callejones] Ecos de la feria listos en " + SceneManager.GetActiveScene().name + ".");
    }

    private static void BuildFairEchoes(CorridorTreadmill treadmill)
    {
        itemsMaterial = null;
        itemsGlowMaterial = null;
        GameObject old = GameObject.Find("EcosDeLaFeria");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("EcosDeLaFeria");
        Material items = ItemsGlowMaterial();
        Material paleta = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/FeriaPaleta.mat");

        // 1. Los objetos (la raiz de cada uno queda en el piso; FairEchoes la mueve y la gira).
        var ids = new[] { "Peluche", "Mascara", "MascaraGato", "Pez", "Bolita", "Papelito", "AficheGato" };
        var models = new GameObject[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            Transform t = Group(root.transform, "Eco_" + ids[i]);
            var d = new MeshDraft();
            switch (ids[i])
            {
                case "Peluche": BearModel(d, Vector3.zero, 1f, BearColor.Marron, 0f); break;
                case "Mascara": MaskModel(d, Vector3.zero, 1f, MaskKind.Zorro); break;
                case "MascaraGato": MaskModel(d, Vector3.zero, 1f, MaskKind.Gato); break;
                case "Pez": FishBagModel(d, Vector3.zero, 1.1f); break;
                case "Bolita": MarbleModel(d, new Vector3(0f, 0.028f, 0f), 0.028f); break;
                case "Papelito": NoteModel(d, Vector3.zero, 20f); break;
            }
            if (ids[i] == "AficheGato")
            {
                Material poster = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/Carteles/P_es_01_fotocopia.mat");
                if (poster != null) SignFace(t, "Afiche", Vector3.zero, new Vector2(0.42f, 0.56f), 0f, poster);
            }
            else
            {
                GameObject mesh = FairDraft(t, "Malla", d, items, true);
                // Las mascaras quedan acostadas, boca arriba.
                if (ids[i].StartsWith("Mascara")) { mesh.transform.localRotation = Quaternion.Euler(-82f, 0f, 0f); mesh.transform.localPosition = new Vector3(0f, 0.03f, 0f); }
            }
            if (ids[i] != "AficheGato") t.localScale = Vector3.one * (ids[i] == "Bolita" ? 1.6f : 1.35f); // que se vean en la penumbra
            models[i] = t.gameObject;
        }

        // 2. Los gatos.
        AudioClip[] meows = LoadRealSounds("Gato");
        GameObject exitCat = StaticCat(root.transform, "GatoDeLaSalida", paleta, false, out AudioSource exitVoice);
        Make3D(exitVoice, 3f, 45f); // se oye de lejos: guia
        GameObject ledgeCat = StaticCat(root.transform, "GatoDeLaCornisa", paleta, true, out AudioSource ledgeVoice);
        Make3D(ledgeVoice, 2f, 20f);

        // 3. Los carteles con patitas.
        Material pawV = Mat("CartelPatitasV", Color.white, 0.1f, baseMap: PawSignTexture("CartelPatitasV", 28, 132));
        Material pawH = Mat("CartelPatitasH", Color.white, 0.1f, baseMap: PawSignTexture("CartelPatitasH", 132, 28));

        var echoes = root.AddComponent<FairEchoes>();
        GameObject player = GameObject.FindWithTag("Player");
        echoes.Setup(treadmill, Object.FindFirstObjectByType<FigurePresence>(),
            player != null ? player.GetComponent<ParanoiaSensor>() : null,
            player != null ? player.GetComponent<FearMeter>() : null,
            ids, models, exitCat, ledgeCat, exitVoice, ledgeVoice, meows, pawV, pawH);
    }

    /// <summary>Un gato sentado (el de la feria, quieto). Con repisa: para ponerlo en una cornisa.</summary>
    private static GameObject StaticCat(Transform parent, string name, Material paleta, bool ledge, out AudioSource voice)
    {
        Transform root = Group(parent, name);
        Transform cat = Group(root, "Gato");
        cat.localScale = Vector3.one * 1.25f;
        var body = new MeshDraft();
        body.Box(new Vector3(0f, 0.13f, -0.05f), new Vector3(0.17f, 0.17f, 0.32f), FBlanco);
        body.Box(new Vector3(0f, 0.2f, 0.07f), new Vector3(0.15f, 0.2f, 0.12f), FBlanco);
        body.Box(new Vector3(0.03f, 0.2f, -0.1f), new Vector3(0.175f, 0.09f, 0.14f), FNegro);
        foreach (float sx in new[] { -0.05f, 0.05f }) body.Box(new Vector3(sx, 0.04f, 0.1f), new Vector3(0.04f, 0.08f, 0.04f), FBlanco);
        body.Box(new Vector3(0f, 0.32f, 0.1f), new Vector3(0.13f, 0.11f, 0.11f), FBlanco);                    // cabeza
        body.Box(new Vector3(0.035f, 0.355f, 0.09f), new Vector3(0.07f, 0.05f, 0.1f), FNegro);
        foreach (float sx in new[] { -0.04f, 0.04f })
            FairTri(body, new Vector3(sx - 0.025f, 0.37f, 0.1f), new Vector3(sx + 0.025f, 0.37f, 0.1f), new Vector3(sx, 0.42f, 0.1f), Vector3.forward, sx < 0f ? FBlanco : FNegro);
        body.Box(new Vector3(-0.03f, 0.33f, 0.156f), new Vector3(0.02f, 0.015f, 0.005f), FVerdeClaro);
        body.Box(new Vector3(0.03f, 0.33f, 0.156f), new Vector3(0.02f, 0.015f, 0.005f), FVerdeClaro);
        body.Box(new Vector3(0.06f, 0.08f, -0.3f), new Vector3(0.035f, 0.035f, 0.22f), FNegro);              // cola
        if (ledge)
        {
            body.Box(new Vector3(0f, -0.03f, -0.05f), new Vector3(0.5f, 0.05f, 0.32f), FMaderaOscura);         // la repisa
            body.Box(new Vector3(-0.2f, -0.12f, -0.17f), new Vector3(0.03f, 0.15f, 0.03f), FMetal);           // soportes
            body.Box(new Vector3(0.2f, -0.12f, -0.17f), new Vector3(0.03f, 0.15f, 0.03f), FMetal);
        }
        FairDraft(cat, "Malla", body, paleta, true);
        voice = NewSource(root, "Voz", null, false, 40);
        voice.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        return root.gameObject;
    }

    /// <summary>Papel con una hilera de patitas rojas (como el sello de la tarjeta).</summary>
    private static Texture2D PawSignTexture(string name, int w, int h)
    {
        EnsureFolder(TexturesDir);
        string path = TexturesDir + "/" + name + ".png";
        var px = new Color[w * h];
        var r = new System.Random(4100 + w);
        Color paper = Hex("#E8D6B0"), ink = Hex("#B8301E");
        for (int i = 0; i < px.Length; i++) px[i] = paper * (0.92f + 0.08f * (float)r.NextDouble());
        bool vertical = h > w;
        int count = 4;
        for (int k = 0; k < count; k++)
        {
            // Centro de cada patita, en zigzag a lo largo del cartel.
            float along = (k + 0.5f) / count;
            float cx = vertical ? w * (k % 2 == 0 ? 0.38f : 0.62f) : w * along;
            float cy = vertical ? h * along : h * (k % 2 == 0 ? 0.38f : 0.62f);
            void Dot(float ox, float oy, float rad)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if ((x - cx - ox) * (x - cx - ox) + (y - cy - oy) * (y - cy - oy) <= rad * rad) px[y * w + x] = ink * (0.9f + 0.1f * (float)r.NextDouble());
            }
            Dot(0f, -1.5f, 3.6f);                                             // almohadilla
            Dot(-4f, 3f, 1.6f); Dot(-1.4f, 5f, 1.6f); Dot(1.4f, 5f, 1.6f); Dot(4f, 3f, 1.6f); // dedos
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = true;
        importer.mipMapBias = -0.3f;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
