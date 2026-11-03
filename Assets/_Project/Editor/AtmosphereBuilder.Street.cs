using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte de AtmosphereBuilder: la calle que se ve al salir del callejon (Paso 22).
/// Menu: Callejones > Calle de la salida (tambien corre con "Construir atmosfera (todo)").
///
/// Antes, detras del torii habia un resplandor rosa: la salida era "una luz". Ahora se ve una
/// calle comun, como al llegar a la esquina de un callejon de verdad. Hay tres calles y en cada
/// partida aparece una (ExitStreet), nunca la misma dos veces seguidas:
///   0  calle de faroles (guirnaldas de faroles rojos, negocios, semaforo, una moto)
///   1  callecita tranquila (casas bajas, plantas, expendedora, postes con cables)
///   2  avenida (almacen de 24 horas, camion, semaforos, senda peatonal)
///
/// Cada calle es una caja abierta de 8.8 x 10 m detras del torii: piso, dos fachadas y un telon
/// al fondo con la foto de la calle que sigue (Herramientas/generar_calles.py arma las cuatro
/// texturas). Las caras usan Shaders/Calle.shader: sin luces (la luz viene pintada) y con menos
/// niebla que el resto, para que la calle se vea desde el callejon.
///
/// Cambios en ModuloSalida:
/// - Resplandor: se apaga su MeshRenderer. Queda el collider: sigue siendo la pared invisible.
/// - Edificios: arriba de las paredes del callejon, como en el resto del laberinto. Sin ellos,
///   por encima de las paredes se veia el cielo y lo que hubiera al lado.
/// - Calle: las tres calles y el ExitStreet.
/// - CorridorSegment: celdas de mas (la caja de la calle), asi ninguna pieza del laberinto se
///   mete adentro de la calle.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string StreetTexDir = Root + "/Textures/Calles";
    private const string CalleShaderName = "Callejones/Calle";
    private const int StreetCount = 3;
    private const float StreetHalfWidth = 4.4f;  // linea de fachadas
    private const float StreetLength = 10f;      // hasta el telon
    private const float StreetHeight = 18f;      // alto de las fachadas y del telon

    // Celdas de 3 x 3 m que ocupa la calle, ademas de las dos del modulo (z = -1.5 y 1.5).
    private static readonly Vector3[] ExitStreetCells =
    {
        new Vector3(-3f, 0f, 1.5f), new Vector3(3f, 0f, 1.5f),
        new Vector3(-3f, 0f, 4.5f), new Vector3(0f, 0f, 4.5f), new Vector3(3f, 0f, 4.5f),
        new Vector3(-3f, 0f, 7.5f), new Vector3(0f, 0f, 7.5f), new Vector3(3f, 0f, 7.5f),
        new Vector3(-3f, 0f, 10.5f), new Vector3(0f, 0f, 10.5f), new Vector3(3f, 0f, 10.5f),
    };

    [MenuItem("Callejones/Calle de la salida", priority = 30)]
    public static void ExitStreetMenu()
    {
        if (!CanRun()) return;
        Transform exit = FindInScene<Transform>(t => t.name == "ModuloSalida");
        if (exit == null)
        {
            EditorUtility.DisplayDialog("Callejones", "No encuentro el ModuloSalida en la escena.", "OK");
            return;
        }
        Mats m = BuildMaterials();
        Undo.RegisterFullObjectHierarchyUndo(exit.gameObject, "Calle de la salida");
        BuildExitStreet(exit, m);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Callejones] Calle de la salida lista. Guarda con Ctrl+S.");
    }

    /// <summary>Lo llama BuildExitTorii (al final) y el menu. No toca el torii.</summary>
    private static void BuildExitStreet(Transform exit, Mats m)
    {
        // 1. Sin resplandor rosa: queda solo el collider.
        Transform glow = exit.Find("Resplandor");
        if (glow != null)
        {
            var r = glow.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }

        // 2. Edificios sobre las paredes del callejon (z de -3 a 0), sin voladizos: no tapan el torii.
        DestroyChild(exit, "Edificios");
        Transform buildings = Group(exit, "Edificios");
        ExitBuilding(buildings, "Izq", new Vector3(-WallInnerX, 0f, -1.5f), 0f, 5, 7710, m);
        ExitBuilding(buildings, "Der", new Vector3(WallInnerX, 0f, -1.5f), 180f, 4, 7720, m);

        // 3. Las tres calles.
        DestroyChild(exit, "Calle");
        Transform street = Group(exit, "Calle");
        var options = new List<GameObject>();
        for (int n = 0; n < StreetCount; n++)
        {
            Transform variant = Group(street, "Calle" + n);
            if (!BuildStreetVariant(variant, n, m))
            {
                Debug.LogWarning("[Callejones] Faltan las texturas de " + StreetTexDir + ". Corre Herramientas/generar_calles.py.");
                DestroyChild(exit, "Calle");
                return;
            }
            variant.gameObject.SetActive(n == 0); // en el editor se ve la primera
            options.Add(variant.gameObject);
        }
        street.gameObject.AddComponent<ExitStreet>().Setup(options.ToArray());

        // 4. Que ninguna pieza del laberinto se meta en la calle.
        CorridorSegment seg = exit.GetComponent<CorridorSegment>();
        if (seg != null)
        {
            seg.SetExtraCells(ExitStreetCells);
            EditorUtility.SetDirty(seg);
        }
    }

    private static void ExitBuilding(Transform parent, string name, Vector3 wallPoint, float yaw, int floors, int seed, Mats m)
    {
        Transform side = Group(parent, name);
        side.localPosition = wallPoint;
        side.localRotation = Quaternion.Euler(0f, yaw, 0f);
        BuildBuildingSide(side, "Salida_" + name, 3f, floors, seed, false, m);
        AddWindowSeedPick(side);
    }

    // ------------------------------------------------------------------ una calle

    private static bool BuildStreetVariant(Transform parent, int n, Mats m)
    {
        Texture2D ground = LoadStreetTexture(n, "Suelo");
        Texture2D left = LoadStreetTexture(n, "Izq");
        Texture2D right = LoadStreetTexture(n, "Der");
        Texture2D back = LoadStreetTexture(n, "Fondo");
        if (ground == null || left == null || right == null || back == null) return false;

        string prefix = "Calles/Calle" + n + "_";
        // El piso tapa al piso del callejon donde se pisan (de z = 0 a 3): corrimiento como los carteles.
        StreetPiece(parent, "Suelo", StreetGroundMesh(), GetCalleMaterial(prefix + "Suelo", ground, Color.white, DecalOffset));
        StreetPiece(parent, "FachadaIzq", StreetSideMesh(false), GetCalleMaterial(prefix + "Izq", left, Color.white, 0f));
        StreetPiece(parent, "FachadaDer", StreetSideMesh(true), GetCalleMaterial(prefix + "Der", right, Color.white, 0f));
        StreetPiece(parent, "Fondo", StreetBackdropMesh(), GetCalleMaterial(prefix + "Fondo", back, Color.white, 0f));

        // Lo que esta dentro de los primeros 10 m va en 3D (lo de mas lejos ya esta en el telon).
        if (n == 0)
        {
            // Dos guirnaldas de faroles rojos, como en la foto de Taipei.
            Material lit = GetCalleMaterial("Calles/Farolito", null, new Color(1.7f, 0.55f, 0.22f, 1f), 0f);
            Mesh chochin = GetOrCreateMesh("Chochin");
            BuildLathe(chochin, ChochinProfile, 8);
            var spans = new List<Span>();
            foreach (float z in new[] { 3.5f, 7f })
            {
                Vector3 a = new Vector3(-StreetHalfWidth + 0.05f, 5.2f, z), b = new Vector3(StreetHalfWidth - 0.05f, 5.2f, z);
                const float sag = 0.5f;
                spans.Add(new Span(a, b, sag, 0.015f));
                for (float x = -3.6f; x <= 3.61f; x += 0.8f)
                {
                    float t = (x - a.x) / (b.x - a.x);
                    float y = 5.2f - sag * 4f * t * (1f - t) - 0.47f;
                    GameObject lantern = MeshChild(parent, "Farolito", chochin, lit, new Vector3(x, y, z));
                    lantern.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            CablesChild(parent, "Calle0_Guirnaldas", spans, m);
        }
        else if (n == 1)
        {
            // Un farol de calle (su luz en el piso ya esta pintada en la textura).
            Material lamp = GetCalleMaterial("Calles/Farol", null, new Color(1.5f, 1.65f, 1.9f, 1f), 0f);
            BoxChild(parent, "Poste", m.cable, new Vector3(-3.9f, 2.3f, 6f), new Vector3(0.09f, 4.6f, 0.09f), Vector3.zero);
            BoxChild(parent, "Brazo", m.cable, new Vector3(-3.6f, 4.55f, 6f), new Vector3(0.6f, 0.05f, 0.05f), Vector3.zero);
            BoxChild(parent, "Lampara", lamp, new Vector3(-3.32f, 4.5f, 6f), new Vector3(0.28f, 0.07f, 0.14f), Vector3.zero);
        }
        return true;
    }

    private static void StreetPiece(Transform parent, string name, Mesh mesh, Material mat)
    {
        GameObject go = MeshChild(parent, name, mesh, mat, Vector3.zero);
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // ------------------------------------------------------------------ mallas (coordenadas del modulo)

    /// <summary>Piso: x de -4.4 a 4.4, z de 0 a 10. UV: u = x, v = z.</summary>
    private static Mesh StreetGroundMesh()
    {
        const float y = 0.002f;
        float w = StreetHalfWidth;
        return StreetQuad("Calle_Suelo",
            new Vector3(-w, y, 0f), new Vector3(w, y, 0f), new Vector3(w, y, StreetLength), new Vector3(-w, y, StreetLength),
            Vector3.up);
    }

    /// <summary>
    /// Fachada de un costado: z de 0 a 10, de 0 a 18 m de alto. UV: u = z (las dos igual, como las
    /// arma el script de Python), v = altura.
    /// </summary>
    private static Mesh StreetSideMesh(bool right)
    {
        float x = right ? StreetHalfWidth : -StreetHalfWidth;
        return StreetQuad(right ? "Calle_FachadaDer" : "Calle_FachadaIzq",
            new Vector3(x, 0f, 0f), new Vector3(x, 0f, StreetLength), new Vector3(x, StreetHeight, StreetLength), new Vector3(x, StreetHeight, 0f),
            right ? Vector3.left : Vector3.right);
    }

    /// <summary>Telon: en z = 10, x de -4.4 a 4.4, de 0 a 18 m. UV: u = x, v = altura.</summary>
    private static Mesh StreetBackdropMesh()
    {
        float w = StreetHalfWidth;
        return StreetQuad("Calle_Fondo",
            new Vector3(-w, 0f, StreetLength), new Vector3(w, 0f, StreetLength), new Vector3(w, StreetHeight, StreetLength), new Vector3(-w, StreetHeight, StreetLength),
            Vector3.back);
    }

    /// <summary>Un cuadrado con UV (0,0), (1,0), (1,1), (0,1) en las esquinas a, b, c, d.</summary>
    private static Mesh StreetQuad(string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
    {
        Mesh mesh = GetOrCreateMesh(name);
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        AddQuad(v, n, t, a, b, c, d, outward);
        var uv = new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        Apply(mesh, v, n, t, uv);
        return mesh;
    }

    // ------------------------------------------------------------------ texturas y materiales

    private static Texture2D LoadStreetTexture(int n, string part)
    {
        string path = StreetTexDir + "/Calle" + n + "_" + part + ".png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) return null;
        }
        SetupSignImport(path); // pixeles duros, sin mipmaps (como los carteles)
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material GetCalleMaterial(string name, Texture2D tex, Color color, float offset)
    {
        Shader shader = Shader.Find(CalleShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[Callejones] No encuentro el shader " + CalleShaderName + ".");
            return null;
        }
        string path = MaterialsDir + "/" + name + ".mat";
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_FogScale", 0.45f);
        mat.SetFloat("_Offset", offset);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
