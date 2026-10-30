using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: las piezas del laberinto con giros y el deposito.
/// Menu: Callejones > Laberinto con giros (tambien corre dentro de "Construir atmosfera (todo)").
///
/// Que hace:
/// 1. Al prefab Modulo (el recto) le agrega dos conexiones, una en cada punta.
/// 2. Crea tres prefabs nuevos de 3 x 3 m: Esquina (giro), Cruce (T) y Fondo (sin salida),
///    con las mismas paredes, piso, persianas, faroles y decoracion al azar que el recto.
/// 3. Crea el prefab Tapa: una persiana que cierra una conexion cuando no entra ninguna pieza.
/// 4. Al ModuloSalida le agrega CorridorSegment (tipo Salida) y su conexion.
/// 5. Llena el deposito: copias de cada pieza como hijas de Laberinto, apagadas.
///    El generador (CorridorTreadmill) las va prendiendo y apagando durante el juego.
/// 6. Anomalias de los bucles: cada pieza de 3 x 3 trae, apagadas, una persiana a medio subir
///    y el hueco oscuro de abajo, y el componente SegmentAnomalies que las prende.
///
/// Convencion de las piezas de 3 x 3: el centro en (0, 0, 0) y cuatro lados:
///   S (z = -1.5, mira hacia -Z), N (z = +1.5), E (x = +1.5), O (x = -1.5).
/// La conexion por la que se entra siempre es S; las demas son salidas.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string EsquinaPath = Root + "/Prefabs/Esquina.prefab";
    private const string CrucePath = Root + "/Prefabs/Cruce.prefab";
    private const string FondoPath = Root + "/Prefabs/Fondo.prefab";
    private const string TapaPath = Root + "/Prefabs/Tapa.prefab";

    // Cuantas copias de cada pieza hay en el deposito. Alcanza para un radio de ~30 m con cruces
    // y para un bucle (cada copia usa hasta 7 rectos, 2 esquinas, 1 cruce y 1 fondo).
    private const int PoolRecto = 26, PoolEsquina = 10, PoolCruce = 6, PoolFondo = 7;

    // Persiana a medio subir (anomalia): el panel mide esto y abajo queda un hueco de 0.9 m.
    private const float HalfShutterHeight = 1.5f;

    [System.Flags]
    private enum Side { None = 0, S = 1, N = 2, E = 4, O = 8 }

    [MenuItem("Callejones/Laberinto con giros (piezas y deposito)", priority = 25)]
    public static void MazeMenu()
    {
        if (!CanRun()) return;
        GameObject farol = AssetDatabase.LoadAssetAtPath<GameObject>(FarolPath);
        if (farol == null)
        {
            EditorUtility.DisplayDialog("Callejones", "Falta el prefab Farol. Ejecuta primero Callejones > Construir atmosfera (todo).", "OK");
            return;
        }
        BuildMaze(BuildMaterials(), farol);
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Laberinto con giros listo. Guarda con Ctrl+S.");
    }

    private static void BuildMaze(Mats m, GameObject farol)
    {
        Mesh shutter = GetOrCreateMesh("Persiana");
        BuildShutter(shutter, ShutterWidth, ShutterHeight, 0.12f, 0.04f);
        // Mismas bandas, pero mas corta: es la persiana levantada hasta la mitad.
        Mesh halfShutter = GetOrCreateMesh("PersianaMedia");
        BuildShutter(halfShutter, ShutterWidth, HalfShutterHeight, 0.12f, 0.04f);

        SetupRectoConnections();
        GameObject esquina = BuildCellPrefab(EsquinaPath, "Esquina", SegmentKind.Esquina, Side.S | Side.E, m, farol, shutter, halfShutter);
        GameObject cruce = BuildCellPrefab(CrucePath, "Cruce", SegmentKind.Cruce, Side.S | Side.E | Side.O, m, farol, shutter, halfShutter);
        GameObject fondo = BuildCellPrefab(FondoPath, "Fondo", SegmentKind.Fondo, Side.S, m, farol, shutter, halfShutter);
        GameObject tapa = BuildCapPrefab(m, shutter);
        SetupExitConnection();
        FillPool(esquina, cruce, fondo, tapa);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ------------------------------------------------------------------ 1. el recto

    private static void SetupRectoConnections()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ModuloPath);
        try
        {
            Transform a = Connection(root.transform, "Conexion_A", new Vector3(0f, 0f, -3f), 180f);
            Transform b = Connection(root.transform, "Conexion_B", new Vector3(0f, 0f, 3f), 0f);
            GameObject open = root.transform.Find("Abierto").gameObject;
            GameObject closed = root.transform.Find("Cerrado").gameObject;
            // Variante 1 (Cerrado) tiene la pared en el medio: corta el paso.
            root.GetComponent<CorridorSegment>().Setup(SegmentKind.Recto, new[] { a, b },
                new[] { open, closed }, 1, new Vector3(3f, 3f, 6f));
            PrefabUtility.SaveAsPrefabAsset(root, ModuloPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Objeto vacio en el borde de la pieza. Su eje Z (azul) apunta hacia afuera.</summary>
    private static Transform Connection(Transform parent, string name, Vector3 pos, float yaw)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    // ------------------------------------------------------------------ 2. piezas de 3 x 3

    private static GameObject BuildCellPrefab(string path, string name, SegmentKind kind, Side open,
        Mats m, GameObject farol, Mesh shutter, Mesh halfShutter)
    {
        var root = new GameObject(name);
        try
        {
            // Piso de 3 x 3 con UV en metros, igual que el del recto.
            var floor = new GameObject("Piso");
            floor.transform.SetParent(root.transform, false);
            Mesh floorMesh = GetFloorMesh(new Vector3(3f, 0f, 3f));
            floor.AddComponent<MeshFilter>().sharedMesh = floorMesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = m.piso;
            floor.AddComponent<MeshCollider>().sharedMesh = floorMesh;

            // Estructura: paredes en los lados cerrados. Es la unica variante de la pieza.
            Transform structure = Group(root.transform, "Estructura");
            // Las paredes miden 3.2 m: 10 cm de mas en cada punta tapan la esquina con la pared vecina.
            if ((open & Side.N) == 0) MetricWall(structure, "ParedN", m.pared, new Vector3(0f, 1.5f, 1.5f), new Vector3(3.2f, 3f, 0.2f));
            if ((open & Side.S) == 0) MetricWall(structure, "ParedS", m.pared, new Vector3(0f, 1.5f, -1.5f), new Vector3(3.2f, 3f, 0.2f));
            if ((open & Side.E) == 0) MetricWall(structure, "ParedE", m.pared, new Vector3(1.5f, 1.5f, 0f), new Vector3(0.2f, 3f, 3.2f));
            if ((open & Side.O) == 0) MetricWall(structure, "ParedO", m.pared, new Vector3(-1.5f, 1.5f, 0f), new Vector3(0.2f, 3f, 3.2f));

            // Columnas donde se juntan dos lados abiertos: ahi ninguna pared tapa el canto
            // y entre las paredes de los pasillos vecinos quedaria una rendija.
            foreach (float x in new[] { -1.5f, 1.5f })
            foreach (float z in new[] { -1.5f, 1.5f })
            {
                Side sx = x > 0f ? Side.E : Side.O;
                Side sz = z > 0f ? Side.N : Side.S;
                if ((open & sx) != 0 && (open & sz) != 0)
                    MetricWall(structure, "Columna", m.pared, new Vector3(x, 1.5f, z), new Vector3(0.3f, 3f, 0.3f));
            }

            // Pared del fondo (N). El Cruce tiene siempre una persiana: ahi va la anomalia de los
            // bucles. La Esquina y el Fondo sortean su fachada (mas abajo, en Decorado).
            GameObject shutterN = null;
            bool closedN = (open & Side.N) == 0;
            if (closedN && kind == SegmentKind.Cruce)
                shutterN = CreateShutterYaw(structure, "PersianaN", new Vector3(0f, 0f, 1.4f), 90f, shutter, m);

            // Para la anomalia de los bucles: la misma persiana levantada hasta la mitad y, abajo,
            // un hueco negro pegado a la pared (parece el interior oscuro del local). Apagados.
            GameObject shutterDown = null, shutterHalf = null, darkGap = null;
            if (shutterN != null)
            {
                float gap = ShutterHeight - HalfShutterHeight;
                shutterDown = shutterN.transform.Find("Panel").gameObject;
                shutterHalf = MeshChild(shutterN.transform, "PanelEntreabierto", halfShutter, m.persiana, new Vector3(0f, gap, 0f));
                darkGap = BoxChild(shutterN.transform, "Hueco", m.hueco, new Vector3(0.007f, gap * 0.5f, 0f),
                    new Vector3(0.01f, gap, ShutterWidth), Vector3.zero);
                shutterHalf.SetActive(false);
                darkGap.SetActive(false);
            }

            // Farol colgando en el centro, de un cable de pared a pared.
            Transform deco = Group(root.transform, "Decorado");
            var lantern = (GameObject)PrefabUtility.InstantiatePrefab(farol, deco);
            lantern.name = "Farol";
            lantern.transform.localPosition = new Vector3(0.2f, 3.15f, 0.2f);
            BoxChild(deco, "Cable", m.cable, new Vector3(0f, 3.15f, 0.2f), new Vector3(3f, 0.025f, 0.025f), Vector3.zero);
            BuildCellDecor(deco, open, m);
            // Fachada sorteada en la pared del fondo (y lugar posible para un Landmark).
            if (closedN && kind != SegmentKind.Cruce)
                BuildFacadeSlot(deco, "FachadaN", new Vector3(0f, 0f, 1.4f), 90f, m, shutter);

            // Conexiones: S siempre primero (es por donde se entra), despues E y O.
            var sockets = new List<Transform> { Connection(root.transform, "Conexion_S", new Vector3(0f, 0f, -1.5f), 180f) };
            if ((open & Side.E) != 0) sockets.Add(Connection(root.transform, "Conexion_E", new Vector3(1.5f, 0f, 0f), 90f));
            if ((open & Side.O) != 0) sockets.Add(Connection(root.transform, "Conexion_O", new Vector3(-1.5f, 0f, 0f), -90f));
            if ((open & Side.N) != 0) sockets.Add(Connection(root.transform, "Conexion_N", new Vector3(0f, 0f, 1.5f), 0f));

            root.AddComponent<CorridorSegment>().Setup(kind, sockets.ToArray(),
                new[] { structure.gameObject }, -1, new Vector3(3f, 3f, 3f));
            root.AddComponent<SegmentAnomalies>().Setup(lantern.GetComponentInChildren<LanternLight>(true),
                shutterDown, shutterHalf, darkGap);

            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Tres juegos de objetos contra las paredes cerradas; DecorRandomizer elige uno.
    /// Siempre lejos del centro, para no tapar el paso.
    /// </summary>
    private static void BuildCellDecor(Transform deco, Side open, Mats m)
    {
        Mesh pot = GetOrCreateMesh("Maceta"); BuildLathe(pot, PotProfile, 7);
        Mesh bush = GetOrCreateMesh("Arbusto"); BuildLathe(bush, BushProfile, 6);
        Mesh bucket = GetOrCreateMesh("Balde"); BuildLathe(bucket, BucketProfile, 7);

        // Lugares libres en el piso, pegados a paredes cerradas.
        var spots = new List<Vector3>();
        if ((open & Side.N) == 0) { spots.Add(new Vector3(-0.95f, 0f, 1.0f)); spots.Add(new Vector3(0.95f, 0f, 1.0f)); }
        if ((open & Side.O) == 0) spots.Add(new Vector3(-1.0f, 0f, -0.6f));
        if ((open & Side.E) == 0) spots.Add(new Vector3(1.0f, 0f, -0.6f));
        if (spots.Count == 0) return;
        Vector3 Spot(int i) => spots[i % spots.Count];

        Transform sets = Group(deco, "Sets");
        Transform a = Group(sets, "SetA");
        CreatePlant(a, "Maceta", Spot(0), pot, bush, m);
        if (spots.Count > 2) CreatePlant(a, "Maceta2", Spot(2), pot, bush, m);

        Transform b = Group(sets, "SetB");
        Vector3 crate = Spot(1);
        SolidBoxChild(b, "Cajon", m.madera, crate + new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 7f, 0f));
        SolidBoxChild(b, "CajonChico", m.madera, crate + new Vector3(0.03f, 0.69f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), new Vector3(0f, 21f, 0f));

        Transform c = Group(sets, "SetC");
        CreateSolidLathe(c, "Balde", Spot(0), bucket, m.balde, new Vector3(0.34f, 0.34f, 0.34f));
        if (spots.Count > 1) CreatePlant(c, "Maceta", Spot(1), pot, bush, m);

        // Carteles y ventanas en las paredes de los costados, si estan cerradas.
        if ((open & Side.O) == 0)
        {
            CreateHangingSign(a, "CartelColgante", new Vector3(-WallInnerX, 2.3f, 0.3f), false, m);
            CreateWindow(c, "Ventana", new Vector3(-WallInnerX, 1.8f, 0f), false, true, m);
        }
        if ((open & Side.E) == 0)
        {
            CreateWindow(a, "Ventana", new Vector3(WallInnerX, 1.8f, 0f), true, false, m);
            CreateHangingSign(b, "CartelColgante", new Vector3(WallInnerX, 2.2f, -0.3f), true, m);
        }

        b.gameObject.SetActive(false);
        c.gameObject.SetActive(false);
        sets.gameObject.AddComponent<DecorRandomizer>().Setup(new[] { a.gameObject, b.gameObject, c.gameObject });
    }

    /// <summary>Pared con malla de UV en metros (la textura no se estira) y collider.</summary>
    private static GameObject MetricWall(Transform parent, string name, Material mat, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = GetBoxMesh(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    /// <summary>Persiana como las del recto, pero con cualquier giro (la malla mira hacia +X).</summary>
    private static GameObject CreateShutterYaw(Transform parent, string name, Vector3 wallPoint, float yaw, Mesh mesh, Mats m)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = wallPoint;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        MeshChild(go.transform, "Panel", mesh, m.persiana, Vector3.zero);
        BoxChild(go.transform, "Caja", m.metal, new Vector3(0.11f, ShutterHeight + 0.13f, 0f), new Vector3(0.22f, 0.26f, ShutterWidth + 0.1f), Vector3.zero);
        BoxChild(go.transform, "GuiaA", m.metal, new Vector3(0.04f, ShutterHeight * 0.5f, -ShutterWidth * 0.5f - 0.03f), new Vector3(0.08f, ShutterHeight, 0.06f), Vector3.zero);
        BoxChild(go.transform, "GuiaB", m.metal, new Vector3(0.04f, ShutterHeight * 0.5f, ShutterWidth * 0.5f + 0.03f), new Vector3(0.08f, ShutterHeight, 0.06f), Vector3.zero);
        return go;
    }

    // ------------------------------------------------------------------ 3. la tapa

    /// <summary>
    /// Pared fina con una persiana mirando hacia -Z. El generador la pone en una conexion que
    /// quedo sin pieza, con su eje Z mirando hacia afuera: la persiana queda del lado del jugador.
    /// </summary>
    private static GameObject BuildCapPrefab(Mats m, Mesh shutter)
    {
        var root = new GameObject("Tapa");
        try
        {
            MetricWall(root.transform, "Pared", m.pared, new Vector3(0f, 1.5f, 0f), new Vector3(3f, 3f, 0.1f));
            CreateShutterYaw(root.transform, "Persiana", new Vector3(0f, 0f, -0.05f), 90f, shutter, m);
            return PrefabUtility.SaveAsPrefabAsset(root, TapaPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ------------------------------------------------------------------ 4. la salida

    private static void SetupExitConnection()
    {
        Transform exit = FindInScene<Transform>(t => t.name == "ModuloSalida");
        if (exit == null) return;
        Undo.RegisterFullObjectHierarchyUndo(exit.gameObject, "Conexion de la salida");

        Transform a = Connection(exit, "Conexion_A", new Vector3(0f, 0f, -3f), 180f);
        CorridorSegment seg = exit.GetComponent<CorridorSegment>();
        if (seg == null) seg = Undo.AddComponent<CorridorSegment>(exit.gameObject);
        seg.Setup(SegmentKind.Salida, new[] { a }, new GameObject[0], -1, new Vector3(3f, 3f, 6f));
        EditorUtility.SetDirty(seg);
    }

    // ------------------------------------------------------------------ 5. el deposito

    private static void FillPool(GameObject esquina, GameObject cruce, GameObject fondo, GameObject tapa)
    {
        CorridorTreadmill treadmill = FindInScene<CorridorTreadmill>(null);
        if (treadmill == null)
        {
            Debug.LogWarning("[Callejones] No encuentro CorridorTreadmill (objeto Laberinto).");
            return;
        }
        Transform maze = treadmill.transform;

        // Cuantas piezas de cada tipo ya hay.
        var count = new Dictionary<SegmentKind, int>();
        foreach (CorridorSegment s in maze.GetComponentsInChildren<CorridorSegment>(true))
        {
            count.TryGetValue(s.Kind, out int n);
            count[s.Kind] = n + 1;
        }

        GameObject recto = AssetDatabase.LoadAssetAtPath<GameObject>(ModuloPath);
        AddCopies(maze, recto, "Recto", PoolRecto, count, SegmentKind.Recto);
        AddCopies(maze, esquina, "Esquina", PoolEsquina, count, SegmentKind.Esquina);
        AddCopies(maze, cruce, "Cruce", PoolCruce, count, SegmentKind.Cruce);
        AddCopies(maze, fondo, "Fondo", PoolFondo, count, SegmentKind.Fondo);

        // Nombres ordenados: Recto_00, Recto_01... (antes eran "Modulo (3)").
        var index = new Dictionary<SegmentKind, int>();
        foreach (CorridorSegment s in maze.GetComponentsInChildren<CorridorSegment>(true))
        {
            index.TryGetValue(s.Kind, out int i);
            index[s.Kind] = i + 1;
            Undo.RecordObject(s.gameObject, "Nombre de pieza");
            s.gameObject.name = s.Kind + "_" + i.ToString("00");
        }

        SetRefs(treadmill, ("capPrefab", tapa));
    }

    private static void AddCopies(Transform maze, GameObject prefab, string name, int wanted,
        Dictionary<SegmentKind, int> count, SegmentKind kind)
    {
        count.TryGetValue(kind, out int have);
        for (int i = have; i < wanted; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, maze);
            Undo.RegisterCreatedObjectUndo(go, "Pieza del deposito");
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.SetActive(false); // en el editor quedan guardadas; el generador las prende en Play
        }
    }
}
