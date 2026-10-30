using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Muestra una marca al lado de la opcion elegida del menu: el farolito en el inicio y el final,
/// el cuadrado rojo en el afiche de la pausa y la raya roja en las opciones (Paso 17).
///
/// "Elegida" es lo mismo para el teclado y el mouse: SelectOnHover hace que pasar el mouse
/// tambien elija la opcion, asi nunca hay dos marcas a la vez.
/// Va en cada boton o barra del menu.
/// </summary>
public class MenuItemMarker : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Tooltip("Lo que aparece cuando la opcion esta elegida.")]
    [SerializeField] private Graphic marker;

    public void Setup(Graphic markerGraphic)
    {
        marker = markerGraphic;
        Show(false);
    }

    private void OnEnable()
    {
        Show(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject);
    }

    public void OnSelect(BaseEventData eventData) => Show(true);
    public void OnDeselect(BaseEventData eventData) => Show(false);

    private void Show(bool visible)
    {
        // Se apaga el componente y no el objeto: asi no se mueve nada de lugar.
        if (marker != null) marker.enabled = visible;
    }
}
