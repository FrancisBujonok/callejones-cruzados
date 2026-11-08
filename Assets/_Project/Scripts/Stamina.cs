using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 27: TROTE (concepto). Con Shift, mientras se camina hacia adelante, el jugador trota:
/// apenas mas rapido (x1.3), no una carrera. Gasta stamina y sube un poco el miedo: apurarse
/// en un lugar asi es admitir que algo te asusta.
///
/// - La stamina va de 0 a 1. Se gasta en 7 s de trote y se recupera en 5 s, un rato despues de
///   soltar Shift.
/// - Si se vacia, no se puede trotar hasta recuperar un tercio.
/// - El cambio de velocidad es gradual, para que la camara no pegue un salto.
/// - Los pasos, el balanceo y la racha del laberinto se miden por distancia, asi que al trotar
///   suenan mas seguido y los cambios llegan antes. No hizo falta tocarlos.
///
/// La barra es de prueba: queda al lado de la del miedo y F3 oculta las dos. En el juego no va
/// a estar. Va en el Player; FirstPersonController le pide la velocidad.
/// </summary>
public class Stamina : MonoBehaviour
{
    [Header("Trote")]
    [Tooltip("Cuanto mas rapido que caminar. 1.3 = un trote, no una carrera.")]
    [SerializeField] private float jogMultiplier = 1.3f;
    [Tooltip("Que tan rapido se pasa de caminar a trotar y al reves (multiplicador por segundo).")]
    [SerializeField] private float blendPerSecond = 1.5f;

    [Header("Stamina")]
    [SerializeField] private float secondsToEmpty = 7f;
    [SerializeField] private float secondsToFull = 5f;
    [Tooltip("Segundos sin trotar antes de empezar a recuperar.")]
    [SerializeField] private float regenDelay = 1.2f;
    [Tooltip("Despues de vaciarse, desde cuanto (0 a 1) se puede volver a trotar.")]
    [SerializeField, Range(0f, 1f)] private float recoverAt = 0.35f;

    [Header("Miedo")]
    [SerializeField] private FearMeter fear;
    [Tooltip("Miedo que suma por segundo de trote (de 100).")]
    [SerializeField] private float fearPerSecond = 0.8f;

    [Header("Barra de prueba")]
    [SerializeField] private bool visible = true;

    public float Value { get; private set; } = 1f;
    public bool Jogging { get; private set; }
    public bool Exhausted { get; private set; }
    public float Multiplier { get; private set; } = 1f;

    private float sinceJog = 99f;
    private float fearBuffer;
    private GUIStyle style;

    private void Awake()
    {
        if (fear == null) fear = GetComponent<FearMeter>();
    }

    /// <summary>
    /// Lo llama FirstPersonController una vez por frame. Devuelve el multiplicador de velocidad.
    /// movingForward: si el jugador camina hacia adelante (de costado o para atras no se trota).
    /// </summary>
    public float Tick(bool movingForward, float dt)
    {
        Jogging = movingForward && ShiftHeld() && !Exhausted && Value > 0f;

        if (Jogging)
        {
            Value = Mathf.Max(0f, Value - dt / secondsToEmpty);
            sinceJog = 0f;
            if (Value <= 0f) Exhausted = true;
            AddJogFear(dt);
        }
        else
        {
            sinceJog += dt;
            if (sinceJog >= regenDelay) Value = Mathf.Min(1f, Value + dt / secondsToFull);
            if (Exhausted && Value >= recoverAt) Exhausted = false;
        }

        Multiplier = Mathf.MoveTowards(Multiplier, Jogging ? jogMultiplier : 1f, blendPerSecond * dt);
        return Multiplier;
    }

    // El miedo se suma de a un punto: AddFear corta la calma del FearMeter, y no hace falta
    // llamarlo en cada frame.
    private void AddJogFear(float dt)
    {
        if (fear == null || fearPerSecond <= 0f) return;
        fearBuffer += fearPerSecond * dt;
        if (fearBuffer < 1f) return;
        fear.AddFear(1f);
        fearBuffer -= 1f;
    }

    private void Update()
    {
        if (TogglePressed()) visible = !visible;
    }

    private static bool Playing() =>
        GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    // Al lado de la barra del miedo (FearHUD), con las mismas medidas.
    private void OnGUI()
    {
        if (!visible || !Playing()) return;

        float k = Screen.height / 540f;
        float pad = 16f * k, h = 7f * k, w = 90f * k;
        float x = pad + 150f * k + 18f * k, y = Screen.height - pad - h;

        if (style == null) style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.Max(11, Mathf.RoundToInt(12f * k));
        style.normal.textColor = new Color(0.75f, 0.8f, 0.85f, 0.7f);

        float lineH = style.fontSize + 6f;
        GUI.Label(new Rect(x - 2f, y - lineH - 1f, w + 120f, lineH),
            Exhausted ? "stamina (concepto) - sin aire" : "stamina (concepto)", style);

        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x - k, y - k, w + 2f * k, h + 2f * k), Texture2D.whiteTexture);
        GUI.color = Exhausted ? new Color(0.55f, 0.45f, 0.4f, 0.8f) : new Color(0.62f, 0.72f, 0.78f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, w * Value, h), Texture2D.whiteTexture);
        // Marca: desde aca se vuelve a trotar despues de quedarse sin aire.
        GUI.color = new Color(1f, 1f, 1f, 0.35f);
        GUI.DrawTexture(new Rect(x + w * recoverAt, y, Mathf.Max(1f, k), h), Texture2D.whiteTexture);
        GUI.color = previous;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool ShiftHeld() =>
        Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
    private static bool TogglePressed() => Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame;
#else
    private static bool ShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    private static bool TogglePressed() => Input.GetKeyDown(KeyCode.F3);
#endif
}
