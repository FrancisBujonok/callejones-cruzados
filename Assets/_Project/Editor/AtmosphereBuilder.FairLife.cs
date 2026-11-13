using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Parte de AtmosphereBuilder (Paso 33): lo que hace que la feria se vea viva y no se pueda
/// saltear. Lo usa AtmosphereBuilder.Fair.cs.
///
/// - La cuadra de antes (x de -72 a -34): la parada donde se baja el jugador, un almacen abierto,
///   maquinas expendedoras, bicicletas, macetas, banderas que anuncian la feria y un carrito de
///   comida. Asi la feria no aparece "de la nada": se la ve venir.
/// - Detras del escenario (x de 34 a 50): una camioneta, cajones, sillas apiladas, banos quimicos y
///   gente que trabaja en la feria descansando.
/// - Los autos (FairTraffic, TrafficCar): sedanes, autitos, taxis, furgones, una camioneta y el
///   colectivo, low poly, con luces prendidas (atardecer) y sonido de motor.
/// - La gente (FairPerson, FairWalker): vendedores en cada puesto, clientes, gente sentada en las
///   mesas y los bancos, un musico, el del tambor y gente que pasea por el pasillo.
/// - El tapon del callejon (AlleyGate) y el recorrido del gato (CatTrail).
///
/// Todo low poly con dos paletas de 16 colores: FeriaPaleta (cosas y autos) y PersonasPaleta
/// (piel, pelo y ropa).
/// </summary>
public static partial class AtmosphereBuilder
{
    // ================================================================== paleta de la gente

    private const int PSkin0 = 0, PSkin1 = 1, PSkin2 = 2, PHairBlack = 3, PHairBrown = 4, PHairGray = 5,
        PWhite = 6, PRed = 7, PBlue = 8, PYellow = 9, PGreen = 10, PPink = 11, PJean = 12, PBlack = 13,
        PBeige = 14, PGray = 15;
    private static readonly string[] PeoplePaletteHex =
    {
        "#E0B896", "#C08A62", "#7A5034", "#1A1412", "#4A3020", "#A8A49C", "#E4E0D6", "#A83226",
        "#34507A", "#D4A43A", "#4E7444", "#C8809A", "#3C4E6A", "#22201E", "#B8A27E", "#6E6E6E",
    };

    private static Material peopleMat, tailLightMat;

    private static Material PeopleMaterial()
    {
        Texture2D tex = GetOrCreateTexture("PersonasPaleta", 16, 16, 7, (px, w, h, r) =>
        {
            for (int i = 0; i < 16; i++)
            {
                Color c = Hex(PeoplePaletteHex[i]);
                int cx = (i % 4) * 4, cy = h - 4 - (i / 4) * 4;
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                        px[(cy + y) * w + cx + x] = c;
            }
        });
        return Mat("PersonasPaleta", Color.white, 0.1f, baseMap: tex);
    }

    private static Material TailLightMaterial() =>
        Mat("LuzTrasera", Hex("#C8281E"), 0.3f, 0f, null, Hex("#E0301E") * 1.4f);

    // ================================================================== la cuadra de antes

    private const float KonbiniX0 = -56.4f, KonbiniX1 = -47.6f;
    private static readonly Vector3 YataiPos = new Vector3(-38.6f, 0f, 8.4f);

