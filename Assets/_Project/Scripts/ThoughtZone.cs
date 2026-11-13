using UnityEngine;

/// <summary>
/// Paso 35: un borde que el personaje no quiere cruzar. En lugar de una valla, hay una pared
/// invisible (un collider aparte) y, del lado del jugador, una franja: si el jugador se queda
/// empujando contra la pared un momento, el personaje piensa por que no sigue (ThoughtText).
///
/// "Empujando" = adentro de la franja y chocando de costado (CharacterController.collisionFlags)
/// durante 'pushSeconds'. Asi no salta por caminar al lado de la pared, solo por insistir.
/// Las lineas van en orden y vuelven a empezar; entre una y otra hay al menos 'cooldown' segundos.
///
/// Va en los objetos "NoQuiereIr..." de la feria. Lo arma Callejones > Feria de dia.
/// </summary>
public class ThoughtZone : MonoBehaviour
{
    [Tooltip("Tamano de la franja (centrada en este objeto, sin girar).")]
    [SerializeField] private Vector3 size = new Vector3(1.2f, 3f, 18f);
    [TextArea] [SerializeField] private string[] lines;
    [SerializeField] private float pushSeconds = 0.35f;
    [SerializeField] private float cooldown = 7f;

    private CharacterController player;
    private float pushing, nextAllowed;
    private int next;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Vector3 zoneSize, string[] thoughts)
    {
        size = zoneSize;
        lines = thoughts;
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (player == null || lines == null || lines.Length == 0) return;
        if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Playing) { pushing = 0f; return; }

        bool inside = new Bounds(transform.position, size).Contains(player.transform.position);
        bool blocked = (player.collisionFlags & CollisionFlags.Sides) != 0;
        pushing = inside && blocked ? pushing + Time.deltaTime : 0f;
        if (pushing < pushSeconds || Time.time < nextAllowed) return;

        ThoughtText.Show(lines[next]);
        next = (next + 1) % lines.Length;
        nextAllowed = Time.time + cooldown;
        pushing = 0f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireCube(transform.position, size);
    }
}
