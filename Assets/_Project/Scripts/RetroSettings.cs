using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controla el estilo PS1. Va en la Main Camera.
/// - Resolucion baja: durante Play, el juego se dibuja a pocas lineas (240 como la PS1)
///   y se agranda con pixeles duros (filtro Point del asset de URP).
/// - Vertices que tiemblan, pocos colores y dithering: los aplica el shader
///   "Callejones/PS1 Lit" leyendo variables globales que escribe este script.
/// Funciona tambien fuera de Play (ExecuteAlways) para ver el efecto en la escena,
/// salvo la resolucion baja, que solo se aplica en Play.
/// Al apagar el componente o salir de Play todo vuelve a como estaba.
/// </summary>
[ExecuteAlways]
public class RetroSettings : MonoBehaviour
{
    private static readonly int SnapId = Shader.PropertyToID("_PS1SnapResolution");
    private static readonly int LevelsId = Shader.PropertyToID("_PS1ColorLevels");
    private static readonly int DitherId = Shader.PropertyToID("_PS1DitherStrength");

    [Header("Resolucion (solo en Play)")]
    [SerializeField] private bool lowResolution = true;
    [Tooltip("Alto de la imagen interna en pixeles. La PS1 usaba 240.")]
    [SerializeField, Range(120, 720)] private int targetHeight = 240;

    [Header("Vertices")]
    [Tooltip("Alto de la grilla a la que saltan los vertices. Mas bajo = mas temblor. 0 = apagado.")]
    [SerializeField, Range(0f, 480f)] private float vertexSnap = 160f;

    [Header("Color")]
    [Tooltip("Niveles por canal de color. La PS1 usaba 32. 0 = apagado.")]
    [SerializeField, Range(0f, 256f)] private float colorLevels = 32f;
    [Tooltip("Intensidad del patron de puntos que disimula los saltos de color.")]
    [SerializeField, Range(0f, 1f)] private float dither = 0.8f;

    private UniversalRenderPipelineAsset urp;
    private float originalScale = -1f;

    private void OnEnable()
    {
        ApplyGlobals();
    }

    private void Update()
    {
        ApplyGlobals();
        if (Application.isPlaying) ApplyResolution();
    }

    private void OnValidate()
    {
        ApplyGlobals();
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(SnapId, 0f);
        Shader.SetGlobalFloat(LevelsId, 0f);
        Shader.SetGlobalFloat(DitherId, 0f);
        RestoreResolution();
    }

    private void ApplyGlobals()
    {
        if (!isActiveAndEnabled) return;
        Shader.SetGlobalFloat(SnapId, vertexSnap);
        Shader.SetGlobalFloat(LevelsId, colorLevels);
        Shader.SetGlobalFloat(DitherId, dither);
    }

    private void ApplyResolution()
    {
        if (urp == null) urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;

        // Se guarda la escala original una sola vez, para devolverla al salir.
        if (originalScale < 0f) originalScale = urp.renderScale;

        float wanted = lowResolution
            ? Mathf.Clamp(targetHeight / (float)Mathf.Max(1, Screen.height), 0.1f, 1f)
            : originalScale;

        if (Mathf.Abs(urp.renderScale - wanted) > 0.001f) urp.renderScale = wanted;
    }

    private void RestoreResolution()
    {
        if (urp != null && originalScale > 0f) urp.renderScale = originalScale;
        originalScale = -1f;
    }
}
