using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Que dice un cartel. Elige uno de varios materiales (cada uno usa un PNG de Textures/Carteles)
/// y se lo pone a todas sus caras: los carteles colgantes y las pancartas tienen dos, una de
/// cada lado, y las dos dicen lo mismo.
///
/// Los pesos hacen que unos salgan mas que otros: la herramienta de editor les da 2 a los
/// carteles de Taiwan y 1 al resto, asi predominan.
/// Se sortea igual que la decoracion: al empezar y cada vez que la pieza se reutiliza.
///
/// Paso 25:
/// - Los carteles de "no mires atras" (SignCatalog.IsWarning) pesan WarningBoost veces mas.
///   LookBackLures lo sube mientras el jugador camina sin darse vuelta, asi que las piezas
///   nuevas de adelante se van llenando de esos carteles. Al darse vuelta vuelve a 1.
/// - SignCatalog.WeightScale ajusta el peso de algunos (el afiche de Manchas sale poco).
/// - Active: los carteles prendidos ahora. SignInspector busca ahi cual se esta mirando.
///
/// Paso 30: menos repeticiones. Cada formato (V5, K, A, M...) recuerda los ultimos carteles que
/// salieron. Mientras un cartel esta en esa lista, pesa 20 veces menos: es muy raro ver el
/// mismo afiche en dos pedazos seguidos. La lista guarda la mitad de los carteles de ese formato
/// que tiene el cartel (como mucho 8). Cada formato va por separado: en los papelitos, las notas
/// (M) y los volantes (A) llevan su propia cuenta, asi frenar los volantes no llena todo de notas.
/// Los de "no mires atras" no se frenan cuando WarningBoost esta alto: ahi la repeticion es a
/// proposito.
/// </summary>
public class SignRandomizer : RandomPick
{
    /// <summary>Paso 25: cuanto mas pesan los carteles de "no mires atras". 1 = normal.</summary>
    public static float WarningBoost = 1f;

    /// <summary>Paso 25: los carteles de las piezas prendidas.</summary>
    public static readonly List<SignRandomizer> Active = new List<SignRandomizer>();

    [Tooltip("Las caras del cartel. Todas muestran lo mismo.")]
    [SerializeField] private Renderer[] faces;
    [Tooltip("Los carteles posibles: un material por cada PNG.")]
    [SerializeField] private Material[] options;
    [Tooltip("Peso de cada cartel (mismo orden que Options). Vacio = todos igual de probables.")]
    [SerializeField] private float[] weights;

    public override int Count => options == null ? 0 : options.Length;
    public Renderer[] Faces => faces;
    /// <summary>El material que se ve ahora (null si todavia no se sorteo).</summary>
    public Material CurrentMaterial => Current >= 0 && Current < Count ? options[Current] : null;

    // Se calculan una vez por cartel, a partir de los nombres de los materiales.
    private bool[] warning;
    private float[] scale;

    // Paso 30: los ultimos que salieron, por formato (la primera parte del nombre: V5, K, A...).
    private static readonly Dictionary<string, List<Material>> recent = new Dictionary<string, List<Material>>();
    private const float RecentPenalty = 0.05f;
    private const int MaxRecent = 8;
    private string[] formatOf;   // el formato de cada opcion
    private int[] keepOf;        // cuantos recuerda ese formato en este cartel

    /// <summary>Lo usa la herramienta de editor al armar el cartel.</summary>
    public void Setup(Renderer[] newFaces, Material[] newOptions, float[] newWeights)
    {
        faces = newFaces;
        options = newOptions;
        weights = newWeights;
    }

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    public override void Show(int index)
    {
        if (Count == 0) return;
        index = Mathf.Clamp(index, 0, options.Length - 1);
        foreach (Renderer face in faces)
            if (face != null) face.sharedMaterial = options[index];
        Current = index;
        if (Application.isPlaying) Remember(index);
    }

    /// <summary>Paso 30: anota que salio este cartel (si ya estaba en la lista, pasa al final).</summary>
    private void Remember(int index)
    {
        Material mat = options[index];
        if (mat == null) return;
        CacheFlags();
        List<Material> list = RecentList(formatOf[index]);
        list.Remove(mat);
        list.Add(mat);
        while (list.Count > keepOf[index]) list.RemoveAt(0);
    }

    private static List<Material> RecentList(string format)
    {
        if (!recent.TryGetValue(format, out List<Material> list))
        {
            list = new List<Material>();
            recent[format] = list;
        }
        return list;
    }

    /// <summary>Sorteo con pesos: se tira un numero entre 0 y la suma, y se ve en que tramo cae.</summary>
    public override int PickIndex(System.Random rng)
    {
        if (Count == 0) return 0;
        CacheFlags();
        bool useWeights = weights != null && weights.Length == Count;
        float total = 0f;
        for (int i = 0; i < Count; i++) total += Weight(i, useWeights);
        if (total <= 0f) return base.PickIndex(rng);

        double r = rng.NextDouble() * total;
        for (int i = 0; i < Count; i++)
        {
            r -= Weight(i, useWeights);
            if (r < 0) return i;
        }
        return Count - 1;
    }

    private float Weight(int i, bool useWeights)
    {
        float w = useWeights ? weights[i] : 1f;
        w *= scale[i];
        if (warning[i]) w *= Mathf.Max(1f, WarningBoost);
        // Paso 30: lo que salio hace poco casi no sale (salvo los avisos cuando estan subiendo).
        bool boosted = warning[i] && WarningBoost > 1.05f;
        if (!boosted && Application.isPlaying && RecentList(formatOf[i]).Contains(options[i])) w *= RecentPenalty;
        return w;
    }

    private void CacheFlags()
    {
        if (warning != null && warning.Length == Count) return;
        warning = new bool[Count];
        scale = new float[Count];
        formatOf = new string[Count];
        keepOf = new int[Count];
        var perFormat = new Dictionary<string, int>();
        for (int i = 0; i < Count; i++)
        {
            string name = options[i] != null ? options[i].name : "";
            warning[i] = SignCatalog.IsWarning(name);
            scale[i] = SignCatalog.WeightScale(name);
            int cut = name.IndexOf('_');
            formatOf[i] = cut > 0 ? name.Substring(0, cut) : name;
            perFormat.TryGetValue(formatOf[i], out int n);
            perFormat[formatOf[i]] = n + 1;
        }
        for (int i = 0; i < Count; i++)
            keepOf[i] = Mathf.Clamp(perFormat[formatOf[i]] / 2, 1, MaxRecent);
    }
}
