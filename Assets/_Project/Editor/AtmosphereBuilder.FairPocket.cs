using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Paso 37: el resto de las actividades de la feria.
///
/// - La pesca del pez dorado (puesto S12): una pileta baja con nueve pececitos, un poi y un cuenco
///   (FishScoop). Premio: un pez en una bolsita.
/// - Comprar: en los puestos de comida (brochetas), frutas (manzana caramelizada), bebidas (ramune)
///   y mascaras (mascara de gato), con StallShop.
/// - La tarjeta de sellos: una cajita en el cartel del programa, antes del arco (StampCardBox).
/// - En el jugador: los sonidos chicos (PocketSounds) y lo que tiene en la mano (HeldItems, en la
///   camara).
/// - La imagen de la mascara puesta (MascaraVista.png), que usa Callejones > Menus.
///
/// Espacio de cada puesto: el frente en z = 0 mirando a +Z (ver BuildStall).
/// </summary>
public static partial class AtmosphereBuilder
{
    // La pileta de la pesca (espacio del puesto).
    private const float PoolX = 1.1f, PoolZ0 = -1.15f, PoolZ1 = -0.15f, PoolTop = 0.45f, PoolWater = 0.4f;

    /// <summary>La pileta de la pesca: cuatro bordes, el agua y el cuenco al costado.</summary>
    private static void FishPool(Transform t, MeshDraft d, FairMats f)
    {
        float zc = (PoolZ0 + PoolZ1) * 0.5f, depth = PoolZ1 - PoolZ0;
        d.Box(new Vector3(0f, PoolTop * 0.5f, PoolZ1 - 0.03f), new Vector3(PoolX * 2f, PoolTop, 0.06f), FPlasticoAzul);
        d.Box(new Vector3(0f, PoolTop * 0.5f, PoolZ0 + 0.03f), new Vector3(PoolX * 2f, PoolTop, 0.06f), FPlasticoAzul);
        d.Box(new Vector3(-PoolX + 0.03f, PoolTop * 0.5f, zc), new Vector3(0.06f, PoolTop, depth), FPlasticoAzul);
        d.Box(new Vector3(PoolX - 0.03f, PoolTop * 0.5f, zc), new Vector3(0.06f, PoolTop, depth), FPlasticoAzul);
        d.Box(new Vector3(0f, 0.02f, zc), new Vector3(PoolX * 2f, 0.04f, depth), FPlasticoAzul);
        GameObject water = MeshChild(t, "Agua", GetFloorMesh(new Vector3(PoolX * 2f - 0.12f, 0f, depth - 0.12f)), f.agua,
            new Vector3(0f, PoolWater, zc));
        water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // El cuenco, sobre un cajon al costado de donde se para el jugador.
        d.Box(new Vector3(0.8f, 0.25f, 0.12f), new Vector3(0.3f, 0.5f, 0.26f), FMadera);
        d.Cylinder(new Vector3(0.8f, 0.5f, 0.12f), 0.09f, 0.05f, 8, FBlanco);
        // Pois de repuesto, apilados del lado del vendedor.
        for (int k = 0; k < 4; k++) d.Cylinder(new Vector3(1.3f, 0.92f + k * 0.012f, -0.6f), 0.06f, 0.008f, 8, k % 2 == 0 ? FRojo : FBlanco);
        d.Box(new Vector3(1.3f, 0.45f, -0.6f), new Vector3(0.3f, 0.9f, 0.3f), FMaderaOscura);
    }

