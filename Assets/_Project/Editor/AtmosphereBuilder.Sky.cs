using UnityEditor;
using UnityEngine;

/// <summary>
/// Parte de AtmosphereBuilder: el cielo nocturno (Paso 18).
/// Menu: Callejones > Cielo nocturno, cables y carteles.
///
/// Antes el fondo era un color liso (el de la niebla) y arriba de las paredes no habia nada.
/// Ahora la camara dibuja un skybox con el shader Callejones/Cielo Nocturno (Shaders/CieloNocturno.shader):
/// abajo tiene el MISMO color de la niebla, asi el fondo de los pasillos no hace corte; mas arriba,
/// el resplandor anaranjado de la ciudad, estrellas, luna, nubes, siluetas de edificios y una
/// vuelta al mundo a lo lejos. Todo con los pocos colores y el dithering del estilo PS1.
///
/// El menu tambien rearma las piezas, porque los cables, los postes y los carteles nuevos
/// (AtmosphereBuilder.Overhead.cs y AtmosphereBuilder.Signs.cs) viven adentro de los prefabs.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string SkyShaderName = "Callejones/Cielo Nocturno";
    private const string SkyMaterialPath = MaterialsDir + "/CieloNocturno.mat";

    [MenuItem("Callejones/Cielo nocturno, cables y carteles", priority = 27)]
    public static void SkyMenu()
    {
        if (!CanRun()) return;
        GameObject farol = AssetDatabase.LoadAssetAtPath<GameObject>(FarolPath);
        if (farol == null)
        {
            EditorUtility.DisplayDialog("Callejones", "Falta el prefab Farol. Ejecuta primero Callejones > Construir atmosfera (todo).", "OK");
            return;
        }

        Mats m = BuildMaterials();  // incluye los carteles nuevos (S, N, D, F y A)
        DecorateModule(m, farol);   // el recto: placas, papelitos, cables, postes y carteles altos
        BuildMaze(m, farol);        // Esquina, Cruce y Fondo: lo mismo, mas el deposito
        SetupNightLighting();       // ahora con el cielo
        AssetDatabase.SaveAssets();
        Debug.Log("[Callejones] Cielo nocturno, cables y carteles listos. Guarda con Ctrl+S.");
    }

    /// <summary>
    /// Crea o actualiza el material del cielo. Los colores salen de la paleta del juego; el resto
    /// (alturas, cantidad de estrellas, luna, nubes) queda en los valores del shader y se puede
    /// tocar a mano en el material. Devuelve null si el shader no compilo.
    /// </summary>
    private static Material BuildSkyMaterial()
    {
        Shader shader = Shader.Find(SkyShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[Callejones] No encuentro el shader " + SkyShaderName + ". Queda el fondo liso.");
            return null;
        }

        Material m = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, SkyMaterialPath);
        }
        else if (m.shader != shader)
        {
            m.shader = shader;
        }

        m.SetColor("_HorizonColor", FogColor);      // igual a la niebla: sin corte al fondo
        m.SetColor("_GlowColor", Hex("#3E190D"));    // resplandor anaranjado de la ciudad
        m.SetColor("_MidColor", Hex("#221A33"));     // violeta apagado
        m.SetColor("_ZenithColor", Hex("#0D1024"));  // azul casi negro
        m.SetColor("_StarColor", Hex("#EDE6D2"));
        m.SetColor("_MoonColor", Hex("#E6D5AB"));
        m.SetColor("_CloudColor", Hex("#3A2128"));
        m.SetColor("_CityColor", Hex("#120705"));    // siluetas, apenas mas oscuras que la niebla
        m.SetColor("_WindowColor", Hex("#D08A45"));
        EditorUtility.SetDirty(m);
        return m;
    }
}
