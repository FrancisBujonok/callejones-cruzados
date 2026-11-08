using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 28: las lineas de los objetos que se pueden mirar (Inspectable). Las lee de un archivo
/// de texto, Dialogos/Objetos.txt, para poder cambiarlas sin tocar codigo.
///
/// Formato del archivo, una linea por renglon:
///     clave | cuando | lo que dice el personaje
/// - clave: la del Inspectable (lata, jaula, persiana...).
/// - cuando: calma, miedo u otra_vez.
///     calma    = lo normal.
///     miedo    = con el miedo en 50 o mas (si la clave tiene alguna).
///     otra_vez = si ya miraste ese tipo de objeto antes (6 de cada 10 veces, si tiene alguna).
/// - Los renglones que empiezan con # y los vacios se ignoran.
///
/// De las lineas que corresponden se elige una al azar, sin repetir la ultima que dijo ese
/// objeto. Si una clave no tiene lineas, dice "No tiene nada de especial."
/// </summary>
public class ObjectLines
{
    private const string Fallback = "No tiene nada de especial.";

    private enum When { Calm, Fear, Again }

    private readonly Dictionary<string, List<string>[]> lines = new Dictionary<string, List<string>[]>();
    private readonly Dictionary<string, string> last = new Dictionary<string, string>();

    public int Count { get; private set; }

    public ObjectLines(TextAsset file)
    {
        if (file == null) return;
        int row = 0;
        foreach (string raw in file.text.Split('\n'))
        {
            row++;
            string text = raw.Trim();
            if (text.Length == 0 || text.StartsWith("#")) continue;
            string[] parts = text.Split(new[] { '|' }, 3);
            if (parts.Length < 3 || !TryParseWhen(parts[1].Trim(), out When when))
            {
                Debug.LogWarning($"[ObjectLines] Renglon {row} mal escrito (va 'clave | calma/miedo/otra_vez | linea'): {text}");
                continue;
            }
            string key = parts[0].Trim().ToLowerInvariant();
            if (!lines.TryGetValue(key, out List<string>[] groups))
            {
                groups = new[] { new List<string>(), new List<string>(), new List<string>() };
                lines[key] = groups;
            }
            groups[(int)when].Add(parts[2].Trim());
            Count++;
        }
    }

    /// <summary>
    /// Elige que dice el personaje.
    /// seen: cuantas veces ya miro objetos con esta clave. fear: el miedo de 0 a 1.
    /// </summary>
    public string Line(string key, int seen, float fear)
    {
        key = (key ?? "").ToLowerInvariant();
        if (!lines.TryGetValue(key, out List<string>[] groups)) return Fallback;

        List<string> pool = groups[(int)When.Calm];
        if (seen > 0 && groups[(int)When.Again].Count > 0 && Random.value < 0.6f) pool = groups[(int)When.Again];
        else if (fear >= 0.5f && groups[(int)When.Fear].Count > 0) pool = groups[(int)When.Fear];
        if (pool.Count == 0) pool = groups[0].Count > 0 ? groups[0] : groups[1].Count > 0 ? groups[1] : groups[2];
        if (pool.Count == 0) return Fallback;

        last.TryGetValue(key, out string previous);
        string chosen = pool[Random.Range(0, pool.Count)];
        if (pool.Count > 1 && chosen == previous)
            chosen = pool[(pool.IndexOf(chosen) + 1 + Random.Range(0, pool.Count - 1)) % pool.Count];
        last[key] = chosen;
        return chosen;
    }

    private static bool TryParseWhen(string word, out When when)
    {
        switch (word.ToLowerInvariant())
        {
            case "calma": when = When.Calm; return true;
            case "miedo": when = When.Fear; return true;
            case "otra_vez":
            case "otra vez": when = When.Again; return true;
            default: when = When.Calm; return false;
        }
    }
}
