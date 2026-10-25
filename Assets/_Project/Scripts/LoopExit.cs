using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Detecta cuando el jugador cruza el final del pasillo de salida.
/// Va en un objeto hijo del modulo de salida que tenga un Box Collider marcado como Is Trigger.
/// Un trigger no frena al jugador: solo avisa cuando alguien entra en su volumen.
///
/// Por ahora solo muestra un mensaje. onLoopCompleted es el enchufe para lo que
/// venga despues: fundido a negro, escena siguiente, cinematica.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LoopExit : MonoBehaviour
{
    [Tooltip("Solo reacciona a objetos con esta etiqueta (Tag).")]
    [SerializeField] private string playerTag = "Player";

    [Header("Eventos")]
    [Tooltip("El jugador salio del loop.")]
    public UnityEvent onLoopCompleted;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessage = true;

    public bool Completed { get; private set; }

    // Reset se llama solo en el editor, al agregar el componente:
    // deja el collider ya marcado como trigger.
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Completed || !other.CompareTag(playerTag)) return;

        Completed = true;
        Debug.Log("[LoopExit] El jugador salio del loop.");
        onLoopCompleted?.Invoke();
    }

    private void OnGUI()
    {
        if (!showDebugMessage || !Completed) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Label(new Rect(0, Screen.height * 0.5f - 30, Screen.width, 60), "Saliste del loop", style);
    }
}
