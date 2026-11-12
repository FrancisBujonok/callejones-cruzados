using UnityEngine;

/// <summary>
/// Paso 32: el final de un capitulo. Una caja invisible (por ejemplo, unos metros adentro del
/// callejon de la feria): cuando el jugador entra, GameFlow hace el fundido y carga el capitulo
/// siguiente, jugando directo y con su tarjeta.
///
/// No usa colliders ni triggers: revisa cada frame si el jugador esta adentro de la caja
/// (Bounds). Asi no depende de la fisica ni de capas. La caja se ve en la vista Scene (Gizmos).
///
/// Va en el objeto "FinDelCapitulo". Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class ChapterExit : MonoBehaviour
{
    [Tooltip("La escena del capitulo siguiente (tiene que estar en Build Settings).")]
    [SerializeField] private string nextScene = "Prototipo";
    [Tooltip("El jugador. Si se deja vacio, busca el objeto con la etiqueta Player.")]
    [SerializeField] private Transform player;
    [Tooltip("Tamano de la caja, centrada en este objeto (sin girar).")]
    [SerializeField] private Vector3 size = new Vector3(3f, 3f, 2f);

    private bool done;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(string scene, Transform playerTransform, Vector3 boxSize)
    {
        nextScene = scene;
        player = playerTransform;
        size = boxSize;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    private void Update()
    {
        if (done || player == null) return;
        if (!new Bounds(transform.position, size).Contains(player.position)) return;
        if (GameFlow.Instance == null || GameFlow.Instance.Current != GameFlow.State.Playing) return;
        done = true;
        Debug.Log("[ChapterExit] Fin del capitulo: sigue " + nextScene + ".");
        GameFlow.Instance.CompleteChapter(nextScene);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(transform.position, size);
    }
}
