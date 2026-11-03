using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 21: el goteo. Va en la Main Camera.
///
/// Recorre los DripSource activos (bordes de toldos, aires acondicionados, el techado, ropa
/// mojada) y cada tanto suelta una gota: cae con gravedad, y al llegar abajo suena y, si cae
/// en el piso, deja una onda que se abre y se deshace. Si cae sobre la chapa del techado suena
/// distinto, y si el jugador pasa justo debajo, le cae en la cabeza (suena al lado de la oreja).
///
/// Gotas, ondas y sonidos salen de grupos fijos que se reusan (nada se crea durante el juego).
///
/// Dos cosas sutiles con el miedo:
/// - Con miedo alto, si te quedas quieto unos segundos, el goteo cerca tuyo se calla. Vuelve
///   cuando te moves.
/// - Con mas miedo, una gota que estas mirando de frente puede quedarse quieta en el aire un
///   instante antes de seguir cayendo. Muy de vez en cuando.
/// </summary>
[DisallowMultipleComponent]
public class DripSystem : MonoBehaviour
{
    private const int DropCount = 24;
    private const int RippleCount = 12;
    private const int VoiceCount = 6;
    private const float Gravity = 9.81f;
    private const float RippleLife = 0.7f;

    private enum Hit { Floor, Thing, Metal, Head }

    private static readonly List<DripSource> sources = new List<DripSource>();
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

    [Header("Referencias")]
    [SerializeField] private FearMeter fear;
    [SerializeField] private CorridorTreadmill treadmill;
    [SerializeField] private Camera view;

    [Header("Aspecto")]
    [Tooltip("Un cuadrado de 1 x 1 m (mira hacia -Z). Lo usan las gotas y las ondas.")]
    [SerializeField] private Mesh quad;
    [SerializeField] private Material dropMaterial;
    [SerializeField] private Material rippleMaterial;
    [Tooltip("Ancho de la gota, en metros.")]
    [SerializeField] private float dropWidth = 0.016f;

    [Header("Sonido")]
    [SerializeField] private AudioClip[] floorClips;
    [SerializeField] private AudioClip[] metalClips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
    [Tooltip("Mas lejos que esto no se oye.")]
    [SerializeField] private float maxDistance = 14f;

    [Header("Con miedo: silencio si te quedas quieto")]
    [SerializeField, Range(0f, 1f)] private float silenceFromFear = 0.5f;
    [SerializeField] private float stillSeconds = 3f;
    [SerializeField] private float silenceRadius = 12f;

    [Header("Con miedo: la gota que se queda en el aire")]
    [SerializeField, Range(0f, 1f)] private float freezeFromFear = 0.6f;
    [SerializeField, Range(0f, 1f)] private float freezeChance = 0.35f;
    [Tooltip("Segundos minimos entre una gota quieta y la siguiente (en todo el juego).")]
    [SerializeField] private float freezeCooldown = 25f;
    [Tooltip("Que tan cerca del centro de la vista tiene que estar (grados).")]
    [SerializeField] private float freezeAngle = 7f;
    [SerializeField] private float freezeDistance = 7f;
    [SerializeField] private Vector2 freezeSeconds = new Vector2(0.5f, 1f);

    /// <summary>Para probar: cuantas gotas cayeron y cuantas se quedaron en el aire.</summary>
    public int DropsSpawned { get; private set; }
    public int Freezes { get; private set; }
    public bool Silenced { get; private set; }
    public static int ActiveSources => sources.Count;

    private class Drop
    {
        public Transform t;
        public Renderer r;
        public Vector3 pos;
        public float speed, landY, frozenUntil;
        public Hit hit;
        public bool active, freezeChecked;
    }

    private class Ripple
    {
        public Transform t;
        public Renderer r;
        public float age;
        public bool active;
    }

    private readonly Drop[] drops = new Drop[DropCount];
    private readonly Ripple[] ripples = new Ripple[RippleCount];
    private readonly AudioSource[] voices = new AudioSource[VoiceCount];
    private readonly RaycastHit[] hits = new RaycastHit[8];
    private MaterialPropertyBlock block;
    private int nextVoice;
    private float stillTime, nextFreezeTime;
    private Vector3 lastPlayer;
    private bool hasLastPlayer;

    public static void Register(DripSource source)
    {
        if (!sources.Contains(source)) sources.Add(source);
    }

