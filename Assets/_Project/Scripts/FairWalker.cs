using UnityEngine;

/// <summary>
/// Paso 33: una persona que pasea por la feria. Recorre una vuelta de puntos (ida por un lado del
/// pasillo, vuelta por el otro), a su propio paso. Cada tanto se para unos segundos a mirar un
/// puesto. Si el jugador esta justo adelante, espera a que se corra (tiene un collider: no se lo
/// atraviesa).
///
/// Mientras camina mueve piernas y brazos y sube y baja un poco; parado, mira para los costados.
/// El modelo mira hacia +Z. Pivotes: "Cabeza", "BrazoIzq", "BrazoDer", "PiernaIzq", "PiernaDer".
/// Lo arma Callejones > Feria de dia (capitulo 1), dentro de "Gente".
/// </summary>
public class FairWalker : MonoBehaviour
{
    [SerializeField] private Vector3[] route;
    [SerializeField] private Transform head, armLeft, armRight, legLeft, legRight;
    [SerializeField] private float speed = 1.2f;
    [Tooltip("Probabilidad de pararse a mirar al llegar a cada punto.")]
    [SerializeField, Range(0f, 1f)] private float stopChance = 0.3f;
    [SerializeField] private Vector2 stopSeconds = new Vector2(2f, 6f);

    private Transform player;
    private int target;
    private float phase, waitUntil, glanceYaw, nextGlance;
    private Quaternion headRest, aL, aR, lL, lR;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Vector3[] points, int startPoint, float walkSpeed, Transform headPivot,
        Transform leftArm, Transform rightArm, Transform leftLeg, Transform rightLeg)
    {
        route = points;
        target = startPoint;
        speed = walkSpeed;
        head = headPivot;
        armLeft = leftArm; armRight = rightArm;
        legLeft = leftLeg; legRight = rightLeg;
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        if (head != null) headRest = head.localRotation;
        if (armLeft != null) aL = armLeft.localRotation;
        if (armRight != null) aR = armRight.localRotation;
        if (legLeft != null) lL = legLeft.localRotation;
        if (legRight != null) lR = legRight.localRotation;
        phase = Random.value * 10f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || route == null || route.Length < 2) return;
        float t = Time.time;

        Vector3 goal = route[target];
        Vector3 to = goal - transform.position;
        to.y = 0f;
        bool walking = t >= waitUntil && !Blocked();

        if (walking)
        {
            if (to.sqrMagnitude > 0.0001f)
            {
                Quaternion face = Quaternion.LookRotation(to.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, face, 1f - Mathf.Exp(-5f * dt));
            }
            Vector3 step = transform.forward * (speed * dt);
            step.y = 0f;
            transform.position += Vector3.ClampMagnitude(step, to.magnitude);
            phase += dt * speed * 5.2f;

            if (to.magnitude < 0.25f)
            {
                target = (target + 1) % route.Length;
                if (Random.value < stopChance) waitUntil = t + Random.Range(stopSeconds.x, stopSeconds.y);
            }
        }

        // Piernas y brazos: un paso completo cada 2 * pi de 'phase'; parado vuelven al lugar.
        float swing = walking ? Mathf.Sin(phase) : 0f;
        float k = 1f - Mathf.Exp(-10f * dt);
        if (legLeft != null) legLeft.localRotation = Quaternion.Slerp(legLeft.localRotation, lL * Quaternion.Euler(swing * 28f, 0f, 0f), k);
        if (legRight != null) legRight.localRotation = Quaternion.Slerp(legRight.localRotation, lR * Quaternion.Euler(-swing * 28f, 0f, 0f), k);
        if (armLeft != null) armLeft.localRotation = Quaternion.Slerp(armLeft.localRotation, aL * Quaternion.Euler(-swing * 18f, 0f, 0f), k);
        if (armRight != null) armRight.localRotation = Quaternion.Slerp(armRight.localRotation, aR * Quaternion.Euler(swing * 18f, 0f, 0f), k);

        // Parado, mira para los costados (hacia los puestos); caminando, adelante.
        if (head != null)
        {
            if (!walking && t >= nextGlance) { glanceYaw = Random.Range(-60f, 60f); nextGlance = t + Random.Range(1.5f, 3.5f); }
            float yaw = walking ? 0f : glanceYaw;
            head.localRotation = Quaternion.Slerp(head.localRotation, headRest * Quaternion.Euler(0f, yaw, 0f), 1f - Mathf.Exp(-3f * dt));
        }
    }

    /// <summary>El jugador esta a menos de 1.3 m y adelante.</summary>
    private bool Blocked()
    {
        if (player == null) return false;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.magnitude < 1.3f && Vector3.Dot(d.normalized, transform.forward) > 0.3f;
    }
}
