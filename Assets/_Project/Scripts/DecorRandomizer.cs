using UnityEngine;

/// <summary>
/// Elige al azar uno de varios juegos de decoracion y apaga los demas.
/// Sortea al empezar y otra vez cada vez que la cinta recicla el modulo
/// (CorridorSegment.ResetForReuse lo llama). Asi el callejon no se repite cada 6 metros,
/// y quien preste atencion puede notar que un tramo "ya no es el mismo".
/// </summary>
public class DecorRandomizer : MonoBehaviour
{
    [Tooltip("Cada elemento es un hijo con un juego completo de decoracion.")]
    [SerializeField] private GameObject[] sets;

    /// <summary>Lo usa la herramienta de editor al armar el modulo.</summary>
    public void Setup(GameObject[] newSets)
    {
        sets = newSets;
    }

    private void Awake()
    {
        Randomize();
    }

    public void Randomize()
    {
        if (sets == null || sets.Length == 0) return;

        int pick = Random.Range(0, sets.Length);
        for (int i = 0; i < sets.Length; i++)
        {
            if (sets[i] != null) sets[i].SetActive(i == pick);
        }
    }
}