    public static void Unregister(DripSource source)
    {
        sources.Remove(source);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sources.Clear();
    }

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(FearMeter fearMeter, CorridorTreadmill corridor, Camera camera, Mesh quadMesh,
        Material drop, Material ripple, AudioClip[] floor, AudioClip[] metal)
    {
        fear = fearMeter;
        treadmill = corridor;
        view = camera;
        quad = quadMesh;
        dropMaterial = drop;
        rippleMaterial = ripple;
        floorClips = floor;
        metalClips = metal;
    }

    private void Awake()
    {
        if (view == null) view = GetComponent<Camera>();
        if (fear == null) fear = FindAnyObjectByType<FearMeter>();
        if (treadmill == null) treadmill = FindAnyObjectByType<CorridorTreadmill>();
        block = new MaterialPropertyBlock();

        var root = new GameObject("Goteo").transform;
        for (int i = 0; i < DropCount; i++)
        {
            drops[i] = new Drop();
            drops[i].r = MakeQuad(root, "Gota", dropMaterial, out drops[i].t);
        }
        for (int i = 0; i < RippleCount; i++)
        {
            ripples[i] = new Ripple();
            ripples[i].r = MakeQuad(root, "Onda", rippleMaterial, out ripples[i].t);
        }
        for (int i = 0; i < VoiceCount; i++)
        {
            var go = new GameObject("GotaSonido");
            go.transform.SetParent(root, false);
            AudioSource a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 0.8f;
            a.maxDistance = maxDistance;
            a.dopplerLevel = 0f;
            voices[i] = a;
        }
    }

