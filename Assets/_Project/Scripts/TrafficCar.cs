using UnityEngine;

/// <summary>
/// Paso 33: un auto de la calle de la feria. Va derecho por su carril (a lo largo de X), frena si
/// tiene otro auto adelante y acelera cuando se libera. Las ruedas giran segun la velocidad, la
/// carroceria se mece un poco y el motor sube de tono al acelerar.
///
/// No usa fisica: lo mueve FairTraffic (lo saca del deposito, lo pone en un carril y lo guarda
/// cuando llega al final). El modelo mira hacia +Z; las ruedas giran alrededor de su X local.
///
/// Lo arma Callejones > Feria de dia (capitulo 1), dentro de "Transito".
/// </summary>
public class TrafficCar : MonoBehaviour
{
    [Tooltip("Largo del auto (m): lo usa FairTraffic para dejar distancia con el de adelante.")]
    [SerializeField] private float length = 4.4f;
    [SerializeField] private float wheelRadius = 0.3f;
    [SerializeField] private Transform[] wheels;
    [Tooltip("La carroceria (sin las ruedas): se mece un poco.")]
    [SerializeField] private Transform body;
    [SerializeField] private AudioSource engine;
    [Tooltip("Velocidad de crucero, minimo y maximo (m/s). 8 m/s = 29 km/h.")]
    [SerializeField] private Vector2 cruiseRange = new Vector2(7f, 11f);
    [Tooltip("Probabilidad de salir del deposito, comparado con los otros (el colectivo sale menos).")]
    [SerializeField] private float weight = 1f;
    [SerializeField] private float acceleration = 2.2f;
    [SerializeField] private float braking = 6f;

    public float Length => length;
    public float Weight => weight;
    public float Speed { get; private set; }
    public int Lane { get; private set; } = -1;
    public bool Driving => Lane >= 0;

    private FairTraffic traffic;
    private float cruise, direction, endX, holdUntil, bobPhase;
    private float basePitch = 1f, baseVolume = 0.4f;
    private Vector3 bodyRest;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(float carLength, float radius, Transform[] wheelList, Transform bodyRoot, AudioSource engineSource,
        Vector2 cruiseSpeeds, float spawnWeight)
    {
        length = carLength;
        wheelRadius = radius;
        wheels = wheelList;
        body = bodyRoot;
        engine = engineSource;
        cruiseRange = cruiseSpeeds;
        weight = spawnWeight;
    }

    private void Awake()
    {
        if (body != null) bodyRest = body.localPosition;
        if (engine != null) { basePitch = engine.pitch; baseVolume = engine.volume; }
    }

    /// <summary>Lo pone a andar en un carril. holdSeconds: cuanto espera quieto antes de arrancar.</summary>
    public void Launch(FairTraffic owner, int lane, float x, float z, float dir, float laneEndX, float startSpeed, float holdSeconds = 0f)
    {
        traffic = owner;
        Lane = lane;
        direction = dir;
        endX = laneEndX;
        cruise = Random.Range(cruiseRange.x, cruiseRange.y);
        Speed = Mathf.Min(startSpeed, cruise);
        holdUntil = Time.time + holdSeconds;
        bobPhase = Random.value * 10f;
        transform.position = new Vector3(x, 0f, z);
        transform.rotation = Quaternion.Euler(0f, dir > 0f ? 90f : -90f, 0f);
        gameObject.SetActive(true);
        if (engine != null && !engine.isPlaying)
        {
            engine.time = engine.clip != null ? Random.Range(0f, engine.clip.length * 0.9f) : 0f; // cada uno en otro punto del loop
            engine.Play();
        }
    }

    public void Park()
    {
        Lane = -1;
        Speed = 0f;
        if (engine != null) engine.Stop();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!Driving || traffic == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Cuanto quiere ir: la de crucero, menos si el de adelante esta cerca (para quedar a unos
        // 6 m + 1 s de distancia), y cero mientras espera (el colectivo en la parada).
        float target = cruise;
        float gap = traffic.GapAhead(this);
        if (gap < float.MaxValue)
        {
            float safe = 5f + Speed * 1.1f;
            target = Mathf.Min(target, cruise * Mathf.Clamp01((gap - 3f) / safe));
        }
        if (Time.time < holdUntil) target = 0f;
        Speed = Mathf.MoveTowards(Speed, target, (target > Speed ? acceleration : braking) * dt);

        transform.position += Vector3.right * (direction * Speed * dt);

        // Ruedas: una vuelta cada 2 * pi * radio metros.
        if (wheels != null && wheelRadius > 0f)
        {
            float degrees = Speed / wheelRadius * Mathf.Rad2Deg * dt;
            foreach (Transform w in wheels) if (w != null) w.Rotate(degrees, 0f, 0f, Space.Self);
        }

        // La carroceria: un vaiven chico, mas notorio en marcha.
        if (body != null)
        {
            bobPhase += dt * (2f + Speed * 0.6f);
            float amount = 0.004f + Mathf.Min(Speed, 10f) * 0.0012f;
            body.localPosition = bodyRest + Vector3.up * (Mathf.Sin(bobPhase) * amount);
        }

        // Motor: mas agudo y un poco mas fuerte con la velocidad.
        if (engine != null)
        {
            float k = cruise > 0f ? Speed / cruise : 0f;
            engine.pitch = basePitch * (0.8f + 0.45f * k);
            engine.volume = baseVolume * (0.6f + 0.4f * k);
        }

        if ((direction > 0f && transform.position.x > endX) || (direction < 0f && transform.position.x < endX))
            traffic.Recycle(this);
    }
}
