using UnityEngine;

/// <summary>
/// Paso 32: el gato blanco y negro de la entrada del callejon (capitulo 1). Es la pista de por donde
/// se sale de la feria, y una referencia a la novela (Hugo busca a su gato).
///
/// 1. Esperando: sentado en la boca del callejon, mirando hacia la feria. Mueve la cola, y la
///    cabeza sigue al jugador cuando esta cerca. Cada tanto maulla (se oye de lejos: sirve para
///    encontrarlo).
/// 2. Cuando el jugador se acerca a menos de 'callDistance', maulla, se da vuelta y camina por el
///    callejon hasta el ultimo punto de 'path'. Ahi desaparece en la oscuridad.
/// 3. El jugador lo sigue y, unos metros adentro, ChapterExit termina el capitulo.
///
/// Va en el objeto "Gato" (en la entrada del callejon). Lo arma Callejones > Feria de dia.
/// El modelo mira hacia +Z; la cola y la cabeza son hijos con nombre "Cola" y "Cabeza".
/// </summary>
public class LeadingCat : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform head;
    [SerializeField] private Transform tail;
    [SerializeField] private AudioSource voice;
    [SerializeField] private AudioClip[] meows;
    [Tooltip("Puntos por los que camina al irse (en el mundo). El ultimo esta en lo oscuro.")]
    [SerializeField] private Vector3[] path;

    [Header("Cuando")]
    [Tooltip("Metros (en el plano) a los que se va por el callejon.")]
    [SerializeField] private float callDistance = 6f;
    [Tooltip("Segundos entre maullidos mientras espera (minimo y maximo).")]
    [SerializeField] private Vector2 meowEvery = new Vector2(14f, 24f);
    [SerializeField] private float walkSpeed = 1.6f;

    public bool Gone { get; private set; }

    private enum Mode { Waiting, Leaving, Gone }
    private Mode mode = Mode.Waiting;
    private int target;
    private float nextMeow;
    private Quaternion headRest, tailRest;
    private float walkPhase;
    private Vector3 basePos;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Transform playerTransform, Transform headPivot, Transform tailPivot, AudioSource source,
        AudioClip[] clips, Vector3[] walkPath)
    {
        player = playerTransform;
        head = headPivot;
        tail = tailPivot;
        voice = source;
        meows = clips;
        path = walkPath;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (head != null) headRest = head.localRotation;
        if (tail != null) tailRest = tail.localRotation;
        nextMeow = Time.time + Random.Range(4f, 8f);
        basePos = transform.position;
    }

    private static bool Playing() =>
        GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    private void Update()
    {
        if (mode == Mode.Gone || player == null) return;
        float t = Time.time;

        // La cola, siempre: un vaiven lento, mas rapido mientras camina.
        if (tail != null)
        {
            float speed = mode == Mode.Leaving ? 6f : 1.7f;
            tail.localRotation = tailRest * Quaternion.Euler(0f, Mathf.Sin(t * speed) * 25f, Mathf.Sin(t * speed * 0.5f) * 8f);
        }

        Vector3 to = player.position - transform.position;
        to.y = 0f;

        if (mode == Mode.Waiting)
        {
            // La cabeza sigue al jugador si esta adelante y cerca.
            if (head != null)
            {
                Vector3 local = transform.InverseTransformDirection(to.normalized);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
                Quaternion want = to.magnitude < 14f ? headRest * Quaternion.Euler(0f, yaw, 0f) : headRest;
                head.localRotation = Quaternion.Slerp(head.localRotation, want, 1f - Mathf.Exp(-4f * Time.deltaTime));
            }

            if (Playing() && t >= nextMeow)
            {
                Meow(0.8f);
                nextMeow = t + Random.Range(meowEvery.x, meowEvery.y);
            }

            if (Playing() && to.magnitude < callDistance && path != null && path.Length > 0)
            {
                Meow(1f);
                mode = Mode.Leaving;
                target = 0;
            }
            return;
        }

        // Se va: camina punto por punto, con un saltito en cada paso.
        Vector3 goal = path[target];
        Vector3 flat = goal - basePos;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0004f)
        {
            Quaternion face = Quaternion.LookRotation(flat.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        basePos = Vector3.MoveTowards(basePos, new Vector3(goal.x, basePos.y, goal.z), walkSpeed * Time.deltaTime);
        walkPhase += Time.deltaTime * 9f;
        transform.position = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(walkPhase)) * 0.025f;
        if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, headRest, 1f - Mathf.Exp(-6f * Time.deltaTime));

        if ((new Vector3(goal.x, basePos.y, goal.z) - basePos).sqrMagnitude < 0.0025f)
        {
            target++;
            if (target >= path.Length)
            {
                mode = Mode.Gone;
                Gone = true;
                gameObject.SetActive(false);
            }
        }
    }

    private void Meow(float volume)
    {
        if (voice == null || meows == null || meows.Length == 0) return;
        voice.pitch = Random.Range(0.95f, 1.08f);
        voice.PlayOneShot(meows[Random.Range(0, meows.Length)], volume);
    }
}
