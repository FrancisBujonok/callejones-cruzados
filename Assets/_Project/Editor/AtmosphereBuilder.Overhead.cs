using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Parte de AtmosphereBuilder: lo que se ve al mirar para arriba (Paso 18).
/// Menu: Callejones > Cielo nocturno, cables y carteles (tambien corre dentro de
/// "Construir atmosfera (todo)" y de "Fachadas, carteles y lugares").
///
/// Arriba de las paredes (miden 3 m) hay:
/// - En el recto, siempre: una linea de cables sobre cada pared, con un soporte en el medio.
///   Y uno de cinco "altos" sorteados: poste con transformador, caja de luz saliente, neon
///   parado sobre la pared, arco de neon de pared a pared o un enredo de cables.
/// - En las piezas de 3 x 3: un poste sobre una pared cerrada con cables hacia los lados
///   abiertos; en la otra variante, el poste en una esquina y un cartel alto.
///
/// Reglas:
/// - Las puntas de los cables en el borde de cada pieza estan siempre en el mismo lugar
///   (a 5 cm del eje de la pared y a 3.75 m de alto): el cable sigue en la pieza de al lado.
/// - Nada pasa de 4.3 m. La caja de cada pieza mide 3 m de alto y VisibilityChecker le suma 1 m
///   de margen. Lo que asoma un poco mas arriba solo se veria mirando casi derecho al cielo desde
///   lejos, y de todos modos lo de arriba solo se sortea de nuevo al reciclar la pieza, detras
///   de la niebla. Los postes y los cables quedan por debajo de 4 m.
/// - Todo sin collider: no frena al jugador ni tapa la linea de vision de VisibilityChecker.
/// - Los cables de cada variante son UNA malla (Meshes/Cables_{pieza}_{variante}): un objeto
///   en vez de cientos de cubitos.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const float CableHeight = 3.75f;   // altura de las puntas en el borde de cada pieza
    private const float CableEdge = 1.45f;     // a 5 cm del eje de la pared (x = 1.5)
    private const float CableThickness = 0.022f;
    private const float PoleTop = 3.97f;       // donde se atan los cables al poste

    // Transformador del poste (radio, altura), de arriba hacia abajo.
    private static readonly Vector2[] TransformerProfile =
    {
        new Vector2(0.09f, 0.48f), new Vector2(0.13f, 0.45f), new Vector2(0.13f, 0.05f), new Vector2(0.11f, 0f),
    };

    /// <summary>Un cable entre dos puntos que cuelga 'sag' metros en el medio.</summary>
    private struct Span
    {
        public Vector3 a, b;
        public float sag, thickness;

        public Span(Vector3 a, Vector3 b, float sag, float thickness = CableThickness)
        {
            this.a = a;
            this.b = b;
            this.sag = sag;
            this.thickness = thickness;
        }
    }

    // ------------------------------------------------------------------ el recto

    private static void BuildRectoHigh(Transform deco, Mats m)
    {
        DestroyChild(deco, "Alto");
        Transform alto = Group(deco, "Alto");
        Mesh transformer = TransformerMesh();

        // Siempre: una linea sobre cada pared, en dos tramos, apoyada en un soporte en el medio.
        Transform lines = Group(alto, "Lineas");
        var wallLines = new List<Span>();
        foreach (float x in new[] { -CableEdge, CableEdge })
        {
            wallLines.Add(new Span(new Vector3(x, CableHeight, -3f), new Vector3(x, CableHeight, 0f), 0.14f));
            wallLines.Add(new Span(new Vector3(x, CableHeight, 0f), new Vector3(x, CableHeight, 3f), 0.14f));
            string side = x < 0f ? "Izq" : "Der";
            BoxChild(lines, "Soporte" + side, m.metal, new Vector3(x, 3.38f, 0f), new Vector3(0.035f, 0.76f, 0.035f), Vector3.zero);
            BoxChild(lines, "Aislador" + side, m.maquina, new Vector3(x, 3.77f, 0f), new Vector3(0.05f, 0.04f, 0.05f), Vector3.zero);
        }
        CablesChild(lines, "Modulo_Lineas", wallLines, m);

        // Uno de cinco, sorteado (DecorRandomizer). Las lineas de arriba quedan siempre.
        Transform variants = Group(alto, "Variantes");
        var options = new List<GameObject>();

        // 1. Poste de cemento sobre la pared derecha, con transformador. Los cables salen de el.
        Transform pole = Group(variants, "Poste");
        Vector3[] hub = BuildPole(pole, "PosteDer", new Vector3(1.5f, 0f, -1.2f), Vector3.left, transformer, m);
        BoxChild(pole, "SoporteManojoA", m.metal, new Vector3(CableEdge, 3.28f, 0.3f), new Vector3(0.03f, 0.56f, 0.03f), Vector3.zero);
        BoxChild(pole, "SoporteManojoB", m.metal, new Vector3(CableEdge, 3.28f, 2.7f), new Vector3(0.03f, 0.56f, 0.03f), Vector3.zero);
        CablesChild(pole, "Modulo_Poste", new List<Span>
        {
            new Span(hub[1], new Vector3(-CableEdge, 3.72f, -2.6f), 0.3f),
            new Span(hub[1], new Vector3(-CableEdge, 3.72f, 0.9f), 0.34f),
            new Span(hub[0], new Vector3(CableEdge, CableHeight, 3f), 0.32f),
            new Span(hub[0], new Vector3(CableEdge, CableHeight, -3f), 0.16f),
            // Acometida: del transformador a la casa, mas gruesa.
            new Span(new Vector3(1.22f, 3.42f, -1.2f), new Vector3(1.42f, 2.95f, -0.45f), 0.05f, 0.03f),
            // Un manojo grueso que cuelga flojo sobre la pared derecha.
            new Span(new Vector3(CableEdge, 3.55f, 0.3f), new Vector3(CableEdge, 3.55f, 2.7f), 0.4f, 0.045f),
        }, m);
        options.Add(pole.gameObject);

        // 2. Caja de luz saliente arriba de la pared izquierda, por encima del toldo (2.9 m):
        //    va de 3.02 a 4.24 m.
        Transform box = Group(variants, "CajaDeLuz");
        CreateProjectingSign(box, "Cartel", new Vector3(-WallInnerX, 3.63f, 1.2f), false, SignSSize, m);
        CablesChild(box, "Modulo_CajaDeLuz", new List<Span>
        {
            new Span(new Vector3(-CableEdge, 3.72f, -0.4f), new Vector3(CableEdge, 3.72f, 0.5f), 0.32f),
            new Span(new Vector3(-CableEdge, 3.7f, 2.5f), new Vector3(CableEdge, 3.7f, 2.95f), 0.22f),
            new Span(new Vector3(-CableEdge, 3.66f, -2.2f), new Vector3(CableEdge, 3.74f, -2.7f), 0.26f),
        }, m);
        options.Add(box.gameObject);

        // 3. Neon parado sobre la pared derecha, de cara al pasillo: letras sueltas contra el cielo.
        Transform neon = Group(variants, "Neon");
        CreateRoofNeon(neon, "Letras", new Vector3(WallInnerX + 0.07f, 3f, -1.4f), 90f, m);
        CablesChild(neon, "Modulo_Neon", new List<Span>
        {
            new Span(new Vector3(-CableEdge, 3.74f, 2.4f), new Vector3(CableEdge, 3.7f, 1.6f), 0.3f),
            new Span(new Vector3(-CableEdge, 3.7f, -2.9f), new Vector3(CableEdge, 3.72f, -2.75f), 0.2f),
            new Span(new Vector3(-CableEdge, 3.68f, 0.2f), new Vector3(CableEdge, 3.73f, 0.6f), 0.36f),
        }, m);
        options.Add(neon.gameObject);

        // 4. Arco de neon de pared a pared, como la entrada de un mercado nocturno.
        Transform arch = Group(variants, "Arco");
        CreateNeonArch(arch, "Arco", 0.95f, m);
        CablesChild(arch, "Modulo_Arco", new List<Span>
        {
            new Span(new Vector3(-CableEdge, 3.7f, -1.9f), new Vector3(CableEdge, 3.74f, -2.6f), 0.28f),
            new Span(new Vector3(-CableEdge, 3.72f, 2.6f), new Vector3(CableEdge, 3.7f, 2.2f), 0.25f),
        }, m);
        options.Add(arch.gameObject);

        // 5. Solo cables cruzados a distintas alturas: el cielo partido en pedazos.
        Transform tangle = Group(variants, "Enredo");
        CablesChild(tangle, "Modulo_Enredo", new List<Span>
        {
            new Span(new Vector3(-CableEdge, 3.74f, -2.7f), new Vector3(CableEdge, 3.7f, -1.1f), 0.38f),
            new Span(new Vector3(-CableEdge, 3.7f, -1.3f), new Vector3(CableEdge, 3.74f, -2.4f), 0.3f),
            new Span(new Vector3(-CableEdge, 3.72f, 0.1f), new Vector3(CableEdge, 3.72f, 0.3f), 0.42f, 0.03f),
            new Span(new Vector3(-CableEdge, 3.66f, 0.5f), new Vector3(CableEdge, 3.75f, 1.9f), 0.3f),
            new Span(new Vector3(-CableEdge, 3.75f, 2.8f), new Vector3(CableEdge, 3.68f, 1.2f), 0.34f),
            new Span(new Vector3(-CableEdge, 3.7f, 1.9f), new Vector3(CableEdge, 3.7f, 2.9f), 0.2f),
        }, m);
        options.Add(tangle.gameObject);

        for (int i = 1; i < options.Count; i++) options[i].SetActive(false); // en el editor se ve el poste
        variants.gameObject.AddComponent<DecorRandomizer>().Setup(options.ToArray());
    }

    // ------------------------------------------------------------------ piezas de 3 x 3

    private static readonly Side[] SideOrder = { Side.N, Side.O, Side.E, Side.S };

    /// <summary>
    /// Poste y cables de una pieza de 3 x 3. Los cables van del poste a las puntas de cada lado
    /// abierto, donde siguen los de la pieza vecina. Dos variantes sorteadas:
    /// A, el poste en el medio de una pared cerrada, con transformador;
    /// B, el poste en una esquina y un cartel alto (caja de luz en una pared del costado o, en el
    /// Cruce, que no tiene, un neon arriba de la pared del fondo).
    /// </summary>
    private static void BuildCellHigh(Transform deco, string piece, Side open, Mats m)
    {
        Side closed = ~open & (Side.S | Side.N | Side.E | Side.O);
        if (closed == Side.None) return; // no hay piezas asi; por las dudas

        Transform alto = Group(deco, "Alto");
        Mesh transformer = TransformerMesh();

        // Puntas en los lados abiertos.
        var ends = new List<Vector3>();
        if ((open & Side.S) != 0) { ends.Add(new Vector3(-CableEdge, CableHeight, -1.5f)); ends.Add(new Vector3(CableEdge, CableHeight, -1.5f)); }
        if ((open & Side.E) != 0) { ends.Add(new Vector3(1.5f, CableHeight, -CableEdge)); ends.Add(new Vector3(1.5f, CableHeight, CableEdge)); }
        if ((open & Side.O) != 0) { ends.Add(new Vector3(-1.5f, CableHeight, -CableEdge)); ends.Add(new Vector3(-1.5f, CableHeight, CableEdge)); }
        if ((open & Side.N) != 0) { ends.Add(new Vector3(-CableEdge, CableHeight, 1.5f)); ends.Add(new Vector3(CableEdge, CableHeight, 1.5f)); }

        Transform variants = Group(alto, "Variantes");

        // A: poste con transformador en el medio de una pared cerrada.
        Transform a = Group(variants, "PosteMedio");
        Side wall = FirstClosed(closed);
        Vector3 midPos = SideCenter(wall), midArm = Inward(wall);
        Vector3[] hubA = BuildPole(a, "Poste", midPos, midArm, transformer, m);
        List<Span> spansA = FanCables(hubA, ends);
        // Acometida: del transformador a la pared de al lado, justo debajo del borde.
        Vector3 along = Vector3.Cross(Vector3.up, midArm).normalized;
        spansA.Add(new Span(midPos + midArm * 0.3f + Vector3.up * 3.45f, midPos + along * 0.75f + midArm * 0.11f + Vector3.up * 2.95f, 0.05f, 0.03f));
        CablesChild(a, piece + "_A", spansA, m);

        // B: poste en una esquina (o corrido sobre la pared) y un cartel alto.
        Transform b = Group(variants, "PosteEsquina");
        CornerSpot(closed, out Vector3 cornerPos, out Vector3 cornerArm);
        Vector3[] hubB = BuildPole(b, "Poste", cornerPos, cornerArm, null, m);
        CablesChild(b, piece + "_B", FanCables(hubB, ends), m);
        if ((closed & Side.O) != 0)
            CreateProjectingSign(b, "CajaDeLuz", new Vector3(-WallInnerX, 2.85f, -0.95f), false, SignSSize, m);
        else if ((closed & Side.E) != 0)
            CreateProjectingSign(b, "CajaDeLuz", new Vector3(WallInnerX, 2.85f, -0.95f), true, SignSSize, m);
        else if ((closed & Side.N) != 0)
            CreateRoofNeon(b, "Neon", new Vector3(0f, 3f, WallInnerX + 0.07f), 0f, m);

        b.gameObject.SetActive(false);
        variants.gameObject.AddComponent<DecorRandomizer>().Setup(new[] { a.gameObject, b.gameObject });
    }

    private static Side FirstClosed(Side closed)
    {
        foreach (Side s in SideOrder)
            if ((closed & s) != 0) return s;
        return Side.N;
    }

    /// <summary>El pie de la pared de ese lado (sobre su eje).</summary>
    private static Vector3 SideCenter(Side s) => s switch
    {
        Side.N => new Vector3(0f, 0f, 1.5f),
        Side.S => new Vector3(0f, 0f, -1.5f),
        Side.E => new Vector3(1.5f, 0f, 0f),
        _ => new Vector3(-1.5f, 0f, 0f),
    };

    /// <summary>Desde esa pared, hacia el centro de la pieza.</summary>
    private static Vector3 Inward(Side s) => -SideCenter(s) / 1.5f;

    /// <summary>Una esquina donde se juntan dos paredes cerradas; si no hay, un punto corrido sobre una pared.</summary>
    private static void CornerSpot(Side closed, out Vector3 pos, out Vector3 arm)
    {
        Side[,] pairs = { { Side.N, Side.O }, { Side.N, Side.E }, { Side.S, Side.O }, { Side.S, Side.E } };
        for (int i = 0; i < pairs.GetLength(0); i++)
        {
            Side p = pairs[i, 0], q = pairs[i, 1];
            if ((closed & p) == 0 || (closed & q) == 0) continue;
            pos = SideCenter(p) + SideCenter(q);
            arm = (Inward(p) + Inward(q)).normalized;
            return;
        }
        Side wall = FirstClosed(closed);
        arm = Inward(wall);
        pos = SideCenter(wall) + Vector3.Cross(Vector3.up, arm).normalized * 1.2f;
    }

    /// <summary>Un cable del poste a cada punta, repartidos entre los dos aisladores.</summary>
    private static List<Span> FanCables(Vector3[] hubs, List<Vector3> ends)
    {
        var spans = new List<Span>();
        for (int i = 0; i < ends.Count; i++)
        {
            Vector3 hub = hubs[i % hubs.Length];
            float sag = 0.06f + 0.07f * Vector3.Distance(hub, ends[i]);
            spans.Add(new Span(hub, ends[i], sag));
        }
        return spans;
    }

    // ------------------------------------------------------------------ piezas sueltas

    private static Mesh TransformerMesh()
    {
        Mesh mesh = GetOrCreateMesh("Transformador");
        BuildLathe(mesh, TransformerProfile, 8);
        return mesh;
    }

    /// <summary>
    /// Poste de cemento que sale de arriba de una pared: la base queda escondida adentro del muro.
    /// floorPos: el pie del poste, sobre el eje de la pared. arm: hacia donde sale el travesano.
    /// Devuelve los dos puntos donde se atan los cables (los aisladores), en coordenadas del padre.
    /// </summary>
    private static Vector3[] BuildPole(Transform parent, string name, Vector3 floorPos, Vector3 arm, Mesh transformer, Mats m)
    {
        arm.y = 0f;
        arm.Normalize();
        Transform t = Group(parent, name);
        t.localPosition = floorPos;
        // +X local = arm. (LookRotation apunta +Z; el +X queda en arm porque Cross(arm, arriba) es su +Z.)
        t.localRotation = Quaternion.LookRotation(Vector3.Cross(arm, Vector3.up), Vector3.up);

        BoxChild(t, "Poste", m.piedra, new Vector3(0f, 3.2f, 0f), new Vector3(0.15f, 1.6f, 0.15f), Vector3.zero); // de 2.4 a 4 m
        BoxChild(t, "Travesano", m.metal, new Vector3(0.26f, 3.88f, 0f), new Vector3(0.62f, 0.07f, 0.07f), Vector3.zero);
        BoxChild(t, "Riostra", m.metal, new Vector3(0.12f, 3.76f, 0f), new Vector3(0.28f, 0.03f, 0.03f), new Vector3(0f, 0f, 40f));
        BoxChild(t, "AisladorA", m.maquina, new Vector3(0.25f, 3.94f, 0f), new Vector3(0.04f, 0.06f, 0.04f), Vector3.zero);
        BoxChild(t, "AisladorB", m.maquina, new Vector3(0.5f, 3.94f, 0f), new Vector3(0.04f, 0.06f, 0.04f), Vector3.zero);
        if (transformer != null)
        {
            MeshChild(t, "Transformador", transformer, m.metal, new Vector3(0.21f, 3.28f, 0f));
            BoxChild(t, "Abrazadera", m.metal, new Vector3(0.1f, 3.62f, 0f), new Vector3(0.1f, 0.04f, 0.2f), Vector3.zero);
        }

        Vector3 up = Vector3.up * PoleTop;
        return new[] { floorPos + arm * 0.25f + up, floorPos + arm * 0.5f + up };
    }

    /// <summary>
    /// Caja de luz saliente: perpendicular a la pared, una cara para cada lado del pasillo y un
    /// mastil pegado a la pared. wallPoint: en la cara de la pared, a la altura del centro.
    /// rightWall: pared derecha o E (la caja sale hacia -X).
    /// </summary>
    private static void CreateProjectingSign(Transform parent, string name, Vector3 wallPoint, bool rightWall, Vector2 size, Mats m)
    {
        Transform t = Group(parent, name);
        t.localPosition = wallPoint;
        t.localRotation = rightWall ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity; // +X local = hacia el pasillo
        float cx = 0.07f + size.x * 0.5f + 0.02f;
        float arms = size.y * 0.5f - 0.1f;
        BoxChild(t, "Mastil", m.metal, new Vector3(-0.05f, 0f, 0f), new Vector3(0.05f, size.y, 0.05f), Vector3.zero);
        BoxChild(t, "BrazoArriba", m.metal, new Vector3(0.02f, arms, 0f), new Vector3(0.16f, 0.03f, 0.03f), Vector3.zero);
        BoxChild(t, "BrazoAbajo", m.metal, new Vector3(0.02f, -arms, 0f), new Vector3(0.16f, 0.03f, 0.03f), Vector3.zero);
        // Las caras quedan 2.5 cm por delante de la caja. Con 1 mm no alcanzaba: el temblor de
        // vertices del estilo PS1 corre cada triangulo un pixel y la caja tapaba la cara.
        BoxChild(t, "Caja", m.cable, new Vector3(cx, 0f, 0f), new Vector3(size.x + 0.04f, size.y + 0.04f, 0.05f), Vector3.zero);
        Material first = FirstOr(m.signsS, m.cartelRojo);
        MeshRenderer front = SignFace(t, "CaraA", new Vector3(cx, 0f, -0.05f), size, 0f, first);
        MeshRenderer back = SignFace(t, "CaraB", new Vector3(cx, 0f, 0.05f), size, 180f, first);
        AddSignPick(t.gameObject, m.signsS, front, back);
    }

    /// <summary>
    /// Neon de letras sueltas parado arriba de una pared, sobre dos rieles finos y oscuros.
    /// wallTop: arriba de la pared (y = 3). yaw: hacia donde miran las letras (como en SignFace).
    /// </summary>
    private static void CreateRoofNeon(Transform parent, string name, Vector3 wallTop, float yaw, Mats m)
    {
        Transform t = Group(parent, name);
        t.localPosition = wallTop;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        // Las letras van de 3.18 a 3.58 m: mas abajo, el borde de la pared y el tejadillo les
        // comian la parte de abajo al mirarlas desde el pasillo.
        float cy = 0.18f + SignNSize.y * 0.5f;
        float hw = SignNSize.x * 0.5f;
        float legs = cy + 0.12f;
        // Detras de las letras (z local positivo), a 5 cm para que el temblor PS1 no las cruce:
        // dos rieles y dos patas.
        const float back = 0.05f;
        BoxChild(t, "RielArriba", m.cable, new Vector3(0f, cy + 0.12f, back), new Vector3(SignNSize.x - 0.1f, 0.025f, 0.025f), Vector3.zero);
        BoxChild(t, "RielAbajo", m.cable, new Vector3(0f, cy - 0.12f, back), new Vector3(SignNSize.x - 0.1f, 0.025f, 0.025f), Vector3.zero);
        BoxChild(t, "PataIzq", m.metal, new Vector3(-hw + 0.12f, legs * 0.5f, back), new Vector3(0.03f, legs, 0.03f), Vector3.zero);
        BoxChild(t, "PataDer", m.metal, new Vector3(hw - 0.12f, legs * 0.5f, back), new Vector3(0.03f, legs, 0.03f), Vector3.zero);
        MeshRenderer face = SignFace(t, "Letras", new Vector3(0f, cy, 0f), SignNSize, 0f, FirstOr(m.signsN, m.cartelRojo));
        AddSignPick(t.gameObject, m.signsN, face);
    }

    /// <summary>Arco de neon de pared a pared: una barra apoyada en las dos paredes y el cartel colgado, con dos caras.</summary>
    private static void CreateNeonArch(Transform parent, string name, float z, Mats m)
    {
        const float barY = 3.68f, signY = 3.4f;
        Transform t = Group(parent, name);
        t.localPosition = new Vector3(0f, 0f, z);
        BoxChild(t, "Barra", m.metal, new Vector3(0f, barY, 0f), new Vector3(3.04f, 0.045f, 0.045f), Vector3.zero);
        BoxChild(t, "PataIzq", m.metal, new Vector3(-CableEdge, (3f + barY) * 0.5f, 0f), new Vector3(0.045f, barY - 3f, 0.045f), Vector3.zero);
        BoxChild(t, "PataDer", m.metal, new Vector3(CableEdge, (3f + barY) * 0.5f, 0f), new Vector3(0.045f, barY - 3f, 0.045f), Vector3.zero);
        float top = signY + SignNSize.y * 0.5f;
        BoxChild(t, "ColganteIzq", m.cable, new Vector3(-0.8f, (top + barY) * 0.5f, 0f), new Vector3(0.012f, barY - top, 0.012f), Vector3.zero);
        BoxChild(t, "ColganteDer", m.cable, new Vector3(0.8f, (top + barY) * 0.5f, 0f), new Vector3(0.012f, barY - top, 0.012f), Vector3.zero);
        Material first = FirstOr(m.signsN, m.cartelRojo);
        MeshRenderer front = SignFace(t, "CaraA", new Vector3(0f, signY, -0.004f), SignNSize, 0f, first);
        MeshRenderer back = SignFace(t, "CaraB", new Vector3(0f, signY, 0.004f), SignNSize, 180f, first);
        AddSignPick(t.gameObject, m.signsN, front, back);
    }

    // ------------------------------------------------------------------ la malla de los cables

    private static void CablesChild(Transform parent, string meshName, List<Span> spans, Mats m)
    {
        Mesh mesh = GetOrCreateMesh("Cables_" + meshName);
        BuildCableMesh(mesh, spans);
        GameObject go = MeshChild(parent, "Cables", mesh, m.cable, Vector3.zero);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    /// <summary>
    /// Cada cable es una parabola (parecida a la curva de un cable colgado) partida en tramos
    /// de unos 30 cm, y cada tramo es un tubo de 4 caras planas, como todo lo demas.
    /// </summary>
    private static void BuildCableMesh(Mesh mesh, List<Span> spans)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        foreach (Span s in spans)
        {
            float length = Vector3.Distance(s.a, s.b);
            int segments = Mathf.Max(4, Mathf.CeilToInt(length / 0.3f));
            Vector3 prev = s.a;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                // 4 t (1 - t) vale 0 en las puntas y 1 en el medio: ahi cuelga 'sag' metros.
                Vector3 p = Vector3.Lerp(s.a, s.b, t) + Vector3.down * (s.sag * 4f * t * (1f - t));
                AddTube(verts, norms, tris, prev, p, s.thickness * 0.5f);
                prev = p;
            }
        }

        mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        Apply(mesh, verts, norms, tris);
    }

    private static void AddTube(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 p, Vector3 q, float half)
    {
        Vector3 dir = (q - p).normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 1e-6f) side = Vector3.right; // tramo vertical
        side.Normalize();
        Vector3 up = Vector3.Cross(side, dir).normalized;

        // Cada tramo se alarga un poquito para que no queden rendijas en los quiebres.
        p -= dir * half;
        q += dir * half;
        Vector3[] corner = { (side + up) * half, (up - side) * half, (-side - up) * half, (side - up) * half };
        for (int k = 0; k < 4; k++)
        {
            Vector3 c0 = corner[k], c1 = corner[(k + 1) % 4];
            AddQuad(v, n, t, p + c0, p + c1, q + c1, q + c0, (c0 + c1).normalized);
        }
    }
}
