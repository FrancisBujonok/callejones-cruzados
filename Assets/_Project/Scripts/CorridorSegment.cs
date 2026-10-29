using UnityEngine;

/// <summary>
/// Estado de visibilidad de un modulo. Lo calcula VisibilityChecker cada frame.
/// </summary>
public enum SegmentVisibility
{
    Unknown,    // todavia no se evaluo
    Visible,    // la camara del jugador lo puede ver (con margen de seguridad)
    BlindSpot,  // punto ciego: se puede cambiar sin que el jugador lo note
    Protected   // el jugador esta adentro o demasiado cerca: nunca se cambia
}

/// <summary>Que forma tiene la pieza. El generador de caminos elige entre estas.</summary>
public enum SegmentKind
{
    Recto,    // 6 m de pasillo recto
    Esquina,  // giro de 90 grados (3 x 3 m)
    Cruce,    // bifurcacion en T (3 x 3 m)
    Fondo,    // callejon sin salida (3 x 3 m)
    Salida    // el modulo del torii
}

/// <summary>
/// Una pieza del laberinto. Guarda varias versiones de si misma (variantes) como hijos
/// y solo deja encendida una a la vez. Cambiar el pasillo = cambiar de variante.
/// Ademas recuerda si cambio y el jugador todavia no lo vio (HasUnseenChange).
///
/// Conexiones: cada pieza tiene puntos de union ("Conexion_..."), objetos vacios en el borde
/// de la pieza cuyo eje Z (azul) apunta hacia afuera. Dos piezas se unen haciendo coincidir
/// una conexion de cada una, mirandose de frente. Asi el generador puede armar caminos con
/// giros y cruces sin saber nada de la forma de cada pieza.
/// </summary>
public class CorridorSegment : MonoBehaviour
{
    [Header("Variantes (hijos de este modulo)")]
    [Tooltip("Cada elemento es un hijo con su propia geometria. Solo uno esta activo a la vez.")]
    [SerializeField] private GameObject[] variants;
    [Tooltip("Indice de la variante con la que arranca el modulo (0 = la primera).")]
    [SerializeField] private int startVariant = 0;
    [Tooltip("Indice de la variante que corta el paso (la pared en el medio). -1 = ninguna.")]
    [SerializeField] private int blockingVariant = -1;

    [Header("Forma y conexiones")]
    [SerializeField] private SegmentKind kind = SegmentKind.Recto;
    [Tooltip("Puntos de union con otras piezas. Su eje Z (azul) apunta hacia afuera de la pieza.")]
    [SerializeField] private Transform[] sockets;

    [Header("Volumen del modulo (para calcular visibilidad)")]
    [Tooltip("Centro de la caja, relativo al modulo. Altura de pared 3 m -> centro en Y = 1.5.")]
    [SerializeField] private Vector3 boundsCenter = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Tamano de la caja: ancho, alto, largo del modulo en metros.")]
    [SerializeField] private Vector3 boundsSize = new Vector3(3f, 3f, 6f);

    public int CurrentVariant { get; private set; } = -1;
    public int VariantCount => variants == null ? 0 : variants.Length;
    public SegmentKind Kind => kind;
    public int SocketCount => sockets == null ? 0 : sockets.Length;
    public Transform GetSocket(int index) => sockets[index];

    /// <summary>true si la variante actual corta el paso.</summary>
    public bool IsBlocking => blockingVariant >= 0 && CurrentVariant == blockingVariant;
    /// <summary>true si pasar a esa variante cortaria el paso.</summary>
    public bool WouldBlock(int variant) => blockingVariant >= 0 && variant == blockingVariant;

    /// <summary>Largo del modulo en metros, a lo largo de su eje Z.</summary>
    public float Length => boundsSize.z * transform.lossyScale.z;

    /// <summary>
    /// Cuantas celdas de 3 x 3 m ocupa a lo largo de su eje Z (Recto 2, Esquina 1...).
    /// El generador usa una grilla de celdas para no encimar piezas.
    /// </summary>
    public int CellCount => Mathf.Max(1, Mathf.RoundToInt(boundsSize.z / 3f));

    /// <summary>Centro de la celda i, en coordenadas locales de la pieza.</summary>
    public Vector3 LocalCellCenter(int i) =>
        new Vector3(boundsCenter.x, 0f, boundsCenter.z - boundsSize.z * 0.5f + 1.5f + 3f * i);

    /// <summary>Estado de visibilidad del ultimo frame. Lo escribe VisibilityChecker.</summary>
    public SegmentVisibility State { get; set; } = SegmentVisibility.Unknown;

