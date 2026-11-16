using UnityEditor;
using UnityEngine;

/// <summary>
/// Paso 38 (provisorio): la adivina y la gente con la que se puede hablar.
///
/// - La adivina (puesto N12): FortuneTeller, con una luz en la bola de cristal y las campanillas.
/// - La duena del gato: una persona nueva al lado del arco, con un monton de afiches en la mano.
///   Al hablar con ella queda la marca "DuenoGato" (CatTrail se acuerda en la segunda aparicion).
/// - Seis personas que ya estaban (la de la parada, el del almacen, la senora del banco, el musico,
///   uno de los trabajadores de atras del escenario y el del kiosco): FairTalk, con lineas cortas.
///
/// Las lineas son provisorias: se cambian aca o, ya armada la escena, en el componente FairTalk de
/// cada persona. Lo arma Callejones > Feria de dia (capitulo 1), despues de la gente.
/// </summary>
public static partial class AtmosphereBuilder
{
    private static void BuildFairTalk(Transform world, FairMats f)
    {
        // 1. La adivina.
        Transform stall = FindDeep(world, "Puesto_N12_Adivina");
        if (stall != null)
        {
            Transform seat = Group(stall, "DondeSeSienta");
            seat.localPosition = new Vector3(0f, 0f, 0.55f);
            seat.localRotation = Quaternion.Euler(0f, 180f, 0f); // mirandola

            var lightGo = new GameObject("LuzBola");
            lightGo.transform.SetParent(stall, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.05f, -0.75f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.65f, 0.75f, 1f);
            light.range = 2.6f;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;

            AudioClip[] bells = LoadRealSounds("Campanillas");
            AudioSource chimes = null;
            if (bells.Length > 0)
            {
                chimes = NewSource(stall, "CampanillasAdivina", bells[0], false, 80);
                chimes.volume = 0.5f;
                Make3D(chimes, 1.5f, 10f);
            }
            stall.gameObject.AddComponent<FortuneTeller>().Setup(seat, light, chimes);
        }
        else Debug.LogWarning("[Callejones] No encontre el puesto de la adivina (Puesto_N12_Adivina).");

        // 2. La duena del gato, al lado del pilar del arco que tiene el afiche (mira a los que llegan).
        Transform people = FindDeep(world, "Gente") ?? world;
        var look = new Look { skin = PSkin0, hair = PHairGray, top = PBeige, bottom = PGray, shoes = PBlack, style = 1 };
        Rig owner = StillPerson(people, "DuenaDelGato", new Vector3(-31.5f, 0f, 3.5f), 270f, look, Pose.Standing, FairPerson.Role.Idle);
        var flyers = new MeshDraft();
        flyers.Box(new Vector3(0.12f, 1.02f, 0.22f), new Vector3(0.21f, 0.28f, 0.03f), FCrema); // la pila de afiches
        flyers.Box(new Vector3(0.12f, 1.05f, 0.24f), new Vector3(0.16f, 0.12f, 0.005f), FNaranja); // el gato manchado
        FairDraft(owner.root, "Afiches", flyers, f.paleta, false);
        owner.root.gameObject.AddComponent<FairTalk>().Setup(new[]
        {
            "Una señora con un montón de papeles. Me da uno: es el afiche del gato.",
            "Dice un nombre, varias veces. Creo que es el del gato.",
            "Señala la feria, después a mí, y junta las manos. Me pide que lo busque.",
        }, "Me vuelve a mostrar el papel. Sigue esperando.", "", "DuenoGato");

        // 3. Gente que ya estaba.
        Talk(world, "Parada", new Vector3(-65.9f, 0f, -4.7f), new[]
        {
            "Mira el reloj y después la calle. Espera el colectivo.",
            "Me pregunta algo. Le muestro que no entiendo y se encoge de hombros.",
        }, "Sigue mirando la calle.", "Me mira fijo. El colectivo no viene.");
        Talk(world, "Almacen", new Vector3(-49f, 0f, 9.9f), new[]
        {
            "Me saluda desde la puerta del almacén.",
            "Me señala para adentro: tiene cosas frías, creo.",
        }, "Me vuelve a saludar.", "Está en la puerta, quieto. Ya no me saluda.");
        Talk(world, "Banco", new Vector3(-17.3f, 0f, -4.6f), new[]
        {
            "Una señora mayor. Me señala el banco para que me siente.",
            "Me cuenta algo largo, riéndose. No entiendo nada, pero me río con ella.",
        }, "Me palmea el brazo.", "Ya no se ríe. Me mira las manos.");
        Talk(world, "Musico", new Vector3(-4f, 0f, -4.6f), new[]
        {
            "Me hace un gesto con la cabeza sin dejar de tocar.",
            "Cuando termina, me guiña un ojo y empieza la misma canción.",
        }, "Sigue tocando.", "Toca la misma canción otra vez. Me mira mientras toca.");
        Talk(world, "Trabajador", new Vector3(39f, 0f, 5.6f), new[]
        {
            "Me hace señas: por acá no se puede pasar.",
            "Señala el escenario y levanta dos dedos. ¿A las dos? ¿En dos horas?",
        }, "Me vuelve a hacer señas para que me vaya.", "Deja de hablar con los otros y me mira.");
        Talk(world, "Kiosco", new Vector3(88.3f, 0f, 7.7f), new[]
        {
            "Me ofrece un diario. No entiendo nada de la tapa.",
            "En una esquina de la página hay una foto chiquita de un gato.",
        }, "Me vuelve a ofrecer el diario.", "Tiene el diario abierto en la página del gato. Me lo muestra.");
    }

    /// <summary>Le pone FairTalk a la persona cuyo nombre empieza con 'prefix' mas cercana a 'near'.</summary>
    private static void Talk(Transform world, string prefix, Vector3 near, string[] lines, string again, string uneasy)
    {
        FairPerson best = null;
        float bestD = 3f;
        foreach (FairPerson p in world.GetComponentsInChildren<FairPerson>(true))
        {
            if (!p.name.StartsWith(prefix)) continue;
            float d = Vector3.Distance(p.transform.position, near);
            if (d < bestD) { bestD = d; best = p; }
        }
        if (best == null) { Debug.LogWarning("[Callejones] No encontre a '" + prefix + "' para hablar."); return; }
        best.gameObject.AddComponent<FairTalk>().Setup(lines, again, uneasy, "");
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            Transform r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
