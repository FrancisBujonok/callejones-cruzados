using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Parte de AtmosphereBuilder: decoracion de las paredes del modulo.
///
/// Hay una parte comun (tejadillo, cables) y tres juegos (SetA, SetB, SetC) con objetos
/// distintos. DecorRandomizer elige uno al empezar y otro cada vez que la cinta recicla
/// el modulo, asi el callejon no se ve igual cada 6 metros.
///
/// Zonas libres (las persianas ocupan el resto):
/// - Pared izquierda (x = -1.4): z de 0.3 a 2.9, por debajo del toldo (2.7 m).
/// - Pared derecha (x = +1.4): z de -2.9 a -0.3.
/// Paso 18: cada juego trae ademas una placa chica (direccion azul o flecha verde de salida).
/// Paso 26: SetD, jaulas vacias apiladas, plumas en el piso y cajas con agujeros para respirar.
/// Nadie lo explica: es la parte humana (y criminal) de la novela, a la vista del que mira.
/// Paso 27: menos cosas en el piso. Una sola maceta (solo en SetA; antes habia dos en SetA y una
/// en SetC), cuatro plumas en vez de diez y sin brillo propio. Y una capa nueva, "Basura", que se
/// sortea aparte de los juegos: nada, latas tiradas, una bolsa, restos de comida o una botella.
/// </summary>
public static partial class AtmosphereBuilder
{
    // Perfiles de torno (radio, altura), de arriba hacia abajo.
    private static readonly Vector2[] PipeProfile = { new Vector2(0.05f, 3f), new Vector2(0.05f, 0f) };
    private static readonly Vector2[] PotProfile = { new Vector2(0.19f, 0.3f), new Vector2(0.17f, 0.26f), new Vector2(0.13f, 0f) };
    private static readonly Vector2[] BushProfile =
    {
        new Vector2(0.05f, 0.5f), new Vector2(0.22f, 0.38f), new Vector2(0.26f, 0.2f),
        new Vector2(0.15f, 0.05f), new Vector2(0.04f, 0f),
    };
    private static readonly Vector2[] BucketProfile = { new Vector2(0.16f, 0.32f), new Vector2(0.13f, 0f) };

