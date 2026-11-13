using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Parte de AtmosphereBuilder (Paso 34): los edificios de la feria, variados. Lo usa
/// AtmosphereBuilder.FairWorld.cs (BuildFairWorld).
///
/// Antes cada tramo era un BuildBuildingSide de los callejones: una pared plana con el edificio
/// abajo y un relleno oscuro hasta 30 m arriba (en el laberinto eso esconde lo que hay detras).
/// En la calle quedaba un muro parejo. Ahora cada tramo es un bloque con volumen (10 m de fondo),
/// su altura, sus paredes laterales a la vista y su techo:
///
///   Alto        5 a 8 pisos, ventanas del atlas, azotea con tanques, casita de la escalera,
///               antenas y a veces un cartel grande arriba.
///   Terraza     3 o 4 pisos con terraza: baranda, macetas, ropa colgada, sillas y mesa.
///   Casa        2 pisos de revoque de color, ventanas con marco (algunas prendidas), postigos,
///               maceteros, tejado a dos aguas (tejas rojas, grises, azules o marrones).
///   Balcones    4 a 6 pisos con un balcon por ventana (plantas, ropa, aires).
///   Escalera    4 a 6 pisos con escalera de incendio en un costado.
///   Templo      (solo enfrente) un lote con torii, faroles de piedra, sendero y un templito.
///   Estacion.   (solo enfrente) un estacionamiento con autos, lineas y el cartel P.
///
/// Las plantas bajas: locales como antes (FairStorefront), solo persianas (sin carteles), la
/// casa (puerta, ventana con rejas, macetas, bicicleta) o un local especial: restaurante con
/// faroles y noren, farmacia con la cruz verde, peluqueria con el poste a rayas o karaoke con
/// neon. Cada local especial sale una vez por fila, cerca de la feria.
///
/// Detras de cada fila hay otra mas lejos (el "fondo"), de 4 a 9 pisos y mas oscura: arriba de
/// las casas bajas se ve una linea de ciudad y no el cielo vacio.
///
/// Espacio de lado, como los edificios de los callejones: la fachada en x = 0 mirando a +X, el
/// bloque hacia -X (fondo), a lo largo de z (centrado) y el piso en y = 0.
/// </summary>
public static partial class AtmosphereBuilder
{
    private enum BlockKind { Alto, Terraza, Casa, Balcones, Escalera, Templo, Estacionamiento }
    private enum GroundKind { Locales, Persianas, Casa, Restaurante, Farmacia, Peluqueria, Karaoke }

    private const float BlockDepth = 10f;
    private static Material neonPink, greenCross;

    private static void BuildFairBlocks(Transform g, Mats m, FairMats f)
    {
        var rng = new System.Random(3401);
        Mesh shutter = GetOrCreateMesh("Persiana");
        neonPink = Mat("NeonRosa", Hex("#F060B0"), 0.3f, 0f, null, Hex("#FF60C0") * 2f);
        greenCross = Mat("CruzVerde", Hex("#3AB860"), 0.3f, 0f, null, Hex("#40D070") * 1.6f);
        int n = 0;

        // Paso 35: una fila puede ir a lo largo de X (las de la calle de la feria) o de Z (las de la
        // transversal del cruce). line = la otra coordenada (donde esta la fachada).
        void Row(float from, float to, float z, float yaw, string prefix, bool far, bool tallAtStart, bool tallAtEnd,
            bool alongZ = false)
        {
            var specials = new List<GroundKind> { GroundKind.Restaurante, GroundKind.Farmacia, GroundKind.Peluqueria, GroundKind.Karaoke };
            bool templeDone = false, parkingDone = false;
            BlockKind prev = BlockKind.Alto;
            float x = from;
            while (x < to - 0.1f)
            {
                float remaining = to - x;
                BlockKind kind;
                float len;
                int floors;
                bool alleyStart = tallAtStart && Mathf.Abs(x - from) < 0.01f;
                bool alleyEnd = tallAtEnd && remaining <= 15f;
                if (alleyStart || alleyEnd)
                {
                    // Al lado del callejon: alto (8 pisos), a la misma altura que sus paredes.
                    kind = BlockKind.Alto;
                    len = alleyEnd ? remaining : 9f;
                    floors = 8;
                }
                else
                {
                    // El templo y el estacionamiento: uno de cada uno, enfrente, en lugares fijos
                    // (pasando x = -45 y x = 5), para que siempre esten a la vista desde la feria.
                    bool gapOk = far && !alongZ && prev != BlockKind.Templo && prev != BlockKind.Estacionamiento;
                    if (gapOk && !templeDone && x >= -45f) kind = BlockKind.Templo;
                    else if (gapOk && !parkingDone && x >= 5f) kind = BlockKind.Estacionamiento;
                    else kind = PickBlock(rng, prev);
                    if (kind == BlockKind.Templo) templeDone = true;
                    if (kind == BlockKind.Estacionamiento) parkingDone = true;
                    len = kind switch
                    {
                        BlockKind.Casa => rng.NextDouble() < 0.5 ? 6f : 9f,
                        BlockKind.Templo => 6f,
                        BlockKind.Estacionamiento => 9f,
                        BlockKind.Terraza => rng.NextDouble() < 0.5 ? 9f : 12f,
                        BlockKind.Alto => new[] { 9f, 12f, 15f }[rng.Next(3)],
                        _ => rng.NextDouble() < 0.5 ? 9f : 12f,
                    };
                    floors = kind switch
                    {
                        BlockKind.Alto => 5 + rng.Next(4),
                        BlockKind.Terraza => 3 + rng.Next(2),
                        BlockKind.Casa => 2,
                        BlockKind.Templo or BlockKind.Estacionamiento => 0,
                        _ => 4 + rng.Next(3),
                    };
                    if (len > remaining) len = remaining;
                }
                if (len < 3f) len = remaining;

                float cx = x + len * 0.5f;
                // Cerca (con locales especiales y mas detalle): lo que se ve desde la feria o el cruce.
                bool near = alongZ ? Mathf.Abs(cx) < 45f : cx > -100f && cx < 62f;
                Transform side = Group(g, prefix + n);
                side.localPosition = alongZ ? new Vector3(z, 0f, cx) : new Vector3(cx, 0f, z);
                side.localRotation = Quaternion.Euler(0f, yaw, 0f);
                string name = "FeriaB_" + prefix + n;

                if (kind == BlockKind.Templo) TempleLot(side, len, rng, f);
                else if (kind == BlockKind.Estacionamiento) ParkingLot(side, len, rng, f);
                else
                {
                    GroundKind ground = GroundKind.Locales;
                    if (kind == BlockKind.Casa) ground = rng.NextDouble() < 0.75 ? GroundKind.Casa : GroundKind.Locales;
                    else if (near && specials.Count > 0 && rng.NextDouble() < 0.6)
                    {
                        int pick = rng.Next(specials.Count);
                        ground = specials[pick];
                        specials.RemoveAt(pick);
                    }
                    else if (rng.NextDouble() < 0.25) ground = GroundKind.Persianas;
                    // El lado que toca el callejon no lleva pared: ya esta la del callejon.
                    // (Fila del norte: z local = +X del mundo.)
                    BuildBlock(side, name, kind, len, floors, ground, alleyStart, alleyEnd, near, rng, shutter, m, f);
                }
                prev = kind;
                n++;
                x += len;
            }
        }

        // Paso 35: la calle sigue hasta x = -152 y 142 y se corta en x = -89..-74 (el cruce: calzada de
        // -86 a -78, vereda de 3 m al oeste y de 4 m al este; los largos quedan multiplos de 3). Del otro lado del cruce, frente a la plaza.
        Row(-152f, CrossWalkX0, FairBackZ, 90f, "NorteOeste", false, false, false);
        Row(CrossWalkX1, AlleyX0, FairBackZ, 90f, "Norte", false, false, true);
        Row(AlleyX1, 142f, FairBackZ, 90f, "Norte", false, true, false);
        Row(-152f, CrossWalkX0, -17.5f, -90f, "SurOeste", true, false, false);
        Row(CrossWalkX1, 142f, -17.5f, -90f, "Sur", true, false, false);
        // Las dos veredas de la transversal, hacia el norte (detras de las fachadas) y hacia el sur.
        Row(FairBackZ + BlockDepth + 0.5f, 121.5f, CrossWalkX0, 0f, "TransOesteN", false, false, false, true);
        Row(FairBackZ + BlockDepth + 0.5f, 121.5f, CrossWalkX1, 180f, "TransEsteN", false, false, false, true);
        Row(-121f, -17.5f - BlockDepth - 0.5f, CrossWalkX0, 0f, "TransOesteS", false, false, false, true);
        Row(-121f, -17.5f - BlockDepth - 0.5f, CrossWalkX1, 180f, "TransEsteS", false, false, false, true);
        // Las puntas de la peatonal: un edificio de frente cierra la vista en cada extremo.
        foreach (var (x, yaw, nameEnd) in new[] { (142f, 180f, "PuntaEste"), (-152f, 0f, "PuntaOeste") })
        {
            Transform side = Group(g, nameEnd);
            side.localPosition = new Vector3(x, 0f, 2.75f);
            side.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BuildBlock(side, "FeriaB_" + nameEnd, BlockKind.Alto, 18f, 6, GroundKind.Locales, false, false, false, rng, shutter, m, f);
        }
        BuildFairSkyline(Group(g, "Fondo"), m);
    }

