using UnityEngine;

/// <summary>
/// Paso 33: una persona quieta de la feria (vendedor, cliente, alguien sentado o esperando).
/// No camina; solo da señales de vida:
/// - respira (el cuerpo sube y baja muy poco);
/// - mira hacia los costados cada tanto y, si el jugador pasa cerca y por delante, lo mira un
///   momento (sin girar el cuerpo);
/// - segun el papel, mueve los brazos: el vendedor atiende (levanta un brazo como si sirviera o
///   cobrara), el cliente señala algo, y el del tambor golpea cada vez que suena el tambor.
///
/// El modelo mira hacia +Z. Pivotes: "Cabeza", "BrazoIzq", "BrazoDer" (hombros).
/// Lo arma Callejones > Feria de dia (capitulo 1), dentro de "Gente".
/// </summary>
public class FairPerson : MonoBehaviour
{
    public enum Role { Idle, Vendor, Customer, Seated, Drummer }

    [SerializeField] private Role role = Role.Idle;
    [SerializeField] private Transform head;
    [SerializeField] private Transform armLeft;
    [SerializeField] private Transform armRight;
    [Tooltip("A cuantos metros mira al jugador si pasa por delante.")]
    [SerializeField] private float noticeDistance = 4f;

    private Transform player;
    private Quaternion headRest, leftRest, rightRest;
    private Vector3 restPos;
    private float breathPhase, nextGlance, glanceYaw, glancePitch;
    private float gestureStart = -99f, gestureLength, nextGesture;
    private bool gestureRight;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Role personRole, Transform headPivot, Transform left, Transform right)
    {
        role = personRole;
        head = headPivot;
        armLeft = left;
        armRight = right;
    }

    private void OnEnable() => FairAmbience.Played += OnSound;
    private void OnDisable() => FairAmbience.Played -= OnSound;

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        restPos = transform.localPosition;
        if (head != null) headRest = head.localRotation;
        if (armLeft != null) leftRest = armLeft.localRotation;
        if (armRight != null) rightRest = armRight.localRotation;
        breathPhase = Random.value * 10f;
        nextGlance = Time.time + Random.Range(0.5f, 4f);
        nextGesture = Time.time + Random.Range(2f, 9f);
    }

    private void OnSound(string layer, float seconds)
    {
        if (role != Role.Drummer || layer != "Tambor") return;
        gestureStart = Time.time;
        gestureLength = Mathf.Max(1f, seconds);
    }

    private void Update()
    {
        float t = Time.time, dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Respirar.
        breathPhase += dt * (role == Role.Drummer ? 2.2f : 1.4f);
        transform.localPosition = restPos + Vector3.up * (Mathf.Sin(breathPhase) * 0.006f);

        Head(t, dt);
        Arms(t, dt);
    }

    private void Head(float t, float dt)
    {
        if (head == null) return;
        float yaw = glanceYaw, pitch = glancePitch;

        if (t >= nextGlance)
        {
            // Mirar para otro lado: casi siempre adelante, a veces a los costados o abajo.
            float r = Random.value;
            glanceYaw = r < 0.45f ? Random.Range(-10f, 10f) : Random.Range(-50f, 50f);
            glancePitch = role == Role.Vendor || role == Role.Customer ? Random.Range(0f, 18f) : Random.Range(-4f, 8f);
            nextGlance = t + Random.Range(1.8f, 5.5f);
        }

        if (player != null)
        {
            Vector3 to = player.position - head.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (flat.magnitude < noticeDistance)
            {
                Vector3 local = transform.InverseTransformDirection(flat.normalized);
                float y = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(y) < 75f) { yaw = Mathf.Clamp(y, -65f, 65f); pitch = 0f; }
            }
        }

        Quaternion want = headRest * Quaternion.Euler(pitch, yaw, 0f);
        head.localRotation = Quaternion.Slerp(head.localRotation, want, 1f - Mathf.Exp(-3f * dt));
    }

    private void Arms(float t, float dt)
    {
        float left = 0f, right = 0f;

        if (role == Role.Drummer)
        {
            // Mientras suena el tambor: golpes alternados, rapidos.
            if (t - gestureStart < gestureLength)
            {
                float k = (t - gestureStart) * 7f;
                left = 35f + 45f * Mathf.Max(0f, Mathf.Sin(k));
                right = 35f + 45f * Mathf.Max(0f, Mathf.Sin(k + Mathf.PI));
            }
            else { left = 30f; right = 30f; }
        }
        else if (role == Role.Vendor || role == Role.Customer)
        {
            if (t >= nextGesture)
            {
                gestureStart = t;
                gestureLength = Random.Range(1.2f, 2.6f);
                gestureRight = Random.value < 0.7f;
                nextGesture = t + gestureLength + Random.Range(role == Role.Vendor ? 3f : 6f, role == Role.Vendor ? 9f : 16f);
            }
            float g = t - gestureStart;
            if (g < gestureLength)
            {
                // Sube, queda un momento y baja.
                float k = Mathf.Sin(Mathf.Clamp01(g / gestureLength) * Mathf.PI);
                float amount = (role == Role.Vendor ? 55f : 70f) * k;
                if (gestureRight) right = amount; else left = amount;
            }
            if (role == Role.Vendor) { left += 12f; right += 12f; } // las manos un poco adelante, sobre el mostrador
        }
        else if (role == Role.Seated) { left = 35f; right = 35f; } // las manos sobre las piernas

        if (armLeft != null)
            armLeft.localRotation = Quaternion.Slerp(armLeft.localRotation, leftRest * Quaternion.Euler(-left, 0f, 0f), 1f - Mathf.Exp(-6f * dt));
        if (armRight != null)
            armRight.localRotation = Quaternion.Slerp(armRight.localRotation, rightRest * Quaternion.Euler(-right, 0f, 0f), 1f - Mathf.Exp(-6f * dt));
    }
}
