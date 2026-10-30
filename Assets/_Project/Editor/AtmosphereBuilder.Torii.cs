using UnityEditor;
using UnityEngine;

/// <summary>
/// Parte de AtmosphereBuilder: la salida del loop como un torii.
///
/// El torii es el portal de los santuarios sintoistas: marca el paso de un espacio
/// comun a uno sagrado. Aca marca el paso fuera del laberinto.
///
/// Cambios en ModuloSalida (objeto de la escena, fuera de Laberinto):
/// - Las paredes llegan solo hasta la mitad del modulo; despues de ellas esta el torii.
/// - Mas alla del torii no hay paredes: el pasillo se abre a la niebla.
/// - Barreras invisibles a los costados para no caerse por los bordes.
/// - "Puerta" pasa a ser un resplandor calido al fondo: la salida se ve como una luz.
/// - Una luz roja baja ilumina el torii desde adelante, como en la foto de referencia.
/// </summary>
public static partial class AtmosphereBuilder
{
    private static readonly Vector2[] ToriiPillarProfile = { new Vector2(0.12f, 3.0f), new Vector2(0.14f, 0f) };
    private static readonly Vector2[] ToriiBaseProfile = { new Vector2(0.17f, 0.25f), new Vector2(0.17f, 0f) };
    private static readonly Vector2[] RopeHalfProfile = { new Vector2(0.05f, 0.65f), new Vector2(0.05f, -0.65f) };
    private static readonly Vector2[] TasselProfile = { new Vector2(0.05f, 0f), new Vector2(0.02f, -0.2f), new Vector2(0.005f, -0.35f) };

    private static void BuildExitTorii(Transform exit, Mats m)
    {
        // 1. Paredes solo en la primera mitad (z de -3 a 0).
        foreach (string wall in new[] { "ParedIzq", "ParedDer" })
        {
            Transform t = exit.Find(wall);
            if (t == null) continue;
            float x = wall == "ParedIzq" ? -1.5f : 1.5f;
            t.localPosition = new Vector3(x, 1.5f, -1.5f);
            ConvertToMetric(t, new Vector3(0.2f, 3f, 3f));
        }

        Transform floor = exit.Find("Piso");
        if (floor != null) ConvertToMetric(floor);

        // 2. La pared del fondo pasa a ser un resplandor.
        Transform glow = exit.Find("Puerta") ?? exit.Find("Resplandor");
        if (glow != null)
        {
            glow.name = "Resplandor";
            glow.localPosition = new Vector3(0f, 1.6f, 3f);
            glow.localRotation = Quaternion.identity;
            ConvertToMetric(glow, new Vector3(3f, 3.2f, 0.1f));
            glow.GetComponent<MeshRenderer>().sharedMaterial = m.resplandor;
        }

        // 3. Barreras invisibles (solo collider) donde ya no hay paredes.
        DestroyChild(exit, "Barreras");
        Transform barriers = Group(exit, "Barreras");
        foreach (float x in new[] { -1.5f, 1.5f })
        {
            var b = new GameObject(x < 0 ? "BarreraIzq" : "BarreraDer");
            b.transform.SetParent(barriers, false);
            b.transform.localPosition = new Vector3(x, 1.5f, 1.5f);
            b.AddComponent<BoxCollider>().size = new Vector3(0.2f, 3f, 3f);
        }

        // 4. El torii, justo despues del final de las paredes.
        DestroyChild(exit, "Torii");
        Transform torii = Group(exit, "Torii");
        torii.localPosition = new Vector3(0f, 0f, 0.3f);
        BuildTorii(torii, m);

        // 5. Luz roja baja, adelante del torii.
        var lightGo = new GameObject("LuzTorii");
        lightGo.transform.SetParent(torii, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.35f, -1f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.28f, 0.15f);
        light.range = 6f;
        light.intensity = 3f;
        light.shadows = LightShadows.None;
    }

