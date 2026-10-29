using UnityEngine;

/// <summary>
/// Elige al azar uno de varios juegos de decoracion y apaga los demas.
/// Sortea al empezar y otra vez cada vez que la cinta recicla el modulo
/// (CorridorSegment.ResetForReuse lo llama). Asi el callejon no se repite cada 6 metros,
/// y quien preste atencion puede notar que un tramo "ya no es el mismo".
/// Los bucles hacen lo contrario a proposito: con Show eligen el MISMO juego en todas las copias.
/// </summary>
public class DecorRandomizer : MonoBehaviour
{
    [Tooltip("Cada elemento es un hijo con un juego completo de decoracion.")]
    [SerializeField] private GameObject[] sets;

    /// <summary>Cuantos juegos hay.</summary>
    public int Count => sets == null ? 0 : sets.Length;

    /// <summary>El juego que se ve ahora (-1 = todavia ninguno).</summary>
    public int Current { get; private set; } = -1;

    /// <summary>Lo usa la herramienta de editor al armar el modulo.</summary>
    public void Setup(GameObject[] newSets)
    {
        sets = newSets;
    }

    private void Awake()
    {
        // Si el generador ya eligio uno antes de prender la pieza, no se vuelve a sortear.
        if (Current < 0) Randomize();
    }

    public void Randomize()
    {
        if (Count == 0) return;
        Show(Random.Range(0, sets.Length));
    }

    /// <summary>Muestra el juego indicado y apaga los demas.</summary>
    public void Show(int index)
    {
        if (Count == 0) return;
        index = Mathf.Clamp(index, 0, sets.Length - 1);
        for (int i = 0; i < sets.Length; i++)
        {
            if (sets[i] != null) sets[i].SetActive(i == index);
        }
        Current = index;
    }
}
