using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// La parte VISIBLE del flujo de juego: paneles (inicio, pausa, opciones, final), fundido a negro,
/// botones y textos. No decide nada: cuando se toca un boton, dispara un evento, y GameFlow
/// decide que hacer.
///
/// Separarlo asi permite cambiar el aspecto sin tocar las reglas, y al reves: en el Paso 17 se
/// cambio todo el aspecto (inicio "tapa de libro", afiche de desaparecido, boleto de vuelta)
/// y las reglas de GameFlow casi no cambiaron. Los paneles los arma el menu
/// Callejones > Menus (AtmosphereBuilder.Menus.cs).
///
/// Va en el objeto "Interfaz" (el Canvas).
/// Todos los fundidos usan tiempo "sin escala" (unscaledDeltaTime): en pausa el juego tiene
/// timeScale 0, y con tiempo normal los menus quedarian congelados.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private CanvasGroup titlePanel;
    [SerializeField] private CanvasGroup pausePanel;
    [SerializeField] private CanvasGroup optionsPanel;
    [SerializeField] private CanvasGroup endingPanel;
    [Tooltip("Imagen negra a pantalla completa para los fundidos.")]
    [SerializeField] private CanvasGroup fade;

    [Header("Inicio")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button titleOptionsButton;
    [SerializeField] private Button titleQuitButton;

    [Header("Pausa")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseOptionsButton;
    [SerializeField] private Button backToTitleButton;
    [SerializeField] private Button pauseQuitButton;
    [Tooltip("\"visto por ultima vez hace ...\": el tiempo de juego.")]
    [SerializeField] private TMPro.TMP_Text pauseSeen;

    [Header("Opciones")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Button optionsBackButton;
    [Tooltip("Cartel oscuro para calibrar el brillo: se aclara o se oscurece con la barra.")]
    [SerializeField] private Graphic calibrationSign;

    [Header("Final")]
    [Tooltip("El boleto: entra desde abajo, como si lo imprimiera una maquina.")]
    [SerializeField] private RectTransform ticket;
    [SerializeField] private TMPro.TMP_Text stepsValue;
    [SerializeField] private TMPro.TMP_Text changesValue;
    [SerializeField] private TMPro.TMP_Text timeValue;
    [SerializeField] private TMPro.TMP_Text ticketNumber;
    [SerializeField] private CanvasGroup endingButtons;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button endingTitleButton;

    [Header("Eventos (los escucha GameFlow)")]
    public UnityEvent onPlay = new UnityEvent();
    public UnityEvent onOptions = new UnityEvent();
    public UnityEvent onOptionsBack = new UnityEvent();
    public UnityEvent onResume = new UnityEvent();
    public UnityEvent onBackToTitle = new UnityEvent();
    public UnityEvent onRestart = new UnityEvent();
    public UnityEvent onQuit = new UnityEvent();

    // Una animacion por panel: si se pide otra antes de que termine (pausar y despausar rapido),
    // la anterior se corta en vez de pelearse con la nueva.
    private readonly Dictionary<CanvasGroup, Coroutine> running = new Dictionary<CanvasGroup, Coroutine>();
    private Vector2 ticketRest;
    private Color calibrationBase = Color.gray;

    private void Awake()
    {
        Listen(playButton, onPlay);
        Listen(titleOptionsButton, onOptions);
        Listen(titleQuitButton, onQuit);
        Listen(resumeButton, onResume);
        Listen(pauseOptionsButton, onOptions);
        Listen(backToTitleButton, onBackToTitle);
        Listen(pauseQuitButton, onQuit);
        Listen(optionsBackButton, onOptionsBack);
        Listen(restartButton, onRestart);
        Listen(endingTitleButton, onBackToTitle);

        // Las barras cambian la opcion en el momento: se ve (y se oye) mientras se ajusta.
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(v => GameSettings.SetMasterVolume(Mathf.RoundToInt(v)));
        if (brightnessSlider != null) brightnessSlider.onValueChanged.AddListener(v =>
        {
            GameSettings.SetBrightness(Mathf.RoundToInt(v));
            UpdateCalibration();
        });
        if (calibrationSign != null) calibrationBase = calibrationSign.color;
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(v => GameSettings.SetSensitivity(Mathf.RoundToInt(v)));

        if (ticket != null) ticketRest = ticket.anchoredPosition;
    }

    private static void Listen(Button button, UnityEvent evt)
    {
        if (button != null) button.onClick.AddListener(evt.Invoke);
    }

    // ------------------------------------------------------------------ paneles

    public void HideAllInstant()
    {
        SetGroup(titlePanel, 0f);
        SetGroup(pausePanel, 0f);
        SetGroup(optionsPanel, 0f);
        SetGroup(endingPanel, 0f);
        SetGroup(endingButtons, 0f);
    }

    public void ShowTitle(bool visible, float seconds) => Fade(titlePanel, visible, seconds);
    public void ShowPause(bool visible, float seconds) => Fade(pausePanel, visible, seconds);
    public void ShowEnding(bool visible, float seconds) => Fade(endingPanel, visible, seconds);
    public void ShowEndingButtons(float seconds) => Fade(endingButtons, true, seconds);

    public void ShowOptions(bool visible, float seconds)
    {
        if (visible) SyncOptions();
        Fade(optionsPanel, visible, seconds);
    }

    public void SelectPlay() => Select(playButton);
    public void SelectResume() => Select(resumeButton);
    public void SelectTitleOptions() => Select(titleOptionsButton);
    public void SelectPauseOptions() => Select(pauseOptionsButton);
    public void SelectRestart() => Select(restartButton);
    public void SelectFirstOption() => Select(volumeSlider);

    /// <summary>Marca una opcion como elegida: asi se puede usar el menu con teclado (flechas + Enter).</summary>
    public void Select(Selectable selectable)
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
    }

    // ------------------------------------------------------------------ textos

    public void SetPauseTime(float seconds)
    {
        if (pauseSeen != null) pauseSeen.text = "visto por última vez hace " + Clock(seconds);
    }

    public void SetEndingStats(int steps, int changes, float seconds, int number)
    {
        // Miles con punto, como se escribe en castellano: 3.412
        if (stepsValue != null) stepsValue.text = steps.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".");
        if (changesValue != null) changesValue.text = changes.ToString();
        if (timeValue != null) timeValue.text = Clock(seconds);
        if (ticketNumber != null) ticketNumber.text = "N.º " + number.ToString("000000");
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    /// <summary>Pone las barras de opciones en los valores guardados (sin volver a guardarlos).</summary>
    private void SyncOptions()
    {
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(GameSettings.Brightness);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(GameSettings.Sensitivity);
        UpdateCalibration();
    }

    /// <summary>
    /// La interfaz se dibuja despues del post-procesado, asi que el brillo del juego no la toca.
    /// Para que el cartel de prueba sirva, se le aplica el mismo cambio de exposicion a mano:
    /// cada paso de exposicion duplica (o divide a la mitad) la luz.
    /// </summary>
    private void UpdateCalibration()
    {
        if (calibrationSign == null) return;
        float factor = Mathf.Pow(2f, GameSettings.ExposureOffset);
        Color c = calibrationBase * factor;
        c.a = calibrationBase.a;
        calibrationSign.color = c;
    }

    // ------------------------------------------------------------------ boleto

    /// <summary>El boleto sube desde abajo de la pantalla y frena despacio en su lugar.</summary>
    public IEnumerator PrintTicket(float seconds)
    {
        if (ticket == null) yield break;
        Vector2 from = ticketRest + Vector2.down * 400f;
        for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            float k = 1f - Mathf.Pow(1f - t / seconds, 3f); // rapido al principio, lento al final
            ticket.anchoredPosition = Vector2.LerpUnclamped(from, ticketRest, k);
            yield return null;
        }
        ticket.anchoredPosition = ticketRest;
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
