using UnityEngine;

/// <summary>
/// Estado de visibilidad de un modulo. Lo calcula VisibilityChecker.
/// </summary>
public enum SegmentVisibility
{
    Unknown,    // todavia no se evaluo
    Visible,    // la camara del jugador lo puede ver
    BlindSpot,  // punto ciego: se puede cambiar sin que el jugador lo note
    Protected   // el jugador esta adentro o demasiado cerca: nunca se cambia
}

/// <summary>
/// Un modulo de pasillo. Guarda varias versiones de si mismo (variantes) como hijos
/// y solo deja encendida una a la vez. Cambiar el pasillo = cambiar de variante.
/// </summary>
public class CorridorSegment : MonoBehaviour
{
    [Header("Variantes (hijos de este modulo)")]
    [Tooltip("Cada elemento es un hijo con su propia geometria. Solo uno esta activo a la vez.")]
    [SerializeField] private GameObject[] variants;
    [Tooltip("Indice de la variante con la que arranca el modulo (0 = la primera).")]
    [SerializeField] private int startVariant = 0;

    [Header("Volumen del modulo (para calcular visibilidad)")]
    [Tooltip("Centro de la caja, relativo al modulo. Altura de pared 3 m -> centro en Y = 1.5.")]
    [SerializeField] private Vector3 boundsCenter = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Tamano de la caja: ancho, alto, largo del modulo en metros.")]
    [SerializeField] private Vector3 boundsSize = new Vector3(3f, 3f, 6f);

    public int CurrentVariant { get; private set; } = -1;
    public int VariantCount => variants == null ? 0 : variants.Length;

    /// <summary>Lo escribe VisibilityChecker; solo se usa para dibujar el gizmo.</summary>
    public SegmentVisibility DebugState { get; set; } = SegmentVisibility.Unknown;

    private void Awake()
    {
        SetVariant(startVariant);
    }

    /// <summary>Enciende la variante indicada y apaga todas las demas.</summary>
    public void SetVariant(int index)
    {
        if (VariantCount == 0)
        {
            Debug.LogWarning($"[CorridorSegment] '{name}' no tiene variantes asignadas.", this);
            return;
        }

        index = Mathf.Clamp(index, 0, VariantCount - 1);
        for (int i = 0; i < VariantCount; i++)
        {
            if (variants[i] != null) variants[i].SetActive(i == index);
        }
        CurrentVariant = index;
    }

    /// <summary>
    /// Pasa a la siguiente variante. Aparece en el menu de tres puntos del componente
    /// en el Inspector, para probar a mano.
    /// </summary>
    [ContextMenu("Siguiente variante")]
    public void NextVariant()
    {
        if (VariantCount == 0) return;
        int current = FindActiveVariant(); // -1 si ninguna esta activa -> pasa a 0
        SetVariant((current + 1) % VariantCount);
    }

    private int FindActiveVariant()
    {
        for (int i = 0; i < VariantCount; i++)
        {
            if (variants[i] != null && variants[i].activeSelf) return i;
        }
        return -1;
    }

    /// <summary>
    /// Caja alineada a los ejes del mundo (AABB) que envuelve el modulo.
    /// Transforma las 8 esquinas de la caja local, asi sirve aunque el modulo este rotado.
    /// </summary>
    public Bounds GetWorldBounds()
    {
        Vector3 e = boundsSize * 0.5f;
        Bounds world = new Bounds(transform.TransformPoint(boundsCenter), Vector3.zero);

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 localCorner = boundsCenter + new Vector3(e.x * x, e.y * y, e.z * z);
            world.Encapsulate(transform.TransformPoint(localCorner));
        }
        return world;
    }

    // Dibuja la caja en la vista Scene: verde = visible, rojo = punto ciego,
    // amarillo = protegido, blanco = sin evaluar.
    private void OnDrawGizmos()
    {
        Bounds b = GetWorldBounds();

        Gizmos.color = DebugState switch
        {
            SegmentVisibility.Visible => Color.green,
            SegmentVisibility.BlindSpot => Color.red,
            SegmentVisibility.Protected => Color.yellow,
            _ => Color.white
        };
        Gizmos.DrawWireCube(b.center, b.size);

        if (DebugState == SegmentVisibility.BlindSpot)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawCube(b.center, b.size);
        }
    }
}