    private static void BuildFishGame(Transform t, Transform spot, FairMats f)
    {
        spot.localPosition = new Vector3(0f, 0f, 0.5f);
        Transform bowl = Group(t, "Cuenco");
        bowl.localPosition = new Vector3(0.8f, 0.5f, 0.12f);

        var rng = new System.Random(3712);
        var fish = new Transform[9];
        for (int i = 0; i < fish.Length; i++)
        {
            var d = new MeshDraft();
            int body = i % 4 == 3 ? FBlanco : FNaranja;
            d.Box(new Vector3(0f, 0.01f, 0.005f), new Vector3(0.032f, 0.018f, 0.065f), body);
            d.Box(new Vector3(0f, 0.012f, -0.042f), new Vector3(0.04f, 0.008f, 0.028f), i % 4 == 3 ? FRojo : FNaranja); // cola
            if (body == FBlanco) d.Box(new Vector3(0f, 0.02f, 0.012f), new Vector3(0.022f, 0.004f, 0.03f), FRojo); // mancha
            GameObject go = FairDraft(t, "Pez" + i, d, f.paleta, false);
            go.transform.localPosition = new Vector3(-0.9f + (float)rng.NextDouble() * 1.8f, PoolWater + 0.006f, -1.0f + (float)rng.NextDouble() * 0.7f);
            fish[i] = go.transform;
        }

        // El poi: un aro rojo con mango y el papel (que se esconde cuando se rompe).
        Transform poi = Group(t, "Poi");
        var frame = RingDraft(0.062f, 0.006f, FRojo);
        frame.Box(new Vector3(0f, 0f, 0.12f), new Vector3(0.014f, 0.008f, 0.12f), FRojo); // mango (hacia el jugador)
        FairDraft(poi, "Aro", frame, f.paleta, false);
        var paperDraft = new MeshDraft();
        paperDraft.Cylinder(new Vector3(0f, -0.001f, 0f), 0.058f, 0.002f, 10, FBlanco);
        GameObject paper = FairDraft(poi, "Papel", paperDraft, f.paleta, false);

        FishScoop game = t.gameObject.AddComponent<FishScoop>();
        var so = new SerializedObject(game);
        GameTexts(so, spot, "Pescar un pez dorado", "Pez", 2, "Peces",
            "Me da un aro con papel. Se va a mojar enseguida.",
            "Me lo pone en una bolsita con agua. Un pez naranja.",
            "Pesqué otro, pero lo devuelve a la pileta. Uno por persona, parece.",
            "Se rompió el papel. El señor me muestra otro, por si quiero.");
        SetObjects(so.FindProperty("fish"), fish);
        so.FindProperty("poi").objectReferenceValue = poi;
        so.FindProperty("paper").objectReferenceValue = paper;
        so.FindProperty("bowl").objectReferenceValue = bowl;
        so.FindProperty("waterY").floatValue = PoolWater;
        so.FindProperty("poolMin").vector2Value = new Vector2(-PoolX + 0.08f, PoolZ0 + 0.08f);
        so.FindProperty("poolMax").vector2Value = new Vector2(PoolX - 0.08f, PoolZ1 - 0.08f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Lo que se compra en cada tipo de puesto (los demas no venden).</summary>
    private static void BuildStallShop(Transform t, int type)
    {
        StallShop.Goods goods;
        string name, stamp;
        int price;
        string[] lines;
        switch (type)
        {
            case 0:
                goods = StallShop.Goods.Brochetas; name = "brochetas"; price = 3; stamp = "Comida";
                lines = new[] { "Le señalo las brochetas. Son de algo que no conozco, pero huelen bien." };
                break;
            case 1:
                goods = StallShop.Goods.Manzana; name = "una manzana caramelizada"; price = 2; stamp = "";
                lines = new[] { "Una manzana bañada en caramelo rojo. Pegajosa." };
                break;
            case 7:
                goods = StallShop.Goods.Ramune; name = "una ramune"; price = 2; stamp = "Bebida";
                lines = new[] { "Una botellita con una bolita adentro. Me muestra cómo empujarla." };
                break;
            case 3:
                goods = StallShop.Goods.MascaraGato; name = "una máscara de gato"; price = 4; stamp = "Mascara";
                lines = new[] { "Elijo una de gato. Se parece al del afiche. M para ponérmela." };
                break;
            default:
                return;
        }
        Transform spot = Group(t, "DondeSeCompra");
        spot.localPosition = new Vector3(0f, 0f, 0.6f);
        t.gameObject.AddComponent<StallShop>().Setup(goods, name, price, stamp, spot, lines);
    }

    /// <summary>La cajita de tarjetas, los sonidos del bolsillo y lo que se tiene en la mano.</summary>
    private static void BuildFairPocket(Transform world, FairMats f, Camera cam, FirstPersonController player)
    {
        // 1. La cajita de tarjetas, colgada entre los postes del cartel del programa (mira a -X).
        Transform box = Group(world, "TarjetasDeSellos");
        box.localPosition = new Vector3(-35.09f, 1.0f, -3.4f);
        var d = new MeshDraft();
        d.Box(Vector3.zero, new Vector3(0.08f, 0.14f, 0.3f), FRojo);
        for (int k = 0; k < 3; k++)
            d.Box(new Vector3(-0.005f + k * 0.012f, 0.08f, 0f), new Vector3(0.004f, 0.06f, 0.22f), FCrema); // tarjetas asomando
        d.Box(new Vector3(-0.042f, 0f, 0f), new Vector3(0.004f, 0.05f, 0.2f), FBlanco); // etiqueta
        FairDraft(box, "Cajita", d, f.paleta, true);
        box.gameObject.AddComponent<StampCardBox>();

        // 2. Los sonidos chicos (en el jugador).
        player.gameObject.AddComponent<PocketSounds>().Setup(new[]
        {
            new PocketSounds.Named { name = "Moneda", clip = GetOrCreateSound("Feria_Moneda", 0.5f, 3701, false, SynthCoin), volume = 0.5f },
            new PocketSounds.Named { name = "Sello", clip = GetOrCreateSound("Feria_Sello", 0.3f, 3702, false, SynthStamp), volume = 0.8f },
            new PocketSounds.Named { name = "Mordida", clip = GetOrCreateSound("Feria_Mordida", 0.3f, 3703, false, SynthBite), volume = 0.6f },
            new PocketSounds.Named { name = "Trago", clip = GetOrCreateSound("Feria_Trago", 0.35f, 3704, false, SynthGulp), volume = 0.6f },
            new PocketSounds.Named { name = "Ramune", clip = GetOrCreateSound("Feria_Ramune", 0.9f, 3705, false, SynthRamune), volume = 0.6f },
            new PocketSounds.Named { name = "Agua", clip = GetOrCreateSound("Feria_Agua", 0.45f, 3706, false, SynthSplash), volume = 0.5f },
            new PocketSounds.Named { name = "Papel", clip = GetOrCreateSound("Feria_Papel", 0.35f, 3707, false, SynthPaper), volume = 0.6f },
        });

        // 3. Lo que se tiene en la mano: abajo a la derecha, un poco mas alla del plano cercano (0.3 m).
        Transform hand = cam.transform;
        Transform stick = Group(hand, "EnLaMano_Brocheta");
        stick.localPosition = new Vector3(0.2f, -0.19f, 0.5f);
        stick.localRotation = Quaternion.Euler(-35f, -15f, 12f);
        var sd = new MeshDraft();
        sd.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.008f, 0.22f, 0.008f), FMadera);
        FairDraft(stick, "Palito", sd, f.paleta, false);
        var bites = new GameObject[3];
        for (int k = 0; k < 3; k++)
        {
            var b = new MeshDraft();
            b.Box(Vector3.zero, new Vector3(0.036f, 0.036f, 0.036f), k == 1 ? FVerdeClaro : FNaranja); // carne glaseada y un pedazo de verdura
            GameObject piece = FairDraft(stick, "Bocado" + k, b, f.paleta, false);
            piece.transform.localPosition = new Vector3(0f, 0.15f - k * 0.04f, 0f); // el de arriba se come primero
            bites[k] = piece;
        }

        Transform apple = Group(hand, "EnLaMano_Manzana");
        apple.localPosition = new Vector3(0.21f, -0.18f, 0.52f);
        var ad = new MeshDraft();
        FairBall(ad, new Vector3(0f, 0.04f, 0f), 0.045f, FRojo);
        ad.Box(new Vector3(0f, -0.04f, 0f), new Vector3(0.007f, 0.12f, 0.007f), FMadera);
        FairDraft(apple, "Manzana", ad, f.paleta, false);

        Transform bottle = Group(hand, "EnLaMano_Ramune");
        bottle.localPosition = new Vector3(0.2f, -0.21f, 0.5f);
        var bd = new MeshDraft();
        bd.Cylinder(Vector3.zero, 0.026f, 0.11f, 8, FPlasticoAzul);
        bd.Cylinder(new Vector3(0f, 0.11f, 0f), 0.014f, 0.04f, 6, FPlasticoAzul);
        bd.Cylinder(new Vector3(0f, 0.15f, 0f), 0.016f, 0.012f, 6, FAzul);
        FairDraft(bottle, "Botella", bd, f.paleta, false);

        cam.gameObject.AddComponent<HeldItems>().Setup(stick, bites, apple, bottle);
    }

