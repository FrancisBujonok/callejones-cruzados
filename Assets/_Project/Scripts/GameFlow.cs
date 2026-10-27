using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Las REGLAS del flujo de juego: en que estado esta la partida y que se puede hacer en cada uno.
///
///   Titulo  --Jugar-->  Jugando  --Esc-->  Pausa  --Esc / Continuar-->  Jugando
///                          |
///                   cruza el torii
///                          v
///                        Final  --cualquier tecla-->  (recarga la escena)  --> Titulo
///
/// Una sola escena: el titulo se ve SOBRE el callejon (el jugador esta quieto y no puede moverse).
/// Volver al titulo recarga la escena, que es la forma mas simple de dejar todo como al principio:
/// miedo en 0, modulos en su lugar, salida apagada.
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

    [Header("Referencias")]
    [SerializeField] private GameUI ui;
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private StepCounter steps;
    [SerializeField] private VisibilityChecker visibility;
    [SerializeField] private LoopExit loopExit;
    [SerializeField] private ExitCondition exitCondition;

    [Header("Tiempos (segundos)")]
    [Tooltip("Fundido desde negro al abrir el juego.")]
    [SerializeField] private float introFade = 2.5f;
    [Tooltip("Lo que tarda en desaparecer el titulo al tocar Jugar.")]
    [SerializeField] private float startFade = 0.8f;
    [Tooltip("Fundido a negro al cruzar la salida.")]
    [SerializeField] private float endFade = 3f;
    [Tooltip("Espera antes de mostrar \"presiona cualquier tecla\" en el final.")]
    [SerializeField] private float endHintDelay = 2f;
    [Tooltip("Fundido a negro antes de recargar la escena.")]
    [SerializeField] private float reloadFade = 0.8f;

    [Header("Pruebas")]
    [Tooltip("Solo en el editor: saltea el titulo y empieza a jugar directo.")]
    [SerializeField] private bool skipTitleInEditor = false;

    public State Current { get; private set; }

    private float playTime;
    private bool canLeaveEnding;
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
            ui.onResume.AddListener(Resume);
            ui.onBackToTitle.AddListener(BackToTitle);
            ui.onQuit.AddListener(Quit);
        }
        if (loopExit != null) loopExit.onLoopCompleted.AddListener(OnLoopCompleted);
    }

    private void OnDisable()
    {
        if (ui != null)
        {
            ui.onPlay.RemoveListener(OnPlayPressed);
            ui.onResume.RemoveListener(Resume);
            ui.onBackToTitle.RemoveListener(BackToTitle);
            ui.onQuit.RemoveListener(Quit);
        }
        if (loopExit != null) loopExit.onLoopCompleted.RemoveListener(OnLoopCompleted);
    }

    private void Start()
    {
        // timeScale y AudioListener.pause son globales: sobreviven a recargar la escena.
        // Si se vuelve al titulo desde la pausa, hay que dejarlos como corresponde.
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SetControl(false);
        if (ui != null)
        {
            ui.HideAllInstant();
            ui.SetFadeInstant(1f);
        }

        if (Application.isEditor && skipTitleInEditor)
        {
            if (ui != null) ui.SetFadeInstant(0f);
            BeginPlay();
            return;
        }

        StartCoroutine(Intro());
    }

    private IEnumerator Intro()
    {
        Current = State.Title;
        if (ui == null) yield break;
        // El titulo aparece "debajo" del negro y el negro se va: se descubre el callejon con el titulo encima.
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
            case State.Playing:
                playTime += Time.deltaTime;
                if (EscapePressed()) Pause();
                // En el editor, al hacer clic afuera del juego se libera el cursor: un clic lo vuelve a tomar.
                else if (Cursor.lockState != CursorLockMode.Locked && ClickPressed()) SetCursor(true);
                break;

            case State.Paused:
                if (EscapePressed()) Resume();
                break;

            case State.Ending:
                if (canLeaveEnding && AnyKeyPressed()) BackToTitle();
                break;
        }
    }

    // ------------------------------------------------------------------ transiciones

    private void OnPlayPressed()
    {
        if (Current == State.Title && !leaving) BeginPlay();
    }

    private void BeginPlay()
    {
        Current = State.Playing;
        if (ui != null)
        {
            ui.ShowTitle(false, startFade);
            ui.Select(null);
        }
        SetControl(true);
    }

    public void Pause()
    {
        if (Current != State.Playing) return;
        Current = State.Paused;
        Time.timeScale = 0f;          // todo lo que usa Time.deltaTime se detiene
        AudioListener.pause = true;   // todos los sonidos se pausan (y siguen desde ahi al volver)
        SetControl(false);
        if (ui != null)
        {
            ui.ShowPause(true, 0.15f);
            ui.SelectResume();
        }
    }

    public void Resume()
    {
        if (Current != State.Paused) return;
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

    public void BackToTitle()
    {
        if (leaving) return;
        leaving = true;
        StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        if (ui != null) yield return ui.FadeScreen(1f, reloadFade);
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

        int seconds = Mathf.FloorToInt(playTime);
        ui.SetEndingStats(
            $"Pasos: {(steps != null ? steps.TotalSteps : 0)}\n" +
            $"Cambios descubiertos: {(visibility != null ? visibility.DiscoveredCount : 0)}\n" +
            $"Tiempo: {seconds / 60}:{seconds % 60:00}");
        ui.ShowEnding(true, 1.5f);

        yield return new WaitForSecondsRealtime(endHintDelay);
        ui.ShowEndingHint(1f);
        canLeaveEnding = true;
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

    private static bool AnyKeyPressed() =>
        (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) || ClickPressed();
#else
    private static bool KeyPressed(DebugKey key) =>
        Input.GetKeyDown(key == DebugKey.ToggleOverlay ? KeyCode.F1 : KeyCode.F2);

    private static bool EscapePressed() => Input.GetKeyDown(KeyCode.Escape);

    private static bool ClickPressed() => Input.GetMouseButtonDown(0);

    private static bool AnyKeyPressed() => Input.anyKeyDown;
#endif
}
