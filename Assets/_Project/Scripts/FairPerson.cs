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
/// Paso 36 (lo raro de a poco): FairUnease decide que parte de la gente "mira de mas"
/// (StareShare). Cada persona tiene su numero (rank): si es menor que esa parte, deja de hacer lo
/// suyo, mira al jugador desde mucho mas lejos y no le saca los ojos de encima; si el jugador se
/// va hacia un costado, gira el cuerpo para seguirlo. Con VendorsStare (cuando el gato ya esta en
/// el callejon) todos los vendedores miran.
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

    /// <summary>Paso 36: que parte de la gente mira al jugador de mas (0 = nadie, 1 = todos). Lo pone FairUnease.</summary>
    public static float StareShare;
    /// <summary>Paso 36: todos los vendedores miran al jugador. Lo pone FairUnease.</summary>
    public static bool VendorsStare;

    private Transform player;
    private Quaternion headRest, leftRest, rightRest, bodyRest;
    private float rank;
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
        bodyRest = transform.localRotation;
        rank = Random.value;
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

        bool watching = Watching();
        Body(watching, dt);
        Head(t, dt, watching);
        Arms(t, dt, watching);
    }

    /// <summary>Paso 36: esta persona mira de mas.</summary>
    private bool Watching()
    {
        if (player == null) return false;
        if (VendorsStare && role == Role.Vendor) return true;
        return rank < StareShare * 0.85f; // nunca todos: siempre queda alguien en lo suyo
    }

    /// <summary>Paso 36: si mira de mas y el jugador se le va de costado, gira el cuerpo despacio.</summary>
    private void Body(bool watching, float dt)
    {
        Quaternion want = bodyRest;
        if (watching && role != Role.Seated)
        {
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            if (to.magnitude < 16f && to.sqrMagnitude > 0.01f)
            {
                Quaternion face = Quaternion.LookRotation(to.normalized);
                Quaternion rest = transform.parent != null ? transform.parent.rotation * bodyRest : bodyRest;
                // El cuerpo gira lo que la cabeza no alcanza (la cabeza llega hasta 70 grados).
                float off = Mathf.DeltaAngle(rest.eulerAngles.y, face.eulerAngles.y);
                float body = off - Mathf.Clamp(off, -70f, 70f);
                want = bodyRest * Quaternion.Euler(0f, body, 0f);
            }
        }
        transform.localRotation = Quaternion.Slerp(transform.localRotation, want, 1f - Mathf.Exp(-1.2f * dt));
    }

    private void Head(float t, float dt, bool watching)
    {
        if (head == null) return;
        float yaw = glanceYaw, pitch = glancePitch;

        if (watching)
        {
            // Mira al jugador, lejos o cerca, sin cortar.
            Vector3 to = player.position + Vector3.up * 0.6f - head.position;
            if (new Vector3(to.x, 0f, to.z).magnitude < 16f)
            {
                Vector3 local = transform.InverseTransformDirection(to.normalized);
                float y = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float p = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
                Quaternion stare = headRest * Quaternion.Euler(Mathf.Clamp(p, -20f, 25f), Mathf.Clamp(y, -70f, 70f), 0f);
                head.localRotation = Quaternion.Slerp(head.localRotation, stare, 1f - Mathf.Exp(-2f * dt));
                return;
            }
        }

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

    private void Arms(float t, float dt, bool watching)
    {
        float left = 0f, right = 0f;

        if (watching && role != Role.Drummer)
        {
            // Deja lo que estaba haciendo: los brazos quietos al costado.
            if (role == Role.Seated) { left = 35f; right = 35f; }
            nextGesture = t + 2f;
        }
        else if (role == Role.Drummer)
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
