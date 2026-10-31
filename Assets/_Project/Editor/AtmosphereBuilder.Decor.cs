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
        CreatePlant(a, "MacetaIzq", new Vector3(-1.12f, 0f, 0.7f), pot, bush, m);
        CreatePlant(a, "MacetaDer", new Vector3(1.12f, 0f, -2.6f), pot, bush, m);
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
        CreatePlant(c, "Maceta", new Vector3(-1.12f, 0f, 0.6f), pot, bush, m);
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

        // En el editor solo se ve el primero; en Play, DecorRandomizer sortea.
        b.gameObject.SetActive(false);
        c.gameObject.SetActive(false);
        sets.gameObject.AddComponent<DecorRandomizer>().Setup(new[] { a.gameObject, b.gameObject, c.gameObject });
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
        MeshRenderer front = SignFace(go.transform, "CaraA", new Vector3(0.24f, 0f, -0.027f), SignV2Size, 0f, first);
        MeshRenderer back = SignFace(go.transform, "CaraB", new Vector3(0.24f, 0f, 0.027f), SignV2Size, 180f, first);
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