    private static void BuildDecorSets(Transform deco, Mats m)
    {
        Mesh pipe = GetOrCreateMesh("Tubo"); BuildLathe(pipe, PipeProfile, 6);
        Mesh pot = GetOrCreateMesh("Maceta"); BuildLathe(pot, PotProfile, 7);
        Mesh bush = GetOrCreateMesh("Arbusto"); BuildLathe(bush, BushProfile, 6);
        Mesh bucket = GetOrCreateMesh("Balde"); BuildLathe(bucket, BucketProfile, 7);

        // ---- Comun: todos los modulos lo tienen.
        Transform comun = Group(deco, "Comun");
        // Tejadillo sobre la pared derecha: mas alto contra la pared, baja hacia el pasillo.
        BoxChild(comun, "Tejadillo", m.teja, new Vector3(1.16f, 2.95f, 0f), new Vector3(0.6f, 0.05f, 5.9f), new Vector3(0f, 0f, 15f));
        BoxChild(comun, "CableParedIzq", m.cable, new Vector3(-1.37f, 2.72f, 0f), new Vector3(0.02f, 0.02f, 6f), Vector3.zero);
        BoxChild(comun, "CableParedDer", m.cable, new Vector3(1.37f, 2.75f, 0f), new Vector3(0.02f, 0.02f, 6f), Vector3.zero);

        // ---- Juegos que se sortean.
        Transform sets = Group(deco, "Sets");

        // A: cartel de papel y aire acondicionado a la derecha, ventana encendida a la izquierda.
        Transform a = Group(sets, "SetA");
        CreateWallSign(a, "Cartel", new Vector3(WallInnerX - 0.035f, 2.0f, -2.0f), m);
        BoxChild(a, "AireAcondicionado", m.metal, new Vector3(1.2f, 2.7f, -0.9f), new Vector3(0.38f, 0.32f, 0.62f), Vector3.zero);
        CreateWindow(a, "Ventana", new Vector3(-WallInnerX, 1.75f, 1.8f), false, true, m);
        CreatePlant(a, "MacetaIzq", new Vector3(-1.12f, 0f, 0.7f), pot, bush, m); // Paso 27: la de la derecha se saco
        CreatePlate(a, "Placa", new Vector3(-WallInnerX, 2.05f, 0.6f), false, m); // direccion o flecha (Paso 18)

        // B: cartel rojo colgante y cano a la derecha, medidor y cajones a la izquierda.
        Transform b = Group(sets, "SetB");
        CreateHangingSign(b, "CartelColgante", new Vector3(WallInnerX, 2.3f, -1.7f), true, m);
        CreatePipe(b, "Cano", new Vector3(1.34f, 0f, -0.45f), pipe, m);
        BoxChild(b, "Medidor", m.metal, new Vector3(-1.33f, 1.5f, 1.0f), new Vector3(0.12f, 0.4f, 0.3f), Vector3.zero);
        BoxChild(b, "MedidorLuz", m.luzRoja, new Vector3(-1.265f, 1.62f, 1.08f), new Vector3(0.02f, 0.04f, 0.04f), Vector3.zero);
        BoxChild(b, "Conducto", m.metal, new Vector3(-1.36f, 0.65f, 1.1f), new Vector3(0.04f, 1.3f, 0.04f), Vector3.zero);
        SolidBoxChild(b, "Cajon", m.madera, new Vector3(-1.12f, 0.25f, 2.3f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 4f, 0f));
        SolidBoxChild(b, "CajonChico", m.madera, new Vector3(-1.1f, 0.69f, 2.32f), new Vector3(0.38f, 0.38f, 0.38f), new Vector3(0f, 17f, 0f));
        CreatePlate(b, "Placa", new Vector3(WallInnerX, 1.95f, -2.55f), true, m);

        // C: ventana apagada y balde a la derecha, cartel de papel colgante y planta a la izquierda,
        // enredadera cayendo del tejadillo.
        Transform c = Group(sets, "SetC");
        CreateWindow(c, "Ventana", new Vector3(WallInnerX, 1.8f, -1.8f), true, false, m);
        CreateSolidLathe(c, "Balde", new Vector3(1.12f, 0f, -0.7f), bucket, m.balde, new Vector3(0.34f, 0.34f, 0.34f));
        CreateHangingSign(c, "CartelColgante", new Vector3(-WallInnerX, 2.15f, 1.7f), false, m);
        // Paso 27: sin maceta (habia demasiadas).
        CreatePlate(c, "Placa", new Vector3(-WallInnerX, 2.1f, 2.55f), false, m);
        Transform ivy = Group(c, "Enredadera");
        float[] ivyZ = { -2.6f, -2.3f, -1.95f, -1.5f, -1.2f };
        float[] ivyLen = { 0.5f, 0.85f, 0.4f, 0.7f, 0.55f };
        for (int i = 0; i < ivyZ.Length; i++)
        {
            float x = 0.92f + 0.04f * (i % 2);
            BoxChild(ivy, "Tallo" + i, m.planta, new Vector3(x, 2.85f - ivyLen[i] * 0.5f, ivyZ[i]), new Vector3(0.03f, ivyLen[i], 0.03f), Vector3.zero);
            BoxChild(ivy, "Hojas" + i, m.planta, new Vector3(x, 2.85f - ivyLen[i], ivyZ[i]), new Vector3(0.1f, 0.12f, 0.1f), new Vector3(0f, 30f * i, 20f));
        }

        // D (Paso 26): jaulas vacias apiladas a la izquierda, plumas en el piso, cajas de madera
        // con agujeros para respirar a la derecha.
        Transform d = Group(sets, "SetD");
        Mesh cage = GetOrCreateMesh("Jaula"); BuildCageMesh(cage);
        CreateCage(d, "Jaula1", new Vector3(-1.16f, 0f, 1.0f), 2f, cage, m);
        CreateCage(d, "Jaula2", new Vector3(-1.16f, 0f, 1.6f), -3f, cage, m);
        CreateCage(d, "Jaula3", new Vector3(-1.14f, 0.405f, 1.28f), 7f, cage, m);
        var feathers = new System.Random(2604);
        Transform plumas = Group(d, "Plumas");
        for (int i = 0; i < 4; i++) // Paso 27: cuatro (eran diez) y con un material sin brillo
        {
            float x = -1.0f + 0.65f * (float)feathers.NextDouble();
            float z = 0.5f + 2.1f * (float)feathers.NextDouble();
            float yaw = 360f * (float)feathers.NextDouble();
            BoxChild(plumas, "Pluma" + i, m.pluma, new Vector3(x, 0.006f, z), new Vector3(0.022f, 0.004f, 0.075f), new Vector3(0f, yaw, 0f));
        }
        CreateBreathingCrate(d, "Caja", new Vector3(1.13f, 0f, -1.6f), new Vector3(0.48f, 0.42f, 0.7f), 0f, 4, m);
        CreateBreathingCrate(d, "CajaChica", new Vector3(1.14f, 0.42f, -1.5f), new Vector3(0.4f, 0.34f, 0.5f), 8f, 3, m);
        BoxChild(plumas, "PlumaCaja", m.pluma, new Vector3(1.05f, 0.765f, -1.42f), new Vector3(0.022f, 0.004f, 0.075f), new Vector3(0f, 40f, 0f));
        CreatePlate(d, "Placa", new Vector3(-WallInnerX, 2.05f, 2.6f), false, m);

        // En el editor solo se ve el primero; en Play, DecorRandomizer sortea.
        b.gameObject.SetActive(false);
        c.gameObject.SetActive(false);
        d.gameObject.SetActive(false);
        sets.gameObject.AddComponent<DecorRandomizer>().Setup(new[] { a.gameObject, b.gameObject, c.gameObject, d.gameObject });

        BuildStreetTrash(deco, m);
    }

