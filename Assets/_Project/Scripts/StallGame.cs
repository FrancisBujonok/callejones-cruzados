using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 36: los premios que el jugador se lleva de la feria. Es "static": sobrevive al cambio de
/// escena, asi el capitulo 2 (los callejones) puede preguntar si el jugador tiene un premio
/// (FairPrizes.Has("Peluche")) y cambiar algo. Premios: Peluche, Mascara (de zorro), Pez.
/// </summary>
public static class FairPrizes
{
    private static readonly HashSet<string> won = new HashSet<string>();
    public static bool Has(string prize) => won.Contains(prize);
    public static void Add(string prize) => won.Add(prize);
    public static IEnumerable<string> All => won;
    /// <summary>Paso 37: se vacia al cargar la feria (FairPocket.ResetAll).</summary>
    public static void Clear() => won.Clear();
}

/// <summary>
/// Paso 36: lo comun de los juegos de los puestos (RingToss, CanToss y, desde el Paso 37,
/// FishScoop). Opcionales:
///
/// 1. Cerca del puesto y mirandolo aparece el aviso "E - jugar (2 monedas)".
/// 2. Con E se paga (Paso 37) y el jugador se para delante del mostrador y queda quieto (puede
///    mirar). Aparece la mira. Clic o E tira; Q deja el juego.
/// 3. Al terminar, el personaje piensa lo que paso y, si gano, se lleva el premio (FairPrizes).
///    Se puede volver a jugar, pero premio hay uno solo. Si tiene la tarjeta, le sellan el
///    casillero de este juego (Paso 37), gane o pierda.
///
/// Lo que pasa sale en la linea de pensamiento (ThoughtText).
/// </summary>
public abstract class StallGame : MonoBehaviour
{
    [Header("Juego")]
    [SerializeField] protected string gameName = "Jugar";
    [Tooltip("Nombre del premio para FairPrizes (Peluche, Mascara, Pez).")]
    [SerializeField] protected string prizeId = "Peluche";
    [SerializeField] protected int shots = 3;
    [Tooltip("Donde se para el jugador para jugar (mirando al puesto, +Z de este punto).")]
    [SerializeField] protected Transform playSpot;
    [SerializeField] protected float promptDistance = 2.4f;
    [Tooltip("Paso 37: cuanto cuesta cada partida (monedas).")]
    [SerializeField] protected int price = 2;
    [Tooltip("Paso 37: el casillero de la tarjeta de sellos (Aros, Latas, Peces).")]
    [SerializeField] protected string stampId = "";

    [Header("Lo que dice el vendedor")]
    [SerializeField] protected string startLine = "«Tres tiros.»";
    [SerializeField] protected string winLine = "«¡Ganaste!»";
    [SerializeField] protected string againWinLine = "«¡Otra vez! Pero premio hay uno solo.»";
    [SerializeField] protected string loseLine = "«Casi. ¿Otra vez?»";

    protected Transform player;
    protected Camera cam;
    protected FirstPersonController fpc;
    private CharacterController controller;
    private bool playing, finishing;
    protected int left;
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
            if (!FairPocket.CanPay(price))
            {
                InteractPrompt.Show(gameName + " (" + price + " monedas)  -  no me alcanza");
                return;
            }
            InteractPrompt.Show("E  -  " + gameName + " (" + price + " monedas)");
            if (UsePressed()) Begin();
            return;
        }

        Simulate(Time.deltaTime);
        if (finishing)
        {
            if (Time.time >= finishAt) Finish();
            return;
        }
        if (QuitPressed()) { Finish(); return; }
        PlayInput();
        if (Done())
        {
            finishing = true;
            finishAt = Time.time + (won ? 0.6f : 1.0f);
        }
    }

    /// <summary>
    /// Lo que se hace con el mouse mientras se juega. Por defecto: clic o E tira (aros, latas).
    /// FishScoop lo cambia (mantener apretado para meter el poi).
    /// </summary>
    protected virtual void PlayInput()
    {
        InteractPrompt.Show("Clic o E: tirar (" + left + ")     Q: dejar");
        if (left > 0 && (ThrowPressed() || UsePressed()) && CanThrow())
        {
            left--;
            Transform c = cam != null ? cam.transform : player;
            Throw(c.position + c.forward * 0.35f - c.up * 0.08f, c.forward);
        }
    }

    /// <summary>La partida termino (gano, o no quedan tiros y no vuela nada).</summary>
    protected virtual bool Done() => won || (left == 0 && !Busy());

    /// <summary>Al empezar una partida (despues de pararse frente al puesto).</summary>
    protected virtual void OnBegin() { }

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
        if (!FairPocket.Pay(price)) return;
        PocketSounds.Play("Moneda");
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
        FairPerson.Serve(transform.position);
        OnBegin();
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
        // Paso 37: el sello, un momento despues (asi se lee primero lo que paso).
        if (!FairPocket.HasStamp(stampId)) Invoke(nameof(GiveStamp), 1.2f);
    }

    private void GiveStamp() => FairPocket.Stamp(stampId);

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
    protected static bool UsePressed() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    protected static bool QuitPressed() => Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
    protected static bool ThrowPressed() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    protected static bool ThrowHeld() => Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
    protected static bool UsePressed() => Input.GetKeyDown(KeyCode.E);
    protected static bool QuitPressed() => Input.GetKeyDown(KeyCode.Q);
    protected static bool ThrowPressed() => Input.GetMouseButtonDown(0);
    protected static bool ThrowHeld() => Input.GetMouseButton(0);
#endif
}