    private static BlockKind PickBlock(System.Random rng, BlockKind prev)
    {
        for (int tries = 0; tries < 4; tries++)
        {
            double r = rng.NextDouble();
            BlockKind k = r < 0.26 ? BlockKind.Alto : r < 0.46 ? BlockKind.Casa : r < 0.64 ? BlockKind.Terraza : r < 0.84 ? BlockKind.Balcones : BlockKind.Escalera;
            if (k != prev) return k;
        }
        return prev == BlockKind.Alto ? BlockKind.Casa : BlockKind.Alto;
    }

    // ================================================================== un bloque

    private static void BuildBlock(Transform side, string name, BlockKind kind, float L, int floors, GroundKind ground,
        bool noWallNeg, bool noWallPos, bool near, System.Random rng, Mesh shutter, Mats m, FairMats f)
    {
        float D = BlockDepth, H = floors * FloorHeight, half = L * 0.5f;
        int style = rng.Next(4);
        int bays = Mathf.Max(1, Mathf.RoundToInt(L / 3f));
        bool plaster = kind == BlockKind.Casa || ((kind == BlockKind.Terraza || kind == BlockKind.Balcones) && rng.NextDouble() < 0.5);
        int[] wallColors = { FCrema, FBlanco, FConcreto, FRosa, FCrema, FVerdeClaro };
        int wallColor = wallColors[rng.Next(wallColors.Length)];

        var front = new MeshDraft();   // atlas de fachadas (m.fachada)
        var detail = new MeshDraft();  // paleta de los callejones (m.detalles): rejas, tanques, ropa
        var pal = new MeshDraft();     // paleta de la feria (f.paleta): revoque, marcos, tejas, locales
        var lit = new MeshDraft();     // ventanas prendidas (m.ventanaLuz)
        var seeds = new System.Random(rng.Next());

        // 1. Planta baja.
        int specialBays = 0;
        if (ground == GroundKind.Casa)
        {
            pal.Box(new Vector3(-D * 0.5f, 1.5f, 0f), new Vector3(D, 3f, L), wallColor);
            HouseGround(side, pal, lit, rng, L, wallColor, f);
            AddSolid(side, "Casa", new Vector3(-D * 0.5f, 1.5f, 0f), new Vector3(D, 3f, L));
        }
        else
        {
            specialBays = SpecialShop(side, pal, lit, ground, L, rng, m, f);
            int skip = specialBays;
            FairStorefront(side, L, rng, near, shutter, m, f, ground != GroundKind.Persianas, j => j < skip);
        }

        // 2. Pisos de arriba.
        if (plaster)
        {
            pal.Box(new Vector3(-D * 0.5f, (FloorHeight + H) * 0.5f, 0f), new Vector3(D, H - FloorHeight, L), wallColor);
            for (int k = 1; k < floors; k++)
            {
                pal.Box(new Vector3(0.04f, k * FloorHeight + 0.05f, 0f), new Vector3(0.1f, 0.1f, L), FConcreto); // borde de losa
                for (int j = 0; j < bays; j++)
                    PlasterWindow(pal, lit, rng, k * FloorHeight, -half + 1.5f + j * 3f, kind == BlockKind.Casa);
            }
        }
        else
        {
            for (int k = 1; k < floors; k++)
            {
                float y0 = k * FloorHeight;
                for (int j = 0; j < bays; j++)
                {
                    float z0 = -half + j * 3f;
                    front.FacadeQuad(0f, z0, z0 + 3f, y0, y0 + FloorHeight, AtlasCell(style, PickWindow(rng, false)), (float)seeds.NextDouble());
                    if (kind != BlockKind.Balcones) BayDetails(detail, rng, 0f, y0, z0 + 1.5f, k);
                }
                detail.Box(new Vector3(0.05f, y0 + 0.06f, 0f), new Vector3(0.1f, 0.12f, L), PalConcreto);
            }
            // Paredes de los costados (se ven arriba de un vecino mas bajo). En la planta baja
            // empiezan detras de la pared de los locales (0.3 m), para no pisarse con ella.
            Vector2 wall = WallUV(style);
            foreach (float s in new[] { -1f, 1f })
            {
                if ((s < 0f && noWallNeg) || (s > 0f && noWallPos)) continue;
                float zs = s * half;
                Vector3 outward = new Vector3(0f, 0f, s);
                front.Quad(new Vector3(-D, 0f, zs), new Vector3(-0.31f, 0f, zs), new Vector3(-0.31f, FloorHeight, zs), new Vector3(-D, FloorHeight, zs), outward, wall);
                front.Quad(new Vector3(-D, FloorHeight, zs), new Vector3(0f, FloorHeight, zs), new Vector3(0f, H, zs), new Vector3(-D, H, zs), outward, wall);
            }
        }

        // 3. Lo que tiene cada tipo en la fachada.
        if (kind == BlockKind.Balcones)
            for (int k = 1; k < floors; k++)
                for (int j = 0; j < bays; j++)
                    Balcony(detail, rng, k * FloorHeight, -half + 1.5f + j * 3f, k == floors - 1);
        if (kind == BlockKind.Escalera)
            FireEscape(detail, floors, rng.NextDouble() < 0.5 ? -half + 1.5f : half - 1.5f);

        // 4. El techo.
        if (kind == BlockKind.Casa) GableRoof(pal, rng, L, D, H, wallColor);
        else if (kind == BlockKind.Terraza) TerraceRoof(detail, rng, L, D, H);
        else FlatRoof(side, detail, rng, L, D, H, kind == BlockKind.Alto && L >= 9f && rng.NextDouble() < 0.35, f);

        if (front.v.Count > 0) DraftChild(side, "Fachada", name + "_Fachada", front, m.fachada);
        if (detail.v.Count > 0) DraftChild(side, "Detalles", name + "_Detalles", detail, m.detalles);
        FairDraft(side, "Revoque", pal, f.paleta, true);
        FairDraft(side, "Luces", lit, m.ventanaLuz, false);
    }

