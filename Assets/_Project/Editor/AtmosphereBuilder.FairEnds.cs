using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Parte de AtmosphereBuilder (Paso 35): las puntas de la feria, sin vallas. Lo usa
/// AtmosphereBuilder.Fair.cs.
///
/// - El cruce (detras del comienzo): la transversal de x = -86 a -78 con semaforos en las cuatro
///   esquinas (TrafficSignals), la senda peatonal por la que el jugador acaba de cruzar, los
///   semaforos de peatones con su pajarito (el sonido que avisa que se puede cruzar) y, en la
///   esquina de la vereda de la transversal, macetas que cierran el paso hacia el norte.
/// - La plaza (del otro lado del cruce, x de -152 a -89): pasto, fuente, bancos, arboles,
///   juegos, faroles, un cerco bajo y gente.
/// - La calle despues del escenario (x de 50 a 142): la peatonal sigue, con puestos tapados con
///   lona (ahi termina la feria), un kiosco de diarios, bicicletas, bancos con gente y gente que
///   camina. No se llega, pero se ve seguir.
///
/// El personaje no cruza esos bordes: hay paredes invisibles y, si insiste, piensa por que
/// (ThoughtZone, en AtmosphereBuilder.FairWorld.cs).
/// </summary>
public static partial class AtmosphereBuilder
{
    // ================================================================== el cruce

