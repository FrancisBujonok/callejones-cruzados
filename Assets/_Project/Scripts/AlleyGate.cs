using UnityEngine;

/// <summary>
/// Paso 33: lo que tapa la entrada del callejon de la feria (un carro de vendedor, cajones
/// apilados y una valla) hasta que el gato termina su recorrido (CatTrail).
///
/// Cuando CatTrail lo abre, no desaparece de golpe delante del jugador: espera a que la entrada
/// quede fuera de la vista de la camara (o lejos, a mas de 'farDistance'). Es lo mismo que hacen
/// los callejones despues: las cosas cambian cuando no las miras. Si el jugador se queda mirando
/// fijo, a los 'fallbackSeconds' se abre igual.
///
/// Va en el objeto "Tapon" de la entrada del callejon. Lo arma Callejones > Feria de dia.
/// </summary>
public class AlleyGate : MonoBehaviour
{
    [Tooltip("Lo que se saca (con sus colliders).")]
    [SerializeField] private GameObject[] blockers;
    [SerializeField] private float farDistance = 35f;
    [SerializeField] private float fallbackSeconds = 25f;

    public bool IsOpen { get; private set; }

    private bool pending;
    private float pendingSince;
    private Bounds area;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(GameObject[] parts) => blockers = parts;

    public void Open()
    {
        if (IsOpen || pending) return;
        pending = true;
        pendingSince = Time.time;
    }

    private void Start()
    {
        // La caja que encierra todo lo que tapa (para saber si la camara la ve).
        bool first = true;
        if (blockers != null)
            foreach (GameObject b in blockers)
            {
                if (b == null) continue;
                foreach (Renderer r in b.GetComponentsInChildren<Renderer>())
                {
                    if (first) { area = r.bounds; first = false; }
                    else area.Encapsulate(r.bounds);
                }
            }
        if (first) area = new Bounds(transform.position, Vector3.one * 3f);
    }

    private void Update()
    {
        if (!pending) return;
        Camera cam = Camera.main;
        bool seen = cam != null && Vector3.Distance(cam.transform.position, area.center) < farDistance &&
                    GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), area);
        if (seen && Time.time - pendingSince < fallbackSeconds) return;

        pending = false;
        IsOpen = true;
        if (blockers != null)
            foreach (GameObject b in blockers)
                if (b != null) b.SetActive(false);
    }
}
