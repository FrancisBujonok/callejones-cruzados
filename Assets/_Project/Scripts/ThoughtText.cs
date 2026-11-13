using TMPro;
using UnityEngine;

/// <summary>
/// Paso 35: lo que piensa el personaje, una linea abajo de la pantalla (por ejemplo, cuando
/// quiere ir para un lado al que no tiene ganas de ir). Aparece, queda unos segundos y se va.
///
/// Hay uno solo (Instance), en el panel "Pensamiento" de la interfaz. Desde cualquier script:
///   ThoughtText.Show("Recien cruce. No tengo ganas de volver.");
/// Si no hay interfaz (o no esta este panel), no hace nada.
///
/// Lo arma Callejones > Menus (y la feria, que usa los mismos menus).
/// </summary>
public class ThoughtText : MonoBehaviour
{
    public static ThoughtText Instance { get; private set; }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;
    [SerializeField] private float fadeSeconds = 0.4f;

    private float showUntil = -1f;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(CanvasGroup canvasGroup, TMP_Text text)
    {
        group = canvasGroup;
        label = text;
    }

    private void Awake()
    {
        Instance = this;
        if (group != null) group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Muestra una linea durante 'seconds' segundos (reemplaza la que hubiera).</summary>
    public static void Show(string line, float seconds = 3.4f)
    {
        if (Instance == null || Instance.label == null) return;
        Instance.label.text = line;
        Instance.showUntil = Time.unscaledTime + seconds;
    }

    public static bool Visible => Instance != null && Instance.group != null && Instance.group.alpha > 0.01f;

    private void Update()
    {
        if (group == null) return;
        bool on = Time.unscaledTime < showUntil && Time.timeScale > 0f;
        float target = on ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeSeconds));
    }
}