    // ------------------------------------------------------------------ Paso 27: basura

    // Perfiles de torno (radio, altura), de arriba hacia abajo. Radio 0 arriba o abajo = tapa.
    private static readonly Vector2[] CanProfile =
    {
        new Vector2(0.026f, 0.122f), new Vector2(0.033f, 0.11f), new Vector2(0.033f, 0.012f), new Vector2(0.028f, 0f),
    };
    private static readonly Vector2[] BagProfile =
    {
        new Vector2(0.015f, 0.6f), new Vector2(0.05f, 0.56f), new Vector2(0.035f, 0.5f), new Vector2(0.19f, 0.43f),
        new Vector2(0.26f, 0.27f), new Vector2(0.25f, 0.1f), new Vector2(0.17f, 0.02f), new Vector2(0.06f, 0f),
    };
    private static readonly Vector2[] BottleProfile =
    {
        new Vector2(0.012f, 0.25f), new Vector2(0.014f, 0.2f), new Vector2(0.038f, 0.15f), new Vector2(0.04f, 0.01f), new Vector2(0.034f, 0f),
    };

    /// <summary>
    /// Basura del recto: un DecorRandomizer propio, aparte de los juegos, con pesos. Casi la mitad
    /// de las veces no hay nada. Las latas van en el medio del pasillo (|x| menor a 0.8: ahi no
    /// hay macetas, cajones ni jaulas de ningun juego) y la bolsa contra la pared derecha, al
    /// fondo (z = -2.6), el unico lugar que todos los juegos dejan libre.
    /// </summary>
    private static void BuildStreetTrash(Transform deco, Mats m)
    {
        TrashMeshSet meshes = TrashMeshes();
        Transform trash = Group(deco, "Basura");

        Transform none = Group(trash, "Nada");

        Transform cans = Group(trash, "Latas");
        CreateCan(cans, "Lata1", new Vector3(0.35f, 0f, 1.2f), 70f, m.lataRoja, false, meshes);
        CreateCan(cans, "Lata2", new Vector3(-0.2f, 0f, -0.9f), 160f, m.lataPlata, true, meshes);

        Transform cans2 = Group(trash, "Latas2");
        CreateCan(cans2, "Lata1", new Vector3(-0.45f, 0f, 2.1f), 20f, m.lataVerde, true, meshes);
        CreateCan(cans2, "Lata2", new Vector3(0.55f, 0f, -1.7f), 115f, m.lataRoja, false, meshes);
        CreateCan(cans2, "Lata3", new Vector3(0.62f, 0f, -1.85f), 40f, m.lataRoja, true, meshes);

        Transform bag = Group(trash, "Bolsa");
        CreateTrashBag(bag, "Bolsa", new Vector3(1.08f, 0f, -2.6f), 30f, m, meshes);
        CreateCan(bag, "Lata", new Vector3(0.7f, 0f, -2.2f), 95f, m.lataPlata, false, meshes);

        Transform food = Group(trash, "Comida");
        CreateTakeaway(food, "Cajita", new Vector3(-0.85f, 0f, 2.75f), -25f, m);
        CreateCan(food, "Lata", new Vector3(-0.4f, 0f, 2.3f), 200f, m.lataVerde, false, meshes);

        Transform bottle = Group(trash, "Botella");
        CreateBottle(bottle, "Botella", new Vector3(0.3f, 0f, 0.4f), 55f, m, meshes);
        CreateCan(bottle, "Lata", new Vector3(-0.5f, 0f, -2.0f), 300f, m.lataRoja, true, meshes);

        GameObject[] options = { none.gameObject, cans.gameObject, cans2.gameObject, bag.gameObject, food.gameObject, bottle.gameObject };
        for (int i = 1; i < options.Length; i++) options[i].SetActive(false);
        var pick = trash.gameObject.AddComponent<DecorRandomizer>();
        pick.Setup(options, new[] { 4f, 1.5f, 1f, 1.2f, 1f, 1f }); // nada: 4 de cada 9.7
    }