    /// <summary>true si el modulo cambio y el jugador todavia no lo vio.</summary>
    public bool HasUnseenChange { get; private set; }

    /// <summary>
    /// true mientras la pieza es parte de un bucle (un tramo que se repite): MazeDirector no la
    /// cambia, porque todas las copias tienen que verse identicas. Lo maneja CorridorTreadmill.
    /// </summary>
    public bool Locked { get; set; }

    private void Awake()
    {
        SetVariant(startVariant);
    }

    /// <summary>Enciende la variante indicada y apaga todas las demas.</summary>
    public void SetVariant(int index)
    {
        if (VariantCount == 0) return; // piezas sin variantes (la salida, por ejemplo)

        index = Mathf.Clamp(index, 0, VariantCount - 1);
        for (int i = 0; i < VariantCount; i++)
        {
            if (variants[i] != null) variants[i].SetActive(i == index);
        }
        CurrentVariant = index;
    }

    /// <summary>Vuelve a la variante inicial (la abierta).</summary>
    public void SetStartVariant() => SetVariant(startVariant);

    /// <summary>Marca que el modulo cambio a espaldas del jugador.</summary>
    public void MarkChanged() => HasUnseenChange = true;

    /// <summary>El jugador ya vio el cambio.</summary>
    public void MarkDiscovered() => HasUnseenChange = false;

    /// <summary>
    /// Deja el modulo como nuevo antes de volver a usarlo:
    /// variante inicial, sin cambios pendientes y sin estado de visibilidad.
    /// </summary>
    public void ResetForReuse()
    {
        SetVariant(startVariant);
        HasUnseenChange = false;
        Locked = false;
        State = SegmentVisibility.Unknown;

        // Nueva decoracion al azar: el tramo que aparece adelante no es igual al que quedo atras.
        foreach (DecorRandomizer decor in GetComponentsInChildren<DecorRandomizer>(true))
            decor.Randomize();

        // Sin anomalias de un bucle anterior: farol prendido y persiana bajada.
        foreach (SegmentAnomalies anomalies in GetComponentsInChildren<SegmentAnomalies>(true))
            anomalies.Clear();
    }

    /// <summary>
    /// Pasa a la siguiente variante y lo marca como cambio sin ver.
    /// Aparece en el menu de tres puntos del componente, para probar a mano.
    /// </summary>
    [ContextMenu("Siguiente variante")]
    public void NextVariant()
    {
        if (VariantCount == 0) return;
        int current = FindActiveVariant(); // -1 si ninguna esta activa -> pasa a 0
        SetVariant((current + 1) % VariantCount);
        if (Application.isPlaying) MarkChanged();
    }

    private int FindActiveVariant()
    {
        for (int i = 0; i < VariantCount; i++)
        {
            if (variants[i] != null && variants[i].activeSelf) return i;
        }
        return -1;
    }

    /// <summary>Lo usa la herramienta de editor al armar las piezas.</summary>
    public void Setup(SegmentKind newKind, Transform[] newSockets, GameObject[] newVariants,
        int newBlockingVariant, Vector3 size)
    {
        kind = newKind;
        sockets = newSockets;
        variants = newVariants;
        blockingVariant = newBlockingVariant;
        startVariant = 0;
        boundsCenter = new Vector3(0f, size.y * 0.5f, 0f);
        boundsSize = size;
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

    // Vista Scene: verde = visible, rojo = punto ciego, amarillo = protegido,
    // blanco = sin evaluar. Esfera magenta = cambio que el jugador todavia no vio.
    // Flechas cian = conexiones (hacia donde se une la proxima pieza).
    private void OnDrawGizmos()
    {
        Bounds b = GetWorldBounds();

        Gizmos.color = State switch
        {
            SegmentVisibility.Visible => Color.green,
            SegmentVisibility.BlindSpot => Color.red,
            SegmentVisibility.Protected => Color.yellow,
            _ => Color.white
        };
        Gizmos.DrawWireCube(b.center, b.size);

        if (State == SegmentVisibility.BlindSpot)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawCube(b.center, b.size);
        }

        if (HasUnseenChange)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(b.center, 0.75f);
        }

        if (sockets == null) return;
        Gizmos.color = Color.cyan;
        foreach (Transform s in sockets)
        {
            if (s == null) continue;
            Vector3 p = s.position + Vector3.up * 0.2f;
            Gizmos.DrawLine(p, p + s.forward * 0.8f);
            Gizmos.DrawWireSphere(p, 0.12f);
        }
    }
}
