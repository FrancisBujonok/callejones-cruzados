using UnityEngine;

/// <summary>
/// Que dice un cartel. Elige uno de varios materiales (cada uno usa un PNG de Textures/Carteles)
/// y se lo pone a todas sus caras: los carteles colgantes y las pancartas tienen dos, una de
/// cada lado, y las dos dicen lo mismo.
///
/// Los pesos hacen que unos salgan mas que otros: la herramienta de editor les da 2 a los
/// carteles de Taiwan y 1 al resto, asi predominan.
/// Se sortea igual que la decoracion: al empezar y cada vez que la pieza se reutiliza.
/// </summary>
public class SignRandomizer : RandomPick
{
    [Tooltip("Las caras del cartel. Todas muestran lo mismo.")]
    [SerializeField] private Renderer[] faces;
    [Tooltip("Los carteles posibles: un material por cada PNG.")]
    [SerializeField] private Material[] options;
    [Tooltip("Peso de cada cartel (mismo orden que Options). Vacio = todos igual de probables.")]
    [SerializeField] private float[] weights;

    public override int Count => options == null ? 0 : options.Length;

    /// <summary>Lo usa la herramienta de editor al armar el cartel.</summary>
    public void Setup(Renderer[] newFaces, Material[] newOptions, float[] newWeights)
    {
        faces = newFaces;
        options = newOptions;
        weights = newWeights;
    }

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
        if (weights == null || weights.Length != Count) return base.PickIndex(rng);
        float total = 0f;
        foreach (float w in weights) total += w;
        double r = rng.NextDouble() * total;
        for (int i = 0; i < Count; i++)
        {
            r -= weights[i];
            if (r < 0) return i;
        }
        return Count - 1;
    }
}
