using UnityEditor;
using UnityEngine;

/// <summary>
/// Parte de AtmosphereBuilder: los objetos que se pueden mirar con E (Paso 28).
///
/// - Inspect(objeto, clave): le agrega un Inspectable. Lo usan los que arman la decoracion
///   (AtmosphereBuilder.Decor, .Maze y .Places), asi cada lata, jaula, ventana o persiana nueva
///   ya sale lista para mirar.
/// - SetupObjectLines: le pasa a SignInspector (en el Player) el archivo Dialogos/Objetos.txt.
///   Ese archivo NO lo genera la herramienta: es para editarlo a mano, y rearmar la escena no
///   lo pisa.
///
/// Claves que usa la decoracion: lata, bolsa, comida, botella, plumas, maceta, balde, jaula,
/// caja_agujeros, cajon, medidor, aire, ventana_luz, ventana_oscura, persiana, noren, sal,
/// equipos, maquina, telefono, altar.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string ObjectLinesPath = Root + "/Dialogos/Objetos.txt";

    /// <summary>Le agrega (o actualiza) un Inspectable. Devuelve el mismo objeto, para encadenar.</summary>
    private static GameObject Inspect(GameObject go, string key, float maxDistance = 2.6f)
    {
        if (go == null) return null;
        Inspectable inspectable = go.GetComponent<Inspectable>();
        if (inspectable == null) inspectable = go.AddComponent<Inspectable>();
        inspectable.Setup(key, maxDistance);
        return go;
    }

    private static void SetupObjectLines()
    {
        SignInspector inspector = FindInScene<SignInspector>(null);
        if (inspector == null)
        {
            Debug.LogWarning("[Callejones] No hay SignInspector en el Player. Ejecuta Callejones > Pistas y mirar de cerca.");
            return;
        }
        TextAsset lines = AssetDatabase.LoadAssetAtPath<TextAsset>(ObjectLinesPath);
        if (lines == null) Debug.LogWarning("[Callejones] Falta " + ObjectLinesPath + ": los objetos van a decir 'No tiene nada de especial.'");
        Undo.RecordObject(inspector, "Lineas de los objetos");
        inspector.SetupObjects(lines, inspector.GetComponent<FearMeter>());
        EditorUtility.SetDirty(inspector);
    }
}