    private Renderer MakeQuad(Transform root, string name, Material material, out Transform t)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.enabled = false;
        t = go.transform;
        return r;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || view == null) return;
        float now = Time.time;
        float fearLevel = fear != null ? fear.Normalized : 0f;

        // Quieto o caminando: se mira el cuerpo del jugador, no la camara (que se balancea).
        Vector3 player = treadmill != null && treadmill.Player != null ? treadmill.Player.position : view.transform.position;
        if (hasLastPlayer)
        {
            Vector3 step = player - lastPlayer;
            step.y = 0f;
            if (step.magnitude / dt > 0.25f) stillTime = 0f;
            else stillTime += dt;
        }
        lastPlayer = player;
        hasLastPlayer = true;
        Silenced = fearLevel >= silenceFromFear && stillTime >= stillSeconds;

        for (int i = sources.Count - 1; i >= 0; i--)
        {
            DripSource s = sources[i];
            if (s == null) { sources.RemoveAt(i); continue; }
            if (now < s.NextTime) continue;
            if (Silenced && (s.transform.position - player).sqrMagnitude < silenceRadius * silenceRadius)
            {
                // Como si contuviera la respiracion: vuelve un rato despues de que te muevas.
                s.NextTime = now + Random.Range(0.4f, 1.2f);
                continue;
            }
            Spawn(s);
            s.NextTime = now + Random.Range(s.Interval.x, s.Interval.y);
        }

        UpdateDrops(dt, now, fearLevel);
        UpdateRipples(dt);
    }

    private void Spawn(DripSource source)
    {
        Drop d = null;
        foreach (Drop candidate in drops)
            if (!candidate.active) { d = candidate; break; }
        if (d == null) return;

        Vector3 start = source.transform.position;
        float roof = source.RoofY;
        d.landY = source.FloorY;
        d.hit = Hit.Floor;
        if (!float.IsNaN(roof))
        {
            d.landY = roof;
            d.hit = Hit.Metal;
        }
        else
        {
            // Lo primero que haya debajo (sin contar al jugador: eso se ve mientras cae).
            int count = Physics.RaycastNonAlloc(start + Vector3.down * 0.02f, Vector3.down, hits, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (IsPlayer(hits[i].collider) || hits[i].distance >= best) continue;
                best = hits[i].distance;
                d.landY = hits[i].point.y;
                d.hit = hits[i].normal.y > 0.7f && hits[i].point.y < source.FloorY + 0.05f ? Hit.Floor : Hit.Thing;
            }
        }

        d.pos = start;
        d.speed = 0f;
        d.frozenUntil = 0f;
        d.freezeChecked = false;
        d.active = true;
        d.r.enabled = true;
        DropsSpawned++;
    }

    private bool IsPlayer(Collider c)
    {
        Transform player = treadmill != null ? treadmill.Player : null;
        return player != null && c.transform.IsChildOf(player);
    }

    private void UpdateDrops(float dt, float now, float fearLevel)
    {
        Transform cam = view.transform;
        Vector3 camPos = cam.position;
        foreach (Drop d in drops)
        {
            if (!d.active) continue;
            bool frozen = now < d.frozenUntil;
            if (frozen) d.speed = 0f;
            else
            {
                d.speed += Gravity * dt;
                d.pos.y -= d.speed * dt;
            }

            // La gota que se queda en el aire: se decide una sola vez, cuando entra al centro
            // de la vista, y solo si todavia le falta bastante para llegar.
            if (!d.freezeChecked && fearLevel >= freezeFromFear && now >= nextFreezeTime)
            {
                Vector3 to = d.pos - camPos;
                if (to.magnitude < freezeDistance && Vector3.Angle(cam.forward, to) < freezeAngle)
                {
                    d.freezeChecked = true;
                    if (d.pos.y - d.landY > 0.6f && Random.value < freezeChance)
                    {
                        d.frozenUntil = now + Random.Range(freezeSeconds.x, freezeSeconds.y);
                        nextFreezeTime = now + freezeCooldown;
                        Freezes++;
                    }
                }
            }

            // Si pasa justo por la cabeza del jugador, le cae encima.
            Vector2 flat = new Vector2(d.pos.x - camPos.x, d.pos.z - camPos.z);
            if (flat.sqrMagnitude < 0.2f * 0.2f && d.pos.y < camPos.y + 0.12f && d.pos.y > camPos.y - 0.3f)
            {
                Land(d, Hit.Head);
                continue;
            }
            if (d.pos.y <= d.landY)
            {
                d.pos.y = d.landY;
                Land(d, d.hit);
                continue;
            }

            // Un trazo vertical de cara a la camara: mas largo cuanto mas rapido cae.
            float length = frozen ? 0.03f : Mathf.Clamp(0.03f + d.speed * 0.012f, 0.03f, 0.2f);
            Vector3 look = d.pos - camPos;
            look.y = 0f;
            d.t.rotation = look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look) : Quaternion.identity;
            d.t.position = d.pos + Vector3.up * (length * 0.5f);
            d.t.localScale = new Vector3(dropWidth, length, 1f);
        }
    }

    private void Land(Drop d, Hit hit)
    {
        d.active = false;
        d.r.enabled = false;
        switch (hit)
        {
            case Hit.Head:
                Play(floorClips, view.transform.position + view.transform.right * 0.15f, volume * 1.5f, Random.Range(1.15f, 1.3f), 0.4f);
                break;
            case Hit.Metal:
                Play(metalClips, d.pos, volume * 0.8f, Random.Range(0.9f, 1.15f), 1f);
                break;
            case Hit.Thing:
                Play(floorClips, d.pos, volume * 0.7f, Random.Range(1.1f, 1.3f), 1f);
                break;
            default:
                Play(floorClips, d.pos, volume, Random.Range(0.88f, 1.12f), 1f);
                SpawnRipple(d.pos);
                break;
        }
    }

    private void SpawnRipple(Vector3 p)
    {
        foreach (Ripple r in ripples)
        {
            if (r.active) continue;
            r.active = true;
            r.age = 0f;
            r.t.position = p + Vector3.up * 0.006f;
            r.t.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            r.r.enabled = true;
            SetRipple(r, 0f);
            return;
        }
    }

    private void UpdateRipples(float dt)
    {
        foreach (Ripple r in ripples)
        {
            if (!r.active) continue;
            r.age += dt;
            float t = r.age / RippleLife;
            if (t >= 1f)
            {
                r.active = false;
                r.r.enabled = false;
                continue;
            }
            SetRipple(r, t);
        }
    }

    private void SetRipple(Ripple r, float t)
    {
        float size = Mathf.Lerp(0.04f, 0.34f, Mathf.Pow(t, 0.6f));
        r.t.localScale = new Vector3(size, size, 1f);
        // La onda se deshace de a pixeles: el recorte sube y se come el anillo.
        r.r.GetPropertyBlock(block);
        block.SetFloat(CutoffId, Mathf.Lerp(0.3f, 1f, t));
        r.r.SetPropertyBlock(block);
    }

    private void Play(AudioClip[] clips, Vector3 position, float vol, float pitch, float spatial)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;
        AudioSource a = voices[nextVoice];
        nextVoice = (nextVoice + 1) % VoiceCount;
        a.transform.position = position;
        a.spatialBlend = spatial;
        a.pitch = pitch;
        a.volume = Mathf.Clamp01(vol);
        a.clip = clip;
        a.Play();
    }
}