    private static TrafficSignals BuildFairCrossing(Transform g, Mats m, FairMats f)
    {
        Material red = Mat("SemaforoRojo", Hex("#E0301E"), 0.3f, 0f, null, Hex("#FF3A20") * 2.2f);
        Material amber = Mat("SemaforoAmarillo", Hex("#F0A020"), 0.3f, 0f, null, Hex("#FFB030") * 2.2f);
        Material green = Mat("SemaforoVerde", Hex("#30C0A0"), 0.3f, 0f, null, Hex("#40F0C0") * 2.2f);
        var mr = new List<GameObject>(); var my = new List<GameObject>(); var mg = new List<GameObject>();
        var cr = new List<GameObject>(); var cy = new List<GameObject>(); var cg = new List<GameObject>();
        var ws = new List<GameObject>(); var wg = new List<GameObject>();
        var poles = new MeshDraft();

        // Las cuatro esquinas: un poste con el semaforo de la calle principal (mirando a los autos
        // que llegan por esa mano) y el de la transversal.
        float west = CrossRoadX0 - 0.7f, east = CrossRoadX1 + 0.7f, south = -15.2f, north = -5.8f;
        foreach (var (cx, cz) in new[] { (west, south), (east, south), (west, north), (east, north) })
        {
            poles.Cylinder(new Vector3(cx, 0f, cz), 0.08f, 3.5f, 6, FNegro);
            VehicleHead(g, "SemaforoPrincipal", new Vector3(cx, 3.0f, cz), cx < -82f ? -90f : 90f, red, amber, green, mr, my, mg, f);
            VehicleHead(g, "SemaforoTransversal", new Vector3(cx, 2.1f, cz), cz < -10f ? 180f : 0f, red, amber, green, cr, cy, cg, f);
        }
        AddSolid(g, "PosteEsquina", new Vector3(east, 1.5f, north), new Vector3(0.3f, 3f, 0.3f));

        // Semaforos de peatones en las puntas de la senda (z de 0 a 4), con su boton.
        AudioSource chirp = null;
        foreach (var (px, pz, yaw) in new[] { (east, -0.3f, -90f), (west, 4.3f, 90f) })
        {
            poles.Cylinder(new Vector3(px, 0f, pz), 0.07f, 2.8f, 6, FNegro);
            poles.Box(new Vector3(px, 1.1f, pz), new Vector3(0.14f, 0.24f, 0.14f), FAmarillo);
            Transform head = Group(g, "SemaforoPeatones");
            head.localPosition = new Vector3(px, 2.35f, pz);
            head.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var d = new MeshDraft();
            d.Box(Vector3.zero, new Vector3(0.34f, 0.66f, 0.24f), FNegro);
            d.Box(new Vector3(0f, 0.15f, 0.125f), new Vector3(0.24f, 0.24f, 0.01f), FMetal);
            d.Box(new Vector3(0f, -0.15f, 0.125f), new Vector3(0.24f, 0.24f, 0.01f), FMetal);
            FairDraft(head, "Caja", d, f.paleta, true);
            ws.Add(BoxChild(head, "Parado", red, new Vector3(0f, 0.15f, 0.135f), new Vector3(0.22f, 0.22f, 0.02f), Vector3.zero));
            wg.Add(BoxChild(head, "Camina", green, new Vector3(0f, -0.15f, 0.135f), new Vector3(0.22f, 0.22f, 0.02f), Vector3.zero));
            if (chirp == null)
            {
                chirp = NewSource(head, "Pajarito", GetOrCreateSound("Cruce_Pajarito", 1.2f, 1401, true, SynthCrossingChirp), true, 110);
                chirp.volume = 0.22f;
                Make3D(chirp, 2f, 22f);
            }
        }
        AddSolid(g, "PostePeatones", new Vector3(east, 1.4f, -0.3f), new Vector3(0.3f, 2.8f, 0.3f));
        FairDraft(g, "Postes", poles, f.paleta, true);

        // La esquina de la vereda de la transversal (al norte): macetas y una bicicleta atada.
        var corner = new MeshDraft();
        foreach (float x in new[] { FairWestX + 0.7f, CrossWalkX1 - 0.8f })
        {
            corner.Box(new Vector3(x, 0.35f, FairBackZ - 0.4f), new Vector3(1.1f, 0.7f, 0.8f), FConcreto);
            FairBall(corner, new Vector3(x, 1.0f, FairBackZ - 0.4f), 0.5f, FVerde);
            FairBall(corner, new Vector3(x + 0.15f, 1.35f, FairBackZ - 0.35f), 0.32f, FVerdeClaro);
        }
        Bicycle(corner, new Vector3((FairWestX + CrossWalkX1) * 0.5f, 0f, FairBackZ - 0.5f), FRojo);
        FairDraft(g, "Esquina", corner, f.paleta, true);

        var go = new GameObject("Semaforos");
        go.transform.SetParent(g, false);
        TrafficSignals signals = go.AddComponent<TrafficSignals>();
        signals.Setup(mr.ToArray(), my.ToArray(), mg.ToArray(), cr.ToArray(), cy.ToArray(), cg.ToArray(), ws.ToArray(), wg.ToArray(), chirp);
        return signals;
    }

    /// <summary>Un semaforo de autos: caja negra con viseras, tres luces (las prendidas son objetos aparte).</summary>
    private static void VehicleHead(Transform parent, string name, Vector3 pos, float yaw, Material red, Material amber, Material green,
        List<GameObject> reds, List<GameObject> ambers, List<GameObject> greens, FairMats f)
    {
        Transform head = Group(parent, name);
        head.localPosition = pos;
        head.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var d = new MeshDraft();
        d.Box(new Vector3(0f, 0f, 0.05f), new Vector3(0.34f, 0.95f, 0.26f), FNegro);
        d.Box(new Vector3(0f, 0f, -0.1f), new Vector3(0.5f, 1.1f, 0.03f), FNegro);
        float[] ys = { 0.3f, 0f, -0.3f };
        foreach (float y in ys)
        {
            d.Box(new Vector3(0f, y, 0.185f), new Vector3(0.2f, 0.2f, 0.01f), FMetal);           // lente apagada
            d.Box(new Vector3(0f, y + 0.13f, 0.27f), new Vector3(0.26f, 0.03f, 0.16f), FNegro); // visera
        }
        d.Box(new Vector3(0f, 0.2f, -0.2f), new Vector3(0.06f, 0.06f, 0.25f), FNegro);         // brazo al poste
        FairDraft(head, "Caja", d, f.paleta, true);
        reds.Add(BoxChild(head, "Rojo", red, new Vector3(0f, ys[0], 0.195f), new Vector3(0.21f, 0.21f, 0.02f), Vector3.zero));
        ambers.Add(BoxChild(head, "Amarillo", amber, new Vector3(0f, ys[1], 0.195f), new Vector3(0.21f, 0.21f, 0.02f), Vector3.zero));
        greens.Add(BoxChild(head, "Verde", green, new Vector3(0f, ys[2], 0.195f), new Vector3(0.21f, 0.21f, 0.02f), Vector3.zero));
    }