    private class TrashMeshSet { public Mesh can, bag, bottle; }

    private static TrashMeshSet TrashMeshes()
    {
        var meshes = new TrashMeshSet
        {
            can = GetOrCreateMesh("Lata"),
            bag = GetOrCreateMesh("BolsaBasura"),
            bottle = GetOrCreateMesh("Botella"),
        };
        BuildLathe(meshes.can, CanProfile, 8);
        BuildLathe(meshes.bag, BagProfile, 7);
        BuildLathe(meshes.bottle, BottleProfile, 7);
        return meshes;
    }

    /// <summary>
    /// Lata tirada, acostada. El torno la arma parada (a lo largo de Y); se gira 90 grados en Z
    /// para acostarla. Aplastada: se achata en su X local, que acostada es la altura.
    /// Sin collider: se pisa.
    /// </summary>
    private static void CreateCan(Transform parent, string name, Vector3 floorPos, float yaw, Material mat, bool crushed, TrashMeshSet meshes)
    {
        float r = 0.033f * (crushed ? 0.45f : 1f);
        Transform pivot = Group(parent, name);
        pivot.localPosition = floorPos + Vector3.up * r;
        pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        GameObject can = MeshChild(pivot, "Cuerpo", meshes.can, mat, new Vector3(0.061f, 0f, 0f)); // centrada en su largo
        can.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        can.transform.localScale = crushed ? new Vector3(0.45f, 0.85f, 1.25f) : Vector3.one;
    }

