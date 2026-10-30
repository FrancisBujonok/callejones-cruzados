using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LUGARES QUE SE RECUERDAN Y MIENTEN.
///
/// Hay pocos lugares especiales (maquina expendedora, altar, telefono publico) y cada uno existe
/// UNA sola vez. Aparecen poco, siempre adelante y fuera de la vista, en un pedazo de pared de
/// un recto o en la pared del fondo de una esquina o de un callejon sin salida. Sirven para
/// orientarse: "ya pase por la maquina".
///
/// La mentira: cuando el jugador vio un lugar de cerca y lo dejo atras, a veces el lugar se va
/// a escondidas (en un punto ciego) y vuelve a aparecer ADELANTE, en el proximo tramo que se arme.
/// El jugador lo encuentra otra vez y no sabe si dio la vuelta. Para el resto del juego es un
/// cambio mas: cuando lo ve, sube el miedo.
///
/// Nunca se pone un lugar en las piezas de un bucle: las copias tienen que ser identicas y cada
/// lugar existe una sola vez.
///
/// Va en el objeto "Laberinto", junto a CorridorTreadmill. Los lugares viven en Laberinto/Lugares.
/// </summary>
public class LandmarkDirector : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Si se deja vacio, usa el de este mismo objeto.")]
    [SerializeField] private CorridorTreadmill paths;
    [Tooltip("La camara del jugador. Si se deja vacia, usa la Main Camera.")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Los lugares. Cada uno es unico: solo puede estar en un sitio a la vez.")]
    [SerializeField] private Landmark[] landmarks;

    [Header("Cuando aparecen")]
    [Tooltip("Metros caminados antes del primer lugar.")]
    [SerializeField] private float firstAfter = 25f;
    [Tooltip("Metros caminados minimos entre un lugar y el siguiente.")]
    [SerializeField] private float minGap = 45f;
    [Tooltip("Pasada esa distancia, probabilidad de que cada pieza nueva adelante traiga un lugar.")]
    [SerializeField, Range(0f, 1f)] private float chancePerPiece = 0.15f;

    [Header("Mentiras")]
    [Tooltip("Probabilidad de que un lugar que el jugador vio y dejo atras reaparezca adelante.")]
    [SerializeField, Range(0f, 1f)] private float lieChance = 0.5f;
    [Tooltip("Cuantas veces seguidas puede reaparecer el mismo lugar.")]
    [SerializeField] private int maxLiesInARow = 2;
    [Tooltip("A menos de esta distancia, en pantalla y sin paredes en el medio, el jugador lo vio.")]
    [SerializeField] private float seeDistance = 9f;
    [Tooltip("Metros por el camino: mas lejos que esto (y en punto ciego) el lugar quedo atras. " +
             "Las piezas nuevas mas cerca que esto no reciben lugares.")]
    [SerializeField] private float leftBehindDistance = 14f;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;

    /// <summary>Cuantas veces un lugar reaparecio adelante.</summary>
    public int LiesTold { get; private set; }

    private Transform storage;
    private float walked;
    private float nextAt;
    private float lastPut = -1000f;
    private Vector3 lastPos;
    private readonly List<Landmark> stored = new List<Landmark>();

    private void Awake()
    {
        if (paths == null) paths = GetComponent<CorridorTreadmill>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (paths == null || playerCamera == null || landmarks == null || landmarks.Length == 0 || landmarks[0] == null)
        {
            Debug.LogWarning("[LandmarkDirector] Faltan referencias o lugares. Ejecuta Callejones > Fachadas y lugares.", this);
            enabled = false;
            return;
        }

        storage = landmarks[0].transform.parent;
        foreach (Landmark l in landmarks)
            if (l != null) l.gameObject.SetActive(false);
        nextAt = firstAfter;
    }

    // El generador avisa cuando pone o recicla una pieza.
    private void OnEnable()
    {
        if (paths == null) return;
        paths.PiecePlaced += OnPiecePlaced;
        paths.PieceRecycled += OnPieceRecycled;
    }

    private void OnDisable()
    {
        if (paths == null) return;
        paths.PiecePlaced -= OnPiecePlaced;
        paths.PieceRecycled -= OnPieceRecycled;
    }

    private void Start()
    {
        lastPos = paths.Player.position;
    }

    private void Update()
    {
        // Metros caminados en el plano. Un salto grande no cuenta.
        Vector3 d = paths.Player.position - lastPos;
        d.y = 0f;
        if (d.magnitude < 2f) walked += d.magnitude;
        lastPos = paths.Player.position;

        foreach (Landmark l in landmarks)
        {
            if (l == null || l.Slot == null) continue;

            // 1. Primero el jugador tiene que verlo de cerca: si no, no hay nada que recordar.
            if (!l.Seen)
            {
                if (CanSee(l)) l.Seen = true;
                continue;
            }

            // 2. Despues, cuando quedo atras y fuera de la vista, se decide una vez si miente.
            if (l.Decided) continue;
            if (paths.PathDistance(l.Host) < leftBehindDistance) continue;
            if (l.Host.State != SegmentVisibility.BlindSpot) continue;

            l.Decided = true;
            if (l.LiesInARow < maxLiesInARow && Random.value < lieChance)
            {
                l.Slot.Release(storage);
                l.Slot = null;
                l.Host = null;
                l.Pending = true;
                l.LiesInARow++;
                Debug.Log($"[LandmarkDirector] '{l.Label}' se fue a escondidas: va a aparecer adelante.");
            }
        }
    }

    /// <summary>Una pieza nueva del azar normal: puede recibir un lugar.</summary>
    private void OnPiecePlaced(CorridorSegment segment)
    {
        if (!paths.Ready || segment.Kind == SegmentKind.Salida) return;

        // Solo adelante y lejos: asi el lugar nunca aparece delante de los ojos del jugador.
        Vector3 toPiece = segment.transform.position - paths.Player.position;
        if (Vector3.Dot(toPiece, paths.ForwardAxis) < 6f || paths.PathDistance(segment) < leftBehindDistance) return;

        LandmarkSlot[] slots = segment.GetComponentsInChildren<LandmarkSlot>(true);
        if (slots.Length == 0) return;
        foreach (LandmarkSlot s in slots)
            if (s.Occupant != null) return; // un lugar por pieza

        // Primero, un lugar que se fue a escondidas y espera reaparecer.
        // Igual deja unos metros desde el ultimo lugar, para que dos no aparezcan pegados.
        Landmark pick = null;
        foreach (Landmark l in landmarks)
            if (l != null && l.Pending) { pick = l; break; }
        bool lie = pick != null;
        if (lie && walked < lastPut + minGap * 0.3f) return;

        // Si no, a veces uno nuevo.
        if (!lie)
        {
            if (walked < nextAt || Random.value > chancePerPiece) return;
            stored.Clear();
            foreach (Landmark l in landmarks)
                if (l != null && l.Slot == null && !l.Pending) stored.Add(l);
            if (stored.Count == 0) return;
            pick = stored[Random.Range(0, stored.Count)];
            pick.LiesInARow = 0;
        }

        LandmarkSlot slot = slots[Random.Range(0, slots.Length)];
        slot.Occupy(pick);
        pick.Slot = slot;
        pick.Host = segment;
        pick.Seen = false;
        pick.Decided = false;
        pick.Pending = false;
        nextAt = walked + minGap;
        lastPut = walked;

        if (lie)
        {
            LiesTold++;
            segment.MarkChanged(); // cuando lo vea, cuenta como un cambio descubierto
            Debug.Log($"[LandmarkDirector] '{pick.Label}' reaparecio adelante en '{segment.name}'.");
        }
        else
        {
            Debug.Log($"[LandmarkDirector] '{pick.Label}' aparece en '{segment.name}'.");
        }
    }

    /// <summary>Una pieza vuelve al deposito: si tenia un lugar, el lugar se guarda.</summary>
    private void OnPieceRecycled(CorridorSegment segment)
    {
        foreach (Landmark l in landmarks)
        {
            if (l == null || l.Host != segment) continue;
            l.Slot.Release(storage);
            l.Slot = null;
            l.Host = null;
            l.LiesInARow = 0;
        }
    }

    /// <summary>Cerca, dentro de la pantalla y sin nada en el medio.</summary>
    private bool CanSee(Landmark l)
    {
        Vector3 cam = playerCamera.transform.position;
        Vector3 c = l.Center;
        if ((c - cam).sqrMagnitude > seeDistance * seeDistance) return false;

        Vector3 vp = playerCamera.WorldToViewportPoint(c);
        if (vp.z <= 0f || vp.x < 0.1f || vp.x > 0.9f || vp.y < 0.05f || vp.y > 0.95f) return false;

        if (Physics.Linecast(cam, c, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform.IsChildOf(l.transform);
        return true;
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible) return;
        var text = new System.Text.StringBuilder("Lugares: ");
        foreach (Landmark l in landmarks)
        {
            if (l == null) continue;
            string state = l.Pending ? "esperando adelante"
                : l.Slot == null ? "guardado"
                : l.Seen ? "visto" : "puesto";
            text.Append(l.Label).Append(" (").Append(state).Append(")  ");
        }
        text.Append("| reaparecieron: ").Append(LiesTold);
        GUI.Label(new Rect(12, 188, 900, 22), text.ToString());
    }
}
