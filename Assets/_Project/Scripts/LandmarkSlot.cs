using UnityEngine;

/// <summary>
/// Un pedazo de pared donde puede ir un lugar (Landmark). Tiene su fachada (persiana, puerta,
/// vidriera...): mientras hay un lugar aca, la fachada se esconde, y vuelve cuando el lugar se va.
/// Lo crea la herramienta Callejones > Fachadas y lugares; lo usa LandmarkDirector.
/// </summary>
public class LandmarkSlot : MonoBehaviour
{
    [Tooltip("La fachada de este pedazo de pared. Se esconde mientras haya un lugar aca.")]
    [SerializeField] private GameObject facade;

    public Landmark Occupant { get; private set; }

    /// <summary>Lo usa la herramienta de editor al armar el prefab.</summary>
    public void Setup(GameObject newFacade)
    {
        facade = newFacade;
    }

    /// <summary>Pone el lugar aca, pegado a la pared.</summary>
    public void Occupy(Landmark landmark)
    {
        Occupant = landmark;
        if (facade != null) facade.SetActive(false);
        landmark.transform.SetParent(transform, false);
        landmark.transform.localPosition = Vector3.zero;
        landmark.transform.localRotation = Quaternion.identity;
        landmark.gameObject.SetActive(true);
    }

    /// <summary>Saca el lugar, lo guarda en 'storage' (apagado) y vuelve a mostrar la fachada.</summary>
    public void Release(Transform storage)
    {
        if (Occupant != null)
        {
            Occupant.gameObject.SetActive(false);
            Occupant.transform.SetParent(storage, false);
            Occupant = null;
        }
        if (facade != null) facade.SetActive(true);
    }
}