    // ------------------------------------------------------------------ la mascara puesta

    /// <summary>
    /// La vista con la mascara puesta: negro con dos agujeros (los ojos), bordes suaves.
    /// 320 x 180 (16:9): los agujeros quedan redondos en una pantalla ancha.
    /// </summary>
    private static Texture2D GetMaskOverlayTexture()
    {
        EnsureFolder(TexturesDir);
        string path = TexturesDir + "/MascaraVista.png";
        const int w = 320, h = 180;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float hole = 0f;
                foreach (float cx in new[] { 112f, 208f })
                {
                    // Un ojo almendrado: elipse algo mas ancha que alta, un poco inclinada hacia afuera.
                    float dx = (x - cx) / 56f, dy = (y - 92f - (x - cx) * (cx < 160f ? 0.12f : -0.12f)) / 44f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    hole = Mathf.Max(hole, Mathf.Clamp01((1f - r) / 0.18f));
                }
                px[y * w + x] = new Color(0.02f, 0.015f, 0.01f, 1f - hole);
            }
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ sonidos chicos

    private static void Tone(float[] o, float start, float f0, float f1, float len, float amp, float decay)
    {
        int s0 = Mathf.RoundToInt(start * SampleRate), n = Mathf.RoundToInt(len * SampleRate);
        double phase = 0;
        for (int i = 0; i < n && s0 + i < o.Length; i++)
        {
            float u = i / (float)n;
            phase += 2.0 * System.Math.PI * Mathf.Lerp(f0, f1, u) / SampleRate;
            o[s0 + i] += amp * Mathf.Exp(-decay * u) * Mathf.Min(1f, i / 40f) * (float)System.Math.Sin(phase);
        }
    }

