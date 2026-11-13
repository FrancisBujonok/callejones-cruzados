using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Paso 36: los juegos de los puestos (opcionales) y "lo raro de a poco".
///
/// - Aros (puesto S3, cerca de la entrada): 21 botellas en la mesa de atras; tres aros sobre el
///   mostrador. Premio: un peluche (RingToss).
/// - Tiro al blanco (puesto N15, cerca del escenario): tres piramides de seis latas; tres pelotitas
///   en una canastita. Premio: una mascara de zorro (CanToss).
/// - LoRaro (FairUnease): con el tiempo y con el gato, la feria se pone rara de a poco.
///
/// Espacio de cada puesto: el frente en z = 0 mirando a +Z (ver BuildStall).
/// </summary>
public static partial class AtmosphereBuilder
{
    // Las botellas de los aros: 7 x 3, cada 16 cm, sobre la mesa de atras (arriba en 0.8).
    private const float BottleSpacing = 0.16f, BottleTableTop = 0.8f, BottleBody = 0.2f, BottleNeck = 0.08f;
    private static IEnumerable<Vector3> BottleSpots()
    {
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 7; col++)
                yield return new Vector3((col - 3) * BottleSpacing, BottleTableTop, -1.44f - row * BottleSpacing);
    }

    /// <summary>Las botellas (verdes y marrones), dibujadas en la mercaderia del puesto.</summary>
    private static void RingBottles(MeshDraft d)
    {
        int k = 0;
        foreach (Vector3 p in BottleSpots())
        {
            int color = k++ % 3 == 1 ? FMaderaOscura : FVerde;
            d.Cylinder(p, 0.036f, BottleBody, 6, color);
            d.Cylinder(p + Vector3.up * BottleBody, 0.014f, BottleNeck, 4, color);
        }
    }

    /// <summary>Peluches (ositos) colgados en la lona de atras, en dos filas.</summary>
    private static void StallPrizePlush(MeshDraft d)
    {
        int[] colors = { FRosa, FAmarillo, FNaranja, FBlanco, FMadera }; // sin azul: la lona puede ser azul
        for (int k = 0; k < 9; k++)
        {
            float x = -1.15f + (k % 5) * 0.575f + (k >= 5 ? 0.29f : 0f), y = k < 5 ? 1.85f : 1.4f;
            int c = colors[k % colors.Length];
            d.Box(new Vector3(x, y, -1.9f), new Vector3(0.2f, 0.22f, 0.12f), c);            // cuerpo
            d.Box(new Vector3(x, y + 0.18f, -1.9f), new Vector3(0.16f, 0.14f, 0.12f), c);   // cabeza
            d.Box(new Vector3(x - 0.07f, y + 0.27f, -1.9f), new Vector3(0.05f, 0.05f, 0.04f), c); // orejas
            d.Box(new Vector3(x + 0.07f, y + 0.27f, -1.9f), new Vector3(0.05f, 0.05f, 0.04f), c);
            d.Box(new Vector3(x, y + 0.16f, -1.835f), new Vector3(0.05f, 0.03f, 0.01f), FNegro); // hocico
        }
    }

    /// <summary>Mascaras de zorro (blancas, con marcas rojas) colgadas en la lona de atras.</summary>
    private static void StallPrizeMasks(MeshDraft d)
    {
        for (int k = 0; k < 7; k++)
        {
            float x = -1.05f + k * 0.35f, y = 1.75f + (k % 2) * 0.22f;
            d.Box(new Vector3(x, y, -1.96f), new Vector3(0.22f, 0.26f, 0.04f), FBlanco);
            d.Box(new Vector3(x - 0.07f, y + 0.15f, -1.96f), new Vector3(0.06f, 0.08f, 0.03f), FBlanco); // orejas
            d.Box(new Vector3(x + 0.07f, y + 0.15f, -1.96f), new Vector3(0.06f, 0.08f, 0.03f), FBlanco);
            d.Box(new Vector3(x - 0.05f, y + 0.03f, -1.935f), new Vector3(0.06f, 0.02f, 0.01f), FRojo);  // ojos
            d.Box(new Vector3(x + 0.05f, y + 0.03f, -1.935f), new Vector3(0.06f, 0.02f, 0.01f), FRojo);
            d.Box(new Vector3(x, y - 0.08f, -1.935f), new Vector3(0.03f, 0.03f, 0.01f), FNegro);        // nariz
        }
    }

    /// <summary>Lo que se mueve del juego (aros o latas y pelotitas) y su componente.</summary>
    private static void BuildStallGame(Transform t, int type, FairMats f)
    {
        Transform spot = Group(t, "DondeSeJuega");
        spot.localPosition = new Vector3(0f, 0f, 0.62f);
        spot.localRotation = Quaternion.Euler(0f, 180f, 0f); // mirando al puesto

        if (type == 9)
        {
            var tops = new List<Transform>();
            int i = 0;
            Transform bocas = Group(t, "Bocas");
            foreach (Vector3 p in BottleSpots())
            {
                Transform b = Group(bocas, "Boca" + i++);
                b.localPosition = p + Vector3.up * (BottleBody + BottleNeck);
                tops.Add(b);
            }
            int[] colors = { FRojo, FAmarillo, FAzul };
            var rings = new Transform[3];
            for (int k = 0; k < 3; k++)
            {
                GameObject r = FairDraft(t, "Aro" + k, RingDraft(0.075f, 0.011f, colors[k]), f.paleta, false);
                r.transform.localPosition = new Vector3(-1.1f + k * 0.2f, 0.955f + k * 0.012f, -0.3f);
                rings[k] = r.transform;
            }
            RingToss game = t.gameObject.AddComponent<RingToss>();
            var so = new SerializedObject(game);
            GameTexts(so, spot, "Jugar a los aros", "Peluche",
                "Me da tres aros. No le entiendo, pero hace el gesto de tirar.",
                "¡Entró! Me da un peluche. Dice algo y se ríe.",
                "Entró otra vez. Me hace que no con la mano: premio hay uno solo.",
                "Nada. Me muestra un dedo: ¿otra vez?");
            SetObjects(so.FindProperty("rings"), rings);
            SetObjects(so.FindProperty("bottleTops"), tops.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            // Tres piramides de seis latas (3-2-1) en la mesa de atras.
            int[] colors = { FRojo, FAmarillo, FAzul, FBlanco, FVerdeClaro };
            var cans = new List<Transform>();
            Transform latas = Group(t, "Latas");
            foreach (float px in new[] { -0.7f, 0f, 0.7f })
                for (int level = 0; level < 3; level++)
                    for (int k = 0; k < 3 - level; k++)
                    {
                        float x = px + (k - (2 - level) * 0.5f) * 0.115f; // latas de 11 cm de ancho, 13 de alto
                        var d = new MeshDraft();
                        d.Cylinder(Vector3.zero, 0.055f, 0.13f, 8, colors[cans.Count % colors.Length]);
                        d.Cylinder(new Vector3(0f, 0.05f, 0f), 0.056f, 0.035f, 8, FBlanco); // la etiqueta
                        GameObject can = FairDraft(latas, "Lata" + cans.Count, d, f.paleta, false);
                        can.transform.localPosition = new Vector3(x, BottleTableTop + level * 0.13f, -1.58f);
                        cans.Add(can.transform);
                    }
            var balls = new Transform[3];
            for (int k = 0; k < 3; k++)
            {
                var d = new MeshDraft();
                FairBall(d, Vector3.zero, 0.04f, k == 1 ? FBlanco : FRojo);
                GameObject b = FairDraft(t, "Pelota" + k, d, f.paleta, false);
                b.transform.localPosition = new Vector3(0.88f + k * 0.07f, 1.03f, -0.3f + (k == 1 ? 0.04f : -0.02f));
                balls[k] = b.transform;
            }
            CanToss game = t.gameObject.AddComponent<CanToss>();
            var so = new SerializedObject(game);
            GameTexts(so, spot, "Jugar al tiro al blanco", "Mascara",
                "Tres pelotitas. Hay que voltear una pirámide entera, creo.",
                "¡Cayó toda! Me da una máscara de zorro.",
                "Otra vez toda. Se ríe, pero no me da otra máscara.",
                "Quedaron latas paradas. Me ofrece otra vuelta.");
            SetObjects(so.FindProperty("balls"), balls);
            SetObjects(so.FindProperty("cans"), cans.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void GameTexts(SerializedObject so, Transform spot, string name, string prize, string start, string win, string again, string lose)
    {
        so.FindProperty("playSpot").objectReferenceValue = spot;
        so.FindProperty("gameName").stringValue = name;
        so.FindProperty("prizeId").stringValue = prize;
        so.FindProperty("startLine").stringValue = start;
        so.FindProperty("winLine").stringValue = win;
        so.FindProperty("againWinLine").stringValue = again;
        so.FindProperty("loseLine").stringValue = lose;
    }

    private static void SetObjects(SerializedProperty list, Object[] items)
    {
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    /// <summary>Un aro chato: 10 tramos, el tubo de 4 caras. El centro en el origen, acostado.</summary>
    private static MeshDraft RingDraft(float radius, float tube, int palette)
    {
        var d = new MeshDraft();
        const int segments = 10, sides = 4;
        Vector3 Point(int s, int k)
        {
            float a = s * Mathf.PI * 2f / segments, b = k * Mathf.PI * 2f / sides + Mathf.PI * 0.25f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            return dir * (radius + Mathf.Cos(b) * tube) + Vector3.up * (Mathf.Sin(b) * tube);
        }
        for (int s = 0; s < segments; s++)
            for (int k = 0; k < sides; k++)
            {
                Vector3 a = Point(s, k), b = Point(s + 1, k), c = Point(s + 1, k + 1), e = Point(s, k + 1);
                float mid = (s + 0.5f) * Mathf.PI * 2f / segments;
                Vector3 center = new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid)) * radius;
                Vector3 outward = (a + b + c + e) * 0.25f - center;
                FairTri(d, a, c, b, outward, palette);
                FairTri(d, a, e, c, outward, palette);
            }
        return d;
    }

    /// <summary>Paso 36: el objeto "LoRaro" (FairUnease), con los carteles y las bombitas de los puestos.</summary>
    private static void BuildFairUnease(Transform world, FairMats f)
    {
        var go = new GameObject("LoRaro");
        go.transform.SetParent(world, false);
        CatTrail trail = world.GetComponentInChildren<CatTrail>(true);
        go.AddComponent<FairUnease>().Setup(trail, FairStallSigns.ToArray(), f.avisos.ToArray(), FairStallLights.ToArray());
    }
}
