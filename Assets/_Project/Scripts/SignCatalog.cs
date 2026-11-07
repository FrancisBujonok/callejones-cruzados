using System.Collections.Generic;

/// <summary>
/// Paso 25: lo que el juego sabe de cada cartel, por el nombre de su PNG (que es tambien el
/// nombre de su material): {formato}_{idioma}_{numero}_{estilo}, por ejemplo V5_tw_06_papel.
///
/// - Que piensa el personaje al mirarlo de cerca (Line). Nunca traduce: no sabe leer chino,
///   japones ni coreano. Comenta lo que ve: el papel, la pintura, un dibujo, la luz.
/// - Cuales dicen "no mires atras" (IsWarning). SignRandomizer los hace salir mas seguido
///   cuanto mas tiempo pasa el jugador sin darse vuelta.
/// - Cuanto pesa en el sorteo ademas de lo que puso la herramienta de editor (WeightScale):
///   el afiche de Manchas sale poco.
///
/// Es una clase "static": una sola tabla para todo el juego, sin objeto en la escena.
/// </summary>
public static class SignCatalog
{
    /// <summary>El afiche de Hugo, en castellano: el unico cartel que el personaje entiende.</summary>
    public const string Manchas = "P_es_01_fotocopia";

    /// <summary>El afiche del gato dibujado (尋貓).</summary>
    private const string CatPoster = "P_tw_03_papel";

    // "Por favor, no mires atras", "No te des vuelta", "Por favor, volve"... en los tres idiomas.
    private static readonly HashSet<string> Warnings = new HashSet<string>
    {
        "V5_tw_06_papel", // 請勿回頭
        "V5_jp_03_papel", // 振り返るな
        "H5_kr_01_rojo",  // 돌아가세요
        "H5_tw_04_papel", // 不要回頭 (Paso 25)
        "V5_tw_09_rojo",  // 別回頭 (Paso 25)
        "V5_kr_03_papel", // 돌아보지마 (Paso 25)
        "H5_jp_02_rojo",  // 振り向くな (Paso 25)
    };

    private static readonly string[] PersonPoster =
    {
        "La foto está tan gastada que la cara podría ser de cualquiera.",
        "No entiendo qué dice. Lo pegaron derecho, con cuidado; los de alrededor están todos torcidos.",
        "Una foto y un sello rojo. No sé si lo buscan o si ya lo encontraron.",
    };

    private static readonly string[] Flyers =
    {
        "No sé qué dicen, pero alguien los pegó hace poco.",
        "Papelitos con teléfonos. Los números los entiendo; lo demás, no.",
        "Pegados unos arriba de otros. Abajo hay más, de hace años.",
    };

    private static readonly string[] PaperSign =
    {
        "No sé qué dice, pero alguien lo pegó hace poco.",
        "Papel mojado y vuelto a secar. La tinta se corrió un poco.",
    };

    private static readonly string[] PaintedSign =
    {
        "No sé leerlo. La pintura se está descascarando en los bordes.",
        "Letras que no entiendo. Una está más gastada que las otras, como si alguien la tocara al pasar.",
    };

    private static readonly string[] LightBox =
    {
        "Una caja de luz. Adentro hay bichos muertos, juntos en una esquina.",
        "Zumba. No entiendo qué dice, pero la luz tiembla cuando la miro de cerca.",
    };

    private static readonly string[] NeonSign =
    {
        "El neón zumba. No entiendo qué dice, pero el color me queda en los ojos cuando parpadeo.",
        "Zumba. No entiendo qué dice, pero la luz tiembla cuando la miro de cerca.",
    };

    private static readonly string[] Address =
    {
        "Una dirección, creo. Los números los entiendo; lo demás, no.",
        "Una placa de dirección. Juraría que ya vi esta misma en otra esquina.",
    };

    private static readonly string[] Arrow =
    {
        "Una flecha. No sé si me dice por dónde salir o por dónde no entrar.",
        "Una flecha verde. Ya seguí varias y sigo acá.",
    };

    private static readonly string[] WarningLines =
    {
        "No sé qué dice. Pero me suena de haberlo visto hace un rato.",
        "Otra vez el mismo cartel. O uno muy parecido.",
        "Están por todos lados. Como si alguien caminara adelante mío, pegándolos.",
    };

    private static readonly string[] ManchasLines =
    {
        "Este sí lo entiendo. Se busca un gato: Manchas.",
        "Faltan dos tiritas con el teléfono. Alguien más lo está buscando.",
        "Dice que se perdió cerca del puerto. No sé para qué lado queda el puerto.",
    };

    /// <summary>Saca el " (Instance)" que Unity agrega a los materiales copiados.</summary>
    public static string Clean(string materialName)
    {
        if (string.IsNullOrEmpty(materialName)) return "";
        int i = materialName.IndexOf(" (", System.StringComparison.Ordinal);
        return i >= 0 ? materialName.Substring(0, i) : materialName;
    }

    public static bool IsWarning(string name) => Warnings.Contains(Clean(name));

    // Los de "no mires atras" nuevos del Paso 25: con calma normal salen poco (asi no cambia
    // cuantos hay al empezar); los hace aparecer WarningBoost, cuando el jugador no se da vuelta.
    private static readonly HashSet<string> RareWarnings = new HashSet<string>
    {
        "H5_tw_04_papel", "V5_tw_09_rojo", "V5_kr_03_papel", "H5_jp_02_rojo",
    };

    /// <summary>Multiplica el peso que puso la herramienta de editor. El afiche de Manchas, poco.</summary>
    public static float WeightScale(string name)
    {
        string clean = Clean(name);
        if (clean == Manchas) return 0.15f;
        if (RareWarnings.Contains(clean)) return 0.35f;
        return 1f;
    }

    /// <summary>
    /// Lo que piensa el personaje. timesSeen: cuantas veces miro antes un cartel igual a este.
    /// warningsSeen: cuantos carteles de "no mires atras" miro antes (de cualquier tipo).
    /// La primera vez, cada tipo de cartel dice siempre lo mismo; las siguientes, va rotando.
    /// </summary>
    public static string Line(string materialName, int timesSeen, int warningsSeen)
    {
        string name = Clean(materialName);
        if (name == Manchas) return ManchasLines[System.Math.Min(timesSeen, ManchasLines.Length - 1)];
        if (Warnings.Contains(name)) return WarningLines[System.Math.Min(warningsSeen, WarningLines.Length - 1)];
        if (name == CatPoster) return "Un gato con manchas. No entiendo nada más, pero el dibujo está hecho con cariño.";

        string[] parts = name.Split('_');
        string format = parts.Length > 0 ? parts[0] : "";
        string style = parts.Length > 3 ? parts[3] : "";
        string[] pool;
        switch (format)
        {
            case "P": pool = PersonPoster; break;
            case "A": pool = Flyers; break;
            case "D": pool = Address; break;
            case "F": pool = Arrow; break;
            case "N": pool = NeonSign; break;
            case "S": pool = style.StartsWith("neon") ? NeonSign : LightBox; break;
            default: pool = style == "papel" ? PaperSign : PaintedSign; break;
        }
        return pool[(StableHash(name) + timesSeen) % pool.Length];
    }

    /// <summary>Un numero que depende solo del texto (string.GetHashCode puede cambiar entre ejecuciones).</summary>
    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }
}
