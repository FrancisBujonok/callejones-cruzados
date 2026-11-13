using UnityEngine;

/// <summary>
/// Paso 32: los sonidos de la feria. Cada "capa" es un grupo de clips que suena cada tanto en un
/// punto al azar adentro de su caja (o siempre en el mismo punto, si la caja es chica):
/// - Gente: el murmullo (clips de 7 s que arrancan cada 2-4 s y se enciman: queda continuo).
/// - Tambor: desde el escenario.
/// - Campanillas: en el puesto de la adivina.
/// - Perros: lejos, detras de los edificios.
///
/// Paso 32b (arreglo): antes cada capa tenia UNA fuente que se movia a un punto nuevo en cada
/// sonido. Como los clips se enciman, al moverla tambien se llevaba los que todavia sonaban: si el
/// punto nuevo caia lejos, el murmullo entero se cortaba de golpe. Ahora cada capa tiene varias
/// "voces" (fuentes) y cada sonido usa una libre, que no se mueve hasta que termina. Y con
/// "Cerca del jugador", el punto se elige dentro de la caja pero a pocos metros de donde estas,
/// asi el murmullo te acompana por todo el pasillo.
///
/// Mas adelante se pueden sumar capas (autos pasando, vendedores que llaman) sin tocar el codigo:
/// es agregar una fila en el Inspector.
///
/// Paso 36 (lo raro de a poco), lo maneja FairUnease:
/// - RepeatAmount: el musico y el tambor repiten siempre el mismo clip, cada vez un poco mas
///   grave (sin variacion).
/// - CrowdFade: el murmullo de la gente se va apagando (tambien lo que ya esta sonando).
///
/// Va en el objeto "SonidoFeria". Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class FairAmbience : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public string name;
        public AudioClip[] clips;
        [Tooltip("Segundos entre un sonido y el siguiente (minimo y maximo).")]
        public Vector2 every = new Vector2(3f, 5f);
        [Range(0f, 1f)] public float volume = 0.7f;
        [Tooltip("Centro de la caja donde suena (en el mundo).")]
        public Vector3 center;
        [Tooltip("Tamano de la caja. Cero = siempre en el centro.")]
        public Vector3 size;
        public float maxDistance = 30f;
        [Tooltip("Variacion de tono (+-).")]
        public float pitchJitter = 0.04f;
        [Tooltip("Cuantos sonidos de esta capa pueden sonar a la vez.")]
        [Range(1, 6)] public int voices = 2;
        [Tooltip("Cerca del jugador: el punto se elige dentro de la caja pero a esta distancia del " +
                 "jugador como mucho (0 = en cualquier lugar de la caja).")]
        public float nearPlayer = 0f;

        [System.NonSerialized] public AudioSource[] sources;
        [System.NonSerialized] public float next;
        [System.NonSerialized] public int last = -1;
    }

    /// <summary>
    /// Paso 33: avisa cada vez que una capa suena (nombre de la capa, duracion del clip). Lo usa
    /// el que toca el tambor (FairPerson) para golpear cuando suena el tambor.
    /// </summary>
    public static event System.Action<string, float> Played;

    /// <summary>Paso 36: 0 = normal; 1 = el musico y el tambor repiten el mismo clip, bien graves.</summary>
    public static float RepeatAmount;
    /// <summary>Paso 36: 0 = normal; 1 = el murmullo de la gente apagado.</summary>
    public static float CrowdFade;

    [SerializeField] private Layer[] layers;
    [Tooltip("El que escucha (la camara del jugador). Vacio = el AudioListener de la escena.")]
    [SerializeField] private Transform listener;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Layer[] list) => layers = list;

    private void Start()
    {
        if (listener == null)
        {
            AudioListener l = FindFirstObjectByType<AudioListener>();
            if (l != null) listener = l.transform;
        }
        if (layers == null) return;
        foreach (Layer layer in layers)
        {
            int count = Mathf.Max(1, layer.voices);
            layer.sources = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Fuente_" + layer.name + "_" + i);
                go.transform.SetParent(transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f;
                s.maxDistance = layer.maxDistance;
                s.dopplerLevel = 0f;
                s.priority = 90;
                layer.sources[i] = s;
            }
            layer.next = Time.time + Random.Range(0f, layer.every.y);
        }
    }

    private void Update()
    {
        if (layers == null) return;
        foreach (Layer layer in layers)
        {
            if (layer.sources == null) continue;
            bool crowd = layer.name == "Gente";
            bool repeats = RepeatAmount > 0f && (layer.name == "Musico" || layer.name == "Tambor");
            float volume = layer.volume * (crowd ? 1f - 0.92f * CrowdFade : 1f);
            if (crowd) // Paso 36: el murmullo que ya suena tambien baja
                foreach (AudioSource s in layer.sources)
                    if (s != null && s.isPlaying) s.volume = Mathf.MoveTowards(s.volume, volume, Time.deltaTime * 0.2f);

            if (layer.clips == null || layer.clips.Length == 0 || Time.time < layer.next) continue;
            layer.next = Time.time + Random.Range(layer.every.x, layer.every.y);

            AudioSource source = FreeSource(layer);
            if (source == null) continue; // todas ocupadas: se saltea este (el siguiente llega enseguida)

            source.transform.position = PickPoint(layer);
            int i = Random.Range(0, layer.clips.Length);
            if (layer.clips.Length > 1 && i == layer.last) i = (i + 1) % layer.clips.Length;
            if (repeats && layer.last >= 0 && Random.value < 0.4f + 0.6f * RepeatAmount) i = layer.last; // la misma, otra vez
            layer.last = i;
            source.clip = layer.clips[i];
            source.volume = volume;
            source.pitch = repeats
                ? 1f - 0.16f * RepeatAmount
                : 1f + Random.Range(-layer.pitchJitter, layer.pitchJitter);
            source.Play();
            Played?.Invoke(layer.name, source.clip.length / Mathf.Max(0.1f, source.pitch));
        }
    }

    private static AudioSource FreeSource(Layer layer)
    {
        foreach (AudioSource s in layer.sources)
            if (s != null && !s.isPlaying) return s;
        return null;
    }

    private Vector3 PickPoint(Layer layer)
    {
        Vector3 r = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
        Vector3 p = layer.center + Vector3.Scale(r, layer.size);
        if (layer.nearPlayer <= 0f || listener == null) return p;

        // Un punto a pocos metros del jugador, metido adentro de la caja.
        Vector2 around = Random.insideUnitCircle * layer.nearPlayer;
        Vector3 near = listener.position + new Vector3(around.x, 0f, around.y);
        Vector3 half = layer.size * 0.5f;
        near.x = Mathf.Clamp(near.x, layer.center.x - half.x, layer.center.x + half.x);
        near.z = Mathf.Clamp(near.z, layer.center.z - half.z, layer.center.z + half.z);
        near.y = layer.center.y;
        return near;
    }
}
