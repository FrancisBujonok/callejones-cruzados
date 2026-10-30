using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: fachadas variadas y lugares que mienten.
/// Menu: Callejones > Fachadas, carteles y lugares (tambien corre dentro de "Construir atmosfera (todo)").
///
/// 1. Fachadas: cada pedazo de pared que antes tenia siempre una persiana ahora sortea entre
///    tres: persiana, entrada con noren y pared con equipos (la puerta y la vidriera se sacaron).
///    Rectos: un pedazo en cada pared. Esquina y Fondo: la pared del fondo. El Cruce conserva su
///    persiana fija porque ahi va la anomalia de los bucles.
/// 2. Arriba de cada recto se sortea: nada, ropa colgada o una pancarta con texto.
/// 3. Lugares: tres prefabs (maquina expendedora, altar, telefono publico), una copia de cada uno
///    en la escena dentro de Laberinto/Lugares, y el componente LandmarkDirector en Laberinto.
///
/// Cada pedazo de pared con fachada es un LandmarkSlot: ahi puede ir un lugar, y mientras esta,
/// la fachada se esconde.
///
/// Convencion: fachadas y lugares se arman mirando hacia +X, con la espalda contra la pared en
/// x = 0, centrados en z = 0 y con el piso en y = 0 (igual que las persianas).
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string MaquinaPath = Root + "/Prefabs/Lugar_Maquina.prefab";
    private const string AltarPath = Root + "/Prefabs/Lugar_Altar.prefab";
    private const string TelefonoPath = Root + "/Prefabs/Lugar_Telefono.prefab";

    // Perfil del farolito de papel de la entrada con noren (radio, altura), de arriba hacia abajo.
    private static readonly Vector2[] ChochinProfile =
    {
        new Vector2(0.06f, 0.42f), new Vector2(0.14f, 0.37f), new Vector2(0.16f, 0.21f),
        new Vector2(0.14f, 0.05f), new Vector2(0.06f, 0f),
    };

    [MenuItem("Callejones/Fachadas, carteles y lugares", priority = 26)]
    public static void PlacesMenu()
    {
        if (!CanRun()) return;
        GameObject farol = AssetDatabase.LoadAssetAtPath<GameObject>(FarolPath);
        if (farol == null)
        {
            EditorUtility.DisplayDialog("Callejones", "Falta el prefab Farol. Ejecuta primero Callejones > Construir atmosfera (todo).", "OK");
            return;
        }

        Mats m = BuildMaterials();  // incluye un material por cada cartel (Textures/Carteles)
        DecorateModule(m, farol);   // el recto entero: decoracion, carteles, fachadas y lo de arriba
        BuildMaze(m, farol);        // Esquina, Cruce y Fondo con sus fachadas y carteles
        BuildLandmarks(m);
        UpdateToriiPlaque(m);       // solo la placa: el torii conserva su sonido
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Fachadas, carteles y lugares listos. Guarda con Ctrl+S.");
    }

    // ------------------------------------------------------------------ 1 y 2. fachadas y arriba

    /// <summary>
    /// En el recto: saca las persianas viejas (estaban dentro de cada variante), pone una fachada
    /// por pared y lo de arriba. Las fachadas van en "Decorado" porque las dos variantes
    /// (Abierto y Cerrado) tienen las mismas paredes de costado.
    /// </summary>
    private static void BuildRectoFacades(Transform root, Mats m, Mesh shutter)
    {
        foreach (string variant in new[] { "Abierto", "Cerrado" })
        {
            Transform v = root.Find(variant);
            if (v != null) DestroyChild(v, "Persianas");
        }

        Transform deco = root.Find("Decorado");
        DestroyChild(deco, "Fachadas");
        DestroyChild(deco, "Arriba");

        // Los mismos lugares donde estaban las persianas: la otra mitad de cada pared es de los Sets.
        Transform facades = Group(deco, "Fachadas");
        BuildFacadeSlot(facades, "FachadaIzq", new Vector3(-WallInnerX, 0f, -1.5f), 0f, m, shutter);
        BuildFacadeSlot(facades, "FachadaDer", new Vector3(WallInnerX, 0f, 1.5f), 180f, m, shutter);
        BuildOverhead(deco, m);
    }

    /// <summary>
    /// Un pedazo de pared con tres fachadas posibles (DecorRandomizer elige una).
    /// La puerta de madera y la vidriera iluminada se sacaron en el Paso 16: no convencian.
    /// </summary>
    private static LandmarkSlot BuildFacadeSlot(Transform parent, string name, Vector3 wallPoint, float yaw, Mats m, Mesh shutter)
    {
        var slot = new GameObject(name);
        slot.transform.SetParent(parent, false);
        slot.transform.localPosition = wallPoint;
        slot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Transform options = Group(slot.transform, "Opciones");
        var list = new List<GameObject>
        {
            CreateShutterYaw(options, "Persiana", Vector3.zero, 0f, shutter, m),
            BuildNoren(options, m),
            BuildUtilityWall(options, m),
        };
        for (int i = 1; i < list.Count; i++) list[i].SetActive(false); // en el editor se ve la persiana
        options.gameObject.AddComponent<DecorRandomizer>().Setup(list.ToArray());

        LandmarkSlot landmarkSlot = slot.AddComponent<LandmarkSlot>();
        landmarkSlot.Setup(options.gameObject);
        return landmarkSlot;
    }

    // Entrada oscura con noren (cortina de tela partida), farolito de papel y sal en la puerta.
    private static GameObject BuildNoren(Transform parent, Mats m)
    {
        Transform t = Group(parent, "Noren");
        BoxChild(t, "Hueco", m.hueco, new Vector3(0.005f, 1.0f, 0f), new Vector3(0.01f, 2.0f, 1.2f), Vector3.zero);
        BoxChild(t, "MarcoIzq", m.madera, new Vector3(0.04f, 1.05f, -0.64f), new Vector3(0.08f, 2.1f, 0.08f), Vector3.zero);
        BoxChild(t, "MarcoDer", m.madera, new Vector3(0.04f, 1.05f, 0.64f), new Vector3(0.08f, 2.1f, 0.08f), Vector3.zero);
        BoxChild(t, "Dintel", m.madera, new Vector3(0.05f, 2.1f, 0f), new Vector3(0.1f, 0.1f, 1.36f), Vector3.zero);
        BoxChild(t, "Barra", m.metal, new Vector3(0.12f, 2.0f, 0f), new Vector3(0.03f, 0.03f, 1.4f), Vector3.zero);
        for (int i = 0; i < 3; i++)
            BoxChild(t, "Tela" + i, m.noren, new Vector3(0.12f, 1.68f, -0.4f + i * 0.4f), new Vector3(0.015f, 0.6f, 0.37f), new Vector3(0f, (i - 1) * 4f, 0f));

        Mesh chochin = GetOrCreateMesh("Chochin");
        BuildLathe(chochin, ChochinProfile, 8);
        BoxChild(t, "BrazoFarolito", m.metal, new Vector3(0.15f, 2.05f, 0.9f), new Vector3(0.3f, 0.03f, 0.03f), Vector3.zero);
        MeshChild(t, "Farolito", chochin, m.farolTela, new Vector3(0.3f, 1.55f, 0.9f));
        BoxChild(t, "FarolitoTapa", m.farolTapa, new Vector3(0.3f, 1.98f, 0.9f), new Vector3(0.14f, 0.04f, 0.14f), Vector3.zero);

        // Morishio: dos montoncitos de sal a los costados de la puerta.
        BoxChild(t, "SalIzq", m.papel, new Vector3(0.12f, 0.05f, -0.78f), new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0f, 45f, 0f));
        BoxChild(t, "SalDer", m.papel, new Vector3(0.12f, 0.05f, 0.78f), new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0f, 45f, 0f));
        return t.gameObject;
    }

    // Pared sin entrada, con equipos: aire acondicionado en el piso, caños, medidor, afiche.
    private static GameObject BuildUtilityWall(Transform parent, Mats m)
    {
        Transform t = Group(parent, "Equipos");
        SolidBoxChild(t, "AireExterior", m.metal, new Vector3(0.16f, 0.28f, -0.55f), new Vector3(0.32f, 0.56f, 0.75f), Vector3.zero);
        BoxChild(t, "Rejilla", m.cable, new Vector3(0.325f, 0.3f, -0.6f), new Vector3(0.01f, 0.4f, 0.4f), Vector3.zero);
        BoxChild(t, "CanoA", m.metal, new Vector3(0.05f, 1.2f, 0.7f), new Vector3(0.06f, 2.4f, 0.06f), Vector3.zero);
        BoxChild(t, "CanoB", m.metal, new Vector3(0.05f, 1.2f, 0.85f), new Vector3(0.06f, 2.4f, 0.06f), Vector3.zero);
        BoxChild(t, "CanoArriba", m.metal, new Vector3(0.05f, 2.3f, 0.3f), new Vector3(0.06f, 0.06f, 1.0f), Vector3.zero);
        BoxChild(t, "MedidorGas", m.metal, new Vector3(0.06f, 1.4f, 0.3f), new Vector3(0.12f, 0.35f, 0.25f), Vector3.zero);
        CreatePoster(t, "Afiche", new Vector3(0.012f, 1.5f, -0.4f), m); // persona buscada, en tres idiomas
        BoxChild(t, "CajaElectrica", m.metal, new Vector3(0.05f, 1.95f, -0.75f), new Vector3(0.1f, 0.45f, 0.35f), Vector3.zero);
        BoxChild(t, "LuzRoja", m.luzRoja, new Vector3(0.105f, 2.05f, -0.65f), new Vector3(0.01f, 0.03f, 0.03f), Vector3.zero);
        return t.gameObject;
    }

    /// <summary>Arriba del recto: nada, ropa colgada de una soga o una pancarta de lado a lado.</summary>
    private static void BuildOverhead(Transform deco, Mats m)
    {
        Transform arriba = Group(deco, "Arriba");
        Transform nada = Group(arriba, "Nada");

        Transform ropa = Group(arriba, "Ropa");
        BoxChild(ropa, "Soga", m.cable, new Vector3(0f, 2.55f, 0.2f), new Vector3(2.76f, 0.015f, 0.015f), Vector3.zero);
        Material[] cloth = { m.papel, m.toldo, m.noren, m.balde, m.maceta };
        float[] x = { -0.95f, -0.45f, 0.1f, 0.6f, 1.0f };
        float[] w = { 0.34f, 0.42f, 0.3f, 0.45f, 0.28f };
        float[] h = { 0.45f, 0.55f, 0.38f, 0.6f, 0.42f };
        for (int i = 0; i < x.Length; i++)
            BoxChild(ropa, "Prenda" + i, cloth[i], new Vector3(x[i], 2.55f - h[i] * 0.5f, 0.2f), new Vector3(w[i], h[i], 0.02f),
                new Vector3(0f, i % 2 == 0 ? 6f : -5f, 0f));

        // Pancarta: dos caras (una para cada lado del pasillo) con el mismo texto horizontal.
        // Mide lo mismo que su PNG en proporcion, asi las letras no se estiran.
        Transform banner = Group(arriba, "Pancarta");
        Material first = FirstOr(m.signsH5, m.cartel);
        MeshRenderer faceA = SignFace(banner, "CaraA", new Vector3(0f, 2.45f, -0.306f), SignH5Size, 0f, first);
        MeshRenderer faceB = SignFace(banner, "CaraB", new Vector3(0f, 2.45f, -0.294f), SignH5Size, 180f, first);
        AddSignPick(banner.gameObject, m.signsH5, faceA, faceB);
        BoxChild(banner, "CuerdaIzq", m.cable, new Vector3(-1.165f, 2.67f, -0.3f), new Vector3(0.44f, 0.015f, 0.015f), new Vector3(0f, 0f, -13f));
        BoxChild(banner, "CuerdaDer", m.cable, new Vector3(1.165f, 2.67f, -0.3f), new Vector3(0.44f, 0.015f, 0.015f), new Vector3(0f, 0f, 13f));

        ropa.gameObject.SetActive(false);
        banner.gameObject.SetActive(false);
        arriba.gameObject.AddComponent<DecorRandomizer>().Setup(new[] { nada.gameObject, ropa.gameObject, banner.gameObject });
    }

    // ------------------------------------------------------------------ 3. los lugares

    private static void BuildLandmarks(Mats m)
    {
        CorridorTreadmill treadmill = FindInScene<CorridorTreadmill>(null);
        if (treadmill == null)
        {
            Debug.LogWarning("[Callejones] No encuentro CorridorTreadmill (objeto Laberinto).");
            return;
        }

        AudioClip hum = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/Farol_Zumbido.wav");
        AudioClip bells = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/Torii_Campanas.wav");
        GameObject[] prefabs =
        {
            SaveLandmark(BuildVendingMachine(m, hum), MaquinaPath),
            SaveLandmark(BuildAltar(m, bells), AltarPath),
            SaveLandmark(BuildPhone(m), TelefonoPath),
        };

        // Una copia de cada uno en la escena, apagada, dentro de Laberinto/Lugares.
        Transform maze = treadmill.transform;
        Transform old = maze.Find("Lugares");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var group = new GameObject("Lugares");
        Undo.RegisterCreatedObjectUndo(group, "Lugares");
        group.transform.SetParent(maze, false);

        var list = new List<Landmark>();
        foreach (GameObject prefab in prefabs)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            go.SetActive(false); // LandmarkDirector los prende cuando los pone en una pared
            list.Add(go.GetComponent<Landmark>());
        }

        LandmarkDirector director = maze.GetComponent<LandmarkDirector>();
        if (director == null) director = Undo.AddComponent<LandmarkDirector>(maze.gameObject);
        var so = new SerializedObject(director);
        SerializedProperty array = so.FindProperty("landmarks");
        array.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static GameObject SaveLandmark(GameObject root, string path)
    {
        try
        {
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Maquina expendedora: luz fria (el unico blanco azulado del callejon) y un zumbido grave.
    private static GameObject BuildVendingMachine(Mats m, AudioClip hum)
    {
        var root = new GameObject("Lugar_Maquina");
        root.AddComponent<Landmark>().Setup("Maquina expendedora", new Vector3(0.36f, 1.0f, 0f));
        Transform t = root.transform;

        SolidBoxChild(t, "Cuerpo", m.maquina, new Vector3(0.36f, 0.925f, 0f), new Vector3(0.72f, 1.85f, 0.9f), Vector3.zero);
        BoxChild(t, "Vitrina", m.maquinaLuz, new Vector3(0.725f, 1.32f, 0f), new Vector3(0.02f, 0.78f, 0.8f), Vector3.zero);
        Material[] cans = { m.torii, m.balde, m.ventanaLuz, m.planta, m.papel, m.maceta };
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 6; col++)
                BoxChild(t, $"Lata{row}{col}", cans[(row * 2 + col) % cans.Length],
                    new Vector3(0.74f, 1.06f + row * 0.25f, -0.3f + col * 0.12f), new Vector3(0.02f, 0.13f, 0.07f), Vector3.zero);
            BoxChild(t, "Botones" + row, m.luzRoja, new Vector3(0.74f, 0.97f + row * 0.25f, 0f), new Vector3(0.012f, 0.025f, 0.72f), Vector3.zero);
        }
        BoxChild(t, "PanelBajo", m.metal, new Vector3(0.725f, 0.5f, 0f), new Vector3(0.02f, 0.62f, 0.8f), Vector3.zero);
        BoxChild(t, "Bandeja", m.hueco, new Vector3(0.735f, 0.28f, 0.05f), new Vector3(0.02f, 0.16f, 0.5f), Vector3.zero);
        BoxChild(t, "Monedero", m.metal, new Vector3(0.74f, 0.82f, 0.3f), new Vector3(0.03f, 0.16f, 0.12f), Vector3.zero);
        BoxChild(t, "Franja", m.luzRoja, new Vector3(0.725f, 1.78f, 0f), new Vector3(0.02f, 0.1f, 0.88f), Vector3.zero);

        Light light = PointLight(t, "Luz", new Vector3(1.05f, 1.3f, 0f), new Color(0.62f, 0.85f, 1f), 3.6f, 1.3f);
        LandmarkSound(light.transform, hum, 0.22f, 0.72f, 7f);
        return root;
    }

    // Altar chico (hokora): base de piedra, casita de madera con techo, cuerda sagrada,
    // ofrendas y dos velas. Una vela parpadea con el miedo (usa LanternLight sin balanceo).
    private static GameObject BuildAltar(Mats m, AudioClip bells)
    {
        var root = new GameObject("Lugar_Altar");
        root.AddComponent<Landmark>().Setup("Altar", new Vector3(0.25f, 0.8f, 0f));
        Transform t = root.transform;

        SolidBoxChild(t, "Base", m.piedra, new Vector3(0.25f, 0.375f, 0f), new Vector3(0.5f, 0.75f, 0.7f), Vector3.zero);
        BoxChild(t, "Casita", m.madera, new Vector3(0.22f, 0.96f, 0f), new Vector3(0.4f, 0.42f, 0.5f), Vector3.zero);
        BoxChild(t, "Puertita", m.hueco, new Vector3(0.425f, 0.95f, 0f), new Vector3(0.01f, 0.28f, 0.26f), Vector3.zero);
        BoxChild(t, "TechoA", m.teja, new Vector3(0.22f, 1.24f, -0.15f), new Vector3(0.56f, 0.035f, 0.36f), new Vector3(-28f, 0f, 0f));
        BoxChild(t, "TechoB", m.teja, new Vector3(0.22f, 1.24f, 0.15f), new Vector3(0.56f, 0.035f, 0.36f), new Vector3(28f, 0f, 0f));
        BoxChild(t, "Cumbrera", m.madera, new Vector3(0.22f, 1.33f, 0f), new Vector3(0.58f, 0.04f, 0.04f), Vector3.zero);
        BoxChild(t, "Cuerda", m.soga, new Vector3(0.44f, 1.13f, 0f), new Vector3(0.03f, 0.03f, 0.52f), Vector3.zero);
        BoxChild(t, "PapelA", m.papel, new Vector3(0.45f, 1.04f, -0.12f), new Vector3(0.005f, 0.12f, 0.04f), Vector3.zero);
        BoxChild(t, "PapelB", m.papel, new Vector3(0.45f, 1.04f, 0.12f), new Vector3(0.005f, 0.12f, 0.04f), Vector3.zero);
        BoxChild(t, "OfrendaA", m.papel, new Vector3(0.42f, 0.78f, -0.07f), new Vector3(0.06f, 0.06f, 0.06f), Vector3.zero);
        BoxChild(t, "OfrendaB", m.papel, new Vector3(0.42f, 0.78f, 0.07f), new Vector3(0.06f, 0.06f, 0.06f), Vector3.zero);
        BoxChild(t, "OfrendaRoja", m.torii, new Vector3(0.36f, 0.79f, 0f), new Vector3(0.05f, 0.08f, 0.05f), Vector3.zero);
        BoxChild(t, "VelaA", m.papel, new Vector3(0.42f, 0.8f, -0.25f), new Vector3(0.03f, 0.1f, 0.03f), Vector3.zero);
        BoxChild(t, "VelaB", m.papel, new Vector3(0.42f, 0.8f, 0.25f), new Vector3(0.03f, 0.1f, 0.03f), Vector3.zero);
        BoxChild(t, "LlamaB", m.resplandor, new Vector3(0.42f, 0.875f, 0.25f), new Vector3(0.025f, 0.04f, 0.025f), Vector3.zero);

        Transform flicker = Group(t, "Vela");
        GameObject flame = BoxChild(flicker, "LlamaA", m.resplandor, new Vector3(0.42f, 0.875f, -0.25f), new Vector3(0.025f, 0.04f, 0.025f), Vector3.zero);
        Light light = PointLight(flicker, "Luz", new Vector3(0.6f, 1.0f, 0f), new Color(1f, 0.72f, 0.4f), 2.4f, 0.9f);
        LanternLight candle = flicker.gameObject.AddComponent<LanternLight>();
        candle.Setup(light, flame.GetComponent<MeshRenderer>());
        var so = new SerializedObject(candle);
        so.FindProperty("swayAngle").floatValue = 0f;          // una vela no se mece
        so.FindProperty("calmFlicker").floatValue = 0.3f;
        so.FindProperty("fearFlicker").floatValue = 0.7f;
        so.FindProperty("flickerSpeed").floatValue = 6f;
        so.FindProperty("emissionColor").colorValue = new Color(1f, 0.55f, 0.2f);
        so.FindProperty("emissionIntensity").floatValue = 1.6f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Campanitas graves y bajitas: el mismo sonido del torii, mas lento.
        LandmarkSound(t, bells, 0.14f, 0.8f, 6f);
        return root;
    }

    // Telefono publico verde, en una repisa con techito, vidrios a los costados y cartel encendido.
    private static GameObject BuildPhone(Mats m)
    {
        var root = new GameObject("Lugar_Telefono");
        root.AddComponent<Landmark>().Setup("Telefono publico", new Vector3(0.25f, 1.2f, 0f));
        Transform t = root.transform;

        SolidBoxChild(t, "Pie", m.metal, new Vector3(0.2f, 0.5f, 0f), new Vector3(0.1f, 1.0f, 0.1f), Vector3.zero);
        BoxChild(t, "Repisa", m.metal, new Vector3(0.19f, 1.0f, 0f), new Vector3(0.38f, 0.04f, 0.55f), Vector3.zero);
        SolidBoxChild(t, "Telefono", m.telefono, new Vector3(0.16f, 1.22f, 0f), new Vector3(0.22f, 0.4f, 0.3f), Vector3.zero);
        BoxChild(t, "Pantalla", m.telefonoLuz, new Vector3(0.275f, 1.36f, 0f), new Vector3(0.01f, 0.06f, 0.14f), Vector3.zero);
        BoxChild(t, "Teclado", m.metal, new Vector3(0.275f, 1.18f, -0.04f), new Vector3(0.01f, 0.12f, 0.1f), Vector3.zero);
        BoxChild(t, "Auricular", m.telefono, new Vector3(0.3f, 1.24f, 0.16f), new Vector3(0.06f, 0.24f, 0.06f), Vector3.zero);
        BoxChild(t, "Cable", m.cable, new Vector3(0.29f, 1.05f, 0.14f), new Vector3(0.015f, 0.25f, 0.015f), Vector3.zero);
        BoxChild(t, "Techo", m.metal, new Vector3(0.25f, 1.82f, 0f), new Vector3(0.5f, 0.03f, 0.7f), Vector3.zero);
        // Postes finos en vez de vidrios: asi el telefono se ve tambien desde el costado.
        BoxChild(t, "PosteIzq", m.metal, new Vector3(0.47f, 1.41f, -0.33f), new Vector3(0.03f, 0.82f, 0.03f), Vector3.zero);
        BoxChild(t, "PosteDer", m.metal, new Vector3(0.47f, 1.41f, 0.33f), new Vector3(0.03f, 0.82f, 0.03f), Vector3.zero);
        BoxChild(t, "Cartel", m.telefonoLuz, new Vector3(0.5f, 1.92f, 0f), new Vector3(0.02f, 0.14f, 0.45f), Vector3.zero);

        PointLight(t, "Luz", new Vector3(0.6f, 1.7f, 0f), new Color(0.75f, 1f, 0.82f), 2.6f, 0.8f);
        return root;
    }

    private static Light PointLight(Transform parent, string name, Vector3 localPos, Color color, float range, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }

    /// <summary>Sonido 3D en loop que arranca cada vez que el lugar se prende (playOnAwake).</summary>
    private static void LandmarkSound(Transform parent, AudioClip clip, float volume, float pitch, float maxDistance)
    {
        if (clip == null) return; // sin el sonido generado (Callejones > Solo sonido), el lugar queda mudo
        AudioSource source = NewSource(parent, "Sonido", clip, true, 180);
        source.volume = volume;
        source.pitch = pitch;
        source.playOnAwake = true;
        Make3D(source, 0.6f, maxDistance);
    }
}
