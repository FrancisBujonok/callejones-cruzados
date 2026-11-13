using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 36: los premios que el jugador se lleva de la feria. Es "static": sobrevive al cambio de
/// escena, asi el capitulo 2 (los callejones) puede preguntar si el jugador tiene un premio
/// (FairPrizes.Has("Peluche")) y cambiar algo.
/// </summary>
public static class FairPrizes
{
    private static readonly HashSet<string> won = new HashSet<string>();
    public static bool Has(string prize) => won.Contains(prize);
    public static void Add(string prize) => won.Add(prize);
    public static IEnumerable<string> All => won;
}

/// <summary>
/// Paso 36: lo comun de los juegos de los puestos (RingToss, CanToss). Opcionales:
///
/// 1. Cerca del puesto y mirandolo aparece el aviso "E - jugar".
/// 2. Con E, el jugador se para delante del mostrador y queda quieto (puede mirar). Aparece la
///    mira. Clic o E tira; Q deja el juego.
/// 3. Al terminar las tiradas (o si gana antes), el vendedor dice algo y, si gano, el jugador se
///    lleva el premio (FairPrizes). Se puede volver a jugar, pero premio hay uno solo.
///
/// Lo que dice el vendedor sale en la linea de pensamiento (ThoughtText), entre comillas.
/// </summary>
public abstract class StallGame : MonoBehaviour
{
    [Header("Juego")]
    [SerializeField] protected string gameName = "Jugar";
    [Tooltip("Nombre del premio para FairPrizes (Peluche, Mascara).")]
    [SerializeField] protected string prizeId = "Peluche";
    [SerializeField] protected int shots = 3;
    [Tooltip("Donde se para el jugador para jugar (mirando al puesto, +Z de este punto).")]
    [SerializeField] protected Transform playSpot;
    [SerializeField] protected float promptDistance = 2.4f;

    [Header("Lo que dice el vendedor")]
    [SerializeField] protected string startLine = "«Tres tiros.»";
    [SerializeField] protected string winLine = "«¡Ganaste!»";
    [SerializeField] protected string againWinLine = "«¡Otra vez! Pero premio hay uno solo.»";
    [SerializeField] protected string loseLine = "«Casi. ¿Otra vez?»";

    protected Transform player;
    protected Camera cam;
    private FirstPersonController fpc;
    private CharacterController controller;
    private bool playing, finishing;
    private int left;
    private float finishAt, promptAgainAt;
    protected bool won;

    public bool Playing => playing;

    protected virtual void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            fpc = p.GetComponent<FirstPersonController>();
            controller = p.GetComponent<CharacterController>();
        }
        cam = Camera.main;
        ResetTargets();
    }

    private static bool GameRunning() => GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    protected virtual void Update()
    {
        if (player == null || playSpot == null) return;
        if (!GameRunning()) return;

        if (!playing)
        {
            if (Busy()) Simulate(Time.deltaTime); // lo que todavia cae despues de terminar
            if (Time.time < promptAgainAt || !Near()) return;
            InteractPrompt.Show("E  -  " + gameName);
            if (UsePressed()) Begin();
            return;
        }

        Simulate(Time.deltaTime);
        if (finishing)
        {
            if (Time.time >= finishAt) Finish();
            return;
        }
        InteractPrompt.Show("Clic o E: tirar (" + left + ")     Q: dejar");
        if (QuitPressed()) { Finish(); return; }
        if (left > 0 && (ThrowPressed() || UsePressed()) && CanThrow())
        {
            left--;
            Transform c = cam != null ? cam.transform : player;
            Throw(c.position + c.forward * 0.35f - c.up * 0.08f, c.forward);
        }
        if (won || (left == 0 && !Busy()))
        {
            finishing = true;
            finishAt = Time.time + (won ? 0.6f : 1.0f);
        }
    }

    private bool Near()
    {
        Vector3 d = playSpot.position - player.position;
        d.y = 0f;
        if (d.magnitude > promptDistance) return false;
        Vector3 look = cam != null ? cam.transform.forward : player.forward;
        Vector3 toStall = (transform.position - player.position);
        toStall.y = 0f; look.y = 0f;
        return Vector3.Dot(look.normalized, toStall.normalized) > 0.55f;
    }

    private void Begin()
    {
        playing = true;
        finishing = false;
        won = false;
        left = shots;
        ResetTargets();
        // El jugador se para delante del mostrador, mirando el puesto.
        if (controller != null) controller.enabled = false;
        player.position = new Vector3(playSpot.position.x, player.position.y, playSpot.position.z);
        player.rotation = Quaternion.Euler(0f, playSpot.eulerAngles.y, 0f);
        if (controller != null) controller.enabled = true;
        if (fpc != null) fpc.MovementLocked = true;
        InteractPrompt.Crosshair(true);
        ThoughtText.Show(startLine, 3f);
    }

    private void Finish()
    {
        playing = false;
        finishing = false;
        if (fpc != null) fpc.MovementLocked = false;
        InteractPrompt.Crosshair(false);
        promptAgainAt = Time.time + 1.5f;
        if (won)
        {
            bool first = !FairPrizes.Has(prizeId);
            FairPrizes.Add(prizeId);
            ThoughtText.Show(first ? winLine : againWinLine, 4f);
        }
        else ThoughtText.Show(loseLine, 3f);
    }

    /// <summary>Vuelve a poner todo en su lugar para una partida nueva.</summary>
    protected abstract void ResetTargets();
    /// <summary>Tira desde 'origin' hacia 'direction'.</summary>
    protected abstract void Throw(Vector3 origin, Vector3 direction);
    /// <summary>Mueve lo que vuela y revisa si pego. Pone won = true si gano.</summary>
    protected abstract void Simulate(float dt);
    /// <summary>Hay algo en el aire todavia.</summary>
    protected abstract bool Busy();
    protected virtual bool CanThrow() => !Busy();

#if ENABLE_INPUT_SYSTEM
    private static bool UsePressed() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    private static bool QuitPressed() => Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
    private static bool ThrowPressed() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
    private static bool UsePressed() => Input.GetKeyDown(KeyCode.E);
    private static bool QuitPressed() => Input.GetKeyDown(KeyCode.Q);
    private static bool ThrowPressed() => Input.GetMouseButtonDown(0);
#endif
}