    private static void BuildFairApproach(Transform g, Mats m, FairMats f)
    {
        var d = new MeshDraft();

        // El almacen (abierto toda la noche): un volumen que sale un metro de la fachada, con
        // vidriera blanca prendida, el cartel de tres franjas y una puerta.
        const float kz0 = 10.9f, kz1 = 12.1f;
        float kxc = (KonbiniX0 + KonbiniX1) * 0.5f, kw = KonbiniX1 - KonbiniX0;
        Material konbiniLight = Mat("AlmacenLuz", Hex("#E6EEE4"), 0.3f, 0f, null, Hex("#DDE8DA") * 1.3f);
        BoxChild(g, "AlmacenVidriera", konbiniLight, new Vector3(kxc, 1.25f, kz0 + 0.02f), new Vector3(kw - 0.3f, 2.3f, 0.04f), Vector3.zero);
        d.Box(new Vector3(kxc, 2.7f, kz0 + 0.1f), new Vector3(kw, 0.6f, 0.2f), FBlanco);              // cartel
        d.Box(new Vector3(kxc, 2.88f, kz0 - 0.005f), new Vector3(kw, 0.12f, 0.01f), FVerde);
        d.Box(new Vector3(kxc, 2.72f, kz0 - 0.005f), new Vector3(kw, 0.08f, 0.01f), FNaranja);
        d.Box(new Vector3(kxc, 2.56f, kz0 - 0.005f), new Vector3(kw, 0.12f, 0.01f), FAzul);
        d.Box(new Vector3(kxc, 3.03f, (kz0 + kz1) * 0.5f), new Vector3(kw + 0.1f, 0.06f, kz1 - kz0 + 0.2f), FConcreto); // techito
        foreach (float x in new[] { KonbiniX0, KonbiniX1 })
            d.Box(new Vector3(x, 1.5f, (kz0 + kz1) * 0.5f), new Vector3(0.2f, 3f, kz1 - kz0), FConcreto);
        for (float x = KonbiniX0 + 1.45f; x < KonbiniX1 - 0.5f; x += 1.45f)
            d.Box(new Vector3(x, 1.25f, kz0 - 0.01f), new Vector3(0.06f, 2.3f, 0.04f), FMetal);       // marcos
        d.Box(new Vector3(kxc + 2.2f, 1.1f, kz0 - 0.02f), new Vector3(1.3f, 2.2f, 0.02f), FMetal);     // puerta
        d.Box(new Vector3(kxc, 0.06f, kz0 - 0.4f), new Vector3(kw, 0.12f, 0.8f), FConcreto);           // escalon
        AddSolid(g, "Almacen", new Vector3(kxc, 1.5f, (kz0 + kz1) * 0.5f), new Vector3(kw, 3f, kz1 - kz0));
        if (f.afiches.Count > 1)
        {
            SignFace(g, "AficheAlmacen", new Vector3(KonbiniX0 + 1.2f, 1.4f, kz0 - 0.03f), new Vector2(0.5f, 0.68f), 180f, f.afiches[1]);
            SignFace(g, "AficheAlmacen2", new Vector3(KonbiniX1 - 1.0f, 1.4f, kz0 - 0.03f), new Vector2(0.5f, 0.68f), 180f, f.afiches[f.afiches.Count - 1]);
        }

        // Maquinas expendedoras al lado del almacen, y una de cigarrillos.
        for (int i = 0; i < 3; i++)
        {
            float x = -45.6f + i * 1.05f;
            d.Box(new Vector3(x, 0.95f, 11.5f), new Vector3(0.98f, 1.9f, 0.75f), i == 2 ? FRojo : FBlanco);
            BoxChild(g, "PanelMaquina" + i, m.maquinaLuz, new Vector3(x, 1.25f, 11.115f), new Vector3(0.78f, 0.9f, 0.02f), Vector3.zero);
            d.Box(new Vector3(x, 0.35f, 11.11f), new Vector3(0.6f, 0.18f, 0.02f), FNegro);
        }
        AddSolid(g, "Maquinas", new Vector3(-44.55f, 0.95f, 11.5f), new Vector3(3.2f, 1.9f, 0.75f));
        d.Cylinder(new Vector3(-42.6f, 0f, 11.4f), 0.22f, 0.8f, 8, FAzul);                              // tacho de latas
        d.Cylinder(new Vector3(-42.1f, 0f, 11.4f), 0.22f, 0.8f, 8, FVerde);

        // Bicicletas estacionadas contra la pared.
        var rng = new System.Random(3301);
        for (int i = 0; i < 6; i++)
        {
            if (i == 3) continue;
            float x = -70f + i * 1.0f;
            int frame = new[] { FRojo, FAzul, FBlanco, FNegro, FVerde, FAmarillo }[rng.Next(6)];
            Bicycle(d, new Vector3(x, 0f, 10.7f), frame);
        }
        d.Box(new Vector3(-67.5f, 0.35f, 11.45f), new Vector3(6.2f, 0.05f, 0.05f), FMetal);              // barra del estacionamiento
        AddSolid(g, "Bicicletas", new Vector3(-67.5f, 0.5f, 10.8f), new Vector3(6.2f, 1f, 1.8f));

        // Macetas con plantas, contra las fachadas.
        foreach (Vector3 p in new[] { new Vector3(-61.5f, 0f, 11.3f), new Vector3(-58.2f, 0f, 11.3f), new Vector3(-40.5f, 0f, 11.3f), new Vector3(-35.2f, 0f, 11.3f) })
        {
            d.Box(p + new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.6f), FConcreto);
            FairBall(d, p + new Vector3(0f, 0.95f, 0f), 0.45f, FVerde);
            FairBall(d, p + new Vector3(0.15f, 1.25f, 0.05f), 0.3f, FVerdeClaro);
            AddSolid(g, "Maceta", p + new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 1f, 0.6f));
        }

        // Un cartel de la feria (mapa / programa) antes del arco, de cara a los que llegan (-X).
        d.Box(new Vector3(-35f, 1.0f, -3.9f), new Vector3(0.08f, 2.0f, 0.08f), FMetal);
        d.Box(new Vector3(-35f, 1.0f, -2.9f), new Vector3(0.08f, 2.0f, 0.08f), FMetal);
        d.Box(new Vector3(-35f, 1.55f, -3.4f), new Vector3(0.05f, 0.85f, 1.15f), FMaderaOscura);
        if (f.afiches.Count > 0)
        {
            Material a = f.afiches.Find(x => x.name.Contains("feria")) ?? f.afiches[0];
            SignFace(g, "ProgramaFeria", new Vector3(-35.03f, 1.55f, -3.4f), new Vector2(0.62f, 0.78f), 90f, a);
        }
        AddSolid(g, "CartelFeria", new Vector3(-35f, 1f, -3.4f), new Vector3(0.2f, 2f, 1.2f));

        // Banderas (nobori) en la vereda que anuncian la feria.
        int flag = 0;
        foreach (float x in new[] { -59.5f, -52.5f, -46f, -39.5f })
            NoboriAt(g, "NoboriCuadra" + flag, new Vector3(x, 0f, -5.75f), flag++ + 3, m, f);

        // Un carrito de comida (yatai) con su farolito.
        Yatai(g, d, YataiPos, m, f);

        FairDraft(g, "Cosas", d, f.paleta, true);
    }

    /// <summary>Una bicicleta mirando hacia -Z (la rueda delantera hacia la calle).</summary>
    private static void Bicycle(MeshDraft d, Vector3 at, int frame)
    {
        AxleCylinder(d, at + new Vector3(0f, 0.33f, -0.5f), 0.33f, 0.04f, 8, FNegro, FMetal);
        AxleCylinder(d, at + new Vector3(0f, 0.33f, 0.5f), 0.33f, 0.04f, 8, FNegro, FMetal);
        d.Box(at + new Vector3(0f, 0.55f, 0f), new Vector3(0.04f, 0.05f, 0.9f), frame);
        d.Box(at + new Vector3(0f, 0.6f, 0.2f), new Vector3(0.04f, 0.45f, 0.04f), frame);
        d.Box(at + new Vector3(0f, 0.86f, 0.22f), new Vector3(0.1f, 0.04f, 0.2f), FNegro);               // asiento
        d.Box(at + new Vector3(0f, 0.75f, -0.45f), new Vector3(0.04f, 0.4f, 0.04f), frame);
        d.Box(at + new Vector3(0f, 0.96f, -0.45f), new Vector3(0.5f, 0.03f, 0.03f), FMetal);            // manubrio
        d.Box(at + new Vector3(0f, 0.72f, -0.62f), new Vector3(0.28f, 0.14f, 0.2f), FMetal);            // canasto
    }

    private static void Yatai(Transform g, MeshDraft d, Vector3 at, Mats m, FairMats f)
    {
        // Mira hacia -Z (hacia la peatonal). Carro de madera de 2 m con dos ruedas, techo y faldon.
        d.Box(at + new Vector3(0f, 0.85f, 0f), new Vector3(2f, 0.1f, 0.9f), FMadera);                  // mostrador
        d.Box(at + new Vector3(0f, 0.5f, 0.1f), new Vector3(1.9f, 0.6f, 0.7f), FMaderaOscura);
        AxleCylinder(d, at + new Vector3(-1.05f, 0.35f, 0.1f), 0.35f, 0.08f, 8, FMaderaOscura, FMadera);
        AxleCylinder(d, at + new Vector3(1.05f, 0.35f, 0.1f), 0.35f, 0.08f, 8, FMaderaOscura, FMadera);
        foreach (float sx in new[] { -0.95f, 0.95f })
            d.Box(at + new Vector3(sx, 1.45f, 0.35f), new Vector3(0.06f, 1.2f, 0.06f), FMadera);
        d.Box(at + new Vector3(0f, 2.08f, 0.05f), new Vector3(2.3f, 0.06f, 1.3f), FMaderaOscura);       // techo
        d.Box(at + new Vector3(0f, 1.92f, -0.6f), new Vector3(2.3f, 0.26f, 0.02f), FRojo);              // faldon
        for (int k = 0; k < 5; k++)                                                                     // brochetas en la parrilla
            d.Box(at + new Vector3(-0.5f + k * 0.12f, 0.93f, -0.15f), new Vector3(0.04f, 0.04f, 0.3f), FNaranja);
        d.Box(at + new Vector3(-0.3f, 0.92f, -0.15f), new Vector3(0.8f, 0.04f, 0.36f), FNegro);
        AddSolid(g, "Carrito", at + new Vector3(0f, 0.9f, 0.1f), new Vector3(2.1f, 1.8f, 1.0f));
        // Farolito rojo colgado del techo.
        var lantern = new List<Matrix4x4> { Matrix4x4.TRS(at + new Vector3(0.75f, 1.45f, -0.55f), Quaternion.identity, Vector3.one) };
        CombinedChild(g, "FarolitoCarrito", GetOrCreateMesh("Chochin"), lantern, m.farolTela);
        if (f.carteles.Count > 0)
        {
            Material sign = f.carteles[f.carteles.Count / 3];
            Vector2 size = FitSign(sign, 1.6f, 0.24f);
            SignFace(g, "CartelCarrito", at + new Vector3(0f, 1.92f, -0.615f), size, 180f, sign);
        }
    }

    private static void NoboriAt(Transform g, string name, Vector3 at, int index, Mats m, FairMats f)
    {
        if (f.nobori.Count == 0) return;
        Transform t = Group(g, name);
        t.localPosition = at;
        BoxChild(t, "Mastil", m.metal, new Vector3(0f, 1.7f, 0f), new Vector3(0.04f, 3.4f, 0.04f), Vector3.zero);
        BoxChild(t, "Brazo", m.metal, new Vector3(0.27f, 3.3f, 0f), new Vector3(0.55f, 0.03f, 0.03f), Vector3.zero);
        Material mat = f.nobori[index % f.nobori.Count];
        Vector2 size = FitSign(mat, 0.5f, 2.2f);
        Vector3 c = new Vector3(0.29f, 3.25f - size.y * 0.5f, 0f);
        SignFace(t, "CaraA", c + new Vector3(0f, 0f, -0.006f), size, 0f, mat);
        SignFace(t, "CaraB", c + new Vector3(0f, 0f, 0.006f), size, 180f, mat);
    }

    // ================================================================== detras del escenario

    private static void BuildFairBackstage(Transform g, Mats m, FairMats f)
    {
        var d = new MeshDraft();

        // Cajones apilados y sillas plegadas.
        var rng = new System.Random(3302);
        foreach (Vector3 p in new[] { new Vector3(36.3f, 0f, 10.9f), new Vector3(37.0f, 0f, 10.9f), new Vector3(36.6f, 0f, 10.2f), new Vector3(44.6f, 0f, 11.2f) })
        {
            int stack = 2 + rng.Next(3);
            for (int k = 0; k < stack; k++)
                d.Box(p + new Vector3(0f, 0.17f + k * 0.34f, 0f), new Vector3(0.55f, 0.32f, 0.4f), rng.NextDouble() < 0.5 ? FPlasticoAzul : FRojo);
            AddSolid(g, "Cajones", p + new Vector3(0f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 0.45f));
        }
        for (int k = 0; k < 8; k++)
            d.Box(new Vector3(46.2f, 0.45f, 11.55f - k * 0.06f), new Vector3(0.45f, 0.9f, 0.04f), FMetal);
        AddSolid(g, "Sillas", new Vector3(46.2f, 0.45f, 11.35f), new Vector3(0.5f, 0.9f, 0.6f));

        // Dos banos quimicos contra la valla.
        foreach (float z in new[] { 10.7f, 9.4f })
        {
            d.Box(new Vector3(49.0f, 1.15f, z), new Vector3(1.1f, 2.3f, 1.15f), FAzul);
            d.Box(new Vector3(49.0f, 2.33f, z), new Vector3(1.15f, 0.06f, 1.2f), FBlanco);
            d.Box(new Vector3(48.44f, 1.1f, z), new Vector3(0.02f, 1.9f, 0.8f), FPlasticoAzul);            // puerta
            AddSolid(g, "Bano", new Vector3(49.0f, 1.15f, z), new Vector3(1.1f, 2.3f, 1.15f));
        }

        // Un generador mas, para el escenario, con sus cables.
        d.Box(new Vector3(36.6f, 0.4f, -3.2f), new Vector3(1.2f, 0.8f, 0.7f), FAmarillo);
        d.Box(new Vector3(36.6f, 0.85f, -3.2f), new Vector3(1.0f, 0.1f, 0.5f), FNegro);
        AddSolid(g, "Generador", new Vector3(36.6f, 0.45f, -3.2f), new Vector3(1.2f, 0.9f, 0.7f));
        var cables = new List<Span>
        {
            new Span(new Vector3(36.6f, 0.06f, -3.2f), new Vector3(33.9f, 0.06f, -0.6f), 0f, 0.02f),
            new Span(new Vector3(36.6f, 0.06f, -3.2f), new Vector3(34.5f, 0.06f, 3.5f), 0f, 0.02f),
        };
        CablesChild(g, "Feria_CablesEscenario", cables, m);

        // Asientos de los que descansan (cajones) y mesa plegable con botellas.
        d.Box(new Vector3(39.0f, 0.21f, 5.6f), new Vector3(0.45f, 0.42f, 0.4f), FPlasticoAzul);
        d.Box(new Vector3(40.9f, 0.21f, 5.6f), new Vector3(0.45f, 0.42f, 0.4f), FRojo);
        d.Box(new Vector3(39.95f, 0.72f, 5.6f), new Vector3(0.9f, 0.04f, 0.6f), FBlanco);
        d.Box(new Vector3(39.95f, 0.36f, 5.6f), new Vector3(0.06f, 0.72f, 0.06f), FMetal);
        for (int k = 0; k < 3; k++) d.Cylinder(new Vector3(39.75f + k * 0.18f, 0.74f, 5.55f), 0.04f, 0.22f, 6, FVerdeClaro);
        AddSolid(g, "Mesita", new Vector3(39.95f, 0.4f, 5.6f), new Vector3(2.4f, 0.8f, 0.7f));

        FairDraft(g, "Cosas", d, f.paleta, true);

        // La camioneta de la feria, estacionada (la misma de la calle, quieta).
        GameObject truck = BuildCarModel(g, "CamionetaEstacionada", CarModel.Camioneta, FBlanco, FBlanco, f,
            out _, out _, out _, out _, out _);
        truck.transform.localPosition = new Vector3(42.2f, 0f, 9.4f);
        truck.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        AddSolid(g, "Camioneta", new Vector3(42.2f, 0.9f, 9.4f), new Vector3(3.5f, 1.8f, 1.6f));
    }

    // ================================================================== autos

    private enum CarModel { Sedan, Compacto, Taxi, Furgon, Camioneta, Colectivo }

    private static void BuildFairTraffic(Transform world, FairMats f, TrafficSignals signals = null)
    {
        Transform g = Group(world, "Transito");
        AudioClip carSound = GetOrCreateSound("Auto_Motor", 4f, 1301, true, SynthCarEngine);
        AudioClip busSound = GetOrCreateSound("Colectivo_Motor", 4f, 1302, true, SynthBusEngine);

        var cars = new List<TrafficCar>();
        TrafficCar bus = null;
        int n = 0;
        void Add(CarModel model, int color, int color2, float weight, Vector2 cruise)
        {
            GameObject go = BuildCarModel(g, model + "_" + n, model, color, color2, f,
                out Transform body, out Transform[] wheels, out float length, out float radius, out float height);
            go.transform.localPosition = new Vector3(200f + n * 12f, 0f, -30f); // fuera de la vista hasta que salga
            AudioSource engine = NewSource(go.transform, "Motor", model == CarModel.Colectivo ? busSound : carSound, true, 120);
            engine.transform.localPosition = new Vector3(0f, 0.6f, length * 0.3f);
            engine.volume = model == CarModel.Colectivo ? 0.6f : 0.42f;
            engine.pitch = model == CarModel.Compacto ? 1.15f : model == CarModel.Furgon ? 0.9f : 1f;
            Make3D(engine, 3f, model == CarModel.Colectivo ? 55f : 42f);
            engine.dopplerLevel = 0.4f;
            TrafficCar car = go.AddComponent<TrafficCar>();
            car.Setup(length, radius, wheels, body, engine, cruise, weight);
            cars.Add(car);
            if (model == CarModel.Colectivo) bus = car;
            n++;
        }
        var normal = new Vector2(7f, 11f);
        Add(CarModel.Sedan, FBlanco, FBlanco, 1f, normal);
        Add(CarModel.Sedan, FMetal, FMetal, 1f, normal);
        Add(CarModel.Sedan, FAzul, FAzul, 1f, normal);
        Add(CarModel.Sedan, FNegro, FNegro, 0.8f, normal);
        Add(CarModel.Compacto, FRojo, FBlanco, 1f, new Vector2(6.5f, 9.5f));
        Add(CarModel.Compacto, FCrema, FBlanco, 1f, new Vector2(6.5f, 9.5f));
        Add(CarModel.Compacto, FVerdeClaro, FBlanco, 0.8f, new Vector2(6.5f, 9.5f));
        Add(CarModel.Taxi, FAmarillo, FNegro, 0.9f, normal);
        Add(CarModel.Taxi, FVerde, FCrema, 0.7f, normal);
        Add(CarModel.Furgon, FBlanco, FAzul, 0.7f, new Vector2(6.5f, 9f));
        Add(CarModel.Furgon, FPlasticoAzul, FBlanco, 0.5f, new Vector2(6.5f, 9f));
        Add(CarModel.Camioneta, FBlanco, FBlanco, 0.6f, new Vector2(6f, 8.5f));
        Add(CarModel.Colectivo, FBlanco, FVerde, 0.12f, new Vector2(6f, 8f));
        // Paso 35: mas autos, porque ahora tambien pasan por la transversal del cruce.
        Add(CarModel.Sedan, FCrema, FCrema, 1f, normal);
        Add(CarModel.Sedan, FRojo, FRojo, 0.8f, normal);
        Add(CarModel.Compacto, FBlanco, FAzul, 1f, new Vector2(6.5f, 9.5f));
        Add(CarModel.Taxi, FAmarillo, FNegro, 0.8f, normal);
        Add(CarModel.Furgon, FBlanco, FRojo, 0.6f, new Vector2(6.5f, 9f));

        // Mano izquierda (como en Japon): hacia el este por el carril de la vereda de la feria.
        // Paso 35: la calle es mas larga (los autos aparecen y desaparecen en la niebla, a 150 m)
        // y hay un cruce con semaforos: cada carril que pasa por el tiene su linea de detencion.
        float rx = (CrossRoadX0 + CrossRoadX1) * 0.5f;
        var lanes = new[]
        {
            new FairTraffic.Lane { name = "HaciaElEste", z = -8.5f, direction = 1f, fromX = -155f, toX = 150f, every = new Vector2(4f, 11f),
                signalGroup = 0, stopAt = CrossRoadX0 - 1.3f },
            new FairTraffic.Lane { name = "HaciaElOeste", z = -12.5f, direction = -1f, fromX = 150f, toX = -155f, every = new Vector2(4f, 11f),
                signalGroup = 0, stopAt = CrossRoadX1 + 1.3f },
            new FairTraffic.Lane { name = "TransversalAlNorte", alongZ = true, z = rx - 2f, direction = 1f, fromX = -150f, toX = 150f,
                every = new Vector2(7f, 16f), signalGroup = 1, stopAt = -15.6f },
            new FairTraffic.Lane { name = "TransversalAlSur", alongZ = true, z = rx + 2f, direction = -1f, fromX = 150f, toX = -150f,
                every = new Vector2(7f, 16f), signalGroup = 1, stopAt = 5.3f },
        };
        g.gameObject.AddComponent<FairTraffic>().Setup(lanes, cars.ToArray(), bus, 0, BusStopX, signals);
    }

    /// <summary>
    /// Un auto low poly mirando hacia +Z, con el piso en y = 0. Carroceria con la paleta de la
    /// feria, faros (crema, con brillo), luces traseras rojas y cuatro (o seis) ruedas aparte para
    /// que giren. color2: el segundo color (franja, techo o caja).
    /// </summary>
    private static GameObject BuildCarModel(Transform parent, string name, CarModel model, int color, int color2, FairMats f,
        out Transform body, out Transform[] wheels, out float length, out float wheelRadius, out float height)
    {
        if (tailLightMat == null) tailLightMat = TailLightMaterial();
        Transform root = Group(parent, name);
        body = Group(root, "Carroceria");
        var d = new MeshDraft();
        var front = new MeshDraft();
        var rear = new MeshDraft();
        var wheelSpots = new List<Vector3>();
        const int Glass = FNegro;

        void Lights(float w, float y, float halfLength, float size = 0.28f)
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                front.Box(new Vector3(sx * w, y, halfLength + 0.005f), new Vector3(size, 0.12f, 0.03f), FBlanco);
                rear.Box(new Vector3(sx * w, y + 0.02f, -halfLength - 0.005f), new Vector3(size, 0.12f, 0.03f), FRojo);
            }
            d.Box(new Vector3(0f, y - 0.25f, halfLength + 0.02f), new Vector3(w * 2f + 0.4f, 0.16f, 0.08f), FNegro);   // paragolpes
            d.Box(new Vector3(0f, y - 0.25f, -halfLength - 0.02f), new Vector3(w * 2f + 0.4f, 0.16f, 0.08f), FNegro);
            d.Box(new Vector3(0f, y - 0.12f, -halfLength - 0.012f), new Vector3(0.32f, 0.11f, 0.01f), FBlanco);      // patente
        }

        switch (model)
        {
            case CarModel.Sedan:
            case CarModel.Taxi:
                length = 4.4f; wheelRadius = 0.31f; height = 1.45f;
                d.Box(new Vector3(0f, 0.62f, 0f), new Vector3(1.7f, 0.56f, 4.4f), color);
                d.Box(new Vector3(0f, 1.14f, -0.2f), new Vector3(1.54f, 0.5f, 2.3f), Glass);
                d.Box(new Vector3(0f, 1.41f, -0.25f), new Vector3(1.5f, 0.06f, 1.9f), model == CarModel.Taxi ? color2 : color);
                foreach (float sx in new[] { -0.76f, 0.76f })
                    foreach (float z in new[] { 0.88f, -0.2f, -1.3f })
                        d.Box(new Vector3(sx, 1.14f, z), new Vector3(0.05f, 0.5f, 0.1f), color);
                d.Box(new Vector3(0f, 0.66f, 2.205f), new Vector3(0.7f, 0.14f, 0.02f), FNegro);                 // parrilla
                if (model == CarModel.Taxi)
                {
                    d.Box(new Vector3(0f, 0.78f, 0f), new Vector3(1.72f, 0.06f, 4.2f), color2);                 // franja
                    front.Box(new Vector3(0f, 1.53f, -0.3f), new Vector3(0.5f, 0.17f, 0.18f), FBlanco);         // cartel TAXI
                }
                Lights(0.62f, 0.72f, 2.2f);
                wheelSpots.AddRange(new[] { new Vector3(-0.78f, 0.31f, 1.35f), new Vector3(0.78f, 0.31f, 1.35f), new Vector3(-0.78f, 0.31f, -1.35f), new Vector3(0.78f, 0.31f, -1.35f) });
                break;

            case CarModel.Compacto:
                length = 3.4f; wheelRadius = 0.27f; height = 1.6f;
                d.Box(new Vector3(0f, 0.58f, 0f), new Vector3(1.48f, 0.5f, 3.4f), color);
                d.Box(new Vector3(0f, 1.15f, -0.2f), new Vector3(1.4f, 0.64f, 2.6f), Glass);
                d.Box(new Vector3(0f, 1.5f, -0.2f), new Vector3(1.42f, 0.07f, 2.7f), color2);
                foreach (float sx in new[] { -0.69f, 0.69f })
                    foreach (float z in new[] { 1.05f, 0f, -1.45f })
                        d.Box(new Vector3(sx, 1.15f, z), new Vector3(0.05f, 0.64f, 0.1f), color);
                d.Box(new Vector3(0f, 1.15f, -1.495f), new Vector3(1.0f, 0.5f, 0.02f), color);                  // porton
                Lights(0.55f, 0.68f, 1.7f, 0.22f);
                wheelSpots.AddRange(new[] { new Vector3(-0.66f, 0.27f, 1.1f), new Vector3(0.66f, 0.27f, 1.1f), new Vector3(-0.66f, 0.27f, -1.1f), new Vector3(0.66f, 0.27f, -1.1f) });
                break;

            case CarModel.Furgon:
                length = 4.7f; wheelRadius = 0.32f; height = 1.95f;
                d.Box(new Vector3(0f, 0.75f, 0f), new Vector3(1.75f, 0.85f, 4.7f), color);
                d.Box(new Vector3(0f, 1.55f, -0.3f), new Vector3(1.72f, 0.75f, 4.1f), color);
                d.Box(new Vector3(0f, 1.52f, 1.755f), new Vector3(1.6f, 0.55f, 0.04f), Glass);                   // parabrisas
                foreach (float sx in new[] { -0.865f, 0.865f })
                    d.Box(new Vector3(sx, 1.52f, 1.15f), new Vector3(0.02f, 0.5f, 0.95f), Glass);
                d.Box(new Vector3(0f, 0.95f, 0f), new Vector3(1.77f, 0.1f, 4.6f), color2);                     // franja
                Lights(0.65f, 0.78f, 2.35f);
                wheelSpots.AddRange(new[] { new Vector3(-0.8f, 0.32f, 1.55f), new Vector3(0.8f, 0.32f, 1.55f), new Vector3(-0.8f, 0.32f, -1.55f), new Vector3(0.8f, 0.32f, -1.55f) });
                break;

            case CarModel.Camioneta:
                length = 3.4f; wheelRadius = 0.27f; height = 1.85f;
                d.Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.25f, 3.3f), FNegro);                     // chasis
                d.Box(new Vector3(0f, 1.05f, 1.1f), new Vector3(1.48f, 1.2f, 1.2f), color);                    // cabina
                d.Box(new Vector3(0f, 1.32f, 1.705f), new Vector3(1.38f, 0.5f, 0.03f), Glass);
                foreach (float sx in new[] { -0.745f, 0.745f })
                    d.Box(new Vector3(sx, 1.32f, 1.15f), new Vector3(0.02f, 0.45f, 0.8f), Glass);
                d.Box(new Vector3(0f, 0.66f, -0.6f), new Vector3(1.48f, 0.1f, 2.0f), color2);                  // caja
                foreach (float sx in new[] { -0.72f, 0.72f })
                    d.Box(new Vector3(sx, 0.86f, -0.6f), new Vector3(0.04f, 0.3f, 2.0f), color2);
                d.Box(new Vector3(0f, 0.86f, -1.58f), new Vector3(1.48f, 0.3f, 0.04f), color2);
                d.Box(new Vector3(-0.3f, 0.88f, -0.4f), new Vector3(0.55f, 0.32f, 0.4f), FPlasticoAzul);       // carga
                d.Box(new Vector3(0.3f, 0.88f, -0.9f), new Vector3(0.55f, 0.32f, 0.4f), FMadera);
                Lights(0.55f, 0.75f, 1.7f, 0.22f);
                wheelSpots.AddRange(new[] { new Vector3(-0.66f, 0.27f, 1.05f), new Vector3(0.66f, 0.27f, 1.05f), new Vector3(-0.66f, 0.27f, -1.05f), new Vector3(0.66f, 0.27f, -1.05f) });
                break;

            default: // Colectivo
                length = 10.5f; wheelRadius = 0.5f; height = 3.1f;
                d.Box(new Vector3(0f, 0.8f, 0f), new Vector3(2.5f, 0.9f, 10.5f), color2);
                d.Box(new Vector3(0f, 2.15f, 0f), new Vector3(2.5f, 1.8f, 10.5f), color);
                foreach (float sx in new[] { -1.255f, 1.255f })
                    d.Box(new Vector3(sx, 2.1f, -0.4f), new Vector3(0.02f, 0.9f, 8.6f), Glass);                   // ventanillas
                d.Box(new Vector3(0f, 2.0f, 5.255f), new Vector3(2.3f, 1.3f, 0.02f), Glass);                     // parabrisas
                d.Box(new Vector3(-1.256f, 1.45f, 3.7f), new Vector3(0.02f, 2.1f, 1.0f), Glass);                 // puerta (lado de la vereda)
                d.Box(new Vector3(0f, 3.07f, 0f), new Vector3(2.3f, 0.06f, 9.6f), FMetal);                       // techo
                front.Box(new Vector3(0f, 2.82f, 5.26f), new Vector3(1.6f, 0.24f, 0.02f), FAmarillo);           // cartel del recorrido
                Lights(0.95f, 0.85f, 5.25f, 0.34f);
                wheelSpots.AddRange(new[] { new Vector3(-1.1f, 0.5f, 3.4f), new Vector3(1.1f, 0.5f, 3.4f), new Vector3(-1.1f, 0.5f, -3.4f), new Vector3(1.1f, 0.5f, -3.4f) });
                break;
        }
        // Sombra de abajo: un rectangulo oscuro apenas sobre el piso (de lejos los "apoya").
        d.Box(new Vector3(0f, 0.02f, 0f), new Vector3(1.4f, 0.01f, length * 0.85f), FNegro);

        FairDraft(body, "Malla", d, f.paleta, true);
        FairDraft(body, "Faros", front, model == CarModel.Taxi || model == CarModel.Colectivo ? f.farolitoAmarillo : f.farola, false);
        FairDraft(body, "LucesTraseras", rear, tailLightMat, false);

        // Ruedas: una malla (cilindro acostado sobre X) por radio, compartida.
        var wheel = new MeshDraft();
        AxleCylinder(wheel, Vector3.zero, wheelRadius, model == CarModel.Colectivo ? 0.3f : 0.2f, 8, FNegro, FMetal);
        var wheelMesh = new Mesh { name = name + "_Rueda" };
        Apply(wheelMesh, wheel.v, wheel.n, wheel.t, wheel.uv);
        var list = new List<Transform>();
        for (int i = 0; i < wheelSpots.Count; i++)
        {
            GameObject w = MeshChild(root, "Rueda" + i, wheelMesh, f.paleta, wheelSpots[i]);
            w.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            list.Add(w.transform);
        }
        wheels = list.ToArray();
        return root.gameObject;
    }

    /// <summary>Un cilindro acostado sobre el eje X (ruedas): costado de un color, tapas de otro.</summary>
    private static void AxleCylinder(MeshDraft d, Vector3 c, float r, float width, int sides, int palette, int capPalette)
    {
        float hw = width * 0.5f;
        Vector2 uv = PaletteUV(palette);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var p0 = new Vector3(0f, Mathf.Cos(a0) * r, Mathf.Sin(a0) * r);
            var p1 = new Vector3(0f, Mathf.Cos(a1) * r, Mathf.Sin(a1) * r);
            d.Quad(c + p0 + Vector3.left * hw, c + p0 + Vector3.right * hw, c + p1 + Vector3.right * hw, c + p1 + Vector3.left * hw,
                (p0 + p1).normalized, uv);
            FairTri(d, c + Vector3.right * hw, c + p0 + Vector3.right * hw, c + p1 + Vector3.right * hw, Vector3.right, capPalette);
            FairTri(d, c + Vector3.left * hw, c + p1 + Vector3.left * hw, c + p0 + Vector3.left * hw, Vector3.left, capPalette);
        }
    }

    // Motor de auto: armonicos de 40 Hz (el "ronroneo" de un cuatro cilindros a bajas vueltas) con
    // un latido a 20 Hz, mas el ruido de las gomas en el asfalto (una banda ancha cerca de 500 Hz).
    // 4 s: 40 Hz y 20 Hz dan vueltas enteras, asi que el loop empalma. Dos pasadas por el filtro,
    // como el viento, para que el ruido tambien empalme.
    private static void SynthCarEngine(float[] o, System.Random r) => SynthEngine(o, r, 40.0, 0.38f, 520f);
    // Colectivo: mas grave (27.5 Hz, un diesel grande) y con mas rumor.
    private static void SynthBusEngine(float[] o, System.Random r) => SynthEngine(o, r, 27.5, 0.3f, 360f);

    private static void SynthEngine(float[] o, System.Random r, double f0, float tire, float tireCenter)
    {
        int n = o.Length;
        var white = new float[n];
        for (int i = 0; i < n; i++) white[i] = Bipolar(r);
        var phase = new float[13];
        for (int h = 0; h < phase.Length; h++) phase[h] = (float)(r.NextDouble() * System.Math.PI * 2.0);
        var road = new Svf();
        float lowA = OnePole(140f), low = 0f;

        for (int lap = 0; lap < 2; lap++)
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int h = 1; h <= 12; h++)
                    sum += (h % 2 == 0 ? 0.65f : 1f) / Mathf.Pow(h, 0.9f) * Sine(f0 * h, i, phase[h]);
                float beat = 0.78f + 0.22f * Sine(f0 * 0.5, i);
                float wobble = 1f + 0.05f * Lfo(3, i, n) + 0.03f * Lfo(7, i, n, 1.1f);
                road.Set(tireCenter * (1f + 0.12f * Lfo(2, i, n)), 0.6f);
                float hiss = road.BandN(white[i]);
                low += lowA * (white[i] - low);
                if (lap == 1) o[i] = sum * beat * wobble * 0.5f + hiss * tire * 2.2f + low * 1.5f;
            }
    }

    // ================================================================== la gente

    private class Look
    {
        public int skin, hair, top, bottom, shoes, style;
        public bool apron, band, dress;
        public float scale = 1f;
    }

    private class Rig { public Transform root, head, armL, armR, legL, legR; }

    private enum Pose { Standing, Walking, Seated }

    private static Look RandomLook(System.Random r, bool vendor)
    {
        var l = new Look();
        double s = r.NextDouble();
        l.skin = s < 0.55 ? PSkin0 : s < 0.88 ? PSkin1 : PSkin2;
        l.hair = new[] { PHairBlack, PHairBlack, PHairBlack, PHairBrown, PHairGray }[r.Next(5)];
        int[] tops = { PWhite, PRed, PBlue, PYellow, PGreen, PPink, PBlack, PBeige, PGray, PWhite, PBlue };
        l.top = tops[r.Next(tops.Length)];
        l.bottom = new[] { PJean, PJean, PBlack, PBeige, PGray }[r.Next(5)];
        l.shoes = r.NextDouble() < 0.5 ? PBlack : PWhite;
        l.style = r.Next(4);
        l.dress = !vendor && l.style == 1 && r.NextDouble() < 0.4;
        l.scale = 0.92f + (float)r.NextDouble() * 0.14f;
        if (vendor)
        {
            l.apron = r.NextDouble() < 0.55;
            l.band = r.NextDouble() < 0.4;
            if (l.band && l.style == 2) l.style = 0;
            if (r.NextDouble() < 0.35) l.top = r.NextDouble() < 0.5 ? PBlue : PRed; // happi (chaqueta de feria)
        }
        return l;
    }

    /// <summary>
    /// Una persona low poly mirando hacia +Z, con los pies en y = 0 (sentada: la cadera a 0.52 m,
    /// la altura de un banco). Cuerpo, cabeza y brazos (y piernas, si camina) son hijos aparte con
    /// el pivote en el cuello, los hombros y la cadera, para poder moverlos.
    /// </summary>
    private static Rig BuildPerson(Transform parent, string name, Vector3 pos, float yaw, Look l, Pose pose)
    {
        if (peopleMat == null) peopleMat = PeopleMaterial();
        var rig = new Rig();
        Transform root = Group(parent, name);
        root.localPosition = pos;
        root.localRotation = Quaternion.Euler(0f, yaw, 0f);
        root.localScale = Vector3.one * l.scale;
        rig.root = root;

        float dy = pose == Pose.Seated ? -0.34f : 0f;
        int legColor = l.dress ? l.skin : l.bottom;
        var body = new MeshDraft();
        foreach (float sx in new[] { -0.09f, 0.09f })
        {
            if (pose == Pose.Standing)
            {
                body.Box(new Vector3(sx, 0.41f, 0f), new Vector3(0.14f, 0.8f, 0.16f), legColor);
                body.Box(new Vector3(sx, 0.035f, 0.04f), new Vector3(0.13f, 0.07f, 0.24f), l.shoes);
            }
            else if (pose == Pose.Seated)
            {
                body.Box(new Vector3(sx, 0.5f, 0.2f), new Vector3(0.14f, 0.14f, 0.42f), l.dress ? l.top : l.bottom);
                body.Box(new Vector3(sx, 0.25f, 0.4f), new Vector3(0.13f, 0.46f, 0.14f), legColor);
                body.Box(new Vector3(sx, 0.035f, 0.46f), new Vector3(0.13f, 0.07f, 0.24f), l.shoes);
            }
        }
        body.Box(new Vector3(0f, 0.86f + dy, pose == Pose.Seated ? 0.03f : 0f), new Vector3(0.36f, 0.12f, 0.21f), l.dress ? l.top : l.bottom);
        if (l.dress && pose != Pose.Seated) body.Box(new Vector3(0f, 0.68f, 0f), new Vector3(0.42f, 0.36f, 0.26f), l.top);
        body.Box(new Vector3(0f, 1.1f + dy, 0f), new Vector3(0.4f, 0.5f, 0.23f), l.top);
        if (l.apron) body.Box(new Vector3(0f, 0.98f + dy, 0.122f), new Vector3(0.34f, 0.62f, 0.02f), PWhite);
        body.Box(new Vector3(0f, 1.4f + dy, 0f), new Vector3(0.1f, 0.1f, 0.1f), l.skin);
        FairDraft(root, "Cuerpo", body, peopleMat, true);

        Transform head = Group(root, "Cabeza");
        head.localPosition = new Vector3(0f, 1.45f + dy, 0f);
        var h = new MeshDraft();
        h.Box(new Vector3(0f, 0.13f, 0.01f), new Vector3(0.2f, 0.24f, 0.21f), l.skin);
        switch (l.style)
        {
            case 0: // corto
                h.Box(new Vector3(0f, 0.26f, -0.005f), new Vector3(0.22f, 0.06f, 0.23f), l.hair);
                h.Box(new Vector3(0f, 0.17f, -0.1f), new Vector3(0.22f, 0.18f, 0.04f), l.hair);
                break;
            case 1: // largo
                h.Box(new Vector3(0f, 0.26f, -0.005f), new Vector3(0.22f, 0.06f, 0.23f), l.hair);
                h.Box(new Vector3(0f, 0.06f, -0.105f), new Vector3(0.23f, 0.4f, 0.05f), l.hair);
                foreach (float sx in new[] { -0.105f, 0.105f })
                    h.Box(new Vector3(sx, 0.14f, -0.03f), new Vector3(0.02f, 0.24f, 0.14f), l.hair);
                break;
            case 2: // gorra
                int cap = l.top == PWhite ? PBlue : l.top;
                h.Box(new Vector3(0f, 0.27f, 0f), new Vector3(0.23f, 0.07f, 0.23f), cap);
                h.Box(new Vector3(0f, 0.245f, 0.15f), new Vector3(0.21f, 0.02f, 0.1f), cap);
                h.Box(new Vector3(0f, 0.15f, -0.1f), new Vector3(0.21f, 0.12f, 0.03f), l.hair);
                break;
            default: // muy corto
                h.Box(new Vector3(0f, 0.255f, -0.01f), new Vector3(0.21f, 0.03f, 0.215f), l.hair);
                break;
        }
        if (l.band) h.Box(new Vector3(0f, 0.2f, 0.005f), new Vector3(0.215f, 0.04f, 0.225f), PWhite);
        foreach (float sx in new[] { -0.05f, 0.05f })
            h.Box(new Vector3(sx, 0.14f, 0.117f), new Vector3(0.035f, 0.022f, 0.01f), PBlack);
        FairDraft(head, "Malla", h, peopleMat, true);
        rig.head = head;

        foreach (int side in new[] { -1, 1 })
        {
            Transform arm = Group(root, side < 0 ? "BrazoIzq" : "BrazoDer");
            arm.localPosition = new Vector3(side * 0.25f, 1.33f + dy, 0f);
            var a = new MeshDraft();
            a.Box(new Vector3(0f, -0.24f, 0f), new Vector3(0.1f, 0.48f, 0.12f), l.top);
            a.Box(new Vector3(0f, -0.53f, 0f), new Vector3(0.085f, 0.1f, 0.1f), l.skin);
            FairDraft(arm, "Malla", a, peopleMat, true);
            if (side < 0) rig.armL = arm; else rig.armR = arm;
        }

        if (pose == Pose.Walking)
            foreach (int side in new[] { -1, 1 })
            {
                Transform leg = Group(root, side < 0 ? "PiernaIzq" : "PiernaDer");
                leg.localPosition = new Vector3(side * 0.09f, 0.82f, 0f);
                var lg = new MeshDraft();
                lg.Box(new Vector3(0f, -0.41f, 0f), new Vector3(0.14f, 0.8f, 0.16f), legColor);
                lg.Box(new Vector3(0f, -0.785f, 0.04f), new Vector3(0.13f, 0.07f, 0.24f), l.shoes);
                FairDraft(leg, "Malla", lg, peopleMat, true);
                if (side < 0) rig.legL = leg; else rig.legR = leg;
            }
        return rig;
    }

    /// <summary>Persona quieta con FairPerson y un collider (para no atravesarla).</summary>
    private static Rig StillPerson(Transform g, string name, Vector3 pos, float yaw, Look l, Pose pose, FairPerson.Role role, bool collider = true)
    {
        Rig rig = BuildPerson(g, name, pos, yaw, l, pose);
        rig.root.gameObject.AddComponent<FairPerson>().Setup(role, rig.head, rig.armL, rig.armR);
        if (collider)
        {
            CapsuleCollider c = rig.root.gameObject.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0f, pose == Pose.Seated ? 0.65f : 0.9f, pose == Pose.Seated ? 0.15f : 0f);
            c.height = pose == Pose.Seated ? 1.3f : 1.8f;
            c.radius = 0.25f;
        }
        return rig;
    }

    private static void BuildFairPeople(Transform world, FairMats f)
    {
        Transform g = Group(world, "Gente");
        var rng = new System.Random(3303);
        int n = 0;
        string Name(string what) => what + "_" + (n++);

        // 1. Vendedores (uno por puesto) y clientes (en algunos).
        Transform vendors = Group(g, "Vendedores");
        Transform customers = Group(g, "Clientes");
        foreach (StallInfo s in FairStalls)
        {
            Look look = RandomLook(rng, true);
            if (s.type == 8)
            {
                look.top = PBlack; look.style = 1; look.hair = PHairGray; look.apron = false; look.band = false;
                StillPerson(vendors, "Adivina", s.vendor, s.yaw, look, Pose.Seated, FairPerson.Role.Seated, false);
                continue;
            }
            StillPerson(vendors, Name("Vendedor"), s.vendor, s.yaw, look, Pose.Standing, FairPerson.Role.Vendor, false);

            Quaternion q = Quaternion.Euler(0f, s.yaw, 0f);
            if (s.type == 4)
            {
                // Peces: chicos sentados en los banquitos, con la red en la mano (una mama atras).
                for (int k = 0; k < 2; k++)
                {
                    Look kid = RandomLook(rng, false);
                    kid.scale = 0.62f;
                    StillPerson(customers, Name("Nene"), s.pos + q * new Vector3(-0.8f + k * 0.8f, 0f, 0.3f), s.yaw + 180f, kid, Pose.Seated, FairPerson.Role.Customer);
                }
                StillPerson(customers, Name("Cliente"), s.pos + q * new Vector3(-0.4f, 0f, 0.95f), s.yaw + 180f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Customer);
                continue;
            }
            if (rng.NextDouble() > 0.5) continue;
            int count = rng.NextDouble() < 0.35 ? 2 : 1;
            for (int k = 0; k < count; k++)
            {
                float x = count == 1 ? (float)(rng.NextDouble() - 0.5) * 1.2f : (k == 0 ? -0.45f : 0.45f);
                if (s.game) x = k == 0 ? 1.1f : -1.1f; // Paso 36: el lugar del medio es para jugar
                Look c = RandomLook(rng, false);
                if (k == 1 && rng.NextDouble() < 0.3) c.scale = 0.66f; // un nene con su papa o su mama
                StillPerson(customers, Name("Cliente"), s.pos + q * new Vector3(x, 0f, 0.72f), s.yaw + 180f + (float)(rng.NextDouble() - 0.5) * 20f,
                    c, Pose.Standing, FairPerson.Role.Customer);
            }
        }

        // 2. Sentados: en las mesas (frente al puesto 3 de la fila norte) y en los bancos.
        Transform seated = Group(g, "Sentados");
        float tx = StallFirstX + 3 * StallStep;
        foreach (var (cx, cz, people) in new[] { (tx - 1.1f, 5.2f, 3), (tx + 1.1f, 8.4f, 2), (tx - 0.8f, 9.8f, 2) })
            for (int k = 0; k < people; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                var p = new Vector3(cx + Mathf.Cos(a) * 0.72f, -0.06f, cz + Mathf.Sin(a) * 0.72f); // banquitos de 42 cm
                float face = Mathf.Atan2(cx - p.x, cz - p.z) * Mathf.Rad2Deg;
                StillPerson(seated, Name("Mesa"), p, face, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
            }
        StillPerson(seated, Name("Banco"), new Vector3(-17.3f, 0f, -4.62f), 0f, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
        StillPerson(seated, Name("Banco"), new Vector3(-16.6f, 0f, -4.62f), 0f, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
        StillPerson(seated, Name("Banco"), new Vector3(22.4f, 0f, -4.62f), 0f, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
        StillPerson(seated, Name("Parada"), new Vector3(BusStopX + 0.7f, 0f, -5.0f), 180f, RandomLook(rng, false), Pose.Seated, FairPerson.Role.Seated);
        StillPerson(seated, Name("Parada"), new Vector3(BusStopX - 1.9f, 0f, -4.7f), 180f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Idle);

        // 3. El musico del banco (con su guitarra) y dos que lo escuchan.
        Look musician = RandomLook(rng, false);
        musician.style = 2;
        Rig mus = StillPerson(g, "Musico", new Vector3(-4f, 0f, -4.62f), 0f, musician, Pose.Seated, FairPerson.Role.Seated);
        var guitar = new MeshDraft();
        guitar.Box(new Vector3(-0.05f, 0.72f, 0.24f), new Vector3(0.32f, 0.38f, 0.1f), PBeige);
        guitar.Box(new Vector3(0.36f, 0.86f, 0.24f), new Vector3(0.5f, 0.05f, 0.05f), PBlack);
        guitar.Box(new Vector3(-0.05f, 0.74f, 0.295f), new Vector3(0.08f, 0.08f, 0.01f), PBlack);
        FairDraft(mus.root, "Guitarra", guitar, peopleMat, true);
        StillPerson(g, Name("Escucha"), new Vector3(-4.6f, 0f, -2.9f), 190f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Idle);
        StillPerson(g, Name("Escucha"), new Vector3(-3.3f, 0f, -3.1f), 165f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Idle);

        // 4. El del tambor, en el escenario (golpea cuando suena el tambor).
        Look drummer = RandomLook(rng, true);
        drummer.top = PRed; drummer.band = true; drummer.apron = false;
        StillPerson(g, "Tambor", new Vector3(30.1f, 0.5f, 1.5f), 90f, drummer, Pose.Standing, FairPerson.Role.Drummer, false);

        // 5. La cuadra de antes: el del carrito, sus clientes, uno en las maquinas y uno en la puerta
        //    del almacen.
        Transform approach = Group(g, "CuadraDeAntes");
        StillPerson(approach, "VendedorCarrito", YataiPos + new Vector3(0.3f, 0f, 0.75f), 180f, RandomLook(rng, true), Pose.Standing, FairPerson.Role.Vendor, false);
        StillPerson(approach, Name("ClienteCarrito"), YataiPos + new Vector3(-0.4f, 0f, -1.0f), 0f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Customer);
        StillPerson(approach, Name("ClienteCarrito"), YataiPos + new Vector3(0.45f, 0f, -1.1f), 350f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Customer);
        StillPerson(approach, Name("Maquina"), new Vector3(-44.5f, 0f, 10.5f), 0f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Customer);
        StillPerson(approach, Name("Almacen"), new Vector3(-49.0f, 0f, 9.9f), 200f, RandomLook(rng, false), Pose.Standing, FairPerson.Role.Idle);

        // 6. Detras del escenario: dos descansando y uno al lado de la camioneta.
        Transform back = Group(g, "DetrasDelEscenario");
        Look w1 = RandomLook(rng, true), w2 = RandomLook(rng, true), w3 = RandomLook(rng, true);
        StillPerson(back, Name("Trabajador"), new Vector3(39.0f, -0.06f, 5.6f), 90f, w1, Pose.Seated, FairPerson.Role.Seated);
        StillPerson(back, Name("Trabajador"), new Vector3(40.9f, -0.06f, 5.6f), -90f, w2, Pose.Seated, FairPerson.Role.Seated);
        StillPerson(back, Name("Trabajador"), new Vector3(42.0f, 0f, 7.9f), 10f, w3, Pose.Standing, FairPerson.Role.Idle);

        // 7. Los que pasean: ida por un lado del pasillo, vuelta por el otro, y algunos que llegan
        //    desde la cuadra de antes.
        Transform walkers = Group(g, "Paseando");
        Vector3[] longLoop =
        {
            new Vector3(-58f, 0f, -1.5f), new Vector3(-44f, 0f, 0.6f), new Vector3(-30.5f, 0f, 0.9f), new Vector3(-15f, 0f, 0.6f),
            new Vector3(0f, 0f, 0.8f), new Vector3(15f, 0f, 0.6f), new Vector3(26f, 0f, 1.0f), new Vector3(26.5f, 0f, 2.4f),
            new Vector3(10f, 0f, 2.6f), new Vector3(-8f, 0f, 2.4f), new Vector3(-30.5f, 0f, 2.3f), new Vector3(-44f, 0f, 3.2f),
            new Vector3(-58f, 0f, 1.5f),
        };
        Vector3[] shortLoop =
        {
            new Vector3(-26f, 0f, 0.7f), new Vector3(-6f, 0f, 0.5f), new Vector3(12f, 0f, 0.8f), new Vector3(25f, 0f, 0.6f),
            new Vector3(25.5f, 0f, 2.5f), new Vector3(5f, 0f, 2.6f), new Vector3(-14f, 0f, 2.3f), new Vector3(-26.5f, 0f, 2.4f),
        };
        Vector3[] Reverse(Vector3[] a) { var r = (Vector3[])a.Clone(); System.Array.Reverse(r); return r; }
        var routes = new[] { longLoop, shortLoop, Reverse(longLoop), Reverse(shortLoop) };
        for (int k = 0; k < 11; k++)
        {
            Vector3[] route = routes[k % routes.Length];
            int start = rng.Next(route.Length);
            Vector3 p = Vector3.Lerp(route[start], route[(start + 1) % route.Length], (float)rng.NextDouble());
            Look look = RandomLook(rng, false);
            if (k % 5 == 4) look.scale = 0.66f;
            Rig rig = BuildPerson(walkers, Name("Paseante"), p, 0f, look, Pose.Walking);
            rig.root.gameObject.AddComponent<FairWalker>().Setup(route, (start + 1) % route.Length, 0.95f + (float)rng.NextDouble() * 0.4f,
                rig.head, rig.armL, rig.armR, rig.legL, rig.legR);
            CapsuleCollider c = rig.root.gameObject.AddComponent<CapsuleCollider>();
            c.center = new Vector3(0f, 0.9f, 0f);
            c.height = 1.8f;
            c.radius = 0.25f;
            Rigidbody body = rig.root.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    // ================================================================== el callejon tapado y el gato

    /// <summary>Un carro de vendedor cruzado, cajones apilados y una valla chica en la boca del callejon.</summary>
    private static AlleyGate BuildAlleyGate(Transform g, Mats m, FairMats f)
    {
        Transform t = Group(g, "Tapon");
        float z = FairBackZ + 0.7f;
        var d = new MeshDraft();
        // El carro (de madera, con lona azul encima), de costado.
        d.Box(new Vector3(8.0f, 0.75f, z), new Vector3(1.9f, 0.7f, 0.85f), FMadera);
        d.Box(new Vector3(8.0f, 1.15f, z), new Vector3(1.95f, 0.12f, 0.9f), FPlasticoAzul);
        AxleCylinder(d, new Vector3(7.3f, 0.32f, z - 0.45f), 0.32f, 0.06f, 8, FMaderaOscura, FMadera);
        d.Box(new Vector3(9.1f, 0.85f, z), new Vector3(0.6f, 0.04f, 0.04f), FMadera);                       // varas
        // Cajones apilados al lado.
        for (int k = 0; k < 4; k++)
            d.Box(new Vector3(9.55f, 0.17f + k * 0.34f, z + 0.1f), new Vector3(0.55f, 0.32f, 0.4f), k % 2 == 0 ? FRojo : FPlasticoAzul);
        // Una valla chica a rayas, por delante.
        d.Box(new Vector3(8.5f, 0.75f, z - 0.75f), new Vector3(2.6f, 0.12f, 0.04f), FRojo);
        d.Box(new Vector3(8.5f, 0.55f, z - 0.75f), new Vector3(2.6f, 0.12f, 0.04f), FBlanco);
        foreach (float x in new[] { 7.3f, 9.7f })
            d.Box(new Vector3(x, 0.4f, z - 0.75f), new Vector3(0.05f, 0.8f, 0.3f), FNegro);
        FairDraft(t, "Malla", d, f.paleta, true);
        BoxCollider col = t.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(8.5f, 1.2f, z - 0.3f);
        col.size = new Vector3(AlleyX1 - AlleyX0, 2.4f, 1.4f);

        var holder = new GameObject("Llave");
        holder.transform.SetParent(g, false);
        AlleyGate gate = holder.AddComponent<AlleyGate>();
        gate.Setup(new[] { t.gameObject });
        return gate;
    }

    /// <summary>
    /// Las cuatro apariciones del gato (ver CatTrail). Las tres primeras se escapan por detras de
    /// algo, para que no se lo vea desaparecer; la cuarta es la que entra al callejon.
    /// </summary>
    private static void BuildCatTrail(Transform world, FairMats f, Transform player, AlleyGate gate, GameObject exit, Vector3[] alleyPath)
    {
        Transform g = Group(world, "RecorridoDelGato");
        LeadingCat c0 = BuildLeadingCat(g, "Gato1_Llegada", f, player, new Vector3(-48f, 0f, 5.2f), -90f,
            new[] { new Vector3(-40f, 0f, 6.6f), new Vector3(-29f, 0f, 7.2f), new Vector3(-24.2f, 0f, 7.2f) }, 7f);
        LeadingCat c1 = BuildLeadingCat(g, "Gato2_Escenario", f, player, new Vector3(29.4f, 0.5f, 3.4f), -90f,
            new[] { new Vector3(31.5f, 0.5f, 3.6f), new Vector3(34.8f, 0.5f, 3.4f) }, 6f);
        LeadingCat c2 = BuildLeadingCat(g, "Gato3_Adivina", f, player, new Vector3(12.2f, 0f, 2.9f), 180f,
            new[] { new Vector3(10.3f, 0f, 4.8f), new Vector3(11.3f, 0f, 7.2f), new Vector3(12.8f, 0f, 7.4f) }, 5f);
        LeadingCat c3 = BuildLeadingCat(g, "Gato4_Callejon", f, player, new Vector3((AlleyX0 + AlleyX1) * 0.5f, 0f, FairBackZ + 0.6f), 180f,
            alleyPath, 6f);
        // Las que vienen despues arrancan apagadas: al prenderse, empiezan de cero (Start).
        c1.gameObject.SetActive(false);
        c2.gameObject.SetActive(false);
        c3.gameObject.SetActive(false);
        g.gameObject.AddComponent<CatTrail>().Setup(new[] { c0, c1, c2, c3 }, gate, exit);
    }
}
