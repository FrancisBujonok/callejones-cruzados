using UnityEngine;

/// <summary>
/// Base comun de todo lo que se sortea cada vez que una pieza se reutiliza:
/// - DecorRandomizer: que juego de objetos se ve (Sets, fachadas, lo de arriba).
/// - SignRandomizer: que dice un cartel.
///
/// CorridorSegment.ResetForReuse llama a Randomize en todos. Los bucles usan la misma base para
/// que sus copias salgan iguales: sortean una sola vez con PickIndex y despues usan Show.
/// </summary>
public abstract class RandomPick : MonoBehaviour
{
    private static readonly System.Random shared = new System.Random();

    /// <summary>Cuantas opciones hay.</summary>
    public abstract int Count { get; }

    /// <summary>La opcion que se ve ahora (-1 = todavia ninguna).</summary>
    public int Current { get; protected set; } = -1;

    /// <summary>Muestra la opcion indicada.</summary>
    public abstract void Show(int index);

    /// <summary>Elige una opcion al azar. Por defecto, todas igual de probables.</summary>
    public virtual int PickIndex(System.Random rng) => rng.Next(Count);

    /// <summary>Elige al azar y la muestra.</summary>
    public void Randomize()
    {
        if (Count == 0) return;
        Show(PickIndex(shared));
    }

    protected virtual void Awake()
    {
        // Si el generador ya eligio antes de prender la pieza, no se vuelve a sortear.
        if (Current < 0) Randomize();
    }
}