    /// <summary>Ventana de casa: marco, vidrio (prendido u oscuro), cruz, alfeizar y extras.</summary>
    private static void PlasterWindow(MeshDraft pal, MeshDraft lit, System.Random rng, float y0, float zc, bool house)
    {
        int frame = rng.NextDouble() < 0.6 ? FBlanco : FMadera;
        float w = house ? 1.2f : 1.4f, h = 1.3f, yc = y0 + 1.55f;
        pal.Box(new Vector3(0.04f, yc, zc), new Vector3(0.08f, h + 0.12f, w + 0.12f), frame);
        if (rng.NextDouble() < 0.38) lit.Box(new Vector3(0.085f, yc, zc), new Vector3(0.02f, h, w), FBlanco);
        else pal.Box(new Vector3(0.085f, yc, zc), new Vector3(0.02f, h, w), FNegro);
        pal.Box(new Vector3(0.1f, yc, zc), new Vector3(0.02f, h, 0.05f), frame);
        pal.Box(new Vector3(0.1f, yc + 0.1f, zc), new Vector3(0.02f, 0.05f, w), frame);
        pal.Box(new Vector3(0.1f, yc - h * 0.5f - 0.06f, zc), new Vector3(0.14f, 0.05f, w + 0.3f), FConcreto);
        double r = rng.NextDouble();
        if (house && r < 0.35)
        {
            int c = rng.NextDouble() < 0.5 ? FVerde : FMaderaOscura;                         // postigos
            pal.Box(new Vector3(0.07f, yc, zc - w * 0.5f - 0.3f), new Vector3(0.04f, h + 0.1f, 0.55f), c);
            pal.Box(new Vector3(0.07f, yc, zc + w * 0.5f + 0.3f), new Vector3(0.04f, h + 0.1f, 0.55f), c);
        }
        else if (r < 0.6)
        {
            pal.Box(new Vector3(0.18f, yc - h * 0.5f - 0.15f, zc), new Vector3(0.24f, 0.16f, w), FMaderaOscura); // macetero
            for (int i = 0; i < 3; i++)
                FairBall(pal, new Vector3(0.18f, yc - h * 0.5f + 0.02f, zc - w * 0.35f + i * w * 0.35f), 0.13f, i == 1 ? FRosa : FVerde);
        }
        else if (r < 0.75)
            pal.Box(new Vector3(0.24f, y0 + 0.45f, zc + w * 0.5f + 0.15f), new Vector3(0.44f, 0.45f, 0.7f), FBlanco); // aire
    }

    /// <summary>Planta baja de casa: puerta con escalon y luz, ventanas con rejas, macetas, buzon.</summary>
    private static void HouseGround(Transform side, MeshDraft pal, MeshDraft lit, System.Random rng, float L, int wallColor, FairMats f)
    {
        float half = L * 0.5f;
        int bays = Mathf.Max(1, Mathf.RoundToInt(L / 3f));
        int door = rng.Next(bays);
        for (int j = 0; j < bays; j++)
        {
            float zc = -half + 1.5f + j * 3f;
            if (j == door)
            {
                pal.Box(new Vector3(0.04f, 1.1f, zc), new Vector3(0.08f, 2.25f, 1.15f), FBlanco);   // marco
                pal.Box(new Vector3(0.07f, 1.05f, zc), new Vector3(0.04f, 2.1f, 1.0f), FMaderaOscura);
                pal.Box(new Vector3(0.25f, 0.06f, zc), new Vector3(0.5f, 0.12f, 1.3f), FConcreto);  // escalon
                lit.Box(new Vector3(0.1f, 2.45f, zc + 0.75f), new Vector3(0.12f, 0.16f, 0.12f), FBlanco); // luz de la puerta
                pal.Box(new Vector3(0.1f, 1.2f, zc - 0.85f), new Vector3(0.14f, 0.25f, 0.3f), FRojo);  // buzon
                continue;
            }
            PlasterWindow(pal, lit, rng, 0f, zc, true);
            for (int b = 0; b < 5; b++)                                                            // rejas
                pal.Box(new Vector3(0.13f, 1.55f, zc - 0.5f + b * 0.25f), new Vector3(0.02f, 1.35f, 0.02f), FMetal);
            if (rng.NextDouble() < 0.5)
            {
                pal.Box(new Vector3(0.35f, 0.2f, zc - 0.6f), new Vector3(0.3f, 0.4f, 0.3f), FMaderaOscura);
                FairBall(pal, new Vector3(0.35f, 0.62f, zc - 0.6f), 0.25f, FVerde);
            }
            if (rng.NextDouble() < 0.3) Bicycle(pal, new Vector3(0.45f, 0f, zc + 0.3f), new[] { FRojo, FAzul, FBlanco }[rng.Next(3)]);
        }
    }

