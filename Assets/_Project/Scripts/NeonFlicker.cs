using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Neon o caja de luz que falla (Paso 19). Va en cada cartel saliente (S), neon suelto (N) y arco.
///
/// - Calma: cada tanto, un parpadeo cortito, como un tubo viejo.
/// - Con miedo: parpadeos mas seguidos y, desde cierto miedo, apagones de hasta un segundo.
/// - Engano 1: falla sobre todo cuando el cartel esta en el borde de la vision. Mirado de frente
///   se queda quieto, y si estaba apagado vuelve a prenderse. "Lo vi parpadear", pero cuando lo
///   miras esta bien.
/// - Engano 2: con miedo alto, al volver a prenderse despues de un apagon puede mostrar por un
///   instante (unos 3 cuadros) otro texto, que no esta en ningun sorteo. Solo si el cartel esta a
///   la vista y como mucho uno cada 20 segundos en todo el juego: tiene que ser raro.
/// - El zumbido sigue a la luz: en cada corte se corta tambien.
///
/// La luz se cambia con un MaterialPropertyBlock (la emision de ESTE cartel, sin copiar el
/// material). Cuando el cartel esta prendido normal, el bloque se borra: asi sigue entrando en
/// el SRP Batcher como los demas. Lee el miedo de FearMeter, igual que LanternLight.
/// </summary>
public class NeonFlicker : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Las caras del cartel. Todas fallan juntas.")]
    [SerializeField] private Renderer[] faces;
    [Tooltip("Textos escondidos del mismo formato. Se elige uno al azar del mismo tipo (caja o neon).")]
    [SerializeField] private Material[] messages;
    [Tooltip("Zumbido del neon (AudioSource 3D en loop).")]
    [SerializeField] private AudioSource hum;
    [SerializeField, Range(0f, 1f)] private float humVolume = 0.07f;

    [Header("Cuantas veces falla (por minuto)")]
    [Tooltip("Sin miedo.")]
    [SerializeField] private float calmPerMinute = 1f;
    [Tooltip("Con el miedo al maximo.")]
    [SerializeField] private float fearPerMinute = 14f;

    [Header("Apagones")]
    [Tooltip("Desde este miedo (0 a 1) algunas fallas son apagones.")]
    [SerializeField, Range(0f, 1f)] private float blackoutFromFear = 0.35f;
    [Tooltip("Que parte de las fallas son apagones con el miedo al maximo.")]
    [SerializeField, Range(0f, 1f)] private float blackoutShareAtMax = 0.45f;
    [SerializeField] private Vector2 blackoutSeconds = new Vector2(0.2f, 0.9f);

    [Header("Vision")]
    [Tooltip("Grados desde el centro de la mirada: mas cerca que esto, el cartel no falla.")]
    [SerializeField] private float steadyAngle = 10f;
    [Tooltip("Desde aca hasta 60 grados es el borde de la vision: ahi falla mas.")]
    [SerializeField] private float peripheryAngle = 22f;
    [Tooltip("Cuanto mas falla en el borde de la vision.")]
    [SerializeField] private float peripheryBoost = 1.6f;
    [Tooltip("Cuanto falla lo que esta detras (no lo ve nadie, pero no tiene que quedar congelado).")]
    [SerializeField] private float behindRate = 0.4f;
    [Tooltip("Mas lejos que esto (metros) no hace falta: la niebla lo tapa.")]
    [SerializeField] private float maxDistance = 20f;

    [Header("Mensaje escondido")]
    [SerializeField, Range(0f, 1f)] private float messageFromFear = 0.6f;
    [Tooltip("Probabilidad de mostrarlo al volver de un apagon.")]
    [SerializeField, Range(0f, 1f)] private float messageChance = 0.3f;
    [Tooltip("Segundos minimos entre dos mensajes, contando TODOS los carteles: tiene que ser raro.")]
    [SerializeField] private float messageCooldown = 20f;
    [Tooltip("Solo si el cartel esta a menos de estos grados del centro de la mirada (si no, nadie lo ve).")]
    [SerializeField] private float messageMaxAngle = 60f;
    [Tooltip("Cuadros minimos que dura (y nunca menos de 0.06 s).")]
    [SerializeField] private int messageFrames = 3;

    // Un tramo de la falla: cuanto dura y con que brillo (0 = apagado, 1 = normal).
    private struct Segment
    {
        public float seconds, level;
        public bool blackout, message;
    }

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static FearMeter fearMeter;
    private static Camera view;
    private static float nextMessageTime; // compartido: un mensaje cada tanto en todo el juego

    // Al entrar en Play los estaticos vuelven a cero (aunque Unity no recargue los scripts).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        fearMeter = null;
        view = null;
        nextMessageTime = 0f;
    }

    private readonly Queue<Segment> queue = new Queue<Segment>();
    private Segment current;
    private bool running;
    private float timer;
    private int messageFrame;
    private float level = 1f, appliedLevel = 1f;
    private Material[] saved;
    private Material shown;
    private bool showingMessage;
    private MaterialPropertyBlock block;
    private bool humStarted;
    private float centeredTime;
    private float viewAngle = 180f;

    public float Level => level;
    public bool ShowingMessage => showingMessage;

    /// <summary>Lo usa la herramienta de editor al armar el cartel.</summary>
    public void Setup(Renderer[] newFaces, Material[] newMessages, AudioSource humSource)
    {
        faces = newFaces;
        messages = newMessages;
        hum = humSource;
    }

    /// <summary>Para probar: fuerza un apagon ahora.</summary>
    public void ForceBlackout(float seconds)
    {
        queue.Clear();
        running = false;
        queue.Enqueue(new Segment { seconds = seconds, level = 0f, blackout = true });
        EnqueueRecovery(1f);
    }

    private void OnEnable()
    {
        humStarted = false;
        queue.Clear();
        running = false;
        level = 1f;
        Apply();
    }

    private void OnDisable()
    {
        RestoreMessage();
        level = 1f;
        Apply();
    }

    private void Update()
    {
        if (!humStarted) StartHum();
        if (fearMeter == null) fearMeter = FindAnyObjectByType<FearMeter>();
        if (view == null) view = Camera.main;

        float fear = fearMeter != null ? fearMeter.Normalized : 0f;
        float vision = VisionRate(out bool centered);
        centeredTime = centered ? centeredTime + Time.deltaTime : 0f;

        // Mirado de frente: un apagon se corta enseguida (y ahi puede aparecer el mensaje).
        if (running && current.blackout && centeredTime > 0.12f && timer > 0.1f)
        {
            queue.Clear();
            timer = 0f;
            EnqueueRecovery(fear);
        }

        if (!running && queue.Count == 0 && vision > 0f)
        {
            // Mas miedo = mas fallas, pero la curva empieza suave (miedo al cuadrado).
            float perMinute = Mathf.Lerp(calmPerMinute, fearPerMinute, fear * fear) * vision;
            if (Random.value < perMinute / 60f * Time.deltaTime) StartGlitch(fear);
        }

        Step();
        Apply();
    }

    // ------------------------------------------------------------------ la falla

    private void StartGlitch(float fear)
    {
        float blackoutShare = blackoutShareAtMax * Mathf.InverseLerp(blackoutFromFear, 1f, fear);
        if (Random.value < blackoutShare)
        {
            queue.Enqueue(new Segment { seconds = Random.Range(blackoutSeconds.x, blackoutSeconds.y), level = 0f, blackout = true });
            EnqueueRecovery(fear);
            return;
        }

        if (IsLightbox())
        {
            // Tubo fluorescente: baja a media luz un par de veces, mas lento que el neon.
            int n = Random.Range(2, 4);
            for (int i = 0; i < n; i++)
            {
                queue.Enqueue(new Segment { seconds = Random.Range(0.06f, 0.18f), level = Random.Range(0.3f, 0.6f) });
                queue.Enqueue(new Segment { seconds = Random.Range(0.05f, 0.15f), level = 1f });
            }
            queue.Enqueue(new Segment { seconds = Random.Range(0.04f, 0.1f), level = 0f });
        }
        else
        {
            // Neon: cortes secos y rapidos.
            int n = Random.Range(2, 6);
            for (int i = 0; i < n; i++)
            {
                queue.Enqueue(new Segment { seconds = Random.Range(0.03f, 0.09f), level = Random.Range(0f, 0.25f) });
                queue.Enqueue(new Segment { seconds = Random.Range(0.03f, 0.08f), level = 1f });
            }
        }
    }

    // Al volver de un apagon: titubea, se prende, y con miedo alto puede mostrar el mensaje.
    private void EnqueueRecovery(float fear)
    {
        queue.Enqueue(new Segment { seconds = 0.05f, level = 1f });
        queue.Enqueue(new Segment { seconds = Random.Range(0.04f, 0.08f), level = 0f });
        bool message = fear >= messageFromFear && viewAngle < messageMaxAngle && Time.time >= nextMessageTime
                       && Random.value < messageChance && PickMessage() != null;
        if (message) nextMessageTime = Time.time + messageCooldown;
        queue.Enqueue(new Segment { seconds = 0.06f, level = 1f, message = message });
    }

    private void Step()
    {
        if (running)
        {
            timer -= Time.deltaTime;
            if (current.message) messageFrame++;
            bool messageDone = !current.message || messageFrame >= messageFrames;
            if (timer > 0f || !messageDone) return;
            RestoreMessage();
            running = false;
        }

        if (queue.Count == 0)
        {
            level = 1f;
            return;
        }

        current = queue.Dequeue();
        running = true;
        timer = current.seconds;
        messageFrame = 0;
        level = current.level;
        if (current.message) ShowMessage();
    }

    // ------------------------------------------------------------------ vision

    // 0 = no fallar (mirado de frente o lejos), 1 = normal, mas de 1 = borde de la vision.
    private float VisionRate(out bool centered)
    {
        centered = false;
        viewAngle = 180f;
        if (view == null) return 1f;
        Vector3 to = transform.position - view.transform.position;
        float distance = to.magnitude;
        if (distance > maxDistance) return 0f;
        float angle = Vector3.Angle(view.transform.forward, to);
        viewAngle = angle;
        if (angle < steadyAngle)
        {
            centered = true;
            return 0f;
        }
        if (angle < peripheryAngle) return Mathf.InverseLerp(steadyAngle, peripheryAngle, angle) * peripheryBoost;
        if (angle < 60f) return peripheryBoost;
        if (angle < 90f) return Mathf.Lerp(peripheryBoost, behindRate, Mathf.InverseLerp(60f, 90f, angle));
        return behindRate;
    }

    // ------------------------------------------------------------------ mensaje

    private bool IsLightbox()
    {
        if (faces == null || faces.Length == 0 || faces[0] == null) return false;
        Material m = showingMessage && saved != null ? saved[0] : faces[0].sharedMaterial;
        return m != null && m.name.Contains("caja");
    }

    // Uno al azar entre los del mismo tipo que el cartel (caja de luz o neon); si no hay, cualquiera.
    private Material PickMessage()
    {
        if (messages == null || messages.Length == 0) return null;
        string kind = IsLightbox() ? "caja" : "neon";
        var same = new List<Material>();
        foreach (Material m in messages)
            if (m != null && m.name.Contains(kind)) same.Add(m);
        if (same.Count > 0) return same[Random.Range(0, same.Count)];
        return messages[Random.Range(0, messages.Length)];
    }

    private void ShowMessage()
    {
        Material message = PickMessage();
        if (message == null || faces == null) return;
        shown = message;
        saved = new Material[faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i] == null) continue;
            saved[i] = faces[i].sharedMaterial;
            faces[i].sharedMaterial = message;
        }
        showingMessage = true;
        appliedLevel = -1f; // el material cambio: hay que volver a aplicar la emision
    }

    private void RestoreMessage()
    {
        if (!showingMessage) return;
        // Si en esos cuadros otro script cambio el cartel (los bucles lo hacen), gana el otro.
        for (int i = 0; i < faces.Length; i++)
            if (faces[i] != null && saved[i] != null && faces[i].sharedMaterial == shown) faces[i].sharedMaterial = saved[i];
        showingMessage = false;
        appliedLevel = -1f;
    }

    // ------------------------------------------------------------------ luz y zumbido

    private void Apply()
    {
        if (hum != null) hum.volume = humVolume * level;
        if (Mathf.Approximately(level, appliedLevel) || faces == null) return;
        appliedLevel = level;
        if (block == null) block = new MaterialPropertyBlock();

        foreach (Renderer face in faces)
        {
            if (face == null) continue;
            if (level >= 0.999f)
            {
                face.SetPropertyBlock(null); // normal: sin bloque, entra en el SRP Batcher
                continue;
            }
            Material m = face.sharedMaterial;
            Color emission = m != null && m.HasProperty(EmissionId) ? m.GetColor(EmissionId) : Color.black;
            face.GetPropertyBlock(block);
            block.SetColor(EmissionId, emission * level);
            face.SetPropertyBlock(block);
        }
    }

    // Igual que LanternLight: el generador apaga y prende las piezas, y el AudioSource se arranca
    // en el primer Update despues de prenderse, en un punto y con un tono apenas distintos.
    private void StartHum()
    {
        humStarted = true;
        if (hum == null || hum.clip == null || !hum.isActiveAndEnabled) return;
        hum.pitch = Random.Range(1.8f, 2.0f); // mas agudo que el de los faroles
        hum.volume = humVolume;
        hum.Play();
        hum.timeSamples = Random.Range(0, hum.clip.samples);
    }
}
