using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// La parte VISIBLE del flujo de juego: paneles (titulo, pausa, final), fundido a negro,
/// botones y textos. No decide nada: cuando se toca un boton, dispara un evento,
/// y GameFlow decide que hacer.
///
/// Separarlo asi permite cambiar el aspecto (los mockups) sin tocar las reglas, y al reves.
/// Va en el objeto "Interfaz" (el Canvas).
///
/// Todos los fundidos usan tiempo "sin escala" (unscaledDeltaTime): en pausa el juego
/// tiene timeScale 0, y con tiempo normal los menus quedarian congelados.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private CanvasGroup titlePanel;
    [SerializeField] private CanvasGroup pausePanel;
    [SerializeField] private CanvasGroup endingPanel;
    [Tooltip("Imagen negra a pantalla completa para los fundidos.")]
    [SerializeField] private CanvasGroup fade;

    [Header("Botones")]
    [SerializeField] private UnityEngine.UI.Button playButton;
    [SerializeField] private UnityEngine.UI.Button titleQuitButton;
    [SerializeField] private UnityEngine.UI.Button resumeButton;
    [SerializeField] private UnityEngine.UI.Button backToTitleButton;
    [SerializeField] private UnityEngine.UI.Button pauseQuitButton;

    [Header("Final")]
    [SerializeField] private TMPro.TMP_Text endingStats;
    [Tooltip("\"Presiona cualquier tecla...\": aparece un rato despues del final.")]
    [SerializeField] private CanvasGroup endingHint;

    [Header("Eventos (los escucha GameFlow)")]
    public UnityEvent onPlay = new UnityEvent();
    public UnityEvent onResume = new UnityEvent();
    public UnityEvent onBackToTitle = new UnityEvent();
    public UnityEvent onQuit = new UnityEvent();

    // Una animacion por panel: si se pide otra antes de que termine (pausar y despausar rapido),
    // la anterior se corta en vez de pelearse con la nueva.
    private readonly System.Collections.Generic.Dictionary<CanvasGroup, Coroutine> running =
        new System.Collections.Generic.Dictionary<CanvasGroup, Coroutine>();

    private void Awake()
    {
        if (playButton != null) playButton.onClick.AddListener(onPlay.Invoke);
        if (titleQuitButton != null) titleQuitButton.onClick.AddListener(onQuit.Invoke);
        if (resumeButton != null) resumeButton.onClick.AddListener(onResume.Invoke);
        if (backToTitleButton != null) backToTitleButton.onClick.AddListener(onBackToTitle.Invoke);
        if (pauseQuitButton != null) pauseQuitButton.onClick.AddListener(onQuit.Invoke);
    }

    // ------------------------------------------------------------------ paneles

    public void HideAllInstant()
    {
        SetGroup(titlePanel, 0f);
        SetGroup(pausePanel, 0f);
        SetGroup(endingPanel, 0f);
        SetGroup(endingHint, 0f);
    }

    public void ShowTitle(bool visible, float seconds) => Fade(titlePanel, visible, seconds);
    public void ShowPause(bool visible, float seconds) => Fade(pausePanel, visible, seconds);
    public void ShowEnding(bool visible, float seconds) => Fade(endingPanel, visible, seconds);
    public void ShowEndingHint(float seconds) => Fade(endingHint, true, seconds);

    public void SelectPlay() => Select(playButton);
    public void SelectResume() => Select(resumeButton);

    /// <summary>Marca un boton como elegido: asi se puede usar el menu con teclado (flechas + Enter).</summary>
    public void Select(UnityEngine.UI.Button button)
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
    }

    public void SetEndingStats(string text)
    {
        if (endingStats != null) endingStats.text = text;
    }

    // ------------------------------------------------------------------ fundido a negro

    public void SetFadeInstant(float alpha)
    {
        if (fade != null) fade.alpha = alpha;
    }

    /// <summary>Lleva el negro a 'alpha' (0 = transparente, 1 = negro total). Se puede esperar con yield.</summary>
    public IEnumerator FadeScreen(float alpha, float seconds)
    {
        if (fade == null) yield break;
        Stop(fade);
        yield return Animate(fade, alpha, seconds);
    }

    // ------------------------------------------------------------------ interno

    private void Fade(CanvasGroup group, bool visible, float seconds)
    {
        if (group == null) return;
        // Solo se puede tocar un panel visible. Al ocultarlo, deja de recibir clics al instante.
        group.interactable = visible;
        group.blocksRaycasts = visible;
        Stop(group);
        running[group] = StartCoroutine(Animate(group, visible ? 1f : 0f, seconds));
    }

    private void Stop(CanvasGroup group)
    {
        if (running.TryGetValue(group, out Coroutine c) && c != null) StopCoroutine(c);
        running.Remove(group);
    }

    private static void SetGroup(CanvasGroup group, float alpha)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = alpha > 0f;
        group.blocksRaycasts = alpha > 0f;
    }

    private static IEnumerator Animate(CanvasGroup group, float target, float seconds)
    {
        float start = group.alpha;
        if (seconds <= 0f)
        {
            group.alpha = target;
            yield break;
        }

        // El paso de tiempo se limita a 1/20 s: el primer frame despues de cargar la escena puede
        // tardar mucho, y sin el limite el fundido se saltearia entero en ese frame.
        for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            // SmoothStep: arranca y termina suave, sin cortes bruscos.
            group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        group.alpha = target;
    }
}
