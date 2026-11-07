using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 26: CAMBIOS CHICOS. Ademas de cambiar la variante de una pieza (un pasillo que se cierra,
/// un giro que aparece), el laberinto puede cambiar un detalle a tu espalda:
/// - Un cartel que ahora dice otra cosa (SignRandomizer). Con el jugador sin darse vuelta, lo mas
///   probable es que pase a decir "no mires atras" (SignRandomizer.WarningBoost).
/// - Una fachada que ahora es otra: la persiana es una entrada con noren, o una pared con
///   medidores (el DecorRandomizer de un LandmarkSlot, si no tiene un lugar puesto).
///
/// Para el resto del juego es un cambio mas: MazeDirector marca la pieza (MarkChanged) y, cuando
/// el jugador la vuelve a ver, cuenta como descubierto (miedo, sonido y salida).
/// Es una clase "static": solo tiene la regla, sin objeto en la escena.
/// </summary>
public static class SubtleChanges
{
    private static readonly List<RandomPick> candidates = new List<RandomPick>();
    private static readonly System.Random rng = new System.Random();

    /// <summary>Cambia un detalle de la pieza. Devuelve false si la pieza no tiene nada que cambiar.</summary>
    public static bool TryApply(CorridorSegment segment, out string what)
    {
        what = null;
        candidates.Clear();
        // Solo lo que esta prendido: un cartel de un juego de decoracion apagado no se ve.
        foreach (RandomPick pick in segment.GetComponentsInChildren<RandomPick>(false))
        {
            if (pick.Count < 2) continue;
            if (pick is SignRandomizer || IsFacade(pick)) candidates.Add(pick);
        }
        if (candidates.Count == 0) return false;

        RandomPick chosen = candidates[Random.Range(0, candidates.Count)];
        int next = chosen.Current;
        // El sorteo de siempre (con sus pesos), pero tiene que salir algo distinto.
        for (int tries = 0; tries < 8 && next == chosen.Current; tries++) next = chosen.PickIndex(rng);
        if (next == chosen.Current) next = (chosen.Current + 1) % chosen.Count;
        chosen.Show(next);

        if (chosen is SignRandomizer sign)
            what = "cartel, ahora " + (sign.CurrentMaterial != null ? sign.CurrentMaterial.name : "otro");
        else
            what = "fachada";
        return true;
    }

    /// <summary>Las fachadas son el DecorRandomizer hijo directo de un LandmarkSlot vacio.</summary>
    private static bool IsFacade(RandomPick pick)
    {
        if (!(pick is DecorRandomizer)) return false;
        Transform parent = pick.transform.parent;
        LandmarkSlot slot = parent != null ? parent.GetComponent<LandmarkSlot>() : null;
        return slot != null && slot.Occupant == null;
    }
}
