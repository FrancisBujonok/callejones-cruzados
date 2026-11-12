using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Parte de AtmosphereBuilder: lo que se ve en la feria (Paso 32). Lo usa AtmosphereBuilder.Fair.cs.
///
/// Casi todo se dibuja con MeshDraft (como los detalles de los edificios) y una paleta propia,
/// FeriaPaleta.png: 16 colores en cuadritos, cada cara apunta a uno. Asi un puesto entero es una
/// o dos mallas. Las mallas de la feria se guardan DENTRO de la escena (no son archivos), salvo las
/// de los edificios, que arma BuildBuildingSide como siempre.
///
/// Puestos (BuildStall): mostrador, mesa de atras, cuatro postes, toldo a rayas, cartel, bombitas y
/// la mercaderia segun el tipo: comida, frutas, juguetes, mascaras, peces, tiro al blanco,
/// farolitos, bebidas y la adivina (uno solo, al lado del callejon).
/// </summary>
public static partial class AtmosphereBuilder
{
    private class FairMats
    {
        public Material paleta, baldosa, asfalto, pintura, revoque, farola, farolitoBlanco, farolitoAmarillo;
        public Material agua, arco, escenario, bola;
        public Material[] toldos;
        public readonly List<Material> carteles = new List<Material>();  // horizontales (H5): los puestos
        public readonly List<Material> afiches = new List<Material>();   // afiches de locales (K)
        public readonly List<Material> nobori = new List<Material>();    // verticales (V5): los nobori
        public Material gatoBuscado;                                     // SE BUSCA / MANCHAS
    }

    // Colores de FeriaPaleta.png (cuadritos de 4 x 4 pixeles, en el orden de PaletteUV).
    private const int FRojo = 0, FBlanco = 1, FAzul = 2, FAmarillo = 3, FVerde = 4, FNaranja = 5, FRosa = 6,
        FMadera = 7, FMaderaOscura = 8, FMetal = 9, FPlasticoAzul = 10, FNegro = 11, FCrema = 12, FVioleta = 13,
        FVerdeClaro = 14, FConcreto = 15;
    private static readonly string[] FairPaletteHex =
    {
        "#B8301E", "#E8E2D0", "#2F5A8A", "#E0B040", "#4A7A40", "#D86A28", "#D88AA0", "#8A5A34",
        "#4A2E1C", "#5A5A5A", "#3A6ABA", "#141010", "#D8C8A0", "#5A3A6A", "#8AB040", "#9A948A",
    };
    private static readonly Color FairNoWindow = new Color(0f, 0f, 0f, 1f);

    // Lo llenan los puestos; lo usan el sonido y las marcas para mas adelante.
    private static readonly List<Vector3> FairVendorSpots = new List<Vector3>();
    private static Vector3 FortuneStallSpot = new Vector3(13f, 1.5f, 5f);

    // Puestos: centros en x y las dos filas (el frente del puesto, del lado del pasillo).
    private const float StallStep = 3.25f, StallFirstX = -26f, RowSouthZ = -1f, RowNorthZ = 4f;
    private const int StallCount = 17;

    // ================================================================== materiales

    private static FairMats BuildFairMaterials(Mats m)
    {
        var f = new FairMats();
        Texture2D paleta = GetOrCreateTexture("FeriaPaleta", 16, 16, 1, (px, w, h, r) =>
        {
            for (int i = 0; i < 16; i++)
            {
                Color c = Hex(FairPaletteHex[i]);
                int cx = (i % 4) * 4, cy = h - 4 - (i / 4) * 4; // fila 0 arriba
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                        px[(cy + y) * w + cx + x] = c;
            }
        });
        f.paleta = Mat("FeriaPaleta", Color.white, 0.15f, baseMap: paleta);

        // Toldos a rayas: 4 rayas por textura, de adelante hacia atras.
        string[,] stripes = { { "Rojo", "#B8301E" }, { "Azul", "#2F5A8A" }, { "Verde", "#3E7040" }, { "Amarillo", "#D8A030" } };
        f.toldos = new Material[stripes.GetLength(0)];
        for (int i = 0; i < f.toldos.Length; i++)
        {
            Color a = Hex(stripes[i, 1]), b = Hex("#E8E0CC");
            Texture2D t = GetOrCreateTexture("ToldoRayas" + stripes[i, 0], 16, 8, 2 + i, (px, w, h, r) =>
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[y * w + x] = ((x / 2) % 2 == 0 ? a : b) * (0.94f + 0.06f * Rand(r));
            });
            f.toldos[i] = Mat("ToldoRayas" + stripes[i, 0], Color.white, 0.15f, baseMap: t);
        }

