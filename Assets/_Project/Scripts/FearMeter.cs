using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Medidor de miedo. Sube cada vez que el laberinto cambia y baja lentamente
/// cuando pasa un rato sin que ocurra nada.
/// Va en el Player. MazeDirector.onMutation se conecta a OnMazeMutated desde el Inspector.
/// </summary>
public class FearMeter : MonoBehaviour
{
    [Header("Valores")]
    [SerializeField] private float maxFear = 100f;
    [Tooltip("Miedo que suma cada cambio del laberinto.")]
    [SerializeField] private float fearPerMutation = 15f;
    [Tooltip("Miedo que baja por segundo cuando no pasa nada.")]
    [SerializeField] private float decayPerSecond = 2f;
    [Tooltip("Segundos de calma antes de que el miedo empiece a bajar.")]
    [SerializeField] private float decayDelay = 5f;

    [Header("Eventos")]
    [Tooltip("Envia el miedo de 0 a 1. Mas adelante: latidos, vineta, distorsion de camara.")]
    public UnityEvent<float> onFearChanged;
    [Tooltip("El miedo llego al maximo.")]
    public UnityEvent onMaxFear;

    [Header("Debug")]
    [SerializeField] private bool showDebugBar = true;

    public float Fear { get; private set; }
    public float Normalized => maxFear > 0f ? Fear / maxFear : 0f;

    private float calmTimer;
    private bool maxReported;

    /// <summary>Conectar aca el evento onMutation de MazeDirector.</summary>
    public void OnMazeMutated()
    {
        AddFear(fearPerMutation);
    }

    public void AddFear(float amount)
    {
        SetFear(Fear + amount);
        calmTimer = 0f; // algo paso: se reinicia la calma
    }

    private void Update()
    {
        calmTimer += Time.deltaTime;

        if (calmTimer >= decayDelay && Fear > 0f)
        {
            SetFear(Fear - decayPerSecond * Time.deltaTime);
        }
    }

    private void SetFear(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxFear);
        if (Mathf.Approximately(clamped, Fear)) return;

        Fear = clamped;
        onFearChanged?.Invoke(Normalized);

        if (Fear >= maxFear && !maxReported)
        {
            maxReported = true;
            Debug.Log("[FearMeter] Miedo al maximo.");
            onMaxFear?.Invoke();
        }
        else if (Fear < maxFear * 0.9f)
        {
            maxReported = false; // puede volver a dispararse si sube de nuevo
        }
    }

    // Barra en pantalla sin Canvas. Solo para el prototipo.
    private void OnGUI()
    {
        if (!showDebugBar) return;

        GUI.Label(new Rect(12, 60, 400, 22), $"Miedo: {Fear:0} / {maxFear:0}");
        GUI.Box(new Rect(12, 82, 204, 14), GUIContent.none);

        Color previous = GUI.color;
        GUI.color = Color.Lerp(Color.yellow, Color.red, Normalized);
        GUI.DrawTexture(new Rect(14, 84, 200f * Normalized, 10), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