    private static void Noise(float[] o, System.Random r, float start, float len, float amp, float decay, float smooth)
    {
        int s0 = Mathf.RoundToInt(start * SampleRate), n = Mathf.RoundToInt(len * SampleRate);
        float lp = 0f;
        for (int i = 0; i < n && s0 + i < o.Length; i++)
        {
            float u = i / (float)n;
            lp = Mathf.Lerp((float)r.NextDouble() * 2f - 1f, lp, smooth); // smooth alto = mas grave
            o[s0 + i] += amp * Mathf.Exp(-decay * u) * lp;
        }
    }

    // Dos monedas que chocan: tonos metalicos que no son armonicos.
    private static void SynthCoin(float[] o, System.Random r)
    {
        foreach (float t in new[] { 0f, 0.09f })
        {
            Tone(o, t, 3150f, 3150f, 0.35f, 0.5f, 9f);
            Tone(o, t, 4730f, 4700f, 0.3f, 0.35f, 12f);
            Tone(o, t, 6020f, 6000f, 0.2f, 0.2f, 16f);
        }
    }

    // El sello: un golpe sordo de madera sobre carton.
    private static void SynthStamp(float[] o, System.Random r)
    {
        Tone(o, 0f, 180f, 90f, 0.18f, 1f, 9f);
        Noise(o, r, 0f, 0.08f, 0.6f, 14f, 0.6f);
    }

    // Una mordida: crujidos cortos.
    private static void SynthBite(float[] o, System.Random r)
    {
        for (int k = 0; k < 5; k++) Noise(o, r, k * 0.035f + (float)r.NextDouble() * 0.01f, 0.05f, 0.8f - k * 0.1f, 10f, 0.2f);
    }

    // Un trago: un "glup" que baja.
    private static void SynthGulp(float[] o, System.Random r)
    {
        Tone(o, 0.02f, 420f, 160f, 0.18f, 1f, 5f);
        Noise(o, r, 0.02f, 0.2f, 0.15f, 6f, 0.85f);
    }

    // La ramune: el "plop" de la bolita y el gas.
    private static void SynthRamune(float[] o, System.Random r)
    {
        Tone(o, 0f, 900f, 300f, 0.05f, 1f, 8f);
        Noise(o, r, 0.03f, 0.85f, 0.35f, 4f, 0.05f);
        for (int k = 0; k < 6; k++) Tone(o, 0.12f + k * 0.07f, 2600f + k * 140f, 2800f, 0.02f, 0.15f, 8f); // la bolita que rebota
    }

    // El poi entrando al agua.
    private static void SynthSplash(float[] o, System.Random r)
    {
        Noise(o, r, 0f, 0.4f, 0.7f, 7f, 0.55f);
        Tone(o, 0.01f, 700f, 1300f, 0.06f, 0.25f, 6f);
    }

    // Papel que se rompe o se dobla.
    private static void SynthPaper(float[] o, System.Random r)
    {
        for (int k = 0; k < 9; k++) Noise(o, r, k * 0.03f + (float)r.NextDouble() * 0.015f, 0.03f, 0.5f + (float)r.NextDouble() * 0.4f, 12f, 0.1f);
    }
}
