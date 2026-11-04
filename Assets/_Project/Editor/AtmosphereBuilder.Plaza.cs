using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: la plaza (Paso 23).
/// Menu: Callejones > Plaza (tambien corre con "Laberinto con giros" y "Construir atmosfera (todo)").
///
/// Una pieza de 9 x 9 m (3 x 3 celdas) con cuatro bocas de 3 m, una en el medio de cada lado:
/// S (por donde se entra), E, O y N. Convencion como las piezas de 3 x 3: centro en (0, 0, 0).
///
/// Que tiene:
/// - Paredes de 3 m entre las bocas y, arriba, ocho edificios de 4, 7 y 10 pisos sin voladizos
///   (como el patio): el cielo se abre en un cuadrado grande.
/// - En el centro, un cantero con un arbol viejo atado con una soga de paja y papeles (shimenawa
///   y shide, como los arboles sagrados de los santuarios) y, delante, un altar chico con velas e
///   incienso. El altar cuelga de un pivote en el centro: Plaza lo gira para la trampa.
/// - Un puesto de fideos cerrado (en uno de tres lugares, DecorRandomizer), una expendedora,
///   dos bancos, macetas, dos guirnaldas de farolitos y dos faroles colgados.
/// - En cada boca, una Tapa apagada (persiana con su pared y un edificio arriba): Plaza la prende
///   para cerrar el callejon por el que llego el jugador.
/// - CorridorSegment (tipo Plaza, 9 celdas) y Plaza (respiro, ojeadas y trampa).
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string PlazaPath = Root + "/Prefabs/Plaza.prefab";
    private const int PoolPlaza = 2;          // la plaza y su posible copia (la plaza repetida)
    private const float PlazaHalf = 4.5f;
    private const float PlazaInner = 4.4f;    // cara interior de sus paredes

    // Perfiles (radio, altura), de arriba hacia abajo.
    private static readonly Vector2[] TrunkProfile =
    {
        new Vector2(0.14f, 4.3f), new Vector2(0.2f, 2.4f), new Vector2(0.27f, 0.5f), new Vector2(0.42f, 0f),
    };
    private static readonly Vector2[] CanopyProfile =
    {
        new Vector2(0.05f, 1.5f), new Vector2(0.9f, 1.35f), new Vector2(1.5f, 0.85f),
        new Vector2(1.6f, 0.45f), new Vector2(1.2f, 0.05f), new Vector2(0.3f, -0.1f),
    };
    private static readonly Vector2[] RopeRingProfile = { new Vector2(0.29f, 0.07f), new Vector2(0.29f, -0.07f) };

    // Lados: hacia afuera y a lo largo (para las dos paredes de cada lado).
    private static readonly string[] PlazaSideNames = { "S", "N", "E", "O" };
    private static readonly Vector3[] PlazaSideOut = { Vector3.back, Vector3.forward, Vector3.right, Vector3.left };
    private static readonly Vector3[] PlazaSideAlong = { Vector3.right, Vector3.right, Vector3.forward, Vector3.forward };
    private static readonly float[] PlazaSideYaw = { -90f, 90f, 180f, 0f }; // como BuildCellBuildings
    // Pisos de los ocho edificios (lado S, N, E, O; dos por lado). Mezclados para que el cielo
    // quede con escalones; los del fondo (N) mas altos.
    private static readonly int[] PlazaFloors = { 10, 4, 10, 7, 4, 7, 7, 4 };

    [MenuItem("Callejones/Plaza", priority = 31)]
    public static void PlazaMenu()
    {
        if (!CanRun()) return;
        GameObject farol = AssetDatabase.LoadAssetAtPath<GameObject>(FarolPath);
        GameObject tapa = AssetDatabase.LoadAssetAtPath<GameObject>(TapaPath);
        if (farol == null || tapa == null)
        {
            EditorUtility.DisplayDialog("Callejones", "Faltan los prefabs Farol o Tapa. Ejecuta primero Callejones > Laberinto con giros.", "OK");
            return;
        }
        Mats m = BuildMaterials();
        GameObject plaza = BuildPlazaPrefab(m, farol, tapa);
        FillPool(AssetDatabase.LoadAssetAtPath<GameObject>(EsquinaPath), AssetDatabase.LoadAssetAtPath<GameObject>(CrucePath),
            AssetDatabase.LoadAssetAtPath<GameObject>(FondoPath), tapa, plaza);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Callejones] Plaza lista. Guarda con Ctrl+S.");
    }

    private static GameObject BuildPlazaPrefab(Mats m, GameObject farol, GameObject tapa)
    {
        Mesh shutter = GetOrCreateMesh("Persiana");
        var root = new GameObject("Plaza");
        try
        {
            // 1. Piso de 9 x 9 (UV en metros, como el resto).
            var floor = new GameObject("Piso");
            floor.transform.SetParent(root.transform, false);
            Mesh floorMesh = GetFloorMesh(new Vector3(9f, 0f, 9f));
            floor.AddComponent<MeshFilter>().sharedMesh = floorMesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = m.piso;
            floor.AddComponent<MeshCollider>().sharedMesh = floorMesh;

            // 2. Paredes entre las bocas y columnas en los cantos de cada boca.
            Transform structure = Group(root.transform, "Estructura");
            for (int s = 0; s < 4; s++)
            {
                Vector3 o = PlazaSideOut[s], a = PlazaSideAlong[s];
                Vector3 size = Abs(a) * 3.2f + Abs(o) * 0.2f + Vector3.up * 3f;
                foreach (int k in new[] { -1, 1 })
                {
                    MetricWall(structure, "Pared" + PlazaSideNames[s], m.pared, o * PlazaHalf + a * (3f * k) + Vector3.up * 1.5f, size);
                    Vector3 edge = o * PlazaHalf + a * (1.5f * k);
                    MetricWall(structure, "Columna", m.pared, edge + Vector3.up * 1.5f, new Vector3(0.3f, 3f, 0.3f));
                    TallColumn(structure, edge, m);
                }
            }

            Transform deco = Group(root.transform, "Decorado");

            // 3. Edificios: dos por lado, cada uno sobre su pared de 3 m.
            Transform buildings = Group(deco, "Edificios");
            for (int s = 0; s < 4; s++)
                for (int k = 0; k < 2; k++)
                {
                    int i = s * 2 + k;
                    Transform side = Group(buildings, "Lado" + PlazaSideNames[s] + k);
                    side.localPosition = PlazaSideOut[s] * PlazaInner + PlazaSideAlong[s] * (k == 0 ? -3f : 3f);
                    side.localRotation = Quaternion.Euler(0f, PlazaSideYaw[s], 0f);
                    BuildBuildingSide(side, "Plaza_" + PlazaSideNames[s] + k, 3f, PlazaFloors[i], 8100 + i * 17, false, m);
                    AddWindowSeedPick(side);
                }

            // 4. El arbol en su cantero, y el altar.
            Transform altarPivot, altar;
            BuildPlazaTree(deco, m);
            BuildPlazaAltar(deco, m, out altarPivot, out altar);

            // 5. Lo de alrededor.
            BuildPlazaStalls(deco, m, shutter);
            BuildPlazaFurniture(deco, m);
            BuildPlazaLights(deco, m, farol);

            // 6. Bocas: S primero (por donde se entra), despues E, O y N; con su telon de niebla.
            var sockets = new[]
            {
                Connection(root.transform, "Conexion_S", new Vector3(0f, 0f, -PlazaHalf), 180f),
                Connection(root.transform, "Conexion_E", new Vector3(PlazaHalf, 0f, 0f), 90f),
                Connection(root.transform, "Conexion_O", new Vector3(-PlazaHalf, 0f, 0f), -90f),
                Connection(root.transform, "Conexion_N", new Vector3(0f, 0f, PlazaHalf), 0f),
            };
            AddCurtains(sockets);

            // 7. Una Tapa apagada en cada boca, donde la pondria el generador (25 cm hacia adentro).
            var closures = new GameObject[sockets.Length];
            for (int i = 0; i < sockets.Length; i++)
            {
                var cap = (GameObject)PrefabUtility.InstantiatePrefab(tapa, root.transform);
                cap.name = "Cierre_" + sockets[i].name.Substring("Conexion_".Length);
                cap.transform.localPosition = sockets[i].localPosition - sockets[i].localRotation * Vector3.forward * 0.25f;
                cap.transform.localRotation = sockets[i].localRotation;
                cap.SetActive(false);
                closures[i] = cap;
            }

            var segment = root.AddComponent<CorridorSegment>();
            segment.Setup(SegmentKind.Plaza, sockets, new[] { structure.gameObject }, -1, new Vector3(9f, 3f, 9f));
            // La caja da las tres celdas del medio (x = 0); las otras seis van aparte.
            segment.SetExtraCells(new[]
            {
                new Vector3(-3f, 0f, -3f), new Vector3(-3f, 0f, 0f), new Vector3(-3f, 0f, 3f),
                new Vector3(3f, 0f, -3f), new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 3f),
            });
            segment.SetClosures(closures);
            root.AddComponent<Plaza>().Setup(altarPivot, altar);

            return PrefabUtility.SaveAsPrefabAsset(root, PlazaPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // ------------------------------------------------------------------ el arbol y el altar

    private static void BuildPlazaTree(Transform deco, Mats m)
    {
        Transform tree = Group(deco, "Arbol");
        SolidBoxChild(tree, "Cantero", m.piedra, new Vector3(0f, 0.22f, 0f), new Vector3(2.4f, 0.44f, 2.4f), Vector3.zero);

        Mesh trunk = GetOrCreateMesh("Plaza_Tronco"); BuildLathe(trunk, TrunkProfile, 7);
        Mesh canopy = GetOrCreateMesh("Plaza_Copa"); BuildLathe(canopy, CanopyProfile, 7);
        Mesh ring = GetOrCreateMesh("Plaza_Soga"); BuildLathe(ring, RopeRingProfile, 8);

        MeshChild(tree, "Tronco", trunk, m.madera, new Vector3(0f, 0.44f, 0f));
        // Copa chica y alta: que no tape el cielo, que es lo que abre la plaza.
        GameObject c0 = MeshChild(tree, "Copa", canopy, m.planta, new Vector3(0f, 4.6f, 0f));
        c0.transform.localScale = Vector3.one * 0.8f;
        GameObject c1 = MeshChild(tree, "Copa", canopy, m.planta, new Vector3(0.75f, 4.2f, 0.4f));
        c1.transform.localScale = Vector3.one * 0.55f;
        c1.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
        GameObject c2 = MeshChild(tree, "Copa", canopy, m.planta, new Vector3(-0.6f, 4.95f, -0.45f));
        c2.transform.localScale = Vector3.one * 0.5f;
        c2.transform.localRotation = Quaternion.Euler(0f, 70f, 0f);
        foreach (GameObject c in new[] { c0, c1, c2 }) c.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        // Shimenawa: la soga alrededor del tronco, con cuatro tiras de papel.
        MeshChild(tree, "Soga", ring, m.soga, new Vector3(0f, 2.05f, 0f));
        for (int i = 0; i < 4; i++)
        {
            float yaw = 45f + 90f * i;
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            BoxChild(tree, "Shide", m.papel, dir * 0.31f + Vector3.up * 1.88f, new Vector3(0.08f, 0.2f, 0.01f), new Vector3(0f, yaw, 0f));
        }
    }

    /// <summary>
    /// El altar mira hacia -Z (hacia la boca S, por donde se entra) y cuelga de un pivote en el
    /// centro de la plaza: girando el pivote, aparece en otro lado del cantero.
    /// </summary>
    private static void BuildPlazaAltar(Transform deco, Mats m, out Transform pivot, out Transform altar)
    {
        pivot = Group(deco, "AltarGiro");
        altar = Group(pivot, "Altar");
        altar.localPosition = new Vector3(0f, 0f, -1.56f);

        SolidBoxChild(altar, "Base", m.piedra, new Vector3(0f, 0.3f, 0f), new Vector3(0.75f, 0.6f, 0.45f), Vector3.zero);
        BoxChild(altar, "Casita", m.madera, new Vector3(0f, 0.83f, 0.03f), new Vector3(0.5f, 0.46f, 0.34f), Vector3.zero);
        BoxChild(altar, "Hueco", m.hueco, new Vector3(0f, 0.8f, -0.145f), new Vector3(0.28f, 0.3f, 0.01f), Vector3.zero);
        BoxChild(altar, "TechoIzq", m.torii, new Vector3(-0.15f, 1.12f, 0.03f), new Vector3(0.38f, 0.04f, 0.46f), new Vector3(0f, 0f, 25f));
        BoxChild(altar, "TechoDer", m.torii, new Vector3(0.15f, 1.12f, 0.03f), new Vector3(0.38f, 0.04f, 0.46f), new Vector3(0f, 0f, -25f));
        BoxChild(altar, "Plato", m.papel, new Vector3(0f, 0.61f, -0.13f), new Vector3(0.16f, 0.02f, 0.12f), Vector3.zero);
        foreach (float x in new[] { -0.27f, 0.27f })
        {
            BoxChild(altar, "Vela", m.papel, new Vector3(x, 0.68f, -0.12f), new Vector3(0.04f, 0.16f, 0.04f), Vector3.zero);
            BoxChild(altar, "Llama", m.luzRoja, new Vector3(x, 0.79f, -0.12f), new Vector3(0.03f, 0.05f, 0.03f), Vector3.zero);
        }
        for (int i = -1; i <= 1; i++)
        {
            Vector3 p = new Vector3(0.05f * i, 0.71f, -0.17f);
            BoxChild(altar, "Incienso", m.cable, p, new Vector3(0.01f, 0.22f, 0.01f), new Vector3(0f, 0f, 8f * i));
            BoxChild(altar, "Brasa", m.luzRoja, p + new Vector3(-0.015f * i, 0.11f, 0f), new Vector3(0.015f, 0.015f, 0.015f), Vector3.zero);
        }

        // Una luz roja chica, como la de las velas (gira con el altar).
        var lightGo = new GameObject("LuzAltar");
        lightGo.transform.SetParent(altar, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.95f, -0.45f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.32f, 0.15f);
        light.range = 3f;
        light.intensity = 1.3f;
        light.shadows = LightShadows.None;
    }

    // ------------------------------------------------------------------ alrededor

    /// <summary>El puesto de fideos cerrado, en uno de tres lugares (DecorRandomizer).</summary>
    private static void BuildPlazaStalls(Transform deco, Mats m, Mesh shutter)
    {
        Mesh chochin = GetOrCreateMesh("Chochin");
        BuildLathe(chochin, ChochinProfile, 8);
        Transform group = Group(deco, "Puestos");
        // Origen del puesto = cara de la pared; el puesto mira hacia -Z (hacia el centro).
        var spots = new[]
        {
            (new Vector3(3f, 0f, PlazaInner), 0f),     // pared N, a la derecha
            (new Vector3(PlazaInner, 0f, -3f), 90f),   // pared E, del lado de la entrada
            (new Vector3(-PlazaInner, 0f, 3f), -90f),  // pared O, del lado del fondo
        };
        var options = new List<GameObject>();
        for (int i = 0; i < spots.Length; i++)
        {
            Transform stall = Group(group, "Puesto" + i);
            stall.localPosition = spots[i].Item1;
            stall.localRotation = Quaternion.Euler(0f, spots[i].Item2, 0f);
            SolidBoxChild(stall, "Cuerpo", m.madera, new Vector3(0f, 1.2f, -0.4f), new Vector3(2.6f, 2.4f, 0.8f), Vector3.zero);
            CreateShutterYaw(stall, "Persiana", new Vector3(0f, 0f, -0.81f), 90f, shutter, m);
            BoxChild(stall, "Toldo", m.toldo, new Vector3(0f, 2.72f, -1.05f), new Vector3(2.8f, 0.05f, 0.9f), new Vector3(-12f, 0f, 0f));
            BoxChild(stall, "Noren", m.noren, new Vector3(0f, 2.3f, -1.45f), new Vector3(2.4f, 0.5f, 0.02f), Vector3.zero);
            foreach (float x in new[] { -1.25f, 1.25f })
            {
                GameObject lantern = MeshChild(stall, "Farolito", chochin, m.farolTela, new Vector3(x, 1.95f, -1.45f));
                lantern.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            SolidBoxChild(stall, "Banqueta", m.madera, new Vector3(-0.6f, 0.22f, -1.5f), new Vector3(0.34f, 0.44f, 0.34f), new Vector3(0f, 12f, 0f));
            SolidBoxChild(stall, "Banqueta", m.madera, new Vector3(0.55f, 0.22f, -1.6f), new Vector3(0.34f, 0.44f, 0.34f), new Vector3(0f, -9f, 0f));
            stall.gameObject.SetActive(i == 0);
            options.Add(stall.gameObject);
        }
        group.gameObject.AddComponent<DecorRandomizer>().Setup(options.ToArray());
    }

    private static void BuildPlazaFurniture(Transform deco, Mats m)
    {
        Transform group = Group(deco, "Muebles");
        PlazaBench(group, new Vector3(-2.6f, 0f, -3.9f), 0f, m);   // contra la pared S, mirando al arbol
        PlazaBench(group, new Vector3(-3.9f, 0f, -2.9f), 90f, m);  // contra la pared O

        // Expendedora contra la pared S, del otro lado de la boca.
        Transform vending = Group(group, "Expendedora");
        vending.localPosition = new Vector3(2.3f, 0f, -PlazaInner);
        SolidBoxChild(vending, "Cuerpo", m.maquina, new Vector3(0f, 0.9f, 0.4f), new Vector3(0.9f, 1.8f, 0.75f), Vector3.zero);
        BoxChild(vending, "Vidriera", m.maquinaLuz, new Vector3(0f, 1.15f, 0.78f), new Vector3(0.7f, 0.9f, 0.02f), Vector3.zero);
        var lightGo = new GameObject("LuzExpendedora");
        lightGo.transform.SetParent(vending, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.2f, 1.3f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.75f, 0.85f, 1f);
        light.range = 3.2f;
        light.intensity = 0.9f;
        light.shadows = LightShadows.None;

        Mesh pot = GetOrCreateMesh("Maceta"); BuildLathe(pot, PotProfile, 7);
        Mesh bush = GetOrCreateMesh("Arbusto"); BuildLathe(bush, BushProfile, 6);
        CreatePlant(group, "Maceta", new Vector3(-2.0f, 0f, 4.0f), pot, bush, m);
        CreatePlant(group, "Maceta", new Vector3(-3.0f, 0f, 4.05f), pot, bush, m);
        CreatePlant(group, "Maceta", new Vector3(4.0f, 0f, 2.1f), pot, bush, m);
    }

    /// <summary>Banco de madera con patas de metal. Origen = contra la pared; mira hacia +Z local.</summary>
    private static void PlazaBench(Transform parent, Vector3 pos, float yaw, Mats m)
    {
        Transform bench = Group(parent, "Banco");
        bench.localPosition = pos;
        bench.localRotation = Quaternion.Euler(0f, yaw, 0f);
        SolidBoxChild(bench, "Asiento", m.madera, new Vector3(0f, 0.45f, 0.25f), new Vector3(1.6f, 0.06f, 0.42f), Vector3.zero);
        BoxChild(bench, "Respaldo", m.madera, new Vector3(0f, 0.78f, 0.06f), new Vector3(1.6f, 0.36f, 0.05f), new Vector3(-8f, 0f, 0f));
        foreach (float x in new[] { -0.7f, 0.7f })
        {
            BoxChild(bench, "Pata", m.metal, new Vector3(x, 0.21f, 0.1f), new Vector3(0.05f, 0.42f, 0.05f), Vector3.zero);
            BoxChild(bench, "Pata", m.metal, new Vector3(x, 0.21f, 0.4f), new Vector3(0.05f, 0.42f, 0.05f), Vector3.zero);
        }
    }

    /// <summary>Dos guirnaldas de farolitos de lado a lado y dos faroles colgados de ellas.</summary>
    private static void BuildPlazaLights(Transform deco, Mats m, GameObject farol)
    {
        Mesh chochin = GetOrCreateMesh("Chochin");
        Transform group = Group(deco, "Guirnaldas");
        const float top = 5.6f, sag = 0.45f;
        var spans = new List<Span>();
        foreach (float z in new[] { -2.6f, 2.6f })
        {
            Vector3 a = new Vector3(-PlazaInner, top, z), b = new Vector3(PlazaInner, top, z);
            spans.Add(new Span(a, b, sag, 0.015f));
            for (float x = -3.6f; x <= 3.61f; x += 0.9f)
            {
                float t = (x - a.x) / (b.x - a.x);
                float y = top - sag * 4f * t * (1f - t) - 0.47f;
                GameObject lantern = MeshChild(group, "Farolito", chochin, m.farolTela, new Vector3(x, y, z));
                lantern.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        CablesChild(group, "Plaza_Guirnaldas", spans, m);

        // Faroles (luz y zumbido) colgados de las guirnaldas con un cable corto.
        foreach (Vector3 p in new[] { new Vector3(-2.25f, 3.6f, -2.6f), new Vector3(2.25f, 3.6f, 2.6f) })
        {
            float t = (p.x + PlazaInner) / (2f * PlazaInner);
            float wire = top - sag * 4f * t * (1f - t);
            var lantern = (GameObject)PrefabUtility.InstantiatePrefab(farol, group);
            lantern.name = "Farol";
            lantern.transform.localPosition = p;
            BoxChild(group, "CableFarol", m.cable, new Vector3(p.x, (p.y + wire) * 0.5f, p.z), new Vector3(0.02f, wire - p.y, 0.02f), Vector3.zero);
        }
    }
}
