using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Al pasar el mouse por un boton, lo marca como "elegido" (el mismo estado que da el teclado).
/// Sin esto, el boton elegido con el teclado y el que esta bajo el mouse se resaltan a la vez,
/// y parece que hay dos opciones marcadas.
/// Va en cada boton del menu.
/// </summary>
public class SelectOnHover : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