    /// <summary>Balcon de un piso: losa que sale 1 m, baranda (llena o de barrotes), y algo adentro.</summary>
    private static void Balcony(MeshDraft d, System.Random rng, float y0, float zc, bool top)
    {
        d.Box(new Vector3(0.5f, y0 + 0.06f, zc), new Vector3(1.0f, 0.12f, 2.7f), PalConcreto);
        if (rng.NextDouble() < 0.5)
            d.Box(new Vector3(0.98f, y0 + 0.55f, zc), new Vector3(0.04f, 0.9f, 2.7f), PalConcreto);   // baranda llena
        else
        {
            d.Box(new Vector3(0.98f, y0 + 1.0f, zc), new Vector3(0.04f, 0.04f, 2.7f), PalReja);
            for (int i = 0; i <= 8; i++)
                d.Box(new Vector3(0.98f, y0 + 0.55f, zc - 1.3f + i * 0.325f), new Vector3(0.02f, 0.9f, 0.02f), PalReja);
        }
        foreach (float s in new[] { -1f, 1f })
            d.Box(new Vector3(0.5f, y0 + 0.55f, zc + s * 1.33f), new Vector3(1.0f, 0.9f, 0.04f), PalConcreto);
        double r = rng.NextDouble();
        if (r < 0.35)
            for (int i = 0; i < 2 + rng.Next(3); i++)
            {
                float z = zc - 1.0f + (float)rng.NextDouble() * 2f;
                d.Box(new Vector3(0.6f, y0 + 0.24f, z), new Vector3(0.22f, 0.24f, 0.22f), PalOxido);
                d.Box(new Vector3(0.6f, y0 + 0.5f, z), new Vector3(0.3f, 0.3f, 0.3f), PalPlanta);
            }
        else if (r < 0.65)
        {
            d.Box(new Vector3(0.6f, y0 + 2.35f, zc), new Vector3(0.02f, 0.02f, 2.5f), PalCano);        // ropa colgada
            for (int i = 0; i < 2 + rng.Next(3); i++)
            {
                float hgt = 0.35f + 0.3f * (float)rng.NextDouble();
                d.Box(new Vector3(0.6f, y0 + 2.33f - hgt * 0.5f, zc - 0.9f + i * 0.55f), new Vector3(0.02f, hgt, 0.4f), ClothColors[rng.Next(ClothColors.Length)]);
            }
        }
        else if (r < 0.85)
            d.Box(new Vector3(0.35f, y0 + 0.35f, zc + 0.8f), new Vector3(0.44f, 0.46f, 0.72f), PalAire);
        if (top) d.Box(new Vector3(0.55f, y0 + 2.95f, zc), new Vector3(1.1f, 0.06f, 2.8f), PalChapa);   // techito del ultimo
    }

    /// <summary>Escalera de incendio en un tramo: un descanso por piso y tramos de escalones.</summary>
    private static void FireEscape(MeshDraft d, int floors, float zc)
    {
        for (int k = 1; k < floors; k++)
        {
            float y = k * FloorHeight + 0.05f;
            d.Box(new Vector3(0.45f, y, zc), new Vector3(0.9f, 0.05f, 2.8f), PalOxido);                // descanso
            d.Box(new Vector3(0.88f, y + 0.95f, zc), new Vector3(0.03f, 0.03f, 2.8f), PalReja);         // baranda
            d.Box(new Vector3(0.88f, y + 0.5f, zc), new Vector3(0.03f, 0.03f, 2.8f), PalReja);
            foreach (float s in new[] { -1.38f, 0f, 1.38f })
                d.Box(new Vector3(0.88f, y + 0.48f, zc + s), new Vector3(0.03f, 0.95f, 0.03f), PalReja);
            if (k == floors - 1) break;
            // Tramo hasta el piso de arriba: 10 escalones en diagonal, alternando el sentido.
            float dir = k % 2 == 0 ? 1f : -1f;
            for (int i = 0; i < 10; i++)
                d.Box(new Vector3(0.45f, y + (i + 1) * 0.29f, zc + dir * (-1.1f + i * 0.22f)), new Vector3(0.7f, 0.04f, 0.24f), PalReja);
        }
        // La escalera que baja hasta 2.2 m del piso.
        foreach (float s in new[] { -0.25f, 0.25f })
            d.Box(new Vector3(0.45f + s, 2.6f, zc + 1.2f), new Vector3(0.03f, 0.9f, 0.03f), PalReja);
        for (int i = 0; i < 3; i++)
            d.Box(new Vector3(0.45f, 2.25f + i * 0.3f, zc + 1.2f), new Vector3(0.5f, 0.03f, 0.03f), PalReja);
    }

    /// <summary>Tejado a dos aguas: la cumbrera a lo largo de la calle, aleros y los triangulos de los costados.</summary>
    private static void GableRoof(MeshDraft pal, System.Random rng, float L, float D, float H, int wallColor)
    {
        int roof = new[] { FRojo, FMetal, FAzul, FMaderaOscura }[rng.Next(4)];
        float rh = 1.5f + (float)rng.NextDouble() * 0.8f, eave = 0.55f, ends = 0.3f, half = L * 0.5f + ends;
        float xr = -D * 0.5f, yr = H + rh, ye = H - 0.18f;
        Vector2 c = PaletteUV(roof);
        var a = new Vector3(eave, ye, -half); var b = new Vector3(eave, ye, half);
        var r0 = new Vector3(xr, yr, half); var r1 = new Vector3(xr, yr, -half);
        pal.Quad(a, b, r0, r1, new Vector3(1f, 1.4f, 0f), c);                                        // adelante
        pal.Quad(new Vector3(-D - eave, ye, half), new Vector3(-D - eave, ye, -half), r1, r0, new Vector3(-1f, 1.4f, 0f), c);
        pal.Quad(new Vector3(0f, H, -half), new Vector3(0f, H, half), b, a, Vector3.down, PaletteUV(FMaderaOscura)); // debajo del alero
        foreach (float s in new[] { -1f, 1f })
            FairTri(pal, new Vector3(0f, H, s * L * 0.5f), new Vector3(-D, H, s * L * 0.5f), new Vector3(xr, yr, s * L * 0.5f), new Vector3(0f, 0f, s), wallColor);
        pal.Box(new Vector3(xr, yr + 0.05f, 0f), new Vector3(0.3f, 0.12f, half * 2f), roof == FMetal ? FNegro : FMaderaOscura); // cumbrera
        // Lineas de tejas: tiras finas paralelas al alero, por la mitad de adelante.
        for (int i = 1; i < 5; i++)
        {
            float t = i / 5f;
            Vector3 p = Vector3.Lerp(new Vector3(eave, ye, 0f), new Vector3(xr, yr, 0f), t);
            pal.Box(p + new Vector3(0f, 0.03f, 0f), new Vector3(0.06f, 0.04f, half * 2f), roof == FRojo ? FMaderaOscura : FNegro);
        }
        if (rng.NextDouble() < 0.6)
        {
            pal.Box(new Vector3(xr, yr + 0.9f, L * 0.2f), new Vector3(0.04f, 1.8f, 0.04f), FMetal);  // antena de tele
            pal.Box(new Vector3(xr, yr + 1.6f, L * 0.2f), new Vector3(0.04f, 0.04f, 1.0f), FMetal);
            pal.Box(new Vector3(xr, yr + 1.3f, L * 0.2f), new Vector3(0.04f, 0.04f, 0.7f), FMetal);
        }
    }

