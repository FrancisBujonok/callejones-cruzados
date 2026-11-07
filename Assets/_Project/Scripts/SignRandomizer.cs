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
        return w;
    }

    private void CacheFlags()
    {
        if (warning != null && warning.Length == Count) return;
        warning = new bool[Count];
        scale = new float[Count];
        for (int i = 0; i < Count; i++)
        {
            string name = options[i] != null ? options[i].name : "";
            warning[i] = SignCatalog.IsWarning(name);
            scale[i] = SignCatalog.WeightScale(name);
        }
    }
}