    private static void CreateTrashBag(Transform parent, string name, Vector3 floorPos, float yaw, Mats m, TrashMeshSet meshes)
    {
        GameObject go = MeshChild(parent, name, meshes.bag, m.bolsa, floorPos);
        go.transform.localRotation = Quaternion.Euler(4f, yaw, -7f);
        go.transform.localScale = new Vector3(1f, 0.9f, 0.82f);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(0.5f, 0.5f, 0.5f);
        box.center = new Vector3(0f, 0.25f, 0f);
    }

    /// <summary>Cajita de comida para llevar, con la tapa abierta, y un par de palitos al lado.</summary>
    private static void CreateTakeaway(Transform parent, string name, Vector3 floorPos, float yaw, Mats m)
    {
        Transform t = Group(parent, name);
        t.localPosition = floorPos;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        BoxChild(t, "Base", m.telgopor, new Vector3(0f, 0.03f, 0f), new Vector3(0.15f, 0.06f, 0.11f), Vector3.zero);
        BoxChild(t, "Adentro", m.hueco, new Vector3(0f, 0.0605f, 0f), new Vector3(0.13f, 0.002f, 0.09f), Vector3.zero);
        BoxChild(t, "Tapa", m.telgopor, new Vector3(0f, 0.09f, 0.085f), new Vector3(0.15f, 0.008f, 0.11f), new Vector3(-62f, 0f, 0f));
        BoxChild(t, "Palito1", m.madera, new Vector3(0.14f, 0.004f, -0.05f), new Vector3(0.006f, 0.006f, 0.2f), new Vector3(0f, 12f, 0f));
        BoxChild(t, "Palito2", m.madera, new Vector3(0.165f, 0.004f, -0.03f), new Vector3(0.006f, 0.006f, 0.2f), new Vector3(0f, 28f, 0f));
    }

    private static void CreateBottle(Transform parent, string name, Vector3 floorPos, float yaw, Mats m, TrashMeshSet meshes)
    {
        Transform pivot = Group(parent, name);
        pivot.localPosition = floorPos + Vector3.up * 0.04f;
        pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        GameObject go = MeshChild(pivot, "Cuerpo", meshes.bottle, m.botella, new Vector3(0.12f, 0f, 0f));
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    }

    // ------------------------------------------------------------------ Paso 26: jaulas y cajas

    private const float CageW = 0.42f, CageH = 0.4f, CageD = 0.55f;

    /// <summary>
    /// Una jaula vacia en una sola malla: base, techo, cuatro postes y barrotes cada 6 cm. Del lado
    /// del pasillo (+X) faltan tres barrotes: la puertita quedo abierta.
    /// Una sola malla y no un cubo por barrote: con tres jaulas por pieza serian 120 objetos.
    /// </summary>
    private static void BuildCageMesh(Mesh mesh)
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        const float bar = 0.01f, step = 0.06f;
        AddBox(v, n, uv, t, new Vector3(0f, 0.01f, 0f), new Vector3(CageW, 0.02f, CageD));               // base
        AddBox(v, n, uv, t, new Vector3(0f, CageH - 0.008f, 0f), new Vector3(CageW, 0.016f, CageD));     // techo
        foreach (float sx in new[] { -1f, 1f })
        foreach (float sz in new[] { -1f, 1f })
            AddBox(v, n, uv, t, new Vector3(sx * (CageW / 2 - 0.01f), CageH / 2, sz * (CageD / 2 - 0.01f)), new Vector3(0.018f, CageH, 0.018f));
        // Barrotes en los lados largos (+X y -X), a lo largo de Z.
        for (float z = -CageD / 2 + step; z < CageD / 2 - 0.02f; z += step)
        {
            AddBox(v, n, uv, t, new Vector3(-CageW / 2 + 0.01f, CageH / 2, z), new Vector3(bar, CageH, bar));
            if (Mathf.Abs(z) > 0.1f) // la puertita abierta, en el medio del lado del pasillo
                AddBox(v, n, uv, t, new Vector3(CageW / 2 - 0.01f, CageH / 2, z), new Vector3(bar, CageH, bar));
        }
        // Barrotes en los lados cortos (+Z y -Z), a lo largo de X.
        for (float x = -CageW / 2 + step; x < CageW / 2 - 0.02f; x += step)
            foreach (float sz in new[] { -1f, 1f })
                AddBox(v, n, uv, t, new Vector3(x, CageH / 2, sz * (CageD / 2 - 0.01f)), new Vector3(bar, CageH, bar));