    private static void BuildTorii(Transform torii, Mats m)
    {
        Mesh pillar = GetOrCreateMesh("Torii_Pilar"); BuildLathe(pillar, ToriiPillarProfile, 8);
        Mesh pillarBase = GetOrCreateMesh("Torii_Base"); BuildLathe(pillarBase, ToriiBaseProfile, 8);
        Mesh ropeHalf = GetOrCreateMesh("Torii_Soga"); BuildLathe(ropeHalf, RopeHalfProfile, 6);
        Mesh tassel = GetOrCreateMesh("Torii_Borla"); BuildLathe(tassel, TasselProfile, 6);

        // Pilares con su base negra.
        foreach (float x in new[] { -1.25f, 1.25f })
        {
            string side = x < 0 ? "Izq" : "Der";
            MeshChild(torii, "Pilar" + side, pillar, m.torii, new Vector3(x, 0f, 0f));
            MeshChild(torii, "Base" + side, pillarBase, m.cable, new Vector3(x, 0f, 0f));
        }

        // Vigas: nuki (abajo), shimaki y kasagi (arriba, con las puntas levantadas).
        BoxChild(torii, "Nuki", m.torii, new Vector3(0f, 2.45f, 0f), new Vector3(3.0f, 0.16f, 0.12f), Vector3.zero);
        BoxChild(torii, "Shimaki", m.torii, new Vector3(0f, 3.05f, 0f), new Vector3(3.3f, 0.12f, 0.2f), Vector3.zero);
        BoxChild(torii, "Kasagi", m.torii, new Vector3(0f, 3.2f, 0f), new Vector3(2.8f, 0.16f, 0.28f), Vector3.zero);
        BoxChild(torii, "KasagiTecho", m.cable, new Vector3(0f, 3.3f, 0f), new Vector3(2.8f, 0.04f, 0.3f), Vector3.zero);
        BoxChild(torii, "KasagiPuntaIzq", m.torii, new Vector3(-1.66f, 3.25f, 0f), new Vector3(0.6f, 0.16f, 0.28f), new Vector3(0f, 0f, -10f));
        BoxChild(torii, "KasagiPuntaDer", m.torii, new Vector3(1.66f, 3.25f, 0f), new Vector3(0.6f, 0.16f, 0.28f), new Vector3(0f, 0f, 10f));

        // Poste central y placa con su nombre: 歸途, "el camino de vuelta" (AtmosphereBuilder.Signs.cs).
        BoxChild(torii, "Gakuzuka", m.torii, new Vector3(0f, 2.75f, 0f), new Vector3(0.12f, 0.5f, 0.1f), Vector3.zero);
        BuildToriiPlaque(torii, m);

        // Shimenawa: soga de paja que cuelga un poco en el medio (dos mitades inclinadas).
        GameObject ropeL = MeshChild(torii, "SogaIzq", ropeHalf, m.soga, new Vector3(-0.62f, 2.17f, -0.08f));
        ropeL.transform.localRotation = Quaternion.Euler(0f, 0f, 84f);
        GameObject ropeR = MeshChild(torii, "SogaDer", ropeHalf, m.soga, new Vector3(0.62f, 2.17f, -0.08f));
        ropeR.transform.localRotation = Quaternion.Euler(0f, 0f, 96f);

        // Borlas de paja y tiras de papel en zigzag (shide).
        foreach (float x in new[] { -0.55f, 0f, 0.55f })
            MeshChild(torii, "Borla", tassel, m.soga, new Vector3(x, 2.1f, -0.08f));

        float[] shideX = { -0.85f, -0.3f, 0.3f, 0.85f };
        for (int i = 0; i < shideX.Length; i++)
        {
            float x = shideX[i];
            BoxChild(torii, "Shide" + i + "a", m.papel, new Vector3(x, 2.0f, -0.09f), new Vector3(0.09f, 0.16f, 0.01f), Vector3.zero);
            BoxChild(torii, "Shide" + i + "b", m.papel, new Vector3(x + 0.03f, 1.85f, -0.09f), new Vector3(0.09f, 0.16f, 0.01f), Vector3.zero);
            BoxChild(torii, "Shide" + i + "c", m.papel, new Vector3(x, 1.7f, -0.09f), new Vector3(0.09f, 0.16f, 0.01f), Vector3.zero);
        }
    }
}