    /// <summary>Azotea: parapeto, tanques en sus patas, casita de la escalera, antenas, aires y a veces un cartel.</summary>
    private static void FlatRoof(Transform side, MeshDraft d, System.Random rng, float L, float D, float H, bool billboard, FairMats f)
    {
        float half = L * 0.5f;
        d.Box(new Vector3(-0.07f, H + 0.45f, 0f), new Vector3(0.14f, 0.9f, L), PalConcreto);
        foreach (float s in new[] { -1f, 1f })
            d.Box(new Vector3(-D * 0.5f, H + 0.45f, s * (half - 0.07f)), new Vector3(D, 0.9f, 0.14f), PalConcreto);
        int tanks = rng.Next(3);
        for (int t = 0; t < tanks; t++)
        {
            float zt = -half + 1.2f + (float)rng.NextDouble() * (L - 2.4f), xt = -2.2f - t * 2.4f;
            foreach (float sx in new[] { -0.45f, 0.45f })
                foreach (float sz in new[] { -0.45f, 0.45f })
                    d.Box(new Vector3(xt + sx, H + 0.4f, zt + sz), new Vector3(0.06f, 0.8f, 0.06f), PalReja);
            d.Cylinder(new Vector3(xt, H + 0.8f, zt), 0.65f, 1.3f, 8, PalTanque);
        }
        if (rng.NextDouble() < 0.7)
        {
            float zs = (float)(rng.NextDouble() - 0.5) * (L - 3f);
            d.Box(new Vector3(-D * 0.62f, H + 1.2f, zs), new Vector3(2.4f, 2.4f, 2.2f), PalConcreto);
            d.Box(new Vector3(-D * 0.62f + 1.21f, H + 1.0f, zs), new Vector3(0.02f, 1.9f, 0.8f), PalOxido);
        }
        for (int i = 0; i < rng.Next(4); i++)
            d.Box(new Vector3(-1.2f - (float)rng.NextDouble() * 5f, H + 0.25f, -half + 0.8f + (float)rng.NextDouble() * (L - 1.6f)), new Vector3(0.7f, 0.5f, 0.8f), PalAire);
        if (rng.NextDouble() < 0.6)
        {
            float za = -half + 0.6f + (float)rng.NextDouble() * (L - 1.2f);
            d.Box(new Vector3(-0.5f, H + 1.6f, za), new Vector3(0.05f, 3.2f, 0.05f), PalCano);
            d.Box(new Vector3(-0.5f, H + 2.8f, za), new Vector3(0.04f, 0.04f, 1.1f), PalCano);
        }
        if (billboard && f.carteles.Count > 0)
        {
            // Cartel grande sobre la azotea, con su estructura.
            Material sign = f.carteles[rng.Next(f.carteles.Count)];
            Vector2 size = FitSign(sign, Mathf.Min(L - 2f, 8f), 2f);
            float yc = H + 1.2f + size.y * 0.5f;
            SignFace(side, "CartelAzotea", new Vector3(-0.8f, yc, 0f), size, -90f, sign);
            d.Box(new Vector3(-0.86f, yc, 0f), new Vector3(0.08f, size.y + 0.15f, size.x + 0.15f), PalReja);
            foreach (float s in new[] { -0.4f, 0.4f })
                d.Box(new Vector3(-1.0f, H + 0.6f + (yc - H) * 0.5f, s * size.x), new Vector3(0.08f, yc - H + 0.2f, 0.08f), PalReja);
        }
    }

    /// <summary>Terraza: parapeto bajo con baranda, macetas, ropa colgada, sillas, mesa y la casita.</summary>
    private static void TerraceRoof(MeshDraft d, System.Random rng, float L, float D, float H)
    {
        float half = L * 0.5f;
        d.Box(new Vector3(-0.07f, H + 0.3f, 0f), new Vector3(0.14f, 0.6f, L), PalConcreto);
        d.Box(new Vector3(-0.07f, H + 1.05f, 0f), new Vector3(0.05f, 0.05f, L), PalReja);
        for (float z = -half + 0.5f; z < half; z += 1f)
            d.Box(new Vector3(-0.07f, H + 0.82f, z), new Vector3(0.03f, 0.45f, 0.03f), PalReja);
        foreach (float s in new[] { -1f, 1f })
            d.Box(new Vector3(-D * 0.5f, H + 0.3f, s * (half - 0.07f)), new Vector3(D, 0.6f, 0.14f), PalConcreto);
        for (int i = 0; i < 3 + rng.Next(4); i++)
        {
            float z = -half + 0.6f + (float)rng.NextDouble() * (L - 1.2f);
            d.Box(new Vector3(-0.45f, H + 0.2f, z), new Vector3(0.4f, 0.4f, 0.4f), PalOxido);
            d.Box(new Vector3(-0.45f, H + 0.6f, z), new Vector3(0.5f, 0.5f, 0.5f), PalPlanta);
        }
        float zl = (float)(rng.NextDouble() - 0.5) * (L - 4f);
        foreach (float s in new[] { -1.5f, 1.5f })
            d.Box(new Vector3(-3f, H + 0.9f, zl + s), new Vector3(0.05f, 1.8f, 0.05f), PalCano);
        d.Box(new Vector3(-3f, H + 1.75f, zl), new Vector3(0.02f, 0.02f, 3f), PalCano);
        for (int i = 0; i < 4; i++)
        {
            float hgt = 0.4f + 0.35f * (float)rng.NextDouble();
            d.Box(new Vector3(-3f, H + 1.73f - hgt * 0.5f, zl - 1.1f + i * 0.7f), new Vector3(0.02f, hgt, 0.5f), ClothColors[rng.Next(ClothColors.Length)]);
        }
        float zt = zl > 0f ? zl - 3.5f : zl + 3.5f;
        if (Mathf.Abs(zt) < half - 1f)
        {
            d.Box(new Vector3(-2.2f, H + 0.7f, zt), new Vector3(0.8f, 0.05f, 0.8f), PalPlastico);   // mesa
            d.Box(new Vector3(-2.2f, H + 0.35f, zt), new Vector3(0.06f, 0.7f, 0.06f), PalPlastico);
            foreach (float s in new[] { -0.7f, 0.7f })
            {
                d.Box(new Vector3(-2.2f, H + 0.42f, zt + s), new Vector3(0.42f, 0.05f, 0.42f), PalPlastico);
                d.Box(new Vector3(-2.2f, H + 0.68f, zt + s * 1.25f), new Vector3(0.42f, 0.5f, 0.05f), PalPlastico);
            }
        }
        d.Box(new Vector3(-D * 0.7f, H + 1.2f, 0f), new Vector3(2.4f, 2.4f, 2.2f), PalConcreto);
        if (rng.NextDouble() < 0.5)
            d.Cylinder(new Vector3(-D * 0.7f, H + 2.4f, 0f), 0.5f, 1.0f, 8, PalTanque);
    }

    // ================================================================== locales especiales

