using UnityEngine;

/// <summary>
/// Paso 21: el "numero de edificio" de las ventanas que se prenden y se apagan.
///
/// Las mallas de los edificios son compartidas: todos los rectos con un lado de 7 pisos usan la
/// misma. Sin esto, dos edificios iguales a la vista prenderian y apagarian las mismas ventanas
/// en el mismo momento. Cada lado sortea un numero (de 16) y se lo pasa al shader con un
/// MaterialPropertyBlock (_CallejonesWindowSeed); el shader lo mezcla con el numero de cada
/// ventana.
///
/// Es un RandomPick: se vuelve a sortear cuando la pieza se reusa, y en un bucle todas las
/// copias usan el mismo numero (asi sus ventanas son iguales).
/// </summary>
public class WindowSeedPick : RandomPick
{
    private const int Seeds = 16;
    private static readonly int SeedId = Shader.PropertyToID("_CallejonesWindowSeed");

    [Tooltip("Las mallas de fachada y fondo del edificio (de todas sus alturas).")]
    [SerializeField] private Renderer[] renderers;

    private MaterialPropertyBlock block;

    public override int Count => Seeds;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Renderer[] newRenderers)
    {
        renderers = newRenderers;
    }

    public override void Show(int index)
    {
        index = Mathf.Clamp(index, 0, Seeds - 1);
        Current = index;
        if (renderers == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(SeedId, (index + 0.5f) / Seeds);
            r.SetPropertyBlock(block);
        }
    }
}
