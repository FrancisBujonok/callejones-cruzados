using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 28: un objeto que se puede mirar con E (una lata, una jaula, una ventana, el telefono).
/// No muestra una imagen como los carteles: el mundo sigue a la vista y abajo aparece lo que
/// piensa el personaje.
///
/// Que dice: la clave (key) busca sus lineas en Dialogos/Objetos.txt, un archivo de texto que
/// se puede editar sin tocar codigo (ver ObjectLines). Hay lineas para cuando hay calma, para
/// cuando hay miedo y para cuando ya lo miraste antes.
///
/// No necesita collider: SignInspector lo encuentra por sus Renderers, igual que a los carteles.
/// Lo agrega la herramienta de editor a cada objeto (AtmosphereBuilder.Inspect).
/// </summary>
public class Inspectable : MonoBehaviour
{
    /// <summary>Los que estan prendidos ahora (los de las piezas apagadas no cuentan).</summary>
    public static readonly List<Inspectable> Active = new List<Inspectable>();

    [Tooltip("Busca las lineas con esta clave en Dialogos/Objetos.txt (por ejemplo: lata, jaula, persiana).")]
    [SerializeField] private string key = "objeto";
    [Tooltip("Metros maximos desde los ojos hasta el centro del objeto.")]
    [SerializeField] private float maxDistance = 2.6f;

    private Renderer[] renderers;

    public string Key => key;
    public float MaxDistance => maxDistance;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(string newKey, float distance)
    {
        key = newKey;
        maxDistance = distance;
    }

    private void OnEnable()
    {
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        Active.Add(this);
    }

    private void OnDisable() => Active.Remove(this);

    /// <summary>
    /// La caja que envuelve lo que se ve del objeto. false si no se ve nada (todo apagado o
    /// fuera de la camara).
    /// </summary>
    public bool TryGetVisibleBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false, visible = false;
        if (renderers == null) return false;
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r.isVisible) visible = true;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any && visible;
    }
}