    /// <summary>Arma el local especial en los primeros tramos; devuelve cuantos tramos usa (0 = ninguno).</summary>
    private static int SpecialShop(Transform side, MeshDraft pal, MeshDraft lit, GroundKind kind, float L, System.Random rng, Mats m, FairMats f)
    {
        float half = L * 0.5f;
        switch (kind)
        {
            case GroundKind.Restaurante:
            {
                float w = Mathf.Min(6f, L), zc = -half + w * 0.5f;
                pal.Box(new Vector3(0.03f, 1.45f, zc), new Vector3(0.06f, 2.6f, w - 0.2f), FMaderaOscura);
                lit.Box(new Vector3(0.07f, 1.4f, zc - w * 0.2f), new Vector3(0.02f, 1.1f, w * 0.42f), FBlanco);
                pal.Box(new Vector3(0.08f, 0.82f, zc - w * 0.2f), new Vector3(0.06f, 0.06f, w * 0.45f), FMadera);
                BoxChild(side, "PuertaRestaurante", m.hueco, new Vector3(0.065f, 1.05f, zc + w * 0.27f), new Vector3(0.02f, 2.1f, 1.2f), Vector3.zero);
                pal.Box(new Vector3(0.14f, 2.12f, zc + w * 0.27f), new Vector3(0.03f, 0.03f, 1.4f), FMetal);
                for (int k = 0; k < 3; k++)
                    BoxChild(side, "Noren" + k, m.noren, new Vector3(0.16f, 1.8f, zc + w * 0.27f - 0.42f + k * 0.42f), new Vector3(0.015f, 0.6f, 0.39f), Vector3.zero);
                var lanterns = new List<Matrix4x4>();
                foreach (float z in new[] { zc - 0.1f, zc + w * 0.47f - 0.2f })
                {
                    pal.Box(new Vector3(0.25f, 2.3f, z), new Vector3(0.5f, 0.03f, 0.03f), FMetal);
                    lanterns.Add(Matrix4x4.TRS(new Vector3(0.45f, 1.75f, z), Quaternion.identity, Vector3.one * 1.25f));
                }
                CombinedChild(side, "FarolesRestaurante", GetOrCreateMesh("Chochin"), lanterns, m.farolTela);
                ShopSign(side, pal, "CartelRestaurante", zc, w - 1f, rng, f);
                pal.Box(new Vector3(0.75f, 0.75f, zc - w * 0.45f), new Vector3(0.04f, 0.7f, 0.5f), FNegro); // pizarron
                pal.Box(new Vector3(0.75f, 0.35f, zc - w * 0.45f), new Vector3(0.3f, 0.7f, 0.04f), FMadera);
                return Mathf.RoundToInt(w / 3f);
            }
            case GroundKind.Farmacia:
            {
                float zc = -half + 1.5f;
                Material light = Mat("AlmacenLuz", Hex("#E6EEE4"), 0.3f, 0f, null, Hex("#DDE8DA") * 1.3f);
                BoxChild(side, "VidrieraFarmacia", light, new Vector3(0.04f, 1.25f, zc), new Vector3(0.04f, 2.1f, 2.6f), Vector3.zero);
                pal.Box(new Vector3(0.06f, 2.7f, zc), new Vector3(0.12f, 0.5f, 3f), FBlanco);
                pal.Box(new Vector3(0.125f, 2.6f, zc), new Vector3(0.01f, 0.1f, 3f), FVerde);
                pal.Box(new Vector3(0.35f, 2.75f, zc + 1.3f), new Vector3(0.6f, 0.04f, 0.04f), FMetal);
                var cross = new MeshDraft();
                cross.Box(new Vector3(0.75f, 2.75f, zc + 1.3f), new Vector3(0.2f, 0.62f, 0.08f), FBlanco);
                cross.Box(new Vector3(0.75f, 2.75f, zc + 1.3f), new Vector3(0.62f, 0.2f, 0.08f), FBlanco);
                FairDraft(side, "CruzVerde", cross, greenCross, false);
                return 1;
            }
            case GroundKind.Peluqueria:
            {
                float zc = -half + 1.5f;
                lit.Box(new Vector3(0.07f, 1.35f, zc - 0.35f), new Vector3(0.02f, 1.3f, 1.6f), FBlanco);
                pal.Box(new Vector3(0.06f, 1.05f, zc + 0.9f), new Vector3(0.05f, 2.1f, 0.8f), FMadera);
                float zp = zc + 1.4f;
                pal.Cylinder(new Vector3(0.25f, 0.95f, zp), 0.11f, 0.1f, 8, FMetal);
                int[] stripes = { FRojo, FBlanco, FAzul, FBlanco };
                for (int i = 0; i < 8; i++)
                    pal.Cylinder(new Vector3(0.25f, 1.05f + i * 0.12f, zp), 0.085f, 0.12f, 8, stripes[i % 4]);
                pal.Cylinder(new Vector3(0.25f, 2.01f, zp), 0.11f, 0.08f, 8, FMetal);
                FairBall(pal, new Vector3(0.25f, 2.16f, zp), 0.09f, FBlanco);
                pal.Box(new Vector3(0.12f, 1.5f, zp), new Vector3(0.24f, 0.04f, 0.04f), FMetal);
                ShopSign(side, pal, "CartelPeluqueria", zc, 2.4f, rng, f);
                return 1;
            }
            case GroundKind.Karaoke:
            {
                float w = L >= 6f ? 6f : 3f, zc = -half + w * 0.5f;
                pal.Box(new Vector3(0.03f, 1.45f, zc), new Vector3(0.06f, 2.6f, w - 0.2f), FNegro);
                pal.Box(new Vector3(0.07f, 1.05f, zc + w * 0.25f), new Vector3(0.04f, 2.1f, 1.0f), FVioleta);
                var neon = new MeshDraft();
                neon.Box(new Vector3(0.1f, 2.42f, zc), new Vector3(0.06f, 0.09f, w - 0.5f), FBlanco);
                neon.Box(new Vector3(0.1f, 0.35f, zc), new Vector3(0.06f, 0.09f, w - 0.5f), FBlanco);
                foreach (float s in new[] { -1f, 1f })
                    neon.Box(new Vector3(0.1f, 1.4f, zc + s * (w * 0.5f - 0.25f)), new Vector3(0.06f, 2.1f, 0.09f), FBlanco);
                foreach (float s in new[] { -0.55f, 0.55f })                                            // marco de la puerta
                    neon.Box(new Vector3(0.1f, 1.1f, zc + w * 0.25f + s), new Vector3(0.06f, 2.2f, 0.07f), FBlanco);
                FairDraft(side, "Neon", neon, neonPink, false);
                Material sign = m.signsN != null && m.signsN.mats.Count > 0 ? m.signsN.mats[rng.Next(m.signsN.mats.Count)] : null;
                if (sign != null)
                {
                    Vector2 size = FitSign(sign, w - 1f, 0.95f);
                    pal.Box(new Vector3(0.05f, 2.95f, zc), new Vector3(0.08f, size.y + 0.1f, size.x + 0.1f), FNegro);
                    SignFace(side, "CartelKaraoke", new Vector3(0.1f, 2.95f, zc), size, -90f, sign);
                }
                if (f.nobori.Count > 0)
                {
                    // Cartel vertical que sale de la pared: se lee desde la vereda.
                    Material v = f.nobori[rng.Next(f.nobori.Count)];
                    Vector2 size = FitSign(v, 0.45f, 1.6f);
                    pal.Box(new Vector3(0.45f, 3.6f, zc - w * 0.5f + 0.3f), new Vector3(0.9f, 0.04f, 0.04f), FMetal);
                    SignFace(side, "VerticalKaraokeA", new Vector3(0.6f, 3.5f - size.y * 0.5f, zc - w * 0.5f + 0.3f - 0.006f), size, 0f, v);
                    SignFace(side, "VerticalKaraokeB", new Vector3(0.6f, 3.5f - size.y * 0.5f, zc - w * 0.5f + 0.3f + 0.006f), size, 180f, v);
                }
                return Mathf.RoundToInt(w / 3f);
            }
        }
        return 0;
    }

