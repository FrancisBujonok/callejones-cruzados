using UnityEngine;

/// <summary>
/// Un lugar que se recuerda: una maquina expendedora, un altar, un telefono publico.
/// Hay UNA sola copia de cada uno, asi que solo puede estar en un sitio a la vez.
/// Lo maneja LandmarkDirector; este componente solo guarda su nombre y en que anda.
///
/// Convencion (igual que las persianas): mira hacia +X, la espalda contra la pared en x = 0,
/// centrado en z = 0 y con el piso en y = 0.
/// </summary>
public class Landmark : MonoBehaviour
{
    [Tooltip("Nombre para el debug (F1).")]
    [SerializeField] private string label = "Lugar";
    [Tooltip("Centro aproximado, relativo al lugar: ahi apunta la linea de vision para saber si el jugador lo vio.")]
    [SerializeField] private Vector3 center = new Vector3(0.35f, 1f, 0f);

    public string Label => label;
    public Vector3 Center => transform.TransformPoint(center);

    /// <summary>Donde esta puesto (null = guardado).</summary>
    public LandmarkSlot Slot { get; set; }
    /// <summary>La pieza donde esta puesto.</summary>
    public CorridorSegment Host { get; set; }
    /// <summary>El jugador lo vio de cerca desde que se puso aca.</summary>
    public bool Seen { get; set; }
    /// <summary>Ya se decidio si esta vez miente o se queda.</summary>
    public bool Decided { get; set; }
    /// <summary>Se fue a escondidas y espera un tramo nuevo adelante para reaparecer.</summary>
    public bool Pending { get; set; }
    /// <summary>Cuantas veces seguidas reaparecio adelante.</summary>
    public int LiesInARow { get; set; }

    /// <summary>Lo usa la herramienta de editor al armar el prefab.</summary>
    public void Setup(string newLabel, Vector3 newCenter)
    {
        label = newLabel;
        center = newCenter;
    }
}