        // Baldosas de la peatonal: 0.5 m (16 pixeles), beige con juntas.
        Texture2D baldosa = GetOrCreateTexture("Baldosas", 64, 64, 61, (px, w, h, r) =>
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int tile = (x / 16) + (y / 16) * 4;
                    float shade = 0.92f + 0.08f * (float)new System.Random(tile * 7 + 3).NextDouble();
                    bool joint = x % 16 == 0 || y % 16 == 0;
                    Color c = Hex("#B8AC98") * shade * (0.95f + 0.05f * Rand(r));
                    px[y * w + x] = joint ? Hex("#7A7064") : c;
                }
        });
        f.baldosa = Mat("Baldosas", Color.white, 0.2f, baseMap: baldosa);

        Texture2D asfalto = GetOrCreateTexture("Asfalto", 64, 64, 67, (px, w, h, r) =>
        {
            for (int i = 0; i < px.Length; i++)
            {
                float n = Rand(r);
                px[i] = Hex("#3A3836") * (0.85f + 0.3f * n * n);
            }
        });
        f.asfalto = Mat("Asfalto", Color.white, 0.25f, baseMap: asfalto);
        f.pintura = Mat("PinturaCalle", Hex("#DEDAD0"), 0.2f);
        f.revoque = Mat("Revoque", Hex("#A89A86"), 0.1f);
        f.farola = Mat("FarolaLuz", Hex("#FFE2B0"), 0.3f, 0f, null, Hex("#FFE2B0") * 1.6f);
        f.farolitoBlanco = Mat("FarolitoBlanco", Hex("#EFE4CC"), 0.2f, 0f, null, Hex("#EFE4CC") * 1.3f);
        f.farolitoAmarillo = Mat("FarolitoAmarillo", Hex("#F2C14E"), 0.2f, 0f, null, Hex("#F2C14E") * 1.5f);
        f.agua = Mat("AguaPecera", Hex("#4A86B0"), 0.9f, 0f, new Color(0.8f, 0.85f, 0.9f), Hex("#2A5A80") * 0.4f);
        f.bola = Mat("BolaCristal", Hex("#B8D8F0"), 0.9f, 0f, new Color(0.9f, 0.9f, 1f), Hex("#7AA8D0") * 0.9f);
        f.arco = FairTextureMat("FeriaArco", "Feria_Arco");
        f.escenario = FairTextureMat("FeriaEscenario", "Feria_Escenario");

        foreach (Material s in m.signsH5.mats) if (s != null && !SignCatalog.IsWarning(s.name)) f.carteles.Add(s);
        foreach (Material s in m.signsK.mats) if (s != null) f.afiches.Add(s);
        foreach (Material s in m.signsV5.mats) if (s != null && !SignCatalog.IsWarning(s.name)) f.nobori.Add(s);
        f.gatoBuscado = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/Carteles/P_es_01_fotocopia.mat");
        return f;
    }

    private static Material FairTextureMat(string matName, string texName)
    {
        string path = Root + "/Textures/Feria/" + texName + ".png";
        if (AssetImporter.GetAtPath(path) != null) SetupSignImport(path);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) Debug.LogWarning("[Callejones] Falta " + path + ". Corre Herramientas/generar_feria.py.");
        return Mat(matName, Color.white, 0.2f, 0f, null, Color.white * 0.15f, tex, tex);
    }

    // ================================================================== el mundo

    private static void BuildFairWorld(Transform world, Mats m, FairMats f)
    {
        FairVendorSpots.Clear();
        // Mallas compartidas que arman otros menus: por si la feria se arma primero.
        BuildLathe(GetOrCreateMesh("Chochin"), ChochinProfile, 8);
        BuildShutter(GetOrCreateMesh("Persiana"), ShutterWidth, ShutterHeight, 0.12f, 0.04f);
        BuildFairGround(Group(world, "Suelo"), f);
        BuildFairBuildings(Group(world, "Edificios"), m, f);
        BuildFairStalls(Group(world, "Puestos"), m, f);
        BuildFairOverhead(Group(world, "Arriba"), m, f);
        BuildFairSidewalk(Group(world, "Vereda"), m, f);
        BuildFairBackStrip(Group(world, "DetrasDeLosPuestos"), m, f);
        BuildFairGate(Group(world, "Arco"), m, f);
        BuildFairStage(Group(world, "Escenario"), m, f);
        BuildFairBounds(Group(world, "Limites"), f);
    }

    // ------------------------------------------------------------------ suelo y calle

    private static void BuildFairGround(Transform g, FairMats f)
    {
        // La peatonal (de la vereda a las fachadas) y la vereda de enfrente.
        FloorPiece(g, "Peatonal", f.baldosa, new Vector3(0f, 0f, 2.75f), new Vector3(222f, 0f, 18.5f));
        FloorPiece(g, "VeredaEnfrente", f.baldosa, new Vector3(1f, 0f, -16f), new Vector3(222f, 0f, 3f));
        FloorPiece(g, "Calle", f.asfalto, new Vector3(1f, -0.15f, -10.5f), new Vector3(222f, 0f, 8f));
        FloorPiece(g, "PisoCallejon", null, new Vector3((AlleyX0 + AlleyX1) * 0.5f, 0.005f, FairBackZ + AlleyDepth * 0.5f), new Vector3(AlleyX1 - AlleyX0, 0f, AlleyDepth));

        // Un solo collider grande para el piso (la calle queda 15 cm mas abajo, pero no se llega).
        var floor = new GameObject("PisoCollider");
        floor.transform.SetParent(g, false);
        BoxCollider box = floor.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.1f, 4f);
        box.size = new Vector3(240f, 0.2f, 60f);

        var d = new MeshDraft();
        // Cordones (15 cm) y lineas de la calle.
        d.Box(new Vector3(1f, -0.075f, -6.65f), new Vector3(222f, 0.15f, 0.3f), FConcreto);
        d.Box(new Vector3(1f, -0.075f, -14.35f), new Vector3(222f, 0.15f, 0.3f), FConcreto);
        FairDraft(g, "Cordones", d, f.paleta, false);

        var paint = new MeshDraft();
        for (float x = -110f; x < 112f; x += 6f)
            paint.Box(new Vector3(x + 1.5f, -0.145f, -10.5f), new Vector3(3f, 0.01f, 0.15f), FBlanco);
        paint.Box(new Vector3(1f, -0.145f, -7.2f), new Vector3(222f, 0.01f, 0.12f), FBlanco);
        paint.Box(new Vector3(1f, -0.145f, -13.8f), new Vector3(222f, 0.01f, 0.12f), FBlanco);
        // Senda peatonal frente a la parada del colectivo.
        for (float z = -13.6f; z <= -7.4f; z += 0.8f)
            paint.Box(new Vector3(-27f, -0.145f, z), new Vector3(3f, 0.01f, 0.4f), FBlanco);
        FairDraft(g, "Pintura", paint, f.pintura, false);
    }

    private static void FloorPiece(Transform parent, string name, Material mat, Vector3 center, Vector3 size)
    {
        Mesh mesh = GetFloorMesh(size);
        GameObject go = MeshChild(parent, name, mesh, mat, center);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        if (mat == null) go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/PisoMojado.mat");
    }

    // ------------------------------------------------------------------ edificios

    /// <summary>
    /// Fachadas a los dos lados, de x = -110 a 112: del lado de la feria (z = 12, mirando al sur) con
    /// un hueco para el callejon (x de 7 a 10), y enfrente (z = -17.5, mirando al norte). Cada tramo
    /// tiene planta baja con locales (persianas, vidrieras, puertas con noren, carteles y toldos) y
    /// arriba los pisos de BuildBuildingSide. Los tramos lejanos llevan menos detalle.
    /// </summary>
    private static void BuildFairBuildings(Transform g, Mats m, FairMats f)
    {
        var rng = new System.Random(3201);
        Mesh shutter = GetOrCreateMesh("Persiana");
        int n = 0;
        void Row(float from, float to, float z, float yaw, int minFloors, int maxFloors, string prefix)
        {
            float x = from;
            while (x < to - 0.1f)
            {
                float len = new[] { 9f, 12f, 15f }[rng.Next(3)];
                if (x + len > to) len = to - x;
                float center = x + len * 0.5f;
                Transform side = Group(g, prefix + n);
                side.localPosition = new Vector3(center, 0f, z);
                side.localRotation = Quaternion.Euler(0f, yaw, 0f);
                bool near = Mathf.Abs(center) < 48f;
                FairStorefront(side, len, rng, near, shutter, m, f);
                BuildBuildingSide(side, "Feria_" + prefix + n, len, rng.Next(minFloors, maxFloors + 1), 900 + n, false, m);
                n++;
                x += len;
            }
        }
        Row(-110f, AlleyX0, FairBackZ, 90f, 3, 5, "Norte");
        Row(AlleyX1, 112f, FairBackZ, 90f, 3, 5, "Norte");
        Row(-110f, 112f, -17.5f, -90f, 3, 6, "Sur");
    }

    /// <summary>Planta baja en espacio de lado: pared en x = 0 mirando a +X, a lo largo de z.</summary>
    private static void FairStorefront(Transform side, float length, System.Random rng, bool near, Mesh shutter, Mats m, FairMats f)
    {
        float half = length * 0.5f;
        SolidBoxChild(side, "PlantaBaja", f.revoque, new Vector3(-0.15f, 1.5f, 0f), new Vector3(0.3f, 3f, length), Vector3.zero);
        var d = new MeshDraft();
        d.Box(new Vector3(0.04f, 3.0f, 0f), new Vector3(0.12f, 0.14f, length), FConcreto); // cornisa
        d.Box(new Vector3(0.03f, 0.08f, 0f), new Vector3(0.08f, 0.16f, length), FMaderaOscura); // zocalo

        int bays = Mathf.RoundToInt(length / 3f);
        for (int j = 0; j < bays; j++)
        {
            float zc = -half + 1.5f + j * 3f;
            int type = near ? rng.Next(5) : 0;
            Transform bay = Group(side, "Local" + j);
            bay.localPosition = new Vector3(0f, 0f, zc);
            switch (type)
            {
                case 0: // persiana baja
                case 1:
                    CreateShutter(bay, "Persiana", Vector3.zero, false, shutter, m);
                    break;
                case 2: // vidriera prendida, con un afiche
                    BoxChild(bay, "Vidriera", m.ventanaLuz, new Vector3(0.02f, 1.35f, 0f), new Vector3(0.04f, 1.6f, 2.3f), Vector3.zero);
                    d.Box(new Vector3(0.05f, 1.35f, zc - 1.2f), new Vector3(0.08f, 1.7f, 0.08f), FMetal);
                    d.Box(new Vector3(0.05f, 1.35f, zc + 1.2f), new Vector3(0.08f, 1.7f, 0.08f), FMetal);
                    d.Box(new Vector3(0.05f, 2.2f, zc), new Vector3(0.08f, 0.08f, 2.48f), FMetal);
                    if (f.afiches.Count > 0)
                        SignFace(bay, "Afiche", new Vector3(0.06f, 1.4f, 0.5f), new Vector2(0.45f, 0.6f), -90f, f.afiches[rng.Next(f.afiches.Count)]);
                    break;
                case 3: // puerta con noren
                    BoxChild(bay, "Hueco", m.hueco, new Vector3(0.01f, 1.05f, 0f), new Vector3(0.02f, 2.1f, 1.3f), Vector3.zero);
                    d.Box(new Vector3(0.05f, 1.1f, zc - 0.7f), new Vector3(0.1f, 2.2f, 0.1f), FMadera);
                    d.Box(new Vector3(0.05f, 1.1f, zc + 0.7f), new Vector3(0.1f, 2.2f, 0.1f), FMadera);
                    d.Box(new Vector3(0.06f, 2.2f, zc), new Vector3(0.12f, 0.1f, 1.5f), FMadera);
                    for (int k = 0; k < 3; k++)
                        BoxChild(bay, "Tela" + k, m.noren, new Vector3(0.14f, 1.78f, -0.42f + k * 0.42f), new Vector3(0.015f, 0.6f, 0.39f), Vector3.zero);
                    break;
                default: // local abierto y oscuro, con mostrador
                    BoxChild(bay, "Hueco", m.hueco, new Vector3(0.01f, 1.2f, 0f), new Vector3(0.02f, 2.3f, 2.4f), Vector3.zero);
                    d.Box(new Vector3(0.25f, 0.45f, zc), new Vector3(0.5f, 0.9f, 2.2f), FMaderaOscura);
                    break;
            }

            if (!near) continue;
            // Arriba de cada local: un cartel horizontal o un toldito.
            if (rng.NextDouble() < 0.6 && f.carteles.Count > 0)
            {
                Material sign = f.carteles[rng.Next(f.carteles.Count)];
                Vector2 size = FitSign(sign, 2.3f, 0.5f);
                d.Box(new Vector3(0.04f, 2.62f, zc), new Vector3(0.06f, size.y + 0.08f, size.x + 0.08f), FNegro);
                SignFace(bay, "Cartel", new Vector3(0.08f, 2.62f, 0f), size, -90f, sign);
            }
            else
            {
                d.Box(new Vector3(0.45f, 2.55f, zc), new Vector3(0.9f, 0.05f, 2.6f), rng.NextDouble() < 0.5 ? FRojo : FAzul);
            }
        }
        FairDraft(side, "Detalles", d, f.paleta, true);
    }

    /// <summary>El tamano de un cartel que entra en w x h sin deformar la imagen.</summary>
    private static Vector2 FitSign(Material sign, float w, float h)
    {
        Texture t = sign != null ? sign.GetTexture("_BaseMap") : null;
        float aspect = t != null && t.height > 0 ? t.width / (float)t.height : 4f;
        return aspect >= w / h ? new Vector2(w, w / aspect) : new Vector2(h * aspect, h);
    }

    // ------------------------------------------------------------------ puestos

    private static void BuildFairStalls(Transform g, Mats m, FairMats f)
    {
        var rng = new System.Random(3202);
        // Huecos: en la fila del norte frente al callejon (y uno para las mesas); en la del sur, dos
        // pasos hacia la vereda.
        var skipNorth = new HashSet<int> { 3, 10, 11 };
        var skipSouth = new HashSet<int> { 5, 13 };
        int[] cycle = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 2, 5, 7, 1, 6 };
        int c = rng.Next(cycle.Length);
        for (int row = 0; row < 2; row++)
        {
            bool north = row == 1;
            for (int i = 0; i < StallCount; i++)
            {
                if ((north ? skipNorth : skipSouth).Contains(i)) continue;
                float x = StallFirstX + i * StallStep;
                int type = north && i == 12 ? 8 : cycle[c++ % cycle.Length]; // la adivina, al lado del callejon
                var pos = new Vector3(x, 0f, north ? RowNorthZ : RowSouthZ);
                BuildStall(g, (north ? "N" : "S") + i, pos, north ? 180f : 0f, type, 3300 + row * 50 + i, m, f);
            }
        }
    }

    private static readonly string[] StallNames = { "Comida", "Frutas", "Juguetes", "Mascaras", "Peces", "TiroAlBlanco", "Farolitos", "Bebidas", "Adivina" };

    /// <summary>Un puesto. Espacio local: el frente (del lado de la gente) en z = 0 mirando a +Z.</summary>
    private static void BuildStall(Transform parent, string id, Vector3 pos, float yaw, int type, int seed, Mats m, FairMats f)
    {
        var rng = new System.Random(seed);
        Transform t = Group(parent, "Puesto_" + id + "_" + StallNames[type]);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        BoxCollider col = t.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 1.2f, -1f);
        col.size = new Vector3(3f, 2.4f, 2f);

        int cloth = new[] { FRojo, FAzul, FVerde, FAmarillo, FCrema }[rng.Next(5)];
        if (type == 8) cloth = FVioleta;
        var d = new MeshDraft();
        bool tub = type == 4;
        if (!tub)
        {
            d.Box(new Vector3(0f, 0.45f, -0.35f), new Vector3(2.8f, 0.9f, 0.6f), cloth);       // faldon del mostrador
            d.Box(new Vector3(0f, 0.92f, -0.35f), new Vector3(2.9f, 0.04f, 0.7f), FMadera);    // tabla
        }
        d.Box(new Vector3(0f, 0.4f, -1.6f), new Vector3(2.6f, 0.8f, 0.5f), FMaderaOscura);     // mesa de atras
        d.Box(new Vector3(0f, 1.3f, -2.0f), new Vector3(2.9f, 2.0f, 0.04f), cloth);            // lona de atras
        foreach (float sx in new[] { -1.45f, 1.45f })
        {
            d.Box(new Vector3(sx, 1.22f, 0.15f), new Vector3(0.06f, 2.44f, 0.06f), FMetal);
            d.Box(new Vector3(sx, 1.1f, -1.95f), new Vector3(0.06f, 2.2f, 0.06f), FMetal);
            d.Box(new Vector3(sx, 0.7f, -1.0f), new Vector3(0.03f, 1.4f, 2f), cloth);          // lona del costado (abajo)
        }

        // Cartel colgado debajo del toldo.
        Material sign = f.carteles.Count > 0 ? f.carteles[rng.Next(f.carteles.Count)] : null;
        if (sign != null && type != 8)
        {
            Vector2 size = FitSign(sign, 1.7f, 0.36f);
            d.Box(new Vector3(0f, 2.0f, 0.13f), new Vector3(size.x + 0.08f, size.y + 0.08f, 0.04f), FNegro);
            SignFace(t, "Cartel", new Vector3(0f, 2.0f, 0.16f), size, 180f, sign);
        }

        var goods = new MeshDraft();
        var lit = new MeshDraft();      // bombitas y lo que brilla (farolitos amarillos)
        StallGoods(t, type, rng, goods, lit, m, f);

        // Bombitas debajo del borde del toldo.
        for (float x = -1.35f; x <= 1.36f; x += 0.3f)
            lit.Box(new Vector3(x, 2.17f + 0.03f * Mathf.Sin(x * 3f), 0.22f), new Vector3(0.05f, 0.06f, 0.05f), FAmarillo);

        FairDraft(t, "Estructura", d, f.paleta, true);
        FairDraft(t, "Mercaderia", goods, f.paleta, true);
        FairDraft(t, "Bombitas", lit, f.farolitoAmarillo, false);

        Material stripes = type == 8 ? f.toldos[1] : f.toldos[rng.Next(f.toldos.Length)];
        GameObject canopy = MeshChild(t, "Toldo", CanopyMesh(3.2f, 2.3f, 2.45f, 2.2f, 0.25f), stripes, Vector3.zero);
        canopy.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;

        FairVendorSpots.Add(t.TransformPoint(new Vector3(0f, 0f, -1.0f)));
        if (type == 8) FortuneStallSpot = t.TransformPoint(new Vector3(0f, 1.6f, -0.8f));
    }

    /// <summary>La mercaderia de cada tipo de puesto.</summary>
    private static void StallGoods(Transform t, int type, System.Random rng, MeshDraft d, MeshDraft lit, Mats m, FairMats f)
    {
        const float top = 0.94f; // arriba del mostrador
        switch (type)
        {
            case 0: // comida: olla, cuencos, brochetas, botellas de salsa y una arrocera atras
                d.Cylinder(new Vector3(-0.8f, top, -0.35f), 0.22f, 0.3f, 8, FMetal);
                d.Cylinder(new Vector3(-0.8f, top + 0.3f, -0.35f), 0.24f, 0.03f, 8, FMetal);
                for (int k = 0; k < 3; k++) d.Cylinder(new Vector3(0.05f, top + k * 0.05f, -0.3f), 0.1f, 0.05f, 8, FBlanco);
                for (int k = 0; k < 6; k++)
                {
                    float x = 0.45f + k * 0.12f;
                    d.Box(new Vector3(x, top + 0.02f, -0.3f), new Vector3(0.03f, 0.03f, 0.35f), FMaderaOscura);
                    d.Box(new Vector3(x, top + 0.04f, -0.24f), new Vector3(0.06f, 0.05f, 0.14f), FNaranja);
                }
                d.Box(new Vector3(1.25f, top + 0.09f, -0.5f), new Vector3(0.06f, 0.18f, 0.06f), FRojo);
                d.Box(new Vector3(1.33f, top + 0.09f, -0.5f), new Vector3(0.06f, 0.18f, 0.06f), FAmarillo);
                d.Cylinder(new Vector3(0.6f, 0.8f, -1.6f), 0.2f, 0.35f, 8, FBlanco);
                break;

            case 1: // frutas: cajones inclinados con frutas
                for (int k = 0; k < 3; k++)
                {
                    float x = -0.9f + k * 0.9f;
                    int fruit = new[] { FRojo, FNaranja, FVerdeClaro }[k];
                    d.Box(new Vector3(x, top + 0.08f, -0.35f), new Vector3(0.75f, 0.16f, 0.55f), FMadera);
                    for (int a = 0; a < 5; a++)
                        for (int b = 0; b < 3; b++)
                            d.Box(new Vector3(x - 0.28f + a * 0.14f, top + 0.2f, -0.52f + b * 0.15f), new Vector3(0.1f, 0.1f, 0.1f), fruit);
                }
                d.Box(new Vector3(-0.6f, 0.95f, -1.6f), new Vector3(0.6f, 0.3f, 0.4f), FPlasticoAzul);
                break;

            case 2: // juguetes: peluches en estantes y globos arriba
                d.Box(new Vector3(0f, 1.25f, -1.85f), new Vector3(2.6f, 0.04f, 0.3f), FMadera);
                d.Box(new Vector3(0f, 1.7f, -1.85f), new Vector3(2.6f, 0.04f, 0.3f), FMadera);
                for (int k = 0; k < 8; k++)
                {
                    int col = new[] { FRosa, FAzul, FAmarillo, FBlanco }[k % 4];
                    float x = -1.1f + (k % 4) * 0.7f, y = k < 4 ? 1.27f : 1.72f;
                    d.Box(new Vector3(x, y + 0.12f, -1.85f), new Vector3(0.22f, 0.24f, 0.18f), col);
                    d.Box(new Vector3(x, y + 0.3f, -1.85f), new Vector3(0.16f, 0.14f, 0.14f), col);
                }
                for (int k = 0; k < 5; k++)
                {
                    var c = new Vector3(-1.0f + k * 0.5f, 2.9f + 0.15f * (k % 2), -0.3f);
                    FairBall(d, c, 0.16f, new[] { FRojo, FAzul, FAmarillo, FVerde, FRosa }[k]);
                    d.Box(new Vector3(c.x, 2.55f, c.z), new Vector3(0.01f, 0.55f, 0.01f), FBlanco);
                }
                break;

            case 3: // mascaras colgadas en la lona de atras
                for (int k = 0; k < 10; k++)
                {
                    float x = -1.15f + (k % 5) * 0.58f, y = k < 5 ? 1.95f : 1.4f;
                    int face = new[] { FBlanco, FRojo, FBlanco, FNegro, FAmarillo }[k % 5];
                    d.Box(new Vector3(x, y, -1.96f), new Vector3(0.24f, 0.3f, 0.04f), face);
                    int eyes = face == FNegro ? FBlanco : FNegro;
                    d.Box(new Vector3(x - 0.05f, y + 0.04f, -1.935f), new Vector3(0.05f, 0.03f, 0.01f), eyes);
                    d.Box(new Vector3(x + 0.05f, y + 0.04f, -1.935f), new Vector3(0.05f, 0.03f, 0.01f), eyes);
                    d.Box(new Vector3(x, y - 0.08f, -1.935f), new Vector3(0.08f, 0.02f, 0.01f), FRojo);
                }
                break;

            case 4: // peces dorados: una pileta baja con pececitos naranjas
                d.Box(new Vector3(0f, 0.2f, -0.65f), new Vector3(2.4f, 0.4f, 1.1f), FPlasticoAzul);
                for (int k = 0; k < 14; k++)
                    d.Box(new Vector3(-1.0f + (float)rng.NextDouble() * 2f, 0.385f, -1.1f + (float)rng.NextDouble() * 0.9f),
                        new Vector3(0.07f, 0.02f, 0.035f), FNaranja);
                GameObject water = MeshChild(t, "Agua", GetFloorMesh(new Vector3(2.3f, 0f, 1.0f)), f.agua, new Vector3(0f, 0.37f, -0.65f));
                water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                for (int k = 0; k < 3; k++) d.Box(new Vector3(-0.8f + k * 0.8f, 0.15f, 0.35f), new Vector3(0.3f, 0.3f, 0.3f), FRojo); // banquitos
                break;

            case 5: // tiro al blanco: botellas en estantes, premios y aros en el mostrador
                for (int s = 0; s < 3; s++)
                {
                    float y = 1.0f + s * 0.4f;
                    d.Box(new Vector3(0f, y, -1.8f), new Vector3(2.6f, 0.04f, 0.3f), FMadera);
                    for (int k = 0; k < 9; k++) d.Cylinder(new Vector3(-1.15f + k * 0.29f, y + 0.02f, -1.8f), 0.05f, 0.22f, 6, FVerde);
                }
                for (int k = 0; k < 6; k++) d.Box(new Vector3(-0.9f + k * 0.12f, top + 0.01f, -0.3f), new Vector3(0.1f, 0.02f, 0.1f), FRojo);
                d.Box(new Vector3(0.9f, top + 0.15f, -0.4f), new Vector3(0.3f, 0.3f, 0.25f), FRosa);
                break;

            case 6: // farolitos de colores colgados del toldo
            {
                var reds = new List<Matrix4x4>(); var whites = new List<Matrix4x4>(); var yellows = new List<Matrix4x4>();
                for (int r = 0; r < 2; r++)
                    for (int k = 0; k < 5; k++)
                    {
                        var p = new Vector3(-1.1f + k * 0.55f, 1.55f + r * 0.0f, -0.25f - r * 0.75f);
                        var mtx = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * 0.9f);
                        ((k + r) % 3 == 0 ? reds : (k + r) % 3 == 1 ? whites : yellows).Add(mtx);
                        d.Box(new Vector3(p.x, 2.0f, p.z), new Vector3(0.01f, 0.1f, 0.01f), FNegro);
                    }
                Mesh chochin = GetOrCreateMesh("Chochin");
                CombinedChild(t, "FarolitosRojos", chochin, reds, m.farolTela);
                CombinedChild(t, "FarolitosBlancos", chochin, whites, f.farolitoBlanco);
                CombinedChild(t, "FarolitosAmarillos", chochin, yellows, f.farolitoAmarillo);
                break;
            }

            case 7: // bebidas: frascos grandes de colores, vasos y una conservadora
                for (int k = 0; k < 4; k++)
                {
                    float x = -1.0f + k * 0.45f;
                    d.Cylinder(new Vector3(x, top, -0.4f), 0.15f, 0.42f, 8, new[] { FRosa, FAmarillo, FVerdeClaro, FNaranja }[k]);
                    d.Cylinder(new Vector3(x, top + 0.42f, -0.4f), 0.1f, 0.05f, 8, FMetal);
                }
                for (int k = 0; k < 3; k++) d.Cylinder(new Vector3(0.8f + k * 0.18f, top, -0.3f), 0.06f, 0.3f, 6, FBlanco);
                d.Box(new Vector3(-0.5f, 0.25f, 0.45f), new Vector3(0.6f, 0.5f, 0.4f), FPlasticoAzul);
                d.Box(new Vector3(-0.5f, 0.52f, 0.45f), new Vector3(0.62f, 0.06f, 0.42f), FBlanco);
                break;

            case 8: // la adivina: cortinas a medio cerrar, una mesita con la bola y campanillas
                foreach (float sx in new[] { -1f, 1f })
                    for (int k = 0; k < 2; k++)
                        BoxChild(t, "Cortina" + sx + k, m.noren, new Vector3(sx * (1.25f - k * 0.28f), 1.3f, 0.12f),
                            new Vector3(0.27f, 2.0f, 0.02f), new Vector3(0f, sx * k * 6f, 0f));
                d.Box(new Vector3(0f, 0.75f, -0.9f), new Vector3(0.7f, 0.04f, 0.7f), FMaderaOscura);
                d.Box(new Vector3(0f, 0.37f, -0.9f), new Vector3(0.1f, 0.74f, 0.1f), FMaderaOscura);
                d.Box(new Vector3(0f, 0.8f, -0.9f), new Vector3(0.7f, 0.06f, 0.72f), FVioleta);
                GameObject ball = MeshChild(t, "Bola", BallMesh(), f.bola, new Vector3(0f, 0.92f, -0.9f));
                ball.transform.localScale = Vector3.one * 0.13f;
                for (int k = 0; k < 4; k++)
                {
                    d.Box(new Vector3(-0.45f + k * 0.3f, 2.05f, 0.05f), new Vector3(0.008f, 0.2f, 0.008f), FBlanco);
                    d.Cylinder(new Vector3(-0.45f + k * 0.3f, 1.86f, 0.05f), 0.035f, 0.08f, 6, FAmarillo);
                }
                if (f.afiches.Count > 0)
                    SignFace(t, "Afiche", new Vector3(0f, 1.75f, -1.97f), new Vector2(0.54f, 0.72f), 180f,
                        f.afiches.Find(a => a.name.Contains("adivina")) ?? f.afiches[0]);
                break;
        }
    }

    /// <summary>El toldo a rayas: una tapa inclinada y el faldon de adelante, de las dos caras.</summary>
    private static Mesh CanopyMesh(float w, float depth, float hFront, float hBack, float valance)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float x0 = -w * 0.5f, x1 = w * 0.5f, zf = 0.25f, zb = -depth;
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, float v0, float v1)
        {
            foreach (Vector3 o in new[] { outward, -outward })
            {
                AddQuad(v, n, t, a, b, c, d, o);
                uv.Add(new Vector2(0f, v0)); uv.Add(new Vector2(2f, v0)); uv.Add(new Vector2(2f, v1)); uv.Add(new Vector2(0f, v1));
            }
        }
        Q(new Vector3(x0, hFront, zf), new Vector3(x1, hFront, zf), new Vector3(x1, hBack, zb), new Vector3(x0, hBack, zb), Vector3.up, 0f, 1f);
        Q(new Vector3(x0, hFront - valance, zf), new Vector3(x1, hFront - valance, zf), new Vector3(x1, hFront, zf), new Vector3(x0, hFront, zf), Vector3.forward, 0f, 0.25f);
        var mesh = new Mesh { name = "Toldo" };
        Apply(mesh, v, n, t, uv);
        return mesh;
    }

    // ------------------------------------------------------------------ arriba del pasillo

    /// <summary>
    /// Postes altos a los dos lados del pasillo (entre puestos), guirnaldas de farolitos en X entre
    /// ellos, banderines a lo largo y de lado a lado, y nobori (banderas verticales) entre medio.
    /// </summary>
    private static void BuildFairOverhead(Transform g, Mats m, FairMats f)
    {
        var poles = new MeshDraft();
        var flags = new MeshDraft();
        var spans = new List<Span>();
        var reds = new List<Matrix4x4>(); var whites = new List<Matrix4x4>(); var yellows = new List<Matrix4x4>();
        const float zs = RowSouthZ + 0.15f, zn = RowNorthZ - 0.15f, top = 4.6f;
        var xs = new List<float>();
        for (float x = StallFirstX + StallStep * 0.5f; x < -StallFirstX; x += StallStep * 2f) xs.Add(x);

        foreach (float x in xs)
            foreach (float z in new[] { zs, zn })
                poles.Box(new Vector3(x, top * 0.5f, z), new Vector3(0.08f, top, 0.08f), FMetal);

        int lantern = 0;
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            // En X entre los postes de un tramo: dos guirnaldas de farolitos.
            foreach (var (a, b) in new[] { (new Vector3(xs[i], top - 0.1f, zs), new Vector3(xs[i + 1], top - 0.1f, zn)),
                                           (new Vector3(xs[i], top - 0.1f, zn), new Vector3(xs[i + 1], top - 0.1f, zs)) })
            {
                const float sag = 0.7f;
                spans.Add(new Span(a, b, sag, 0.012f));
                int count = Mathf.FloorToInt(Vector3.Distance(a, b) / 0.85f);
                for (int k = 1; k < count; k++)
                {
                    float u = k / (float)count;
                    Vector3 p = Vector3.Lerp(a, b, u) + Vector3.down * (sag * 4f * u * (1f - u) + 0.45f);
                    var mtx = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one);
                    (lantern % 4 == 2 ? whites : lantern % 4 == 3 ? yellows : reds).Add(mtx);
                    lantern++;
                }
            }
            // Banderines a lo largo de cada lado y de lado a lado.
            Bunting(flags, spans, new Vector3(xs[i], 3.9f, zs), new Vector3(xs[i + 1], 3.9f, zs), 0.35f, i);
            Bunting(flags, spans, new Vector3(xs[i], 3.9f, zn), new Vector3(xs[i + 1], 3.9f, zn), 0.35f, i + 3);
            Bunting(flags, spans, new Vector3(xs[i], 4.2f, zs), new Vector3(xs[i], 4.2f, zn), 0.4f, i + 1);
        }

        FairDraft(g, "Postes", poles, f.paleta, true);
        FairDraft(g, "Banderines", flags, f.paleta, false);
        CablesChild(g, "Feria_Guirnaldas", spans, m);
        Mesh chochin = GetOrCreateMesh("Chochin");
        CombinedChild(g, "FarolitosRojos", chochin, reds, m.farolTela);
        CombinedChild(g, "FarolitosBlancos", chochin, whites, f.farolitoBlanco);
        CombinedChild(g, "FarolitosAmarillos", chochin, yellows, f.farolitoAmarillo);

        // Nobori: en los otros huecos entre puestos, banderas altas mirando a lo largo del pasillo.
        int flag = 0;
        for (float x = StallFirstX + StallStep * 1.5f; x < -StallFirstX - 1f; x += StallStep * 4f)
            foreach (float z in new[] { zs - 0.05f, zn + 0.05f })
            {
                if (f.nobori.Count == 0) break;
                Transform t = Group(g, "Nobori" + flag);
                t.localPosition = new Vector3(x, 0f, z);
                BoxChild(t, "Mastil", m.metal, new Vector3(0f, 1.7f, 0f), new Vector3(0.04f, 3.4f, 0.04f), Vector3.zero);
                BoxChild(t, "Brazo", m.metal, new Vector3(0f, 3.3f, z < 1.5f ? 0.25f : -0.25f), new Vector3(0.03f, 0.03f, 0.55f), Vector3.zero);
                Material mat = f.nobori[flag % f.nobori.Count];
                Vector2 size = FitSign(mat, 0.5f, 2.2f);
                Vector3 c = new Vector3(0f, 3.25f - size.y * 0.5f, z < 1.5f ? 0.27f : -0.27f);
                SignFace(t, "CaraA", c + new Vector3(0.006f, 0f, 0f), size, -90f, mat);
                SignFace(t, "CaraB", c - new Vector3(0.006f, 0f, 0f), size, 90f, mat);
                flag++;
            }
    }

    /// <summary>Banderines triangulares colgando de un cable (de las dos caras).</summary>
    private static void Bunting(MeshDraft d, List<Span> spans, Vector3 a, Vector3 b, float sag, int colorOffset)
    {
        spans.Add(new Span(a, b, sag, 0.008f));
        int[] colors = { FRojo, FAmarillo, FAzul, FVerde, FBlanco, FRosa };
        float len = Vector3.Distance(a, b);
        int count = Mathf.FloorToInt(len / 0.36f);
        Vector3 along = (b - a).normalized;
        Vector3 side = Vector3.Cross(along, Vector3.up).normalized;
        for (int k = 1; k < count; k++)
        {
            float u = k / (float)count;
            Vector3 p = Vector3.Lerp(a, b, u) + Vector3.down * (sag * 4f * u * (1f - u));
            Vector3 l = p - along * 0.12f, r = p + along * 0.12f, tip = p + Vector3.down * 0.28f;
            int pal = colors[(k + colorOffset) % colors.Length];
            FairTri(d, l, r, tip, side, pal);
            FairTri(d, l, r, tip, -side, pal);
        }
    }

    // ------------------------------------------------------------------ la vereda (lado de la calle)

    private static void BuildFairSidewalk(Transform g, Mats m, FairMats f)
    {
        var d = new MeshDraft();
        var heads = new MeshDraft();

        // Bolardos a lo largo del cordon.
        for (float x = -33f; x <= 33f; x += 1.6f)
        {
            d.Cylinder(new Vector3(x, 0f, -6.2f), 0.09f, 0.85f, 6, FNegro);
            d.Cylinder(new Vector3(x, 0.62f, -6.2f), 0.095f, 0.1f, 6, FAmarillo);
        }

        // Faroles de calle, con el afiche del gato perdido en algunos.
        float[] lamps = { -30f, -17f, -4f, 9f, 22f };
        for (int i = 0; i < lamps.Length; i++)
        {
            float x = lamps[i];
            d.Cylinder(new Vector3(x, 0f, -5.9f), 0.08f, 5.6f, 6, FVerde);
            d.Box(new Vector3(x, 5.55f, -6.5f), new Vector3(0.07f, 0.07f, 1.2f), FVerde);
            heads.Box(new Vector3(x, 5.42f, -7.05f), new Vector3(0.28f, 0.12f, 0.45f), FBlanco);
            if (f.gatoBuscado != null && i % 2 == 0)
            {
                SignFace(g, "GatoBuscado" + i, new Vector3(x, 1.6f, -5.81f), new Vector2(0.3f, 0.42f), 180f, f.gatoBuscado);
                SignFace(g, "GatoBuscadoB" + i, new Vector3(x, 1.55f, -5.99f), new Vector2(0.3f, 0.42f), 0f, f.gatoBuscado);
            }
        }

        // Arboles en canteros, con bancos entre medio.
        float[] trees = { -23.5f, -10.5f, 2.5f, 15.5f, 28.5f };
        foreach (float x in trees)
        {
            d.Box(new Vector3(x, 0.25f, -4.9f), new Vector3(1.2f, 0.5f, 1.2f), FConcreto);
            d.Box(new Vector3(x, 0.47f, -4.9f), new Vector3(1.0f, 0.06f, 1.0f), FMaderaOscura);
            d.Cylinder(new Vector3(x, 0.5f, -4.9f), 0.12f, 2.4f, 6, FMaderaOscura);
            FairBall(d, new Vector3(x, 3.4f, -4.9f), 1.25f, FVerde);
            FairBall(d, new Vector3(x + 0.4f, 4.1f, -4.6f), 0.85f, FVerdeClaro);
            AddSolid(g, "Cantero", new Vector3(x, 0.6f, -4.9f), new Vector3(1.2f, 1.2f, 1.2f));
        }
        foreach (float x in new[] { -17f, -4f, 9f, 22f })
        {
            FairBench(d, new Vector3(x, 0f, -4.6f), 0f);
            AddSolid(g, "Banco", new Vector3(x, 0.4f, -4.6f), new Vector3(1.8f, 0.8f, 0.6f));
        }
        foreach (float x in new[] { -12.5f, 5.5f, 18.5f })
        {
            d.Cylinder(new Vector3(x, 0f, -4.3f), 0.25f, 0.85f, 8, FVerde);
            d.Cylinder(new Vector3(x, 0.85f, -4.3f), 0.27f, 0.05f, 8, FNegro);
            AddSolid(g, "Tacho", new Vector3(x, 0.45f, -4.3f), new Vector3(0.5f, 0.9f, 0.5f));
        }

        // Parada de colectivo frente a la senda peatonal.
        const float bx = -27f, bz = -5.3f;
        foreach (float sx in new[] { -1.3f, 1.3f })
            d.Box(new Vector3(bx + sx, 1.2f, bz + 0.3f), new Vector3(0.08f, 2.4f, 0.08f), FMetal);
        d.Box(new Vector3(bx, 2.45f, bz), new Vector3(2.9f, 0.08f, 1.3f), FMetal);
        d.Box(new Vector3(bx, 1.4f, bz + 0.62f), new Vector3(2.7f, 1.8f, 0.03f), FPlasticoAzul);
        FairBench(d, new Vector3(bx, 0f, bz + 0.25f), 180f);
        d.Box(new Vector3(bx + 1.7f, 1.5f, bz - 0.4f), new Vector3(0.06f, 3f, 0.06f), FMetal);
        d.Box(new Vector3(bx + 1.7f, 2.9f, bz - 0.4f), new Vector3(0.45f, 0.45f, 0.04f), FAzul);
        if (f.afiches.Count > 0)
            SignFace(g, "AficheParada", new Vector3(bx - 0.6f, 1.45f, bz + 0.6f), new Vector2(0.6f, 0.8f), 180f, f.afiches[f.afiches.Count / 2]);
        AddSolid(g, "Parada", new Vector3(bx, 1.2f, bz + 0.45f), new Vector3(2.8f, 2.4f, 0.5f));

        FairDraft(g, "Cosas", d, f.paleta, true);
        FairDraft(g, "LucesFaroles", heads, f.farola, false);
    }

    private static void FairBench(MeshDraft d, Vector3 at, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 P(float x, float y, float z) => at + q * new Vector3(x, y, z);
        d.Box(P(0f, 0.45f, 0f), AbsV(q * new Vector3(1.8f, 0.06f, 0.45f)), FMadera);
        d.Box(P(0f, 0.75f, -0.2f), AbsV(q * new Vector3(1.8f, 0.3f, 0.05f)), FMadera);
        foreach (float sx in new[] { -0.75f, 0.75f })
            d.Box(P(sx, 0.22f, 0f), AbsV(q * new Vector3(0.06f, 0.44f, 0.4f)), FMetal);
    }

    private static Vector3 AbsV(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static void AddSolid(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name + "Collider");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.AddComponent<BoxCollider>().size = size;
    }

    // ------------------------------------------------------------------ detras de los puestos del norte

    private static void BuildFairBackStrip(Transform g, Mats m, FairMats f)
    {
        var d = new MeshDraft();
        var rng = new System.Random(3203);

        // Cajones apilados detras de los puestos.
        for (int i = 0; i < StallCount; i++)
        {
            if (i == 3 || i == 10 || i == 11 || rng.NextDouble() < 0.4) continue;
            float x = StallFirstX + i * StallStep + (float)(rng.NextDouble() - 0.5) * 1.6f;
            int stack = 1 + rng.Next(3);
            for (int k = 0; k < stack; k++)
            {
                int col = rng.NextDouble() < 0.5 ? FMadera : rng.NextDouble() < 0.5 ? FPlasticoAzul : FRojo;
                d.Box(new Vector3(x + k * 0.03f, 0.17f + k * 0.34f, 6.45f), new Vector3(0.55f, 0.32f, 0.4f), col);
            }
        }

        // El generador y sus cables por el piso hasta algunos puestos.
        const float gx = -1.5f, gz = 9f;
        d.Box(new Vector3(gx, 0.4f, gz), new Vector3(1.2f, 0.8f, 0.7f), FAmarillo);
        d.Box(new Vector3(gx, 0.85f, gz), new Vector3(1.0f, 0.1f, 0.5f), FNegro);
        d.Cylinder(new Vector3(gx + 0.45f, 0.9f, gz), 0.04f, 0.3f, 6, FMetal);
        AddSolid(g, "Generador", new Vector3(gx, 0.45f, gz), new Vector3(1.2f, 0.9f, 0.7f));
        var cables = new List<Span>();
        foreach (float x in new[] { -13f, -6.5f, 0f, 13f, 19.5f })
            cables.Add(new Span(new Vector3(gx, 0.06f, gz), new Vector3(x, 0.06f, 6.1f), 0f, 0.02f));
        CablesChild(g, "Feria_CablesPiso", cables, m);

        // Mesas plegables y banquitos de plastico en el hueco de la fila (frente al puesto 3).
        float sx = StallFirstX + 3 * StallStep;
        foreach (var (tx, tz) in new[] { (sx - 1.1f, 5.2f), (sx + 1.1f, 8.4f), (sx - 0.8f, 9.8f) })
        {
            d.Box(new Vector3(tx, 0.72f, tz), new Vector3(0.9f, 0.04f, 0.9f), FBlanco);
            d.Box(new Vector3(tx, 0.36f, tz), new Vector3(0.06f, 0.72f, 0.06f), FMetal);
            AddSolid(g, "Mesa", new Vector3(tx, 0.4f, tz), new Vector3(0.9f, 0.8f, 0.9f));
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                var p = new Vector3(tx + Mathf.Cos(a) * 0.7f, 0f, tz + Mathf.Sin(a) * 0.7f);
                d.Cylinder(p, 0.15f, 0.42f, 6, FRojo);
            }
        }

        // Motos estacionadas contra las fachadas, al este del callejon.
        for (int k = 0; k < 3; k++)
        {
            float x = 20f + k * 1.4f;
            d.Box(new Vector3(x, 0.45f, 11.2f), new Vector3(0.35f, 0.5f, 1.3f), new[] { FRojo, FBlanco, FAzul }[k]);
            d.Cylinder(new Vector3(x, 0f, 10.65f), 0.22f, 0.12f, 8, FNegro);
            d.Cylinder(new Vector3(x, 0f, 11.75f), 0.22f, 0.12f, 8, FNegro);
            d.Box(new Vector3(x, 0.95f, 10.75f), new Vector3(0.6f, 0.05f, 0.05f), FMetal);
            AddSolid(g, "Moto", new Vector3(x, 0.5f, 11.2f), new Vector3(0.5f, 1f, 1.4f));
        }

        // Una maquina expendedora contra la pared, al oeste.
        d.Box(new Vector3(-6f, 0.95f, 11.55f), new Vector3(1.0f, 1.9f, 0.7f), FBlanco);
        BoxChild(g, "PanelMaquina", m.maquinaLuz, new Vector3(-6f, 1.2f, 11.19f), new Vector3(0.8f, 0.9f, 0.02f), Vector3.zero);
        AddSolid(g, "Maquina", new Vector3(-6f, 0.95f, 11.55f), new Vector3(1f, 1.9f, 0.7f));

        // Bolsas de basura junto a un poste.
        TrashMeshSet trash = TrashMeshes();
        CreateTrashBag(g, "Bolsa0", new Vector3(-21f, 0f, 11.3f), 20f, m, trash);
        CreateTrashBag(g, "Bolsa1", new Vector3(-20.5f, 0f, 11.5f), 80f, m, trash);
        CreateTrashBag(g, "Bolsa2", new Vector3(27f, 0f, 11.4f), 140f, m, trash);

        FairDraft(g, "Cosas", d, f.paleta, true);
    }

    // ------------------------------------------------------------------ arco de entrada

    private static void BuildFairGate(Transform g, Mats m, FairMats f)
    {
        const float x = -30.5f, z0 = -1.6f, z1 = 4.6f;
        float zc = (z0 + z1) * 0.5f, span = z1 - z0;
        var d = new MeshDraft();
        foreach (float z in new[] { z0, z1 })
        {
            d.Box(new Vector3(x, 0.2f, z), new Vector3(0.6f, 0.4f, 0.6f), FConcreto);
            SolidBoxChild(g, "Pilar", m.torii, new Vector3(x, 2.4f, z), new Vector3(0.32f, 4.8f, 0.32f), Vector3.zero);
        }
        BoxChild(g, "VigaBaja", m.torii, new Vector3(x, 4.0f, zc), new Vector3(0.24f, 0.24f, span), Vector3.zero);
        BoxChild(g, "VigaAlta", m.torii, new Vector3(x, 4.65f, zc), new Vector3(0.3f, 0.26f, span + 1.2f), Vector3.zero);
        BoxChild(g, "Techo", m.teja, new Vector3(x, 4.9f, zc), new Vector3(0.9f, 0.18f, span + 1.6f), Vector3.zero);
        d.Box(new Vector3(x, 4.33f, zc), new Vector3(0.18f, 0.5f, 1.85f), FNegro);
        SignFace(g, "PlacaOeste", new Vector3(x - 0.1f, 4.33f, zc), new Vector2(1.7f, 0.53f), -90f + 180f, f.arco);
        SignFace(g, "PlacaEste", new Vector3(x + 0.1f, 4.33f, zc), new Vector2(1.7f, 0.53f), -90f, f.arco);
        FairDraft(g, "Bases", d, f.paleta, true);

        // Dos faroles grandes colgando del arco y el afiche del gato en un pilar.
        Mesh chochin = GetOrCreateMesh("Chochin");
        var big = new List<Matrix4x4>
        {
            Matrix4x4.TRS(new Vector3(x, 3.1f, zc - 1.6f), Quaternion.identity, Vector3.one * 1.8f),
            Matrix4x4.TRS(new Vector3(x, 3.1f, zc + 1.6f), Quaternion.identity, Vector3.one * 1.8f),
        };
        CombinedChild(g, "FarolesGrandes", chochin, big, m.farolTela);
        if (f.gatoBuscado != null)
            SignFace(g, "GatoBuscado", new Vector3(x + 0.17f, 1.5f, z1), new Vector2(0.3f, 0.42f), -90f, f.gatoBuscado);
    }

    // ------------------------------------------------------------------ escenario

    private static void BuildFairStage(Transform g, Mats m, FairMats f)
    {
        const float x0 = 28.6f, x1 = 33.8f, z0 = -0.9f, z1 = 3.9f;
        float xc = (x0 + x1) * 0.5f, zc = (z0 + z1) * 0.5f;
        var d = new MeshDraft();
        d.Box(new Vector3(xc, 0.25f, zc), new Vector3(x1 - x0, 0.5f, z1 - z0), FMadera);
        d.Box(new Vector3(x0 - 0.25f, 0.12f, zc), new Vector3(0.5f, 0.24f, 1.4f), FMaderaOscura);
        AddSolid(g, "Tarima", new Vector3(xc, 0.25f, zc), new Vector3(x1 - x0, 0.5f, z1 - z0));
        foreach (float z in new[] { z0 + 0.1f, z1 - 0.1f })
            d.Box(new Vector3(x1 - 0.1f, 2.0f, z), new Vector3(0.1f, 3.0f, 0.1f), FMetal);
        d.Box(new Vector3(x1 - 0.1f, 3.5f, zc), new Vector3(0.1f, 0.1f, z1 - z0), FMetal);
        SignFace(g, "Telon", new Vector3(x1 - 0.18f, 2.05f, zc), new Vector2(3.9f, 2.6f), 90f, f.escenario);
        // Taiko en su soporte y uno chico.
        d.Box(new Vector3(30.8f, 0.8f, zc), new Vector3(0.5f, 0.6f, 0.9f), FMaderaOscura);
        d.Cylinder(new Vector3(30.8f, 1.1f, zc), 0.45f, 0.6f, 10, FMadera);
        d.Cylinder(new Vector3(30.8f, 1.7f, zc), 0.42f, 0.02f, 10, FCrema);
        d.Cylinder(new Vector3(32.2f, 0.5f, zc + 1.2f), 0.25f, 0.4f, 8, FRojo);
        d.Cylinder(new Vector3(32.2f, 0.9f, zc + 1.2f), 0.23f, 0.02f, 8, FCrema);
        FairDraft(g, "Tarima", d, f.paleta, true);
        // El toldo mira al oeste (hacia la gente): girado -90, su frente (+Z local) queda hacia -X.
        GameObject roof = MeshChild(g, "Toldo", CanopyMesh(z1 - z0 + 0.4f, x1 - x0 - 0.2f, 3.6f, 3.3f, 0.3f), f.toldos[0], new Vector3(x0 + 0.05f, 0f, zc));
        roof.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        roof.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
    }

    // ------------------------------------------------------------------ limites

    /// <summary>Vallas de obra en las puntas y paredes invisibles (la calle y las puntas).</summary>
    private static void BuildFairBounds(Transform g, FairMats f)
    {
        var d = new MeshDraft();
        foreach (float x in new[] { -34.3f, 34.3f })
        {
            for (float z = -6f; z < 11.8f; z += 2.05f)
            {
                d.Box(new Vector3(x, 0.55f, z + 1f), new Vector3(0.06f, 0.9f, 1.9f), FBlanco);
                d.Box(new Vector3(x, 0.75f, z + 1f), new Vector3(0.07f, 0.18f, 1.9f), FRojo);
                d.Box(new Vector3(x, 0.4f, z + 1f), new Vector3(0.07f, 0.12f, 1.9f), FRojo);
                d.Box(new Vector3(x, 0.05f, z + 0.1f), new Vector3(0.5f, 0.1f, 0.12f), FNegro);
            }
            AddSolid(g, "Valla", new Vector3(x, 1.5f, 2.75f), new Vector3(0.4f, 3f, 18.5f));
        }
        AddSolid(g, "Cordon", new Vector3(0f, 1.5f, -6.35f), new Vector3(69f, 3f, 0.3f));
        FairDraft(g, "Vallas", d, f.paleta, true);
    }

    // ------------------------------------------------------------------ la salida (el callejon)

    /// <summary>
    /// El callejon detras de la feria (x de 7 a 10, de z = 12 a 22): paredes de ladrillo con pisos
    /// arriba que sobresalen, piso mojado, un farol rojo en la entrada, un cartel, el afiche del gato,
    /// el fondo oscuro, el gato que guia (LeadingCat) y el fin del capitulo (ChapterExit) a 6 m.
    /// </summary>
    private static void BuildFairExit(Transform world, Mats m, FairMats f, Transform player)
    {
        Transform g = Group(world, "Callejon");
        float xc = (AlleyX0 + AlleyX1) * 0.5f, z0 = FairBackZ, z1 = FairBackZ + AlleyDepth, zc = (z0 + z1) * 0.5f;

        foreach (bool left in new[] { true, false })
        {
            Transform side = Group(g, left ? "ParedOeste" : "ParedEste");
            side.localPosition = new Vector3(left ? AlleyX0 : AlleyX1, 0f, zc);
            side.localRotation = Quaternion.Euler(0f, left ? 0f : 180f, 0f);
            SolidBoxChild(side, "PlantaBaja", m.pared, new Vector3(-0.15f, 1.5f, 0f), new Vector3(0.3f, 3f, AlleyDepth), Vector3.zero);
            BuildBuildingSide(side, "Feria_Callejon" + (left ? "O" : "E"), AlleyDepth - 1f, 4, left ? 971 : 972, true, m);
        }
        // El fondo: oscuro, como si el callejon siguiera y doblara.
        SolidBoxChild(g, "Fondo", m.hueco, new Vector3(xc, 3f, z1), new Vector3(AlleyX1 - AlleyX0, 6f, 0.3f), Vector3.zero);
        AddSolid(g, "PisoCallejon", new Vector3(xc, -0.1f, zc), new Vector3(AlleyX1 - AlleyX0, 0.2f, AlleyDepth));

        // La entrada: farol rojo, cables, un cartel vertical y el afiche del gato.
        GameObject farol = AssetDatabase.LoadAssetAtPath<GameObject>(FarolPath);
        if (farol != null)
        {
            var f1 = (GameObject)PrefabUtility.InstantiatePrefab(farol, g);
            f1.transform.localPosition = new Vector3(xc, 3.2f, z0 + 0.8f);
            var f2 = (GameObject)PrefabUtility.InstantiatePrefab(farol, g);
            f2.transform.localPosition = new Vector3(xc + 0.4f, 3.2f, z0 + 5.5f);
        }
        var cables = new List<Span>
        {
            new Span(new Vector3(AlleyX0, 3.25f, z0 + 0.8f), new Vector3(AlleyX1, 3.25f, z0 + 0.8f), 0.08f),
            new Span(new Vector3(AlleyX0, 3.25f, z0 + 5.5f), new Vector3(AlleyX1, 3.25f, z0 + 5.5f), 0.1f),
            new Span(new Vector3(AlleyX0, 3.6f, z0 + 2f), new Vector3(AlleyX1, 3.4f, z0 + 7f), 0.3f),
        };
        CablesChild(g, "Feria_CablesCallejon", cables, m);
        if (f.nobori.Count > 0)
        {
            Material v = f.nobori.Find(s => s.name.Contains("jp")) ?? f.nobori[0];
            Vector2 size = FitSign(v, 0.4f, 1.6f);
            SignFace(g, "CartelEntrada", new Vector3(AlleyX0 + 0.03f, 2.0f, z0 + 1.6f), size, -90f, v);
        }
        if (f.gatoBuscado != null)
            SignFace(g, "GatoBuscado", new Vector3(AlleyX1 - 0.03f, 1.5f, z0 + 1.2f), new Vector2(0.3f, 0.42f), 90f, f.gatoBuscado);

        // El gato y el fin del capitulo.
        BuildLeadingCat(g, f, player, new Vector3(xc, 0f, z0 + 0.6f),
            new[] { new Vector3(xc, 0f, z0 + 3f), new Vector3(xc + 0.2f, 0f, z0 + 7f), new Vector3(xc, 0f, z1 - 0.5f) });
        var exit = new GameObject("FinDelCapitulo");
        exit.transform.SetParent(g, false);
        exit.transform.localPosition = new Vector3(xc, 1.5f, z0 + 6.5f);
        exit.AddComponent<ChapterExit>().Setup("Prototipo", player, new Vector3(AlleyX1 - AlleyX0, 3f, 2f));
    }

    /// <summary>Un gato blanco con manchas negras, sentado, mirando hacia la feria (low poly).</summary>
    private static void BuildLeadingCat(Transform parent, FairMats f, Transform player, Vector3 at, Vector3[] path)
    {
        Transform cat = Group(parent, "Gato");
        cat.localPosition = at;
        cat.localRotation = Quaternion.Euler(0f, 180f, 0f);
        cat.localScale = Vector3.one * 1.25f; // un poco mas grande que uno real: si no, en la penumbra no se ve

        var body = new MeshDraft();
        body.Box(new Vector3(0f, 0.13f, -0.05f), new Vector3(0.17f, 0.17f, 0.32f), FBlanco);
        body.Box(new Vector3(0f, 0.2f, 0.07f), new Vector3(0.15f, 0.2f, 0.12f), FBlanco);       // pecho (sentado)
        body.Box(new Vector3(0.03f, 0.2f, -0.1f), new Vector3(0.175f, 0.09f, 0.14f), FNegro);    // mancha
        foreach (float sx in new[] { -0.05f, 0.05f })
            body.Box(new Vector3(sx, 0.04f, 0.1f), new Vector3(0.04f, 0.08f, 0.04f), FBlanco);   // patas
        FairDraft(cat, "Cuerpo", body, f.paleta, true);

        Transform head = Group(cat, "Cabeza");
        head.localPosition = new Vector3(0f, 0.32f, 0.1f);
        var h = new MeshDraft();
        h.Box(Vector3.zero, new Vector3(0.13f, 0.11f, 0.11f), FBlanco);
        h.Box(new Vector3(0.035f, 0.035f, -0.01f), new Vector3(0.07f, 0.05f, 0.1f), FNegro);    // media cara negra
        foreach (float sx in new[] { -0.04f, 0.04f })
            FairTri(h, new Vector3(sx - 0.025f, 0.05f, 0f), new Vector3(sx + 0.025f, 0.05f, 0f), new Vector3(sx, 0.1f, 0f), Vector3.forward, sx < 0f ? FBlanco : FNegro);
        h.Box(new Vector3(-0.03f, 0.01f, 0.056f), new Vector3(0.02f, 0.015f, 0.005f), FVerdeClaro);  // ojos
        h.Box(new Vector3(0.03f, 0.01f, 0.056f), new Vector3(0.02f, 0.015f, 0.005f), FVerdeClaro);
        FairDraft(head, "Malla", h, f.paleta, true);

        Transform tail = Group(cat, "Cola");
        tail.localPosition = new Vector3(0f, 0.08f, -0.2f);
        var t = new MeshDraft();
        t.Box(new Vector3(0.06f, 0.0f, -0.1f), new Vector3(0.035f, 0.035f, 0.22f), FNegro);
        t.Box(new Vector3(0.11f, 0.03f, -0.2f), new Vector3(0.035f, 0.035f, 0.08f), FNegro);
        FairDraft(tail, "Malla", t, f.paleta, true);

        AudioSource voice = NewSource(cat, "Voz", null, false, 40);
        voice.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        Make3D(voice, 2f, 28f);
        cat.gameObject.AddComponent<LeadingCat>().Setup(player, head, tail, voice, LoadRealSounds("Gato"), path);
    }

    // ================================================================== ayudas de malla

    private static GameObject FairDraft(Transform parent, string name, MeshDraft d, Material mat, bool shadows)
    {
        if (d.v.Count == 0) return null;
        var mesh = new Mesh { name = parent.name + "_" + name };
        mesh.indexFormat = d.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        Apply(mesh, d.v, d.n, d.t, d.uv);
        GameObject go = MeshChild(parent, name, mesh, mat, Vector3.zero);
        go.GetComponent<MeshRenderer>().shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    /// <summary>Muchas copias de una malla (farolitos) juntas en una sola, con un material.</summary>
    private static void CombinedChild(Transform parent, string name, Mesh source, List<Matrix4x4> where, Material mat)
    {
        if (where.Count == 0 || source == null) return;
        var parts = new CombineInstance[where.Count];
        for (int i = 0; i < where.Count; i++) parts[i] = new CombineInstance { mesh = source, transform = where[i] };
        var mesh = new Mesh { name = parent.name + "_" + name };
        mesh.indexFormat = source.vertexCount * where.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.CombineMeshes(parts, true, true);
        GameObject go = MeshChild(parent, name, mesh, mat, Vector3.zero);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void FairTri(MeshDraft d, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int palette)
    {
        Vector2 uv = PaletteUV(palette);
        int first = d.v.Count;
        AddTriangle(d.v, d.n, d.t, a, b, c, outward);
        for (int i = first; i < d.v.Count; i++) { d.uv.Add(uv); d.c.Add(FairNoWindow); }
    }

    /// <summary>Una "bola" de 8 caras (octaedro): frutas, globos, copas de arboles. Muy PS1.</summary>
    private static void FairBall(MeshDraft d, Vector3 c, float r, int palette)
    {
        Vector3 up = c + Vector3.up * r, down = c + Vector3.down * r * 0.85f;
        Vector3[] ring = { c + Vector3.right * r, c + Vector3.forward * r, c + Vector3.left * r, c + Vector3.back * r };
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = ring[i], b = ring[(i + 1) % 4];
            FairTri(d, up, a, b, (up + a + b) / 3f - c, palette);
            FairTri(d, down, b, a, (down + a + b) / 3f - c, palette);
        }
    }

    private static Mesh BallMesh()
    {
        var d = new MeshDraft();
        FairBall(d, Vector3.zero, 1f, FBlanco);
        var mesh = new Mesh { name = "Bola" };
        Apply(mesh, d.v, d.n, d.t, d.uv);
        return mesh;
    }
}
