using UnityEngine;

/// <summary>
/// Paso 37: los sonidos chicos del bolsillo y de los puestos (moneda, sello, mordida, trago,
/// la bolita de la ramune, el agua y el papel del poi). Va en el Player de la feria.
/// Cualquier script los pide por nombre: PocketSounds.Play("Moneda").
/// </summary>
public class PocketSounds : MonoBehaviour
{
    [System.Serializable]
    public struct Named { public string name; public AudioClip clip; public float volume; }

    public static PocketSounds Instance { get; private set; }

    [SerializeField] private Named[] sounds;
    private AudioSource source;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Named[] list) => sounds = list;

    private void Awake()
    {
        Instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 60;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Play(string name, float pitch = 1f)
    {
        if (Instance == null || Instance.sounds == null) return;
        foreach (Named n in Instance.sounds)
        {
            if (n.name != name || n.clip == null) continue;
            Instance.source.pitch = pitch;
            Instance.source.PlayOneShot(n.clip, n.volume > 0f ? n.volume : 0.8f);
            return;
        }
    }
}
