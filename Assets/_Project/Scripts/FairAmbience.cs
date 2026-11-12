using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 32: los sonidos de la feria. Cada "capa" es un grupo de clips que suena cada tanto en un
/// punto al azar adentro de su caja (o siempre en el mismo punto, si la caja es chica):
/// - Gente: el murmullo (clips de 7 s que arrancan cada 2-4 s y se enciman: queda continuo).
/// - Tambor: desde el escenario.
/// - Campanillas: en el puesto de la adivina.
/// - Perros: lejos, detras de los edificios.
/// Cada capa usa su propia fuente de sonido (hija de este objeto) y nunca repite el mismo clip dos
/// veces seguidas.
///
/// Mas adelante se pueden sumar capas (autos pasando, vendedores que llaman) sin tocar el codigo:
/// es agregar una fila en el Inspector.
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

        [System.NonSerialized] public AudioSource source;
        [System.NonSerialized] public float next;
        [System.NonSerialized] public int last = -1;
    }

    [SerializeField] private Layer[] layers;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Layer[] list) => layers = list;

    private void Start()
    {
        if (layers == null) return;
        foreach (Layer layer in layers)
        {
            var go = new GameObject("Fuente_" + layer.name);
            go.transform.SetParent(transform, false);
            layer.source = go.AddComponent<AudioSource>();
            layer.source.playOnAwake = false;
            layer.source.spatialBlend = 1f;
            layer.source.rolloffMode = AudioRolloffMode.Linear;
            layer.source.minDistance = 2f;
            layer.source.maxDistance = layer.maxDistance;
            layer.source.dopplerLevel = 0f;
            layer.next = Time.time + Random.Range(0f, layer.every.y);
        }
    }

    private void Update()
    {
        if (layers == null) return;
        foreach (Layer layer in layers)
        {
            if (layer.source == null || layer.clips == null || layer.clips.Length == 0 || Time.time < layer.next) continue;
            layer.next = Time.time + Random.Range(layer.every.x, layer.every.y);

            Vector3 r = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
            layer.source.transform.position = layer.center + Vector3.Scale(r, layer.size);
            int i = Random.Range(0, layer.clips.Length);
            if (layer.clips.Length > 1 && i == layer.last) i = (i + 1) % layer.clips.Length;
            layer.last = i;
            layer.source.pitch = 1f + Random.Range(-layer.pitchJitter, layer.pitchJitter);
            layer.source.PlayOneShot(layer.clips[i], layer.volume);
        }
    }
}
