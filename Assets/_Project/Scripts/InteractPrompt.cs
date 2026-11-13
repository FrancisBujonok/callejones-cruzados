using TMPro;
using UnityEngine;

/// <summary>
/// Paso 36: el aviso de accion ("E - Jugar a los aros") arriba del pensamiento y la mira (un
/// puntito en el centro) que aparece mientras se apunta en un juego.
///
/// Hay uno solo (Instance), en el panel "Accion" de la interfaz. Cada script que quiera mostrar
/// algo llama Show cada cuadro mientras corresponda; si nadie lo llama, el aviso se apaga solo.
///
/// Lo arma Callejones > Menus (y la feria, que usa los mismos menus).
/// </summary>
public class InteractPrompt : MonoBehaviour
{
    public static InteractPrompt Instance { get; private set; }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;
    [SerializeField] private GameObject crosshair;

    private int shownFrame = -10;
    private bool crosshairOn;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(CanvasGroup canvasGroup, TMP_Text text, GameObject aim)
    {
        group = canvasGroup;
        label = text;
        crosshair = aim;
    }

    private void Awake()
    {
        Instance = this;
        if (group != null) group.alpha = 0f;
        if (crosshair != null) crosshair.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Muestra el aviso este cuadro (hay que llamarlo cada cuadro mientras se quiera ver).</summary>
    public static void Show(string text)
    {
        if (Instance == null || Instance.label == null) return;
        if (Instance.label.text != text) Instance.label.text = text;
        Instance.shownFrame = Time.frameCount;
    }

    public static void Crosshair(bool on)
    {
        if (Instance == null) return;
        Instance.crosshairOn = on;
    }

    private void LateUpdate()
    {
        bool on = Time.frameCount - shownFrame <= 1 && Time.timeScale > 0f;
        if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, on ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        if (crosshair != null && crosshair.activeSelf != (crosshairOn && Time.timeScale > 0f))
            crosshair.SetActive(crosshairOn && Time.timeScale > 0f);
    }
}