    private static void ShopSign(Transform side, MeshDraft pal, string name, float zc, float width, System.Random rng, FairMats f)
    {
        if (f.carteles.Count == 0) return;
        Material sign = f.carteles[rng.Next(f.carteles.Count)];
        Vector2 size = FitSign(sign, width, 0.48f);
        pal.Box(new Vector3(0.04f, 2.72f, zc), new Vector3(0.06f, size.y + 0.08f, size.x + 0.08f), FNegro);
        SignFace(side, name, new Vector3(0.08f, 2.72f, zc), size, -90f, sign);
    }

    // ================================================================== lotes sin edificio

    /// <summary>Un templo chico entre dos edificios: torii, sendero, faroles de piedra, templito y arboles.</summary>
    private static void TempleLot(Transform side, float L, System.Random rng, FairMats f)
    {
        float D = BlockDepth;
        var pal = new MeshDraft();
        var glow = new MeshDraft();
        pal.Box(new Vector3(-D * 0.5f, 0.025f, 0f), new Vector3(D, 0.05f, L), FConcreto);                // grava
        for (int i = 0; i < 7; i++)
            pal.Box(new Vector3(-0.6f - i * 0.95f, 0.065f, 0f), new Vector3(0.75f, 0.04f, 0.9f), FBlanco); // sendero
        // Torii.
        foreach (float s in new[] { -1f, 1f })
            pal.Box(new Vector3(-1.2f, 1.3f, s * 1.1f), new Vector3(0.18f, 2.6f, 0.18f), FRojo);
        pal.Box(new Vector3(-1.2f, 2.2f, 0f), new Vector3(0.14f, 0.12f, 2.5f), FRojo);
        pal.Box(new Vector3(-1.2f, 2.58f, 0f), new Vector3(0.22f, 0.18f, 3.0f), FRojo);
        pal.Box(new Vector3(-1.2f, 2.73f, 0f), new Vector3(0.26f, 0.1f, 3.25f), FNegro);
        pal.Box(new Vector3(-1.08f, 2.02f, 0f), new Vector3(0.06f, 0.06f, 2.1f), FCrema);                 // soga
        for (int i = 0; i < 4; i++)
            pal.Box(new Vector3(-1.05f, 1.86f, -0.6f + i * 0.4f), new Vector3(0.01f, 0.24f, 0.1f), FBlanco); // papeles
        // Faroles de piedra.
        foreach (float s in new[] { -1f, 1f })
        {
            float z = s * 1.55f;
            pal.Box(new Vector3(-3.8f, 0.15f, z), new Vector3(0.6f, 0.3f, 0.6f), FConcreto);
            pal.Box(new Vector3(-3.8f, 0.7f, z), new Vector3(0.22f, 0.8f, 0.22f), FConcreto);
            pal.Box(new Vector3(-3.8f, 1.15f, z), new Vector3(0.5f, 0.1f, 0.5f), FConcreto);
            glow.Box(new Vector3(-3.8f, 1.38f, z), new Vector3(0.3f, 0.35f, 0.3f), FBlanco);
            pal.Box(new Vector3(-3.8f, 1.62f, z), new Vector3(0.62f, 0.12f, 0.62f), FConcreto);
            pal.Box(new Vector3(-3.8f, 1.75f, z), new Vector3(0.18f, 0.14f, 0.18f), FConcreto);
        }
        // El templito, sobre una base de piedra.
        pal.Box(new Vector3(-7.2f, 0.3f, 0f), new Vector3(1.8f, 0.6f, 1.8f), FConcreto);
        pal.Box(new Vector3(-7.2f, 1.15f, 0f), new Vector3(1.2f, 1.1f, 1.2f), FMadera);
        pal.Box(new Vector3(-6.59f, 1.1f, 0f), new Vector3(0.02f, 0.8f, 0.6f), FMaderaOscura);
        Vector2 rc = PaletteUV(FMaderaOscura);
        pal.Quad(new Vector3(-6.35f, 1.7f, -0.85f), new Vector3(-6.35f, 1.7f, 0.85f), new Vector3(-7.2f, 2.3f, 0.85f), new Vector3(-7.2f, 2.3f, -0.85f), new Vector3(1f, 1.4f, 0f), rc);
        pal.Quad(new Vector3(-8.05f, 1.7f, 0.85f), new Vector3(-8.05f, 1.7f, -0.85f), new Vector3(-7.2f, 2.3f, -0.85f), new Vector3(-7.2f, 2.3f, 0.85f), new Vector3(-1f, 1.4f, 0f), rc);
        foreach (float s in new[] { -1f, 1f })
            FairTri(pal, new Vector3(-6.6f, 1.7f, s * 0.6f), new Vector3(-7.8f, 1.7f, s * 0.6f), new Vector3(-7.2f, 2.25f, s * 0.6f), new Vector3(0f, 0f, s), FMadera);
        pal.Box(new Vector3(-6.2f, 0.75f, 0f), new Vector3(0.4f, 0.3f, 0.7f), FMaderaOscura);           // caja de ofrendas
        // Arboles al fondo.
        foreach (float s in new[] { -1f, 1f })
        {
            float z = s * (L * 0.5f - 1.1f);
            pal.Cylinder(new Vector3(-8.6f, 0f, z), 0.18f, 3f, 6, FMaderaOscura);
            FairBall(pal, new Vector3(-8.6f, 3.6f, z), 1.5f, FVerde);
            FairBall(pal, new Vector3(-8.2f, 4.4f, z * 0.9f), 1.0f, FVerdeClaro);
        }
        FairDraft(side, "Templo", pal, f.paleta, true);
        FairDraft(side, "LuzFaroles", glow, f.farolitoAmarillo, false);
    }

