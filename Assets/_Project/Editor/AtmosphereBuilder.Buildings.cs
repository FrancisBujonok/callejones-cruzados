using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Parte de AtmosphereBuilder: los edificios (Paso 20). Kowloon como referencia: los puestos
/// quedan abajo y arriba hay casas, piso sobre piso, que se cierran sobre el pasillo.
/// Menu: Callejones > Edificios, techados y patio (tambien corre con "Construir atmosfera (todo)",
/// "Fachadas, carteles y lugares" y "Cielo nocturno, cables y carteles").
///
/// Que arma:
/// - En cada lado de los rectos, un edificio de 4, 7 o 10 pisos de 3 m (sorteado, DecorRandomizer).
///   La planta baja es la de siempre (puestos, persianas, noren). El primer piso va al ras de la
///   pared; desde el segundo los pisos sobresalen hacia el pasillo (0.35, 0.7 y 1 m), y arriba de
///   todo el cielo queda en una franja de 1 m.
/// - Cada piso tiene ventanas de casas (apagadas, luz calida, tele, altar) y detalles: rejas con
///   macetas, aires acondicionados, canas con ropa colgada, bordes de losa. En la azotea, parapeto,
///   tanque de agua y antena. Si el edificio es bajo, detras sigue otro mas oscuro hasta 30 m.
/// - Las piezas de 3 x 3 tienen edificios en sus paredes cerradas; el Cruce es el patio: sin
///   voladizos, con ropa colgada de lado a lado y un cartel alto.
/// - La Tapa (la persiana que cierra una conexion libre) tambien tiene edificio arriba.
///
/// Reglas que cuidan el truco del laberinto:
/// - Todo queda dentro de la propia celda (nunca pasa la mitad de la pared, x = 1.5): asi nunca
///   aparece dentro del pasillo vecino.
/// - Todo termina a 30 m (o en un fondo que llega a 30 m). Con la misma altura en todos lados,
///   desde un pasillo nunca se ve lo de arriba de otro, y una pieza nueva al lado no asoma.
/// - Los edificios son iguales en las dos variantes del recto (abierto y cerrado): el director
///   solo cambia la planta baja, asi VisibilityChecker no cambia.
/// - Sin colliders: no frenan al jugador ni tapan la linea de vision de VisibilityChecker.
/// - Cada lado es como mucho tres mallas (fachada, fondo y detalles) con tres materiales.
///
/// Espacio de lado: cada edificio se arma con la pared en x = 0 y el pasillo hacia +X, a lo largo
/// de z (centrado) y con el piso en y = 0, como las fachadas. Despues se gira a su pared.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string BuildingsTexDir = Root + "/Textures/Edificios";
    private const float FloorHeight = 3f;
    private const int MaxFloors = 10;   // 30 m: todo termina ahi
    private static readonly int[] FloorChoices = { 4, 7, 10 };

    // Indices de Paleta.png (Herramientas/generar_fachadas.py).
    private const int PalReja = 0, PalAire = 1, PalOxido = 2, PalCano = 3, PalRopaRoja = 4, PalRopaAzul = 5,
        PalRopaBlanca = 6, PalRopaAmarilla = 7, PalRopaRosa = 8, PalPlanta = 9, PalTanque = 10,
        PalConcreto = 11, PalChapa = 12, PalPlastico = 13, PalMadera = 14;
    private static readonly int[] ClothColors = { PalRopaRoja, PalRopaAzul, PalRopaBlanca, PalRopaAmarilla, PalRopaRosa };

    // Columnas del atlas Fachadas.png.
    private const int WinDark = 0, WinWarm = 1, WinTv = 2, WinAltar = 3;

    /// <summary>Cuanto sobresale cada piso hacia el pasillo. 0 = planta baja; el 1 va al ras.</summary>
    private static float Overhang(int floor) => floor <= 1 ? 0f : floor <= 4 ? 0.35f : floor <= 7 ? 0.7f : 1f;

    [MenuItem("Callejones/Edificios, techados y patio", priority = 28)]
    public static void BuildingsMenu()
    {
        if (!CanRun()) return;
        GameObject farol = AssetDatabase.LoadAssetAtPath<GameObject>(FarolPath);
        if (farol == null)
        {
            EditorUtility.DisplayDialog("Callejones", "Falta el prefab Farol. Ejecuta primero Callejones > Construir atmosfera (todo).", "OK");
            return;
        }
        Mats m = BuildMaterials();
        DecorateModule(m, farol);   // el recto, con sus edificios y el techado
        BuildMaze(m, farol);        // Esquina, Cruce (patio), Fondo y Tapa
        SetupNightLighting();       // el cielo y la niebla del color del cielo
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Edificios, techados y patio listos. Guarda con Ctrl+S.");
    }

    // ------------------------------------------------------------------ materiales

    private static void BuildBuildingMaterials(Mats m)
    {
        Texture2D atlas = LoadBuildingTexture("Fachadas");
        Texture2D light = LoadBuildingTexture("Fachadas_Luz");
        Texture2D palette = LoadBuildingTexture("Paleta");
        if (atlas == null || palette == null)
            Debug.LogWarning("[Callejones] Faltan las texturas de " + BuildingsTexDir + ". Corre Herramientas/generar_fachadas.py.");

        // Las ventanas prendidas brillan con su propio color (Fachadas_Luz es negro en el resto).
        m.fachada = Mat("Fachada", Color.white, 0.1f, 0f, null, Color.white, atlas, light);
        // El edificio de atras: el mismo atlas, mas oscuro y con menos brillo, parece mas lejos.
        m.fachadaFondo = Mat("FachadaFondo", new Color(0.5f, 0.5f, 0.56f), 0.05f, 0f, null, Color.white * 0.55f, atlas, light);
        // Detalles: cada cubo apunta a un color de la paleta. Un poco de brillo propio para que
        // se lean contra la fachada oscura.
        m.detalles = Mat("Detalles", Color.white, 0.2f, 0f, null, Color.white * 0.12f, palette, palette);
        m.tuboLuz = Mat("TuboFluorescente", Hex("#E6F5E0"), 0.3f, 0f, null, Hex("#E6F5E0") * 1.8f);
    }

    private static Texture2D LoadBuildingTexture(string name)
    {
        string path = BuildingsTexDir + "/" + name + ".png";
        if (!File.Exists(path)) return null;
        SetupSignImport(path); // pixeles duros, sin mipmaps ni compresion (como los carteles)
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ el recto

    /// <summary>Un edificio por lado, cada uno con sus tres alturas sorteadas.</summary>
    private static void BuildRectoBuildings(Transform deco, Mats m)
    {
        DestroyChild(deco, "Edificios");
        Transform group = Group(deco, "Edificios");
        BuildSideChoices(group, "Izq", new Vector3(-WallInnerX, 0f, 0f), 0f, "Recto_Izq", 6f, 1100, true, m);
        BuildSideChoices(group, "Der", new Vector3(WallInnerX, 0f, 0f), 180f, "Recto_Der", 6f, 2200, true, m);
    }

    /// <summary>Las tres alturas de un lado (4, 7 y 10 pisos) y su DecorRandomizer.</summary>
    private static void BuildSideChoices(Transform parent, string name, Vector3 wallPoint, float yaw, string meshPrefix,
        float length, int seed, bool overhangs, Mats m)
    {
        Transform side = Group(parent, name);
        side.localPosition = wallPoint;
        side.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var options = new List<GameObject>();
        foreach (int floors in FloorChoices)
        {
            Transform option = Group(side, "Pisos" + floors);
            BuildBuildingSide(option, meshPrefix + "_P" + floors, length, floors, seed + floors, overhangs, m);
            options.Add(option.gameObject);
        }
        for (int i = 1; i < options.Count; i++) options[i].SetActive(false); // en el editor, el bajo
        side.gameObject.AddComponent<DecorRandomizer>().Setup(options.ToArray());
    }

    // ------------------------------------------------------------------ piezas de 3 x 3 y tapa

    private static void BuildCellBuildings(Transform deco, SegmentKind kind, Side open, Mats m)
    {
        Transform group = Group(deco, "Edificios");
        Side closed = ~open & (Side.S | Side.N | Side.E | Side.O);
        foreach (Side s in SideOrder)
        {
            if ((closed & s) == 0) continue;
            Vector3 wallPoint = SideCenter(s) * (WallInnerX / 1.5f);
            float yaw = s == Side.N ? 90f : s == Side.S ? -90f : s == Side.E ? 180f : 0f;
            if (kind == SegmentKind.Cruce)
            {
                // El patio: diez pisos al ras, sin voladizos, asi el cielo se abre en un cuadrado.
                Transform side = Group(group, "Patio" + s);
                side.localPosition = wallPoint;
                side.localRotation = Quaternion.Euler(0f, yaw, 0f);
                BuildBuildingSide(side, "Patio_" + s, 3f, MaxFloors, 3300, false, m);
                continue;
            }
            // Los lados de las piezas comparten mallas (dos versiones, A y B, segun el lado).
            bool versionA = s == Side.N || s == Side.E;
            BuildSideChoices(group, "Lado" + s, wallPoint, yaw, versionA ? "Celda_A" : "Celda_B", 3f, versionA ? 4400 : 5500, true, m);
        }
        if (kind == SegmentKind.Cruce) BuildPatioExtras(group, m);
    }

    /// <summary>Arriba de la Tapa: un edificio de diez pisos, para que el pasillo no termine en cielo.</summary>
    private static void BuildCapBuilding(Transform root, Mats m)
    {
        Transform side = Group(root, "Edificio");
        side.localPosition = new Vector3(0f, 0f, -0.05f);
        side.localRotation = Quaternion.Euler(0f, 90f, 0f); // la fachada mira hacia -Z, como la persiana
        BuildBuildingSide(side, "Tapa", 3f, MaxFloors, 6600, false, m);
    }

    /// <summary>Columna de las esquinas abiertas, de 3 a 30 m.</summary>
    private static void TallColumn(Transform parent, Vector3 foot, Mats m)
    {
        float h = FloorHeight * MaxFloors - 3f;
        MeshChild(parent, "ColumnaAlta", GetBoxMesh(new Vector3(0.3f, h, 0.3f)), m.pared, foot + Vector3.up * (3f + h * 0.5f));
    }

    /// <summary>
    /// El patio (Cruce): ropa colgada de lado a lado a distintas alturas, entre las dos columnas y la
    /// fachada del fondo, y un cartel grande en lo alto de la fachada.
    /// </summary>
    private static void BuildPatioExtras(Transform group, Mats m)
    {
        Transform patio = Group(group, "PatioExtras");
        var cloth = new MeshDraft();
        var rng = new System.Random(7700);
        var lines = new List<Span>();
        // Bajas: el patio mide 3 x 3 m y todo lo que esta muy arriba queda casi en la vertical.
        // Asi se ven al llegar desde el pasillo de entrada.
        float[] heights = { 5.2f, 7.4f, 10.5f, 14.5f };
        for (int i = 0; i < heights.Length; i++)
        {
            float y = heights[i];
            // Alterna: de columna a columna (sobre la entrada) y de una columna a la fachada del fondo.
            Vector3 a, b;
            if (i % 2 == 0) { a = new Vector3(-1.35f, y, -1.35f); b = new Vector3(1.35f, y + 0.3f, -1.35f); }
            else { a = new Vector3(1.35f, y, -1.35f); b = new Vector3(-1.2f, y - 0.4f, 1.38f); }
            lines.Add(new Span(a, b, 0.25f, 0.02f));
            int count = 3 + rng.Next(3);
            for (int k = 0; k < count; k++)
            {
                float t = (k + 0.7f) / (count + 0.4f);
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (0.25f * 4f * t * (1f - t));
                float w = 0.3f + 0.25f * (float)rng.NextDouble(), h = 0.45f + 0.35f * (float)rng.NextDouble();
                Vector3 along = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                cloth.Panel(p + Vector3.down * (h * 0.5f), along, w, h, ClothColors[rng.Next(ClothColors.Length)]);
            }
        }
        CablesChild(patio, "Patio_Sogas", lines, m);
        DraftChild(patio, "Ropa", "Edif_Patio_Ropa", cloth, m.detalles);

        // Cartel grande colgado delante de la fachada del fondo, de cara a la entrada (S): de 6.5
        // a 9.5 m, se lee desde el pasillo al acercarse. Es el mismo cartel saliente de los
        // pasillos, girado para quedar paralelo a la fachada, a 15 cm (tapa media ventana, como
        // en Hong Kong).
        Transform holder = Group(patio, "CartelAlto");
        holder.localPosition = new Vector3(0.35f, 8f, WallInnerX - 0.15f);
        holder.localRotation = Quaternion.identity; // +X local hacia el E: las caras miran al N y al S
        CreateProjectingSign(holder, "Cartel", Vector3.zero, false, new Vector2(0.8f, 3.03f), m);
    }

    // ------------------------------------------------------------------ telones de niebla

    private const string CurtainShaderName = "Callejones/Telon de niebla";
    private const float CurtainHeight = 31f; // hasta arriba de los parapetos

    /// <summary>
    /// Un telon de niebla en cada conexion: un plano de 3 x 31 m, 2 cm hacia adentro de la pieza,
    /// que mira hacia adentro. Pinta el color de la niebla (TelonNiebla.shader). CorridorTreadmill
    /// lo deja prendido solo mientras la conexion no tiene vecina: asi el fondo de un pasillo
    /// largo se ve como niebla, no como cielo, y la pieza que aparece despues no se nota.
    /// Sin collider: no frena ni tapa la linea de vision de VisibilityChecker.
    /// </summary>
    private static void AddCurtains(Transform[] sockets)
    {
        Material mat = GetCurtainMaterial();
        if (mat == null) return;
        Mesh mesh = GetOrCreateMesh("Telon");
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        AddQuad(v, n, t, new Vector3(-1.5f, 0f, 0f), new Vector3(1.5f, 0f, 0f),
            new Vector3(1.5f, CurtainHeight, 0f), new Vector3(-1.5f, CurtainHeight, 0f), Vector3.back);
        Apply(mesh, v, n, t);
        foreach (Transform socket in sockets)
        {
            DestroyChild(socket, "Telon");
            GameObject go = MeshChild(socket, "Telon", mesh, mat, new Vector3(0f, 0f, -0.02f));
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    private static Material GetCurtainMaterial()
    {
        Shader shader = Shader.Find(CurtainShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[Callejones] No se encontro el shader " + CurtainShaderName + ": las piezas quedan sin telon de niebla.");
            return null;
        }
        string path = MaterialsDir + "/TelonNiebla.mat";
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
        return m;
    }

    // ------------------------------------------------------------------ el tramo techado

    /// <summary>
    /// Techo de chapas a 3.95 m (justo debajo de las ventanas del primer piso), con un panel de
    /// plastico, uno que falta, canos, dos tubos fluorescentes y su luz fria.
    /// </summary>
    private static void BuildCoveredRoof(Transform roof, Mats m)
    {
        var d = new MeshDraft();
        const float y = 3.95f;
        // Vigas de lado a lado y un larguero.
        foreach (float z in new[] { -2.9f, -1.0f, 1.0f, 2.9f })
            d.Box(new Vector3(0f, y - 0.06f, z), new Vector3(2.76f, 0.08f, 0.08f), PalOxido);
        d.Box(new Vector3(0f, y - 0.05f, 0f), new Vector3(0.06f, 0.06f, 6f), PalOxido);
        // Paneles: chapa, chapa con un hueco, plastico, chapa.
        float[] z0 = { -3f, -1.5f, 0f, 1.5f };
        for (int i = 0; i < 4; i++)
        {
            float zc = z0[i] + 0.75f;
            float tilt = (i % 2 == 0 ? 1f : -1f) * 0.04f;
            if (i == 1)
            {
                // Falta media chapa: por ahi se ven los pisos de arriba.
                d.Box(new Vector3(-0.7f, y + tilt, zc), new Vector3(1.4f, 0.03f, 1.48f), PalChapa);
                continue;
            }
            int color = i == 2 ? PalPlastico : PalChapa;
            d.Box(new Vector3(0f, y + tilt, zc), new Vector3(2.76f, 0.03f, 1.48f), color);
            if (color == PalChapa)
                for (int r = 0; r < 7; r++) // nervios de la chapa acanalada
                    d.Box(new Vector3(-1.2f + r * 0.4f, y + tilt - 0.025f, zc), new Vector3(0.04f, 0.03f, 1.48f), PalChapa);
        }
        // Canos colgando bajo el techo.
        d.Box(new Vector3(-1.05f, y - 0.2f, 0f), new Vector3(0.08f, 0.08f, 6f), PalCano);
        d.Box(new Vector3(0.9f, y - 0.25f, 0f), new Vector3(0.05f, 0.05f, 6f), PalCano);
        DraftChild(roof, "Techo", "Edif_Techado", d, m.detalles);

        // Tubos fluorescentes (brillan) con sus alambres, y la luz fria que tiran.
        foreach (float z in new[] { -1.2f, 1.3f })
        {
            BoxChild(roof, "Tubo", m.tuboLuz, new Vector3(0.1f, y - 0.42f, z), new Vector3(0.05f, 0.05f, 1.2f), Vector3.zero);
            BoxChild(roof, "Alambre", m.cable, new Vector3(0.1f, y - 0.22f, z - 0.5f), new Vector3(0.01f, 0.38f, 0.01f), Vector3.zero);
            BoxChild(roof, "Alambre", m.cable, new Vector3(0.1f, y - 0.22f, z + 0.5f), new Vector3(0.01f, 0.38f, 0.01f), Vector3.zero);
        }
        PointLight(roof, "LuzTubos", new Vector3(0.1f, y - 0.6f, 0f), new Color(0.75f, 1f, 0.85f), 5f, 0.9f);
    }

    // ------------------------------------------------------------------ un lado de edificio

    /// <summary>
    /// Arma un edificio en espacio de lado y lo cuelga de 'parent' como hasta tres mallas.
    /// floors cuenta la planta baja (que ya existe): se arman los pisos 1 a floors - 1.
    /// </summary>
    private static void BuildBuildingSide(Transform parent, string meshName, float length, int floors, int seed, bool overhangs, Mats m)
    {
        var rng = new System.Random(seed);
        var front = new MeshDraft();
        var back = new MeshDraft();
        var detail = new MeshDraft();
        int style = rng.Next(4);
        int bays = Mathf.Max(1, Mathf.RoundToInt(length / 3f));
        float half = length * 0.5f;
        float Off(int k) => overhangs ? Overhang(k) : 0f;

        // 1. Pisos: un tramo de fachada con ventana por cada 3 m, borde de losa y detalles.
        for (int k = 1; k < floors; k++)
        {
            float u = Off(k), y0 = k * FloorHeight;
            for (int j = 0; j < bays; j++)
            {
                float z0 = -half + j * 3f;
                front.FacadeQuad(u, z0, z0 + 3f, y0, y0 + FloorHeight, AtlasCell(style, PickWindow(rng, false)));
                BayDetails(detail, rng, u, y0, z0 + 1.5f, k);
            }
            detail.Box(new Vector3(u + 0.05f, y0 + 0.06f, 0f), new Vector3(0.1f, 0.12f, length), PalConcreto);
        }

        // 2. Los voladizos: por cada grupo de pisos que sobresale igual, su techo de abajo y sus
        //    tapas a los costados (se ven cuando la pieza vecina sobresale menos).
        Vector2 wall = WallUV(style);
        int a = 2;
        while (a < floors)
        {
            float u = Off(a);
            int b = a;
            while (b + 1 < floors && Mathf.Approximately(Off(b + 1), u)) b++;
            if (u > 0f)
            {
                float y0 = a * FloorHeight, y1 = (b + 1) * FloorHeight;
                front.Quad(new Vector3(0f, y0, -half), new Vector3(u, y0, -half), new Vector3(u, y0, half), new Vector3(0f, y0, half), Vector3.down, wall);
                front.Quad(new Vector3(0f, y0, -half), new Vector3(u, y0, -half), new Vector3(u, y1, -half), new Vector3(0f, y1, -half), Vector3.back, wall);
                front.Quad(new Vector3(0f, y0, half), new Vector3(u, y0, half), new Vector3(u, y1, half), new Vector3(0f, y1, half), Vector3.forward, wall);
            }
            a = b + 1;
        }

        // 3. La azotea: techo, parapeto, tanque de agua y antena.
        float top = floors * FloorHeight, uTop = Off(floors - 1);
        if (uTop > 0f)
            front.Quad(new Vector3(0f, top, -half), new Vector3(uTop, top, -half), new Vector3(uTop, top, half), new Vector3(0f, top, half), Vector3.up, wall);
        detail.Box(new Vector3(uTop - 0.07f, top + 0.45f, 0f), new Vector3(0.14f, 0.9f, length), PalConcreto);
        // El tanque va entre el parapeto y el fondo (x = -0.08): nada pasa de la mitad de la
        // pared, asi que en una azotea angosta el tanque es chico, y si no entra, no va.
        float tankRadius = Mathf.Min(0.42f, (uTop - 0.08f) * 0.5f - 0.01f);
        if (tankRadius >= 0.12f && rng.NextDouble() < 0.6)
        {
            float zt = -half + 0.7f + (float)rng.NextDouble() * (length - 1.4f);
            detail.Cylinder(new Vector3(-0.07f + tankRadius, top, zt), tankRadius, 1.1f, 8, PalTanque);
        }
        if (rng.NextDouble() < 0.5)
        {
            // Antena atada al parapeto.
            float za = -half + 0.4f + (float)rng.NextDouble() * (length - 0.8f);
            detail.Box(new Vector3(uTop - 0.07f, top + 1.2f, za), new Vector3(0.04f, 2.4f, 0.04f), PalCano);
            detail.Box(new Vector3(uTop - 0.07f, top + 2.1f, za), new Vector3(0.03f, 0.03f, 0.9f), PalCano);
        }

        // 4. Si el edificio es bajo, detras sigue otro hasta 30 m: mas oscuro, casi sin luces.
        for (int k = floors; k < MaxFloors; k++)
        {
            float y0 = k * FloorHeight;
            for (int j = 0; j < bays; j++)
            {
                float z0 = -half + j * 3f;
                back.FacadeQuad(-0.08f, z0, z0 + 3f, y0, y0 + FloorHeight, AtlasCell((style + 1) % 4, PickWindow(rng, true)));
            }
        }

        string prefix = "Edif_" + meshName;
        DraftChild(parent, "Fachada", prefix + "_Fachada", front, m.fachada);
        DraftChild(parent, "Fondo", prefix + "_Fondo", back, m.fachadaFondo);
        DraftChild(parent, "Detalles", prefix + "_Detalles", detail, m.detalles);
    }

    /// <summary>
    /// Lo que hay en la ventana de un piso: reja con macetas (y a veces ropa adentro), aire
    /// acondicionado o una cana con ropa colgada. En el primer piso solo ropa (mas abajo estan
    /// los carteles, los cables y el techado).
    /// </summary>
    private static void BayDetails(MeshDraft d, System.Random rng, float u, float y0, float zc, int floor)
    {
        double r = rng.NextDouble();
        if (floor == 1) r = 0.5 + r * 0.5; // primer piso: ropa o nada
        // Arriba de todo (1 m de voladizo) quedan 80 cm hasta el edificio de enfrente: ahi no
        // entran rejas ni aires. Macetas en el alfeizar o ropa en una cana corta.
        if (u > 0.9f && r < 0.5)
        {
            int pots = 1 + rng.Next(3);
            for (int i = 0; i < pots; i++)
            {
                float z = zc - 0.6f + (float)rng.NextDouble() * 1.2f;
                d.Box(new Vector3(u + 0.08f, y0 + 1.11f, z), new Vector3(0.14f, 0.14f, 0.14f), PalOxido);
                d.Box(new Vector3(u + 0.08f, y0 + 1.25f, z), new Vector3(0.18f, 0.14f, 0.18f), PalPlanta);
            }
            return;
        }

        if (r < 0.28)
        {
            // Reja de hierro (tie chuang): piso, techito, barrotes al frente y macetas.
            d.Box(new Vector3(u + 0.18f, y0 + 0.96f, zc), new Vector3(0.36f, 0.05f, 1.8f), PalReja);
            d.Box(new Vector3(u + 0.2f, y0 + 2.5f, zc), new Vector3(0.42f, 0.04f, 1.9f), rng.NextDouble() < 0.5 ? PalOxido : PalChapa);
            for (int i = 0; i <= 6; i++)
                d.Box(new Vector3(u + 0.35f, y0 + 1.72f, zc - 0.87f + i * 0.29f), new Vector3(0.02f, 1.5f, 0.02f), PalReja);
            d.Box(new Vector3(u + 0.35f, y0 + 1.4f, zc), new Vector3(0.02f, 0.02f, 1.78f), PalReja);
            d.Box(new Vector3(u + 0.35f, y0 + 2.05f, zc), new Vector3(0.02f, 0.02f, 1.78f), PalReja);
            int pots = 1 + rng.Next(3);
            for (int i = 0; i < pots; i++)
            {
                float z = zc - 0.6f + (float)rng.NextDouble() * 1.2f;
                d.Box(new Vector3(u + 0.2f, y0 + 1.07f, z), new Vector3(0.16f, 0.16f, 0.16f), PalOxido);
                d.Box(new Vector3(u + 0.2f, y0 + 1.22f, z), new Vector3(0.22f, 0.16f, 0.22f), PalPlanta);
            }
            if (rng.NextDouble() < 0.5)
                d.Box(new Vector3(u + 0.24f, y0 + 2.12f, zc + 0.35f), new Vector3(0.02f, 0.55f, 0.4f), ClothColors[rng.Next(ClothColors.Length)]);
        }
        else if (r < 0.5)
        {
            // Aire acondicionado bajo la ventana, sobre dos mensulas.
            float z = zc + (rng.NextDouble() < 0.5 ? -0.55f : 0.55f);
            d.Box(new Vector3(u + 0.22f, y0 + 0.62f, z), new Vector3(0.44f, 0.46f, 0.72f), PalAire);
            d.Box(new Vector3(u + 0.45f, y0 + 0.62f, z), new Vector3(0.02f, 0.3f, 0.5f), PalCano); // rejilla
            d.Box(new Vector3(u + 0.2f, y0 + 0.37f, z - 0.25f), new Vector3(0.4f, 0.03f, 0.03f), PalReja);
            d.Box(new Vector3(u + 0.2f, y0 + 0.37f, z + 0.25f), new Vector3(0.4f, 0.03f, 0.03f), PalReja);
        }
        else if (r < 0.75)
        {
            // Cana de ropa: sale de la ventana hacia el pasillo, sin pasar la mitad (x = 1.4).
            float length = Mathf.Min(1.1f, 1.3f - u);
            if (length < 0.3f) return;
            float z = zc + (rng.NextDouble() < 0.5 ? -0.35f : 0.35f);
            float y = y0 + 2.35f;
            d.Box(new Vector3(u + length * 0.5f, y, z), new Vector3(length, 0.03f, 0.03f), PalMadera);
            int count = Mathf.Min(1 + rng.Next(3), Mathf.Max(1, (int)(length / 0.33f)));
            float slot = (length - 0.1f) / count;
            for (int i = 0; i < count; i++)
            {
                float w = Mathf.Min(0.22f + 0.18f * (float)rng.NextDouble(), slot - 0.03f);
                float h = 0.35f + 0.35f * (float)rng.NextDouble();
                float x = u + 0.08f + (i + 0.5f) * slot;
                d.Box(new Vector3(x, y - h * 0.5f - 0.02f, z), new Vector3(w, h, 0.02f), ClothColors[rng.Next(ClothColors.Length)]);
            }
        }
    }

    private static int PickWindow(System.Random rng, bool far)
    {
        double r = rng.NextDouble();
        if (far) return r < 0.82 ? WinDark : r < 0.92 ? WinWarm : r < 0.97 ? WinTv : WinAltar;
        return r < 0.5 ? WinDark : r < 0.77 ? WinWarm : r < 0.9 ? WinTv : WinAltar;
    }

    /// <summary>Rectangulo UV de una celda del atlas (fila = pared, columna = ventana).</summary>
    private static Rect AtlasCell(int row, int col)
    {
        const float inset = 0.25f / 128f;
        return new Rect(col * 0.25f + inset, 1f - (row + 1) * 0.25f + inset, 0.25f - 2f * inset, 0.25f - 2f * inset);
    }

    /// <summary>
    /// Un pixel de pared lisa: arriba a la izquierda de la primera celda de la fila, donde no hay
    /// ventana ni junta de azulejos (pixel 5, 1).
    /// </summary>
    private static Vector2 WallUV(int row) => new Vector2(5.5f / 128f, 1f - (row * 32f + 1.5f) / 128f);

    private static Vector2 PaletteUV(int index) =>
        new Vector2(((index % 4) * 4 + 2) / 16f, 1f - ((index / 4) * 4 + 2) / 16f);

    private static void DraftChild(Transform parent, string objectName, string meshName, MeshDraft draft, Material mat)
    {
        if (draft.v.Count == 0) return;
        Mesh mesh = GetOrCreateMesh(meshName);
        mesh.indexFormat = draft.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        Apply(mesh, draft.v, draft.n, draft.t, draft.uv);
        GameObject go = MeshChild(parent, objectName, mesh, mat, Vector3.zero);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------ armado de mallas

    /// <summary>Una malla en construccion: caras planas (sombreado plano), con UV.</summary>
    private class MeshDraft
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        /// <summary>Cuadrado de un solo color (todas las esquinas con el mismo UV).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector2 color)
        {
            AddQuad(v, n, t, a, b, c, d, outward);
            for (int i = 0; i < 4; i++) uv.Add(color);
        }

        /// <summary>
        /// Tramo de fachada mirando hacia +X, en x = u. Visto desde el pasillo, z crece hacia la
        /// derecha, asi la textura no queda espejada.
        /// </summary>
        public void FacadeQuad(float u, float z0, float z1, float y0, float y1, Rect cell)
        {
            AddQuad(v, n, t, new Vector3(u, y0, z0), new Vector3(u, y0, z1), new Vector3(u, y1, z1), new Vector3(u, y1, z0), Vector3.right);
            uv.Add(new Vector2(cell.xMin, cell.yMin));
            uv.Add(new Vector2(cell.xMax, cell.yMin));
            uv.Add(new Vector2(cell.xMax, cell.yMax));
            uv.Add(new Vector2(cell.xMin, cell.yMax));
        }

        /// <summary>Cubo alineado a los ejes, de un color de la paleta.</summary>
        public void Box(Vector3 center, Vector3 size, int palette)
        {
            Vector2 c = PaletteUV(palette);
            Vector3 h = size * 0.5f;
            Vector3 p(float x, float y, float z) => center + new Vector3(x * h.x, y * h.y, z * h.z);
            Quad(p(1, -1, -1), p(1, -1, 1), p(1, 1, 1), p(1, 1, -1), Vector3.right, c);
            Quad(p(-1, -1, -1), p(-1, -1, 1), p(-1, 1, 1), p(-1, 1, -1), Vector3.left, c);
            Quad(p(-1, 1, -1), p(1, 1, -1), p(1, 1, 1), p(-1, 1, 1), Vector3.up, c);
            Quad(p(-1, -1, -1), p(1, -1, -1), p(1, -1, 1), p(-1, -1, 1), Vector3.down, c);
            Quad(p(-1, -1, 1), p(1, -1, 1), p(1, 1, 1), p(-1, 1, 1), Vector3.forward, c);
            Quad(p(-1, -1, -1), p(1, -1, -1), p(1, 1, -1), p(-1, 1, -1), Vector3.back, c);
        }

        /// <summary>Panel fino (ropa colgada) de ancho w a lo largo de 'along', con dos caras.</summary>
        public void Panel(Vector3 center, Vector3 along, float w, float h, int palette)
        {
            Vector2 c = PaletteUV(palette);
            Vector3 side = along * (w * 0.5f), up = Vector3.up * (h * 0.5f);
            Vector3 normal = Vector3.Cross(along, Vector3.up).normalized;
            Vector3 a = center - side - up, b = center + side - up, cc = center + side + up, d = center - side + up;
            Quad(a, b, cc, d, normal, c);
            Quad(a, b, cc, d, -normal, c);
        }

        /// <summary>Cilindro parado (tanque de agua) con tapa arriba.</summary>
        public void Cylinder(Vector3 foot, float radius, float height, int sides, int palette)
        {
            Vector2 c = PaletteUV(palette);
            Vector3 top = foot + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Quad(foot + d0, foot + d1, top + d1, top + d0, (d0 + d1).normalized, c);
                int first = v.Count;
                AddTriangle(v, n, t, top, top + d0, top + d1, Vector3.up);
                for (int k = first; k < v.Count; k++) uv.Add(c);
            }
        }
    }
}
