using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controlador en primera persona minimo para el prototipo gris.
/// - El cuerpo (este GameObject) gira a izquierda/derecha.
/// - La camara (hija) gira arriba/abajo.
/// - Se mueve con CharacterController: sin fisica, preciso, se frena contra paredes.
/// Funciona con el Input System nuevo, con el viejo o con ambos activos.
/// Paso 27: con Shift se trota (Stamina, en el mismo objeto): un poco mas rapido, solo hacia adelante.
/// Paso 36: MovementLocked deja al jugador quieto pero mirando (lo usan los juegos de los puestos).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La Main Camera, colocada como hija de este objeto a la altura de los ojos.")]
    [SerializeField] private Transform cameraPivot;

    [Header("Movimiento")]
    [Tooltip("Metros por segundo. Lento a proposito: es terror.")]
    [SerializeField] private float walkSpeed = 2.5f;
    [Tooltip("Aceleracion hacia abajo en m/s^2. La real es -9.81; un poco mas se siente menos 'flotante'.")]
    [SerializeField] private float gravity = -15f;
    [Tooltip("Paso 27: el trote con Shift. Si se deja vacio, usa el Stamina de este mismo objeto (si hay).")]
    [SerializeField] private Stamina stamina;

    [Header("Mirada")]
    [Tooltip("Grados por pixel de movimiento del mouse.")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [Tooltip("Angulo maximo para mirar arriba o abajo.")]
    [SerializeField] private float maxLookAngle = 80f;

    /// <summary>Paso 36: mientras esta en true, el jugador no camina (pero puede mirar).</summary>
    public bool MovementLocked { get; set; }

    private CharacterController controller;
    private float pitch;             // rotacion acumulada de la camara en X (arriba/abajo)
    private float verticalVelocity;  // velocidad de caida en Y

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (stamina == null) stamina = GetComponent<Stamina>();

        if (cameraPivot == null)
        {
            Debug.LogError("[FirstPersonController] Falta asignar Camera Pivot en el Inspector.", this);
            enabled = false;
        }
    }

    // Si en la escena hay un GameFlow, el decide cuando se bloquea el cursor (titulo, pausa, final).
    // Si no hay (por ejemplo, en una escena de prueba), este script lo maneja solo, como antes.
    private void Start()
    {
        if (GameFlow.Instance == null) LockCursor(true);
    }

    private void Update()
    {
        if (GameFlow.Instance == null) HandleCursor();

        // Mientras el cursor esta libre (Escape), no movemos la camara ni el cuerpo.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Look(ReadLookDelta());
        Move(MovementLocked ? Vector2.zero : ReadMoveInput());
    }

    // ---------- Mirada ----------
    private void Look(Vector2 delta)
    {
        // La sensibilidad del Inspector, multiplicada por la que eligio el jugador en Opciones.
        float sensitivity = mouseSensitivity * GameSettings.SensitivityScale;

        // Izquierda/derecha: gira todo el cuerpo en Y.
        transform.Rotate(Vector3.up, delta.x * sensitivity);

        // Arriba/abajo: solo la camara, limitada para no dar la vuelta.
        pitch -= delta.y * sensitivity;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // ---------- Movimiento ----------
    private void Move(Vector2 input)
    {
        // Direccion relativa a hacia donde mira el cuerpo.
        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        if (direction.sqrMagnitude > 1f) direction.Normalize(); // que la diagonal no sea mas rapida

        // Gravedad: pegado al piso cuando esta apoyado, acelerando hacia abajo cuando no.
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        // Paso 27: el trote multiplica la velocidad (1 = caminar). Se llama siempre, aunque no se
        // mueva, para que la stamina se recupere.
        float speed = walkSpeed * (stamina != null ? stamina.Tick(input.y > 0.1f, Time.deltaTime) : 1f);

        Vector3 velocity = direction * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    // ---------- Cursor ----------
    private void HandleCursor()
    {
        if (EscapePressed()) LockCursor(false);
        else if (Cursor.lockState != CursorLockMode.Locked && ClickPressed()) LockCursor(true);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ---------- Lectura de input (compatible con ambos sistemas) ----------
#if ENABLE_INPUT_SYSTEM
    private static Vector2 ReadMoveInput()
    {
        Keyboard k = Keyboard.current;
        if (k == null) return Vector2.zero;
        float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
        float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    private static Vector2 ReadLookDelta()
    {
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    }

    private static bool EscapePressed() =>
        Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

    private static bool ClickPressed() =>
        Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
    private static Vector2 ReadMoveInput()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    private static Vector2 ReadLookDelta()
    {
        // El Input Manager viejo ya escala el mouse x0.1; multiplicamos x10
        // para que la sensibilidad se sienta igual que con el sistema nuevo (pixeles).
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
    }

    private static bool EscapePressed() => Input.GetKeyDown(KeyCode.Escape);

    private static bool ClickPressed() => Input.GetMouseButtonDown(0);
#endif
}
