using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 24: indicador de miedo para las pruebas. Una barra fina en la esquina inferior
/// izquierda, visible solo mientras se juega, para ver el ritmo sin abrir los textos de F1.
///
/// - La barra va de amarillo apagado a rojo segun FearMeter.Normalized.
/// - Una marca blanca indica desde donde puede saltar la trampa de la plaza (0,5).
/// - Una sombra mas clara recuerda el pico reciente y baja despacio: se ve cuanto subio
///   el ultimo susto aunque el miedo ya este bajando.
/// - F3 la muestra u oculta. Es provisoria: se puede borrar el componente sin tocar nada mas.
///
/// Va en el Player, junto a FearMeter. Dibuja con OnGUI, sin Canvas, como los demas textos
/// de prueba. Se escala con el alto de la pantalla (referencia 540 px).
/// </summary>
public class FearHUD : MonoBehaviour
{
    [Tooltip("Si se deja vacio, usa el FearMeter de este mismo objeto.")]
    [SerializeField] private FearMeter fear;
    [Tooltip("Arranca visible. F3 la muestra u oculta durante el juego.")]
    [SerializeField] private bool visible = true;
    [Tooltip("Marca en la barra (0 a 1): desde aca puede saltar la trampa de la plaza.")]
    [SerializeField, Range(0f, 1f)] private float markAt = 0.5f;
    [Tooltip("Lo que baja por segundo la sombra del pico reciente (0 a 1).")]
    [SerializeField] private float peakFallPerSecond = 0.04f;

    private float peak;
    private GUIStyle style;

    private void Awake()
    {
        if (fear == null) fear = GetComponent<FearMeter>();
        if (fear == null) fear = FindAnyObjectByType<FearMeter>();
    }

    private void Update()
    {
        if (TogglePressed()) visible = !visible;
        if (fear == null) return;
        float now = fear.Normalized;
        peak = now >= peak ? now : Mathf.Max(now, peak - peakFallPerSecond * Time.deltaTime);
    }

    private static bool Playing() =>
        GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    private void OnGUI()
    {
        if (!visible || fear == null || !Playing()) return;

        float k = Screen.height / 540f;
        float w = 150f * k, h = 7f * k, pad = 16f * k;
        float x = pad, y = Screen.height - pad - h;
        float value = fear.Normalized;

        if (style == null) style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.Max(11, Mathf.RoundToInt(12f * k));
        style.normal.textColor = new Color(0.85f, 0.8f, 0.7f, 0.75f);

        Color previous = GUI.color;

        // Etiqueta chica arriba de la barra.
        float lineH = style.fontSize + 6f;
        GUI.Label(new Rect(x - 2f, y - lineH - 1f, w + 60f, lineH), $"miedo {fear.Fear:0}", style);

        // Fondo.
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x - k, y - k, w + 2f * k, h + 2f * k), Texture2D.whiteTexture);

        // Sombra del pico reciente.
        GUI.color = new Color(0.9f, 0.85f, 0.75f, 0.22f);
        GUI.DrawTexture(new Rect(x, y, w * peak, h), Texture2D.whiteTexture);

        // Miedo actual.
        Color low = new Color(0.75f, 0.68f, 0.35f, 0.9f);
        Color high = new Color(0.8f, 0.12f, 0.1f, 0.95f);
        GUI.color = Color.Lerp(low, high, value);
        GUI.DrawTexture(new Rect(x, y, w * value, h), Texture2D.whiteTexture);

        // Marca de la trampa de la plaza.
        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        GUI.DrawTexture(new Rect(x + w * markAt - 0.5f * k, y - 2f * k, Mathf.Max(1f, k), h + 4f * k), Texture2D.whiteTexture);

        GUI.color = previous;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TogglePressed() => Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame;
#else
    private static bool TogglePressed() => Input.GetKeyDown(KeyCode.F3);
#endif
}
