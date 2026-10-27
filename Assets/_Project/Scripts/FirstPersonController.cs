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

    [Header("Mirada")]
    [Tooltip("Grados por pixel de movimiento del mouse.")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [Tooltip("Angulo maximo para mirar arriba o abajo.")]
    [SerializeField] private float maxLookAngle = 80f;

    private CharacterController controller;
    private float pitch;             // rotacion acumulada de la camara en X (arriba/abajo)
    private float verticalVelocity;  // velocidad de caida en Y

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

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
        Move(ReadMoveInput());
    }

    // ---------- Mirada ----------
    private void Look(Vector2 delta)
    {
        // Izquierda/derecha: gira todo el cuerpo en Y.
        transform.Rotate(Vector3.up, delta.x * mouseSensitivity);

        // Arriba/abajo: solo la camara, limitada para no dar la vuelta.
        pitch -= delta.y * mouseSensitivity;
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

        Vector3 velocity = direction * walkSpeed + Vector3.up * verticalVelocity;
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
