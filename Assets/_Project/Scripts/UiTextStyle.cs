using TMPro;
using UnityEngine;

/// <summary>
/// Paso 39: como se ven los textos de la interfaz (opcion "Texto" del menu de opciones,
/// GameSettings.TextStyle):
///
/// - Retro (0): como siempre. La interfaz se dibuja dentro de la imagen de 240 lineas, con la
///   fuente pixelada (DotGothic16): queda gruesa y algo borrosa, como en la PS1.
/// - Nitido (1): la misma fuente, pero la interfaz se dibuja a la resolucion de la pantalla
///   (Canvas en "Screen Space - Overlay"): las letras quedan con bordes limpios. El juego sigue a
///   240 lineas.
/// - Legible (2): como Nitido, con una fuente pensada para leerse facil (Atkinson Hyperlegible).
///
/// Va en el objeto "Interfaz" (el Canvas). Lo arma Callejones > Menus.
/// </summary>
public class UiTextStyle : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [Tooltip("La camara que dibuja la interfaz en modo Retro (CamaraInterfaz).")]
    [SerializeField] private Camera uiCamera;
    [SerializeField] private TMP_FontAsset retroFont;
    [SerializeField] private TMP_FontAsset legibleFont;

    private int applied = -1;
    private float planeDistance = 0.32f;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Canvas c, Camera cam, TMP_FontAsset retro, TMP_FontAsset legible)
    {
        canvas = c; uiCamera = cam; retroFont = retro; legibleFont = legible;
    }

    private void Awake()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) planeDistance = canvas.planeDistance;
        GameSettings.Load();
        Apply(GameSettings.TextStyle);
    }

    private void OnEnable() => GameSettings.TextStyleChanged += Apply;
    private void OnDisable() => GameSettings.TextStyleChanged -= Apply;

    private void Apply(int style)
    {
        if (style == applied || canvas == null) return;
        applied = style;

        if (style == 0 && uiCamera != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = planeDistance;
            uiCamera.enabled = true;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (uiCamera != null) uiCamera.enabled = false;
        }

        TMP_FontAsset font = style == 2 && legibleFont != null ? legibleFont : retroFont;
        if (font == null) return;
        foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.font == font) continue;
            if (t.font != retroFont && t.font != legibleFont) continue; // otra fuente a proposito: no se toca
            t.font = font;
            t.fontSharedMaterial = font.material;
        }
    }
}