    /// <summary>Un estacionamiento entre dos edificios: piso, lineas, topes, autos, cartel P y un farol.</summary>
    private static void ParkingLot(Transform side, float L, System.Random rng, FairMats f)
    {
        float D = BlockDepth, half = L * 0.5f;
        var pal = new MeshDraft();
        var glow = new MeshDraft();
        pal.Box(new Vector3(-D * 0.5f, 0.02f, 0f), new Vector3(D, 0.04f, L), FConcreto);
        int slots = Mathf.Max(1, Mathf.FloorToInt(L / 2.8f));
        float slot = L / slots;
        for (int i = 0; i <= slots; i++)
            pal.Box(new Vector3(-5.5f, 0.045f, -half + i * slot), new Vector3(5.5f, 0.01f, 0.08f), FBlanco);
        var models = new[] { CarModel.Sedan, CarModel.Compacto, CarModel.Furgon, CarModel.Compacto };
        int[] colors = { FBlanco, FMetal, FRojo, FAzul, FCrema, FNegro };
        for (int i = 0; i < slots; i++)
        {
            float z = -half + (i + 0.5f) * slot;
            pal.Box(new Vector3(-8.0f, 0.08f, z), new Vector3(0.2f, 0.12f, 1.3f), FAmarillo);           // tope
            if (rng.NextDouble() < 0.3) continue;
            int color = colors[rng.Next(colors.Length)];
            GameObject car = BuildCarModel(side, "AutoEstacionado" + i, models[rng.Next(models.Length)], color, FBlanco, f,
                out _, out _, out _, out _, out _);
            car.transform.localPosition = new Vector3(-5.6f, 0f, z);
            car.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        }
        // Cartel P (azul con la letra blanca hecha de tiras).
        float zs = half - 0.45f;
        pal.Box(new Vector3(-0.3f, 1.4f, zs), new Vector3(0.08f, 2.8f, 0.08f), FMetal);
        pal.Box(new Vector3(-0.25f, 2.95f, zs), new Vector3(0.04f, 0.8f, 0.8f), FAzul);
        pal.Box(new Vector3(-0.225f, 2.95f, zs - 0.15f), new Vector3(0.01f, 0.56f, 0.1f), FBlanco);
        pal.Box(new Vector3(-0.225f, 3.18f, zs), new Vector3(0.01f, 0.1f, 0.34f), FBlanco);
        pal.Box(new Vector3(-0.225f, 2.96f, zs), new Vector3(0.01f, 0.1f, 0.34f), FBlanco);
        pal.Box(new Vector3(-0.225f, 3.07f, zs + 0.15f), new Vector3(0.01f, 0.32f, 0.1f), FBlanco);
        // Farol del fondo y una maquina expendedora.
        pal.Cylinder(new Vector3(-9.2f, 0f, -half + 0.6f), 0.07f, 4.2f, 6, FMetal);
        pal.Box(new Vector3(-8.85f, 4.15f, -half + 0.6f), new Vector3(0.7f, 0.06f, 0.06f), FMetal);
        glow.Box(new Vector3(-8.55f, 4.05f, -half + 0.6f), new Vector3(0.36f, 0.1f, 0.24f), FBlanco);
        pal.Box(new Vector3(-9.4f, 0.95f, half - 1.0f), new Vector3(0.75f, 1.9f, 1.0f), FRojo);
        FairDraft(side, "Estacionamiento", pal, f.paleta, true);
        FairDraft(side, "LuzFarol", glow, f.farola, false);
    }

    // ================================================================== el fondo

    /// <summary>
    /// Una fila de edificios mas lejos, detras de cada fila de la calle: 4 a 9 pisos, mas oscuros
    /// (FachadaFondo, casi sin luces), con parapeto y algun tanque. Solo fachada y costados.
    /// </summary>
    private static void BuildFairSkyline(Transform g, Mats m)
    {
        var rng = new System.Random(3402);
        int n = 0;
        void Row(float z, float yaw, string prefix)
        {
            float x = -172f;
            while (x < 172f)
            {
                // Hueco para la transversal del cruce (se ve la calle seguir hacia el fondo).
                if (x > -104f && x < -60f) { x = -60f; continue; }
                float len = 9f + 3f * rng.Next(5);
                int floors = 4 + rng.Next(6);   // 4 a 9 pisos: que no se arme otro muro
                int style = rng.Next(4);
                float H = floors * FloorHeight, half = len * 0.5f, D = 8f;
                Transform side = Group(g, prefix + n);
                side.localPosition = new Vector3(x + half, 0f, z);
                side.localRotation = Quaternion.Euler(0f, yaw, 0f);
                var front = new MeshDraft();
                var detail = new MeshDraft();
                var seeds = new System.Random(rng.Next());
                int bays = Mathf.RoundToInt(len / 3f);
                for (int k = 1; k < floors; k++)
                    for (int j = 0; j < bays; j++)
                    {
                        float z0 = -half + j * 3f;
                        front.FacadeQuad(0f, z0, z0 + 3f, k * FloorHeight, (k + 1) * FloorHeight, AtlasCell(style, PickWindow(rng, true)), (float)seeds.NextDouble());
                    }
                Vector2 wall = WallUV(style);
                front.Quad(new Vector3(0f, 0f, -half), new Vector3(0f, 0f, half), new Vector3(0f, FloorHeight, half), new Vector3(0f, FloorHeight, -half), Vector3.right, wall);
                front.Quad(new Vector3(-D, 0f, -half), new Vector3(0f, 0f, -half), new Vector3(0f, H, -half), new Vector3(-D, H, -half), Vector3.back, wall);
                front.Quad(new Vector3(-D, 0f, half), new Vector3(0f, 0f, half), new Vector3(0f, H, half), new Vector3(-D, H, half), Vector3.forward, wall);
                detail.Box(new Vector3(-0.07f, H + 0.45f, 0f), new Vector3(0.14f, 0.9f, len), PalConcreto);
                if (rng.NextDouble() < 0.5)
                    detail.Cylinder(new Vector3(-2.5f, H, (float)(rng.NextDouble() - 0.5) * (len - 3f)), 0.8f, 1.6f, 8, PalTanque);
                if (rng.NextDouble() < 0.4)
                    detail.Box(new Vector3(-D * 0.5f, H + 1.2f, (float)(rng.NextDouble() - 0.5) * (len - 3f)), new Vector3(2.4f, 2.4f, 2.4f), PalConcreto);
                DraftChild(side, "Fachada", "FeriaB_Fondo" + prefix + n + "_Fachada", front, m.fachadaFondo);
                DraftChild(side, "Detalles", "FeriaB_Fondo" + prefix + n + "_Detalles", detail, m.detalles);
                n++;
                x += len + (rng.NextDouble() < 0.4 ? 3f + 3f * rng.Next(3) : 0f);
            }
        }
        Row(FairBackZ + 17f, 90f, "N");
        Row(-31f, -90f, "S");
    }
}