        mesh.Clear();
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
    }

    /// <summary>Un prisma con sus 6 caras (vertices propios por cara: sombreado plano).</summary>
    private static void AddBox(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 c, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 dir in dirs)
        {
            Vector3 a = dir.y != 0f ? Vector3.right : Vector3.up;   // un eje de la cara
            Vector3 b = Vector3.Cross(dir, a);                       // el otro
            int start = v.Count;
            Vector3 center = c + Vector3.Scale(dir, h);
            Vector3 ea = Vector3.Scale(a, h), eb = Vector3.Scale(b, h);
            v.Add(center - ea - eb); v.Add(center + ea - eb); v.Add(center + ea + eb); v.Add(center - ea + eb);
            for (int k = 0; k < 4; k++) n.Add(dir);
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
            // Unity dibuja la cara de adelante cuando los vertices van en el sentido del reloj.
            t.Add(start); t.Add(start + 1); t.Add(start + 2);
            t.Add(start); t.Add(start + 2); t.Add(start + 3);
        }
    }

    private static void CreateCage(Transform parent, string name, Vector3 pos, float yaw, Mesh cage, Mats m)
    {
        GameObject go = MeshChild(parent, name, cage, m.metal, pos);
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(CageW, CageH, CageD);
        box.center = new Vector3(0f, CageH * 0.5f, 0f);
    }

    /// <summary>Caja de madera con una fila de agujeritos negros del lado del pasillo (-X).</summary>
    private static void CreateBreathingCrate(Transform parent, string name, Vector3 floorPos, Vector3 size, float yaw, int holes, Mats m)
    {
        Transform t = Group(parent, name);
        t.localPosition = floorPos;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        SolidBoxChild(t, "Madera", m.madera, new Vector3(0f, size.y * 0.5f, 0f), size, Vector3.zero);
        float faceX = -size.x * 0.5f - 0.003f;
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < holes; i++)
            {
                float z = (i - (holes - 1) * 0.5f) * (size.z / (holes + 0.5f));
                float y = size.y * (row == 0 ? 0.68f : 0.45f);
                if (row == 1 && i % 2 == 1) continue; // la segunda fila, salteada
                BoxChild(t, "Agujero" + row + i, m.hueco, new Vector3(faceX, y, z), new Vector3(0.006f, 0.035f, 0.035f), Vector3.zero);
            }
    }

    private static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    /// <summary>Cubo decorativo CON collider: para lo que esta en el piso y no se debe atravesar.</summary>
    private static GameObject SolidBoxChild(Transform parent, string name, Material mat, Vector3 pos, Vector3 scale, Vector3 euler)
    {
        GameObject go = BoxChild(parent, name, mat, pos, scale, euler);
        go.AddComponent<BoxCollider>();
        return go;
    }

    private static void CreateSolidLathe(Transform parent, string name, Vector3 pos, Mesh mesh, Material mat, Vector3 colliderSize)
    {
        GameObject go = MeshChild(parent, name, mesh, mat, pos);
        var box = go.AddComponent<BoxCollider>();
        box.size = colliderSize;
        box.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
    }

    // Ventana con marco, vidrio (encendido o apagado), rejas y alfeizar.
    // Como las persianas: se arma mirando hacia +X y la de la pared derecha se gira 180 grados.
    private static void CreateWindow(Transform parent, string name, Vector3 wallPoint, bool rightWall, bool lit, Mats m)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = wallPoint;
        go.transform.localRotation = rightWall ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        Transform t = go.transform;

        BoxChild(t, "Marco", m.metal, new Vector3(0.03f, 0f, 0f), new Vector3(0.06f, 0.95f, 0.75f), Vector3.zero);
        BoxChild(t, "Vidrio", lit ? m.ventanaLuz : m.ventanaOscura, new Vector3(0.065f, 0f, 0f), new Vector3(0.01f, 0.85f, 0.65f), Vector3.zero);
        for (int i = 0; i < 4; i++)
            BoxChild(t, "Reja" + i, m.metal, new Vector3(0.11f, 0f, -0.27f + i * 0.18f), new Vector3(0.02f, 0.95f, 0.02f), Vector3.zero);
        BoxChild(t, "RejaArriba", m.metal, new Vector3(0.11f, 0.2f, 0f), new Vector3(0.02f, 0.02f, 0.75f), Vector3.zero);
        BoxChild(t, "RejaAbajo", m.metal, new Vector3(0.11f, -0.2f, 0f), new Vector3(0.02f, 0.02f, 0.75f), Vector3.zero);
        BoxChild(t, "Alfeizar", m.metal, new Vector3(0.07f, -0.5f, 0f), new Vector3(0.14f, 0.04f, 0.85f), Vector3.zero);
    }

    // Cartel colgante perpendicular a la pared, con su soporte. Tiene dos caras (se ve desde los
    // dos lados del pasillo) y un solo SignRandomizer, asi las dos dicen lo mismo.
    private static void CreateHangingSign(Transform parent, string name, Vector3 wallPoint, bool rightWall, Mats m)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = wallPoint;
        go.transform.localRotation = rightWall ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        BoxChild(go.transform, "Soporte", m.metal, new Vector3(0.24f, 0.45f, 0f), new Vector3(0.48f, 0.03f, 0.03f), Vector3.zero);
        BoxChild(go.transform, "Tabla", m.cable, new Vector3(0.24f, 0f, 0f), new Vector3(SignV2Size.x + 0.04f, SignV2Size.y + 0.04f, 0.05f), Vector3.zero);
        Material first = FirstOr(m.signsV2, m.cartel);
        // Paso 21: 1.5 cm por fuera de la tabla (antes 2 mm: la tabla tapaba la cara a ratos).
        MeshRenderer front = SignFace(go.transform, "CaraA", new Vector3(0.24f, 0f, -0.04f), SignV2Size, 0f, first);
        MeshRenderer back = SignFace(go.transform, "CaraB", new Vector3(0.24f, 0f, 0.04f), SignV2Size, 180f, first);
        AddSignPick(go, m.signsV2, front, back);
    }

    private static void CreatePipe(Transform parent, string name, Vector3 floorPoint, Mesh pipe, Mats m)
    {
        GameObject go = MeshChild(parent, name, pipe, m.metal, floorPoint);
        for (int i = 0; i < 3; i++)
            BoxChild(go.transform, "Abrazadera" + i, m.metal, new Vector3(0.04f, 0.6f + i * 0.9f, 0f), new Vector3(0.08f, 0.04f, 0.14f), Vector3.zero);
    }

    // Maceta con un arbusto low-poly. Tiene collider para no atravesarla.
    private static void CreatePlant(Transform parent, string name, Vector3 floorPoint, Mesh pot, Mesh bush, Mats m)
    {
        GameObject go = MeshChild(parent, name, pot, m.maceta, floorPoint);
        MeshChild(go.transform, "Arbusto", bush, m.planta, new Vector3(0f, 0.27f, 0f));
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(0.4f, 0.8f, 0.4f);
        box.center = new Vector3(0f, 0.4f, 0f);
    }
}
