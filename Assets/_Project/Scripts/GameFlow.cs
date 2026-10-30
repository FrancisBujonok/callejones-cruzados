using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Las REGLAS del flujo de juego: en que estado esta la partida y que se puede hacer en cada uno.
///
///   Inicio  --Entrar-->  (recarga la escena, sin inicio)  -->  Jugando
///   Jugando --Esc-->  Pausa  --Esc / Seguir-->  Jugando
///   Inicio o Pausa  --Opciones-->  Opciones  --Volver / Esc-->  (vuelve a donde estaba)
///   Jugando --cruza el torii-->  Final
///   Final   --Volver a entrar-->  (recarga, sin inicio)  -->  Jugando
///   Final o Pausa  --Volver al inicio-->  (recarga)  -->  Inicio
///
/// Una sola escena. En el inicio el jugador camina solo por el laberinto (TitleWalker) y la
/// ventana del inicio muestra lo que ve (MenuCamera). Por eso Entrar RECARGA la escena: la
/// partida empieza de cero, con un laberinto nuevo, sin importar por donde anduvo la caminata.
/// Una variable estatica (enterOnLoad) sobrevive a la recarga y le avisa al GameFlow nuevo que
/// tiene que saltear el inicio.
///
/// Teclas de prueba: F1 muestra u oculta los textos de prueba; con ellos visibles, F2 abre la salida.
///
/// Va en el objeto "Juego". La parte visible (paneles, botones) esta en GameUI.
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum State { Title, Playing, Paused, Ending }

    /// <summary>El GameFlow de la escena. FirstPersonController lo usa para saber si manejar el cursor.</summary>
    public static GameFlow Instance { get; private set; }

    /// <summary>
    /// true = al cargar la escena, empezar a jugar directo (sin el inicio).
    /// Es estatica: las variables estaticas no se borran al recargar la escena.
    /// </summary>
    private static bool enterOnLoad;

    private const string ExitsKey = "Callejones.Salidas";

    [Header("Referencias")]
    [SerializeField] private GameUI ui;
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private StepCounter steps;
    [SerializeField] private VisibilityChecker visibility;
    [SerializeField] private LoopExit loopExit;
    [SerializeField] private ExitCondition exitCondition;
    [Tooltip("Hace caminar al jugador solo durante el inicio.")]
    [SerializeField] private TitleWalker titleWalker;
    [Tooltip("La ventana del inicio y la foto del afiche de la pausa.")]
    [SerializeField] private MenuCamera menuCamera;

    [Header("Tiempos (segundos)")]
    [Tooltip("Fundido desde negro al abrir el juego.")]
    [SerializeField] private float introFade = 2.5f;
    [Tooltip("Fundido a negro al tocar Entrar.")]
    [SerializeField] private float startFade = 0.8f;
    [Tooltip("Fundido desde negro al empezar a jugar.")]
    [SerializeField] private float enterFade = 1.8f;
    [Tooltip("Fundido a negro al cruzar la salida.")]
    [SerializeField] private float endFade = 3f;
    [Tooltip("Lo que tarda el boleto en subir.")]
    [SerializeField] private float ticketTime = 1.4f;
    [Tooltip("Espera antes de que aparezcan los botones del final.")]
    [SerializeField] private float endButtonsDelay = 1.2f;
    [Tooltip("Fundido a negro antes de recargar la escena.")]
    [SerializeField] private float reloadFade = 0.8f;

    [Header("Pruebas")]
    [Tooltip("Solo en el editor: saltea el inicio y empieza a jugar directo.")]
    [SerializeField] private bool skipTitleInEditor = false;

    public State Current { get; private set; }

    private float playTime;
    private bool optionsOpen;
    private bool leaving;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        if (ui != null)
        {
            ui.onPlay.AddListener(OnPlayPressed);
            ui.onOptions.AddListener(OpenOptions);
            ui.onOptionsBack.AddListener(CloseOptions);
            ui.onResume.AddListener(Resume);
            ui.onBackToTitle.AddListener(BackToTitle);
            ui.onRestart.AddListener(Restart);
            ui.onQuit.AddListener(Quit);
        }
        if (loopExit != null) loopExit.onLoopCompleted.AddListener(OnLoopCompleted);
    }

    private void OnDisable()
    {
        if (ui != null)
        {
            ui.onPlay.RemoveListener(OnPlayPressed);
            ui.onOptions.RemoveListener(OpenOptions);
            ui.onOptionsBack.RemoveListener(CloseOptions);
            ui.onResume.RemoveListener(Resume);
            ui.onBackToTitle.RemoveListener(BackToTitle);
            ui.onRestart.RemoveListener(Restart);
            ui.onQuit.RemoveListener(Quit);
        }
        if (loopExit != null) loopExit.onLoopCompleted.RemoveListener(OnLoopCompleted);
    }

    private void Start()
    {
        // timeScale y AudioListener.pause son globales: sobreviven a recargar la escena.
        // Si se vuelve al inicio desde la pausa, hay que dejarlos como corresponde.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameSettings.Apply();

        SetControl(false);
        if (ui != null)
        {
            ui.HideAllInstant();
            ui.SetFadeInstant(1f);
        }

        bool enter = enterOnLoad || (Application.isEditor && skipTitleInEditor);
        enterOnLoad = false;
        if (enter)
        {
            BeginPlay();
            if (ui != null) StartCoroutine(ui.FadeScreen(0f, enterFade));
            return;
        }

        StartCoroutine(Intro());
    }

    private IEnumerator Intro()
    {
        Current = State.Title;
        // Durante la caminata del inicio no tiene que aparecer la salida (seria adelantar el final).
        if (exitCondition != null) exitCondition.enabled = false;
        if (titleWalker != null) titleWalker.Begin();
        if (ui == null) yield break;

        ui.ShowTitle(true, 0f);
        ui.SelectPlay();
        yield return ui.FadeScreen(0f, introFade);
    }

    private void Update()
    {
        if (KeyPressed(DebugKey.ToggleOverlay)) DebugOverlay.Visible = !DebugOverlay.Visible;
        if (DebugOverlay.Visible && Current == State.Playing && KeyPressed(DebugKey.ForceExit) && exitCondition != null)
            exitCondition.Trigger();

        switch (Current)
        {
            case State.Title:
                if (optionsOpen && EscapePressed()) CloseOptions();
                break;

            case State.Playing:
                playTime += Time.deltaTime;
                if (EscapePressed()) Pause();
                // En el editor, al hacer clic afuera del juego se libera el cursor: un clic lo vuelve a tomar.
                else if (Cursor.lockState != CursorLockMode.Locked && ClickPressed()) SetCursor(true);
                break;

            case State.Paused:
                if (EscapePressed())
                {
                    if (optionsOpen) CloseOptions();
                    else Resume();
                }
                break;
        }
    }

    // ------------------------------------------------------------------ transiciones

    private void OnPlayPressed()
    {
        if (Current != State.Title || leaving || optionsOpen) return;
        enterOnLoad = true;
        StartCoroutine(Reload(startFade));
    }

    private void BeginPlay()
    {
        Current = State.Playing;
        playTime = 0f;
        if (ui != null) ui.Select(null);
        SetControl(true);
    }

    public void Pause()
    {
        if (Current != State.Playing) return;
        Current = State.Paused;
        Time.timeScale = 0f;          // todo lo que usa Time.deltaTime se detiene
        AudioListener.pause = true;   // todos los sonidos se pausan (y siguen desde ahi al volver)
        SetControl(false);
        // La foto del afiche: lo que se estaba viendo justo al pausar.
        if (menuCamera != null) menuCamera.TakePhoto();
        if (ui != null)
        {
            ui.SetPauseTime(playTime);
            ui.ShowPause(true, 0.15f);
            ui.SelectResume();
        }
    }

    public void Resume()
    {
        if (Current != State.Paused || optionsOpen) return;
        Current = State.Playing;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (ui != null)
        {
            ui.ShowPause(false, 0.15f);
            ui.Select(null);
        }
        SetControl(true);
    }

    private void OpenOptions()
    {
        if (optionsOpen || leaving || (Current != State.Title && Current != State.Paused)) return;
        optionsOpen = true;
        if (ui == null) return;
        if (Current == State.Title) ui.ShowTitle(false, 0.2f);
        else ui.ShowPause(false, 0.2f);
        ui.ShowOptions(true, 0.2f);
        ui.SelectFirstOption();
    }

    private void CloseOptions()
    {
        if (!optionsOpen) return;
        optionsOpen = false;
        GameSettings.Save();
        if (ui == null) return;
        ui.ShowOptions(false, 0.2f);
        if (Current == State.Title)
        {
            ui.ShowTitle(true, 0.2f);
            ui.SelectTitleOptions();
        }
        else
        {
            ui.ShowPause(true, 0.2f);
            ui.SelectPauseOptions();
        }
    }

    /// <summary>Volver al inicio (desde la pausa o el final): recarga la escena y muestra el inicio.</summary>
    public void BackToTitle()
    {
        if (leaving) return;
        enterOnLoad = false;
        StartCoroutine(Reload(reloadFade));
    }

    /// <summary>Volver a entrar (desde el final): recarga la escena y empieza a jugar directo.</summary>
    public void Restart()
    {
        if (leaving) return;
        enterOnLoad = true;
        StartCoroutine(Reload(reloadFade));
    }

    private IEnumerator Reload(float seconds)
    {
        leaving = true;
        if (ui != null) yield return ui.FadeScreen(1f, seconds);
        // Recargar la escena deja todo como al principio. Start del nuevo GameFlow
        // devuelve timeScale y el audio a la normalidad.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // en el editor, "salir" = salir de Play
#else
        Application.Quit();
#endif
    }

    private void OnLoopCompleted()
    {
        if (Current != State.Playing) return;
        Current = State.Ending;
        SetControl(false);
        StartCoroutine(EndingSequence());
    }

    private IEnumerator EndingSequence()
    {
        if (ui == null) yield break;

        yield return ui.FadeScreen(1f, endFade);

        // El numero del boleto: cuantas veces se salio del laberinto en esta computadora.
        int number = PlayerPrefs.GetInt(ExitsKey, 0) + 1;
        PlayerPrefs.SetInt(ExitsKey, number);
        PlayerPrefs.Save();

        ui.SetEndingStats(
            steps != null ? steps.TotalSteps : 0,
            visibility != null ? visibility.DiscoveredCount : 0,
            playTime,
            number);
        ui.ShowEnding(true, 0.3f);
        // El negro se aclara un poco: detras del boleto queda, apenas, la luz del torii.
        // (0.93 y no menos: la mezcla se hace en espacio lineal y deja ver mas de lo que parece.)
        StartCoroutine(ui.FadeScreen(0.93f, ticketTime + 1f));
        yield return ui.PrintTicket(ticketTime);

        yield return new WaitForSecondsRealtime(endButtonsDelay);
        ui.ShowEndingButtons(0.6f);
        ui.SelectRestart();
    }

    // ------------------------------------------------------------------ control y cursor

    /// <summary>Prende o apaga el control del jugador y bloquea o libera el cursor.</summary>
    private void SetControl(bool on)
    {
        if (controller != null) controller.enabled = on;
        SetCursor(on);
    }

    private static void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ------------------------------------------------------------------ teclas

    private enum DebugKey { ToggleOverlay, ForceExit }

#if ENABLE_INPUT_SYSTEM
    private static bool KeyPressed(DebugKey key)
    {
        Keyboard k = Keyboard.current;
        if (k == null) return false;
        return key == DebugKey.ToggleOverlay ? k.f1Key.wasPressedThisFrame : k.f2Key.wasPressedThisFrame;
    }

    private static bool EscapePressed() =>
        Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

    private static bool ClickPressed() =>
        Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
    private static bool KeyPressed(DebugKey key) =>
        Input.GetKeyDown(key == DebugKey.ToggleOverlay ? KeyCode.F1 : KeyCode.F2);

    private static bool EscapePressed() => Input.GetKeyDown(KeyCode.Escape);

    private static bool ClickPressed() => Input.GetMouseButtonDown(0);
#endif
}
