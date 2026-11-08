using UnityEngine;

/// <summary>
/// Elige al azar uno de varios juegos de decoracion y apaga los demas.
/// Sortea al empezar y otra vez cada vez que la cinta recicla el modulo
/// (CorridorSegment.ResetForReuse lo llama). Asi el callejon no se repite cada 6 metros,
/// y quien preste atencion puede notar que un tramo "ya no es el mismo".
/// Los bucles hacen lo contrario a proposito: con Show eligen el MISMO juego en todas las copias.
/// El sorteo en si esta en RandomPick (la base comun con SignRandomizer).
///
/// Paso 27: pesos opcionales. Sin pesos, todos los juegos son igual de probables (como antes).
/// La basura los usa para que "nada" salga mas seguido que cada tipo de basura.
/// </summary>
public class DecorRandomizer : RandomPick
{
    [Tooltip("Cada elemento es un hijo con un juego completo de decoracion.")]
    [SerializeField] private GameObject[] sets;
    [Tooltip("Paso 27: peso de cada juego (mismo orden). Vacio = todos iguales.")]
    [SerializeField] private float[] weights;

    /// <summary>Cuantos juegos hay.</summary>
    public override int Count => sets == null ? 0 : sets.Length;

    /// <summary>Lo usa la herramienta de editor al armar el modulo.</summary>
    public void Setup(GameObject[] newSets, float[] newWeights = null)
    {
        sets = newSets;
        weights = newWeights;
    }

    /// <summary>Con pesos: cada juego sale en proporcion a su peso. Sin pesos: todos iguales.</summary>
    public override int PickIndex(System.Random rng)
    {
        if (weights == null || weights.Length != Count) return base.PickIndex(rng);
        float total = 0f;
        foreach (float w in weights) total += Mathf.Max(0f, w);
        if (total <= 0f) return base.PickIndex(rng);
        float r = (float)rng.NextDouble() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            r -= Mathf.Max(0f, weights[i]);
            if (r < 0f) return i;
        }
        return weights.Length - 1;
    }

    /// <summary>Muestra el juego indicado y apaga los demas.</summary>
    public override void Show(int index)
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