    // El "pajarito" de los semaforos de peatones (como en Japon): dos piadas cortas, una que sube y
    // otra que baja, y silencio. 1.2 s de loop.
    private static void SynthCrossingChirp(float[] o, System.Random r)
    {
        void Chirp(float start, float f0, float f1, float len, float amp)
        {
            int s0 = Mathf.RoundToInt(start * SampleRate), n = Mathf.RoundToInt(len * SampleRate);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < o.Length; i++)
            {
                float u = i / (float)n;
                phase += 2.0 * System.Math.PI * Mathf.Lerp(f0, f1, u) / SampleRate;
                float env = Mathf.Sin(u * Mathf.PI);
                o[s0 + i] += amp * env * (float)System.Math.Sin(phase);
            }
        }
        Chirp(0.00f, 2800f, 3500f, 0.07f, 1f);
        Chirp(0.16f, 3300f, 2500f, 0.09f, 0.9f);
    }

    // ================================================================== la plaza

    private static void BuildFairPlaza(Transform g, Mats m, FairMats f)
    {
        Texture2D grassTex = GetOrCreateTexture("Pasto", 32, 32, 71, (pix, tw, th, r) =>
        {
            for (int i = 0; i < pix.Length; i++)
            {
                float n = Rand(r);
                pix[i] = Color.Lerp(Hex("#4A6A34"), Hex("#6E8A42"), n * n) * (0.9f + 0.1f * Rand(r));
            }
        });
        UseMipmaps(grassTex);
        Material grass = Mat("Pasto", Color.white, 0.1f, baseMap: grassTex);
        const float x0 = -147f, x1 = -93f;
        float xc = (x0 + x1) * 0.5f, w = x1 - x0;
        FloorPiece(g, "PastoNorte", grass, new Vector3(xc, 0.03f, 8.1f), new Vector3(w, 0f, 6.2f));
        FloorPiece(g, "PastoSur", grass, new Vector3(xc, 0.03f, -3.0f), new Vector3(w, 0f, 4.4f));

        var d = new MeshDraft();
        var glow = new MeshDraft();
        var rng = new System.Random(3501);

        // Cerco bajo de arbustos alrededor del pasto, con huecos para entrar.
        foreach (float z in new[] { 4.85f, -0.65f })
            for (float x = x0; x < x1; x += 6f)
                if (Mathf.Abs(x + 3f - xc) > 4f) d.Box(new Vector3(x + 2.6f, 0.3f, z), new Vector3(5.2f, 0.6f, 0.5f), FVerde);
        // Arbustos sobre la vereda de la transversal, con el hueco de la senda.
        for (float z = -6f; z < 12f; z += 2f)
            if (z + 2f < 0f || z > 4f) d.Box(new Vector3(CrossWalkX0 - 0.4f, 0.35f, z + 1f), new Vector3(0.6f, 0.7f, 1.9f), FVerde);

        // La fuente en el medio del camino.
        var water = new MeshDraft();
        d.Cylinder(new Vector3(xc, 0f, 2f), 2.1f, 0.5f, 12, FConcreto);
        water.Cylinder(new Vector3(xc, 0.42f, 2f), 1.85f, 0.03f, 12, FBlanco);
        d.Cylinder(new Vector3(xc, 0f, 2f), 0.28f, 1.3f, 8, FConcreto);
        d.Cylinder(new Vector3(xc, 1.3f, 2f), 0.75f, 0.15f, 10, FConcreto);
        water.Cylinder(new Vector3(xc, 1.44f, 2f), 0.62f, 0.02f, 10, FBlanco);
        AddSolid(g, "Fuente", new Vector3(xc, 0.5f, 2f), new Vector3(4.2f, 1f, 4.2f));

        // Arboles en el pasto.
        for (int i = 0; i < 9; i++)
        {
            bool northSide = i % 3 != 0;
            float x = x0 + 3f + (float)rng.NextDouble() * (w - 6f);
            float z = northSide ? 6.2f + (float)rng.NextDouble() * 4.5f : -4.5f + (float)rng.NextDouble() * 3f;
            if (Mathf.Abs(x - (-138f)) < 5f && northSide) continue; // lugar de los juegos
            d.Cylinder(new Vector3(x, 0f, z), 0.16f, 2.6f, 6, FMaderaOscura);
            FairBall(d, new Vector3(x, 3.3f, z), 1.3f + (float)rng.NextDouble() * 0.5f, FVerde);
            FairBall(d, new Vector3(x + 0.4f, 4.1f, z - 0.3f), 0.8f, FVerdeClaro);
        }

        // Bancos sobre el camino y faroles.
        foreach (float x in new[] { -140f, -130f, -108f, -99f })
        {
            FairBench(d, new Vector3(x, 0f, 4.35f), 180f);
            FairBench(d, new Vector3(x + 2f, 0f, -0.15f), 0f);
        }
        foreach (float x in new[] { -143f, -126f, -112f, -96f })
        {
            d.Cylinder(new Vector3(x, 0f, 4.5f), 0.07f, 3.6f, 6, FNegro);
            d.Box(new Vector3(x, 3.65f, 4.5f), new Vector3(0.36f, 0.12f, 0.36f), FNegro);
            glow.Box(new Vector3(x, 3.45f, 4.5f), new Vector3(0.3f, 0.3f, 0.3f), FBlanco);
        }

        // Juegos: un tobogan y una hamaca.
        const float px = -138f, pz = 8.6f;
        foreach (float sx in new[] { -0.4f, 0.4f })
            d.Box(new Vector3(px - 1.6f + sx, 0.9f, pz), new Vector3(0.06f, 1.8f, 0.06f), FRojo);    // escalera
        for (int k = 0; k < 5; k++) d.Box(new Vector3(px - 1.6f, 0.3f + k * 0.35f, pz), new Vector3(0.8f, 0.04f, 0.06f), FRojo);
        d.Box(new Vector3(px - 1.2f, 1.8f, pz), new Vector3(0.9f, 0.08f, 0.9f), FAmarillo);           // descanso
        Vector2 slide = PaletteUV(FAzul);
        d.Quad(new Vector3(px - 0.75f, 1.8f, pz - 0.35f), new Vector3(px - 0.75f, 1.8f, pz + 0.35f), new Vector3(px + 1.4f, 0.2f, pz + 0.35f), new Vector3(px + 1.4f, 0.2f, pz - 0.35f), new Vector3(0.6f, 1f, 0f), slide);
        d.Quad(new Vector3(px - 0.75f, 1.8f, pz - 0.35f), new Vector3(px - 0.75f, 1.8f, pz + 0.35f), new Vector3(px + 1.4f, 0.2f, pz + 0.35f), new Vector3(px + 1.4f, 0.2f, pz - 0.35f), new Vector3(-0.6f, -1f, 0f), slide);
        const float hx = -131.5f;
        foreach (float sz in new[] { -1.2f, 1.2f })
            foreach (float sx in new[] { -0.5f, 0.5f })
                d.Box(new Vector3(hx + sx * 0.6f, 1.1f, pz + sz), new Vector3(0.06f, 2.2f, 0.06f), FAzul);
        d.Box(new Vector3(hx, 2.2f, pz), new Vector3(0.08f, 0.08f, 2.6f), FAzul);
        foreach (float sz in new[] { -0.45f, 0.45f })
        {
            foreach (float s in new[] { -0.18f, 0.18f })
                d.Box(new Vector3(hx, 1.45f, pz + sz + s), new Vector3(0.02f, 1.5f, 0.02f), FMetal);
            d.Box(new Vector3(hx, 0.68f, pz + sz), new Vector3(0.3f, 0.05f, 0.42f), FMaderaOscura);
        }

        FairDraft(g, "Plaza", d, f.paleta, true);
        FairDraft(g, "Agua", water, f.agua, false);
        FairDraft(g, "LucesPlaza", glow, f.farola, false);

        // Gente en la plaza: sentados, una mama con su nene en los juegos y dos que dan la vuelta
        // a la fuente.
        Transform people = Group(g, "Gente");
        StillPerson(people, "BancoPlaza0", new Vector3(-140.3f, 0f, 4.37f), 180f, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
        StillPerson(people, "BancoPlaza1", new Vector3(-106.4f, 0f, -0.17f), 0f, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
        StillPerson(people, "BancoPlaza2", new Vector3(-105.6f, 0f, -0.17f), 0f, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
        StillPerson(people, "Mama", new Vector3(-133.2f, 0f, 7.0f), 60f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Idle);
        Look kid = RandomLook(rng, false);
        kid.scale = 0.62f;
        StillPerson(people, "Nene", new Vector3(hx, 0.38f, pz - 0.45f), 90f, kid, Pose.Seated, FairPerson.Role.Seated, false);
        var loop = new List<Vector3>();
        for (int k = 0; k < 10; k++)
        {
            float a = k * Mathf.PI * 2f / 10f;
            loop.Add(new Vector3(xc + Mathf.Cos(a) * 3.3f, 0f, 2f + Mathf.Sin(a) * 1.6f));
        }
        for (int k = 0; k < 2; k++)
            Walker(people, "PaseoPlaza" + k, loop.ToArray(), k * 5, 0.9f + k * 0.15f, RandomLook(rng, false));
    }

    /// <summary>Una persona que camina por 'route' (FairWalker), con collider.</summary>
    private static void Walker(Transform parent, string name, Vector3[] route, int start, float speed, Look look)
    {
        Rig rig = BuildPerson(parent, name, route[start % route.Length], 0f, look, Pose.Walking);
        rig.root.gameObject.AddComponent<FairWalker>().Setup(route, (start + 1) % route.Length, speed,
            rig.head, rig.armL, rig.armR, rig.legL, rig.legR);
        CapsuleCollider c = rig.root.gameObject.AddComponent<CapsuleCollider>();
        c.center = new Vector3(0f, 0.9f, 0f);
        c.height = 1.8f;
        c.radius = 0.25f;
        Rigidbody body = rig.root.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    // ================================================================== la calle despues del escenario

    private static void BuildFairEastStreet(Transform g, Mats m, FairMats f)
    {
        var d = new MeshDraft();
        var lit = new MeshDraft();
        var rng = new System.Random(3502);

        // Puestos tapados con lona: la feria se termina aca (dos filas cortas, cerradas).
        for (int i = 0; i < 6; i++)
            foreach (float z in new[] { 5f, -2f })
            {
                float x = 56f + i * 3.4f;
                if (rng.NextDouble() < 0.2) continue;
                int tarp = rng.NextDouble() < 0.6 ? FPlasticoAzul : FVerde;
                d.Box(new Vector3(x, 1.0f, z), new Vector3(2.9f, 2.0f, 1.9f), tarp);
                d.Box(new Vector3(x, 2.05f, z), new Vector3(3.0f, 0.12f, 2.0f), tarp);
                for (int k = 0; k < 3; k++)
                    d.Box(new Vector3(x - 1.0f + k, 1.0f, z + (z > 1.5f ? -0.97f : 0.97f)), new Vector3(0.03f, 2.0f, 0.03f), FNegro); // sogas
                if (rng.NextDouble() < 0.5)
                    d.Box(new Vector3(x + 1.7f, 0.17f, z), new Vector3(0.5f, 0.32f, 0.4f), FMadera);
            }

        // Kiosco de diarios, con revistas y su luz.
        const float kx = 88f, kz = 9.6f;
        d.Box(new Vector3(kx, 1.2f, kz), new Vector3(2.4f, 2.4f, 1.8f), FVerde);
        d.Box(new Vector3(kx, 2.5f, kz - 1.1f), new Vector3(2.6f, 0.08f, 0.9f), FAmarillo);
        lit.Box(new Vector3(kx, 1.4f, kz - 0.91f), new Vector3(1.8f, 1.0f, 0.02f), FBlanco);
        for (int k = 0; k < 8; k++)
            d.Box(new Vector3(kx - 0.9f + (k % 4) * 0.6f, 0.55f + (k / 4) * 0.35f, kz - 1.0f), new Vector3(0.45f, 0.3f, 0.04f),
                new[] { FRojo, FAzul, FAmarillo, FBlanco, FRosa }[rng.Next(5)]);

        // Bicicletas, macetas y maquinas contra las fachadas.
        for (int i = 0; i < 5; i++) Bicycle(d, new Vector3(95f + i * 1.1f, 0f, 10.8f), new[] { FRojo, FAzul, FBlanco, FNegro }[rng.Next(4)]);
        foreach (float x in new[] { 80f, 118f, 134f })
        {
            d.Box(new Vector3(x, 0.3f, 11.3f), new Vector3(0.8f, 0.6f, 0.6f), FConcreto);
            FairBall(d, new Vector3(x, 0.95f, 11.3f), 0.45f, FVerde);
        }
        for (int i = 0; i < 2; i++)
        {
            d.Box(new Vector3(124.5f + i * 1.05f, 0.95f, 11.5f), new Vector3(0.98f, 1.9f, 0.75f), FBlanco);
            lit.Box(new Vector3(124.5f + i * 1.05f, 1.25f, 11.115f), new Vector3(0.78f, 0.9f, 0.02f), FBlanco);
        }

        FairDraft(g, "Cosas", d, f.paleta, true);
        FairDraft(g, "Luces", lit, m.ventanaLuz, false);

        // Gente: dos que charlan, uno en el kiosco, dos sentados en los bancos y tres caminando.
        Transform people = Group(g, "Gente");
        StillPerson(people, "Charla0", new Vector3(104f, 0f, 6f), 80f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Customer);
        StillPerson(people, "Charla1", new Vector3(105.1f, 0f, 6.2f), 260f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Customer);
        StillPerson(people, "Kiosco", new Vector3(kx + 0.3f, 0f, kz - 1.9f), 0f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Customer);
        StillPerson(people, "Banco0", new Vector3(87.3f, 0f, -4.62f), 0f, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
        StillPerson(people, "Banco1", new Vector3(113.4f, 0f, -4.62f), 0f, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
        Vector3[] route =
        {
            new Vector3(56f, 0f, 1.0f), new Vector3(90f, 0f, 0.6f), new Vector3(130f, 0f, 1.0f),
            new Vector3(130.5f, 0f, 3.2f), new Vector3(90f, 0f, 3.4f), new Vector3(56.5f, 0f, 3.0f),
        };
        for (int k = 0; k < 3; k++)
            Walker(people, "Paseante" + k, route, k * 2, 0.95f + k * 0.12f, RandomLook(rng, false));
    }
}
