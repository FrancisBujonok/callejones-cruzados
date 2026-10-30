using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Generador de caminos (antes era una "cinta" recta).
///
/// IDEA: el laberinto no existe entero. Solo existe lo que esta cerca del jugador, medido
/// SIGUIENDO EL CAMINO (no en linea recta). Cada frame:
///   1. Se averigua en que pieza esta el jugador.
///   2. Se calcula la distancia por el camino desde esa pieza a todas las demas.
///   3. RELLENAR: toda conexion libre a menos de buildRadius recibe una pieza nueva,
///      elegida al azar con reglas (recto, esquina, cruce, callejon sin salida).
///   4. RECICLAR: las piezas "hoja" (unidas a una sola) a mas de keepRadius y en punto
///      ciego vuelven al deposito, listas para usarse en otro lado.
///
/// Las piezas se unen por sus conexiones (ver CorridorSegment). Para no encimar dos piezas,
/// el piso se divide en una grilla de celdas de 3 x 3 m: cada pieza ocupa 1 o 2 celdas
/// y una pieza nueva solo se pone si sus celdas estan libres.
///
/// Reglas para que el azar se sienta natural y nunca rompa el juego:
/// - Nunca se genera un camino que vuelva hacia atras (respecto del "adelante" del Laberinto):
///   asi un camino no puede dar la vuelta y chocarse consigo mismo.
/// - Despues de cada giro o cruce hay entre minStraight y maxStraight rectos (al azar).
/// - Un lado de un cruce puede ser un callejon sin salida, pero nunca los dos.
/// - Hacia atras desde el comienzo hay un callejon sin salida.
/// - Si una conexion no tiene lugar para ninguna pieza, se tapa con una persiana.
/// - El director nunca cierra un pasillo que deje al jugador sin salida, y si igual pasara,
///   este script abre uno cerrado a escondidas (cuenta como un cambio mas).
///
/// La salida (el torii): cuando ExitCondition la pide, se pone en la proxima conexion que se
/// rellene adelante del jugador. Si el jugador se aleja por otro camino, el torii se recicla
/// y vuelve a aparecer mas adelante: la salida "lo sigue".
///
/// Los bucles (un mismo tramo que se repite varias veces seguidas) estan en
/// CorridorTreadmill.Loops.cs: es la misma clase, partida en dos archivos (partial).
///
/// Va en el objeto "Laberinto"; todas las piezas son hijas de Laberinto (el deposito).
/// </summary>
public partial class CorridorTreadmill : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El objeto Player.")]
    [SerializeField] private Transform player;
    [Tooltip("Si se deja vacio, usa el VisibilityChecker de este mismo objeto.")]
    [SerializeField] private VisibilityChecker visibility;
    [Tooltip("Modulo de salida (con CorridorSegment de tipo Salida). Va FUERA de Laberinto.")]
    [SerializeField] private GameObject exitModule;
    [Tooltip("Persiana que tapa una conexion cuando no entra ninguna pieza.")]
    [SerializeField] private GameObject capPrefab;

    [Header("Distancias por el camino (metros)")]
    [Tooltip("Todo lo que este a menos de esta distancia del jugador tiene que existir.")]
    [SerializeField] private float buildRadius = 22f;
    [Tooltip("Las piezas a mas de esta distancia (y fuera de la vista) se reciclan.")]
    [SerializeField] private float keepRadius = 32f;
    [Tooltip("Piezas que se pueden poner o quitar por frame (evita tirones).")]
    [SerializeField] private int maxChangesPerFrame = 2;
    [Tooltip("Una conexion libre mas cerca que esto se rellena aunque el jugador la este mirando.")]
    [SerializeField] private float urgentDistance = 14f;

    [Header("Azar")]
    [Tooltip("Peso de seguir derecho. Mas alto = pasillos mas largos.")]
    [SerializeField] private float straightWeight = 5f;
    [Tooltip("Peso de girar (izquierda o derecha).")]
    [SerializeField] private float turnWeight = 3f;
    [Tooltip("Peso de una bifurcacion en T.")]
    [SerializeField] private float crossWeight = 2f;
    [Tooltip("Probabilidad de que un lado de un cruce sea un callejon sin salida.")]
    [SerializeField, Range(0f, 1f)] private float deadEndChance = 0.35f;
    [Tooltip("Rectos minimos despues de un giro o cruce.")]
    [SerializeField] private int minStraight = 1;
    [Tooltip("Rectos maximos obligados despues de un giro o cruce.")]
    [SerializeField] private int maxStraight = 2;
    [Tooltip("Rectos seguros al empezar, para la vista del titulo.")]
    [SerializeField] private int straightAtStart = 3;
    [Tooltip("0 = distinto en cada partida. Otro numero = siempre el mismo laberinto (sirve para probar).")]
    [SerializeField] private int seed = 0;

    [Header("Eventos")]
    [Tooltip("Cada vez que una pieza vuelve al deposito.")]
    public UnityEvent onSegmentRecycled;
    [Tooltip("Cuando la salida aparece en el camino.")]
    public UnityEvent onExitPlaced;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;

    public bool ExitRequested { get; private set; }
    public bool ExitPlaced => exitNode != null;
    public int RecycledCount { get; private set; }
    public CorridorSegment CurrentSegment => current != null ? current.seg : null;

    /// <summary>true cuando ya se armo el laberinto inicial.</summary>
    public bool Ready => ready;
    public Transform Player => player;
    /// <summary>El "adelante" del laberinto, en el mundo.</summary>
    public Vector3 ForwardAxis => forwardAxis;

    /// <summary>
    /// Se dispara cuando el azar normal pone una pieza nueva (no las de un bucle ni la salida).
    /// La usa LandmarkDirector para decidir si ahi va un lugar.
    /// </summary>
    public event System.Action<CorridorSegment> PiecePlaced;
    /// <summary>Se dispara justo antes de que una pieza vuelva al deposito.</summary>
    public event System.Action<CorridorSegment> PieceRecycled;

    /// <summary>Metros por el camino desde la pieza del jugador hasta esa pieza (infinito si no esta puesta).</summary>
    public float PathDistance(CorridorSegment segment) =>
        segment != null && nodes.TryGetValue(segment, out Node node) ? node.dist : float.PositiveInfinity;

    // ------------------------------------------------------------------ datos internos

    /// <summary>Una pieza puesta en el camino y con quien esta unida.</summary>
    private class Node
    {
        public CorridorSegment seg;
        public Node[] links;          // por conexion: la pieza unida (null = libre)
        public GameObject[] caps;     // por conexion: persiana que la tapa (null = sin tapar)
        public readonly List<Vector2Int> cells = new List<Vector2Int>();
        public int run;               // rectos seguidos desde el ultimo giro o cruce
        public int straightNeeded;    // rectos obligados antes del proximo giro
        public bool hasDeadEnd;       // cruces: ya tiene un lado sin salida
        public float dist;            // distancia por el camino hasta el jugador

        // Si la pieza es parte de un bucle: cual, en que copia y que paso de la plantilla.
        public LoopPlan loop;
        public int loopCopy = -1;
        public int loopStep = -1;
    }

    private struct Option
    {
        public SegmentKind kind;
        public int via;        // por cual de SUS conexiones se engancha la pieza nueva
        public float weight;
    }

    private const float CellSize = 3f;

    private readonly Dictionary<SegmentKind, List<CorridorSegment>> pool = new Dictionary<SegmentKind, List<CorridorSegment>>();
    private readonly Dictionary<SegmentKind, CorridorSegment> prototypes = new Dictionary<SegmentKind, CorridorSegment>();
    private readonly Dictionary<CorridorSegment, Node> nodes = new Dictionary<CorridorSegment, Node>();
    private readonly Dictionary<Vector2Int, Node> occupied = new Dictionary<Vector2Int, Node>();
    private readonly List<GameObject> capPool = new List<GameObject>();
    private readonly HashSet<long> deferred = new HashSet<long>();
    private readonly List<Option> options = new List<Option>();

    private System.Random rng;
    private Vector3 gridOrigin;   // centro de la primera celda, en coordenadas de Laberinto
    private Vector3 forwardAxis;  // "adelante" del laberinto, en el mundo
    private Node current;
    private Node exitNode;
    private CorridorSegment exitSegment;
    private float escapeTimer;
    private bool ready;

    // ------------------------------------------------------------------ inicio

    // Start y no Awake: asi VisibilityChecker ya lleno su lista de modulos en su Awake.
    private void Start()
    {
        if (visibility == null) visibility = GetComponent<VisibilityChecker>();
        if (player == null || visibility == null)
        {
            Debug.LogError("[CorridorTreadmill] Falta asignar Player o Visibility en el Inspector.", this);
            enabled = false;
            return;
        }

        rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);
        forwardAxis = transform.forward;
        forwardAxis.y = 0f;
        forwardAxis.Normalize();
        InitLoops();

        // 1. Todas las piezas hijas van al deposito, apagadas.
        foreach (CorridorSegment s in GetComponentsInChildren<CorridorSegment>(true))
        {
            if (s.SocketCount == 0) continue;
            if (!prototypes.ContainsKey(s.Kind)) prototypes[s.Kind] = s;
            ReturnToPool(s);
        }

        if (exitModule != null)
        {
            exitSegment = exitModule.GetComponent<CorridorSegment>();
            exitModule.SetActive(false);
            if (exitSegment == null)
                Debug.LogWarning("[CorridorTreadmill] El modulo de salida no tiene CorridorSegment. Ejecuta Callejones > Laberinto con giros.", this);
        }

        if (!pool.ContainsKey(SegmentKind.Recto) || pool[SegmentKind.Recto].Count == 0)
        {
            Debug.LogError("[CorridorTreadmill] No hay piezas Recto con conexiones. Ejecuta Callejones > Laberinto con giros.", this);
            enabled = false;
            return;
        }

        // 2. Primera pieza: un recto debajo del jugador, mirando hacia adelante.
        CorridorSegment first = Take(SegmentKind.Recto);
        Vector3 p = player.position;
        first.transform.SetPositionAndRotation(
            new Vector3(p.x, transform.position.y, p.z), Quaternion.LookRotation(forwardAxis));
        gridOrigin = transform.InverseTransformPoint(first.transform.TransformPoint(first.LocalCellCenter(0)));
        current = Activate(first, null, -1, -1);
        current.run = 0;
        current.straightNeeded = straightAtStart;

        // 3. Hacia atras: un recto y un callejon sin salida.
        int back = BackwardSocket(current);
        if (back >= 0)
        {
            Node behind = PlaceKind(current, back, SegmentKind.Recto, 0);
            if (behind != null)
            {
                int far = FirstFreeSocket(behind);
                if (far >= 0) PlaceKind(behind, far, SegmentKind.Fondo, 0);
            }
        }

        // 4. Todo lo que tiene que existir alrededor, de una vez (antes del primer frame).
        ComputeDistances();
        for (int i = 0; i < 400 && FillStep(true); i++) ComputeDistances();

        ready = true;
        Debug.Log($"[CorridorTreadmill] Laberinto listo: {nodes.Count} piezas activas.");
    }

    // ------------------------------------------------------------------ cada frame

    private void Update()
    {
        if (!ready) return;

        UpdateCurrent();
        UpdateLoops();
        ComputeDistances();
        deferred.Clear();

        // Primero rellenar (un hueco a la vista es peor que una pieza de mas)...
        int changes = 0;
        while (changes < maxChangesPerFrame && FillStep(false))
        {
            changes++;
            ComputeDistances();
        }

        // ...y despues reciclar lo que quedo lejos.
        if (changes < maxChangesPerFrame) RecycleFar(maxChangesPerFrame - changes);

        // Cada tanto, comprobar que el jugador siempre tenga por donde seguir.
        escapeTimer -= Time.deltaTime;
        if (escapeTimer <= 0f)
        {
            escapeTimer = 1f;
            if (!HasEscape(null)) OpenHiddenBlock();
        }
    }

    /// <summary>La pieza donde esta el jugador: la que ocupa su celda.</summary>
    private void UpdateCurrent()
    {
        if (occupied.TryGetValue(CellOf(player.position), out Node node)) current = node;
    }

    /// <summary>
    /// Distancia por el camino desde la pieza del jugador a todas las demas (algoritmo de
    /// Dijkstra simplificado: son pocas piezas). Dos piezas unidas estan a la distancia entre
    /// sus centros, porque la conexion queda justo en la linea que los une.
    /// </summary>
    private void ComputeDistances()
    {
        foreach (Node n in nodes.Values) n.dist = float.PositiveInfinity;
        if (current == null) return;

        current.dist = 0f;
        var open = new List<Node> { current };
        while (open.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < open.Count; i++) if (open[i].dist < open[best].dist) best = i;
            Node n = open[best];
            open.RemoveAt(best);

            foreach (Node next in n.links)
            {
                if (next == null) continue;
                float d = n.dist + Vector3.Distance(n.seg.transform.position, next.seg.transform.position);
                if (d < next.dist)
                {
                    next.dist = d;
                    open.Add(next);
                }
            }
        }
    }

    // ------------------------------------------------------------------ rellenar

    /// <summary>
    /// Busca la conexion libre mas cercana (dentro de buildRadius) y le pone una pieza.
    /// Devuelve false si no hay nada para rellenar.
    /// </summary>
    private bool FillStep(bool initial)
    {
        Node bestNode = null;
        int bestSocket = -1;
        float bestDist = buildRadius;

        foreach (Node n in nodes.Values)
        {
            if (float.IsInfinity(n.dist)) continue;
            for (int i = 0; i < n.links.Length; i++)
            {
                if (n.links[i] != null || n.caps[i] != null) continue;
                if (deferred.Contains(Key(n, i))) continue;
                float d = n.dist + n.seg.GetSocket(i).localPosition.magnitude;
                if (d <= bestDist)
                {
                    bestDist = d;
                    bestNode = n;
                    bestSocket = i;
                }
            }
        }
        if (bestNode == null) return false;

        // Si la pieza nueva se veria aparecer, se espera a que el jugador mire a otro lado.
        // Excepcion: si la conexion ya esta muy cerca, se pone igual. Que algo aparezca de golpe
        // es raro, pero un agujero al vacio delante del jugador es mucho peor.
        bool urgent = bestDist < urgentDistance;

        // Si esta conexion le toca a un bucle, la pieza no se sortea: es la que dice la plantilla.
        if (FillLoopSocket(bestNode, bestSocket, initial, urgent)) return true;

        BuildOptions(bestNode, bestSocket);
        foreach (Option o in options)
        {
            CorridorSegment proto = o.kind == SegmentKind.Salida ? exitSegment : Prototype(o.kind);
            if (proto == null) continue;
            Pose(proto, o.via, bestNode.seg.GetSocket(bestSocket), out Vector3 pos, out Quaternion rot);
            if (!CellsFree(proto, pos, rot)) continue;

            if (!initial && !urgent && visibility.CouldBeSeen(PoseBounds(proto, pos, rot), RenderSettings.fogEndDistance + 2f))
            {
                deferred.Add(Key(bestNode, bestSocket));
                return true; // cuenta como trabajo hecho: se sigue con otra conexion
            }

            Node placed = o.kind == SegmentKind.Salida
                ? PlaceExit(bestNode, bestSocket, o.via, pos, rot)
                : Place(bestNode, bestSocket, Take(o.kind), o.via, pos, rot);
            if (placed != null)
            {
                if (o.kind != SegmentKind.Salida) PiecePlaced?.Invoke(placed.seg);
                return true;
            }
        }

        // Nada entra (otra pieza ocupa ese lugar): se tapa con una persiana.
        Cap(bestNode, bestSocket);
        return true;
    }

    /// <summary>
    /// Arma la lista de piezas posibles para una conexion, ya mezclada al azar segun los pesos.
    /// El orden importa: se prueba la primera, si no entra la segunda, y asi.
    /// </summary>
    private void BuildOptions(Node parent, int socket)
    {
        options.Clear();
        Transform s = parent.seg.GetSocket(socket);
        bool backward = Vector3.Dot(s.forward, forwardAxis) < -0.5f;

        // Hacia atras solo hay callejones sin salida.
        if (backward)
        {
            AddIfValid(SegmentKind.Fondo, 0, 1f, s);
            return;
        }

        // La salida, si fue pedida y esta conexion queda bien adelante del jugador.
        if (ExitRequested && exitNode == null && exitSegment != null && IsAhead(parent, socket))
            AddIfValid(SegmentKind.Salida, 0, 1000f, s);

        // Un lado de un cruce puede ser un callejon sin salida (nunca los dos).
        if (parent.seg.Kind == SegmentKind.Cruce && !parent.hasDeadEnd && rng.NextDouble() < deadEndChance)
            AddIfValid(SegmentKind.Fondo, 0, 500f, s);

        // Si todavia faltan rectos obligados, el recto tiene un peso enorme. Los giros y cruces
        // se agregan igual con peso minimo: son el plan B si el recto no entra.
        bool mustGoStraight = parent.run < parent.straightNeeded;
        float t = mustGoStraight ? 0.01f : 1f;
        AddIfValid(SegmentKind.Recto, 0, mustGoStraight ? 100f : straightWeight, s);
        // Esquina por una conexion u otra = girar a la derecha o a la izquierda.
        AddIfValid(SegmentKind.Esquina, 0, turnWeight * 0.5f * t, s);
        AddIfValid(SegmentKind.Esquina, 1, turnWeight * 0.5f * t, s);
        // Cruce por su conexion 0 = T (izquierda y derecha); por 1 o 2 = seguir y doblar.
        AddIfValid(SegmentKind.Cruce, 0, crossWeight / 3f * t, s);
        AddIfValid(SegmentKind.Cruce, 1, crossWeight / 3f * t, s);
        AddIfValid(SegmentKind.Cruce, 2, crossWeight / 3f * t, s);
        // Ultimo recurso antes de tapar: un callejon sin salida.
        AddIfValid(SegmentKind.Fondo, 0, 0.0001f, s);

        // Mezcla con pesos: a cada opcion se le asigna -log(u)/peso y se ordena de menor a mayor.
        // Las de mas peso tienden a quedar primeras, pero cualquiera puede ganar.
        var keys = new Dictionary<int, double>();
        for (int i = 0; i < options.Count; i++)
            keys[i] = -System.Math.Log(1.0 - rng.NextDouble()) / options[i].weight;
        var order = new List<int>(keys.Keys);
        order.Sort((a, b) => keys[a].CompareTo(keys[b]));
        var sorted = new List<Option>();
        foreach (int i in order) sorted.Add(options[i]);
        options.Clear();
        options.AddRange(sorted);
    }

    /// <summary>Agrega la opcion si hay piezas de ese tipo y ninguna de sus salidas mira hacia atras.</summary>
    private void AddIfValid(SegmentKind kind, int via, float weight, Transform target)
    {
        CorridorSegment proto = kind == SegmentKind.Salida ? exitSegment : Prototype(kind);
        if (proto == null || via >= proto.SocketCount) return;
        if (kind != SegmentKind.Salida && (!pool.TryGetValue(kind, out var list) || list.Count == 0)) return;

        Pose(proto, via, target, out _, out Quaternion rot);
        for (int i = 0; i < proto.SocketCount; i++)
        {
            if (i == via) continue;
            Vector3 dir = rot * (proto.GetSocket(i).localRotation * Vector3.forward);
            if (Vector3.Dot(dir, forwardAxis) < -0.5f) return; // tendria una salida hacia atras
        }
        options.Add(new Option { kind = kind, via = via, weight = weight });
    }

    private bool IsAhead(Node parent, int socket)
    {
        Vector3 sp = parent.seg.GetSocket(socket).position;
        float along = Vector3.Dot(sp - player.position, forwardAxis);
        float pathDist = parent.dist + parent.seg.GetSocket(socket).localPosition.magnitude;
        return along > 6f && pathDist > 12f;
    }

    // ------------------------------------------------------------------ poner y quitar piezas

    /// <summary>
    /// Donde y como girada tiene que quedar una pieza para que su conexion 'via'
    /// coincida con la conexion 'target', mirandose de frente.
    /// </summary>
    private static void Pose(CorridorSegment proto, int via, Transform target, out Vector3 pos, out Quaternion rot) =>
        PoseAt(proto, via, target.position, target.forward, out pos, out rot);

    /// <summary>Lo mismo, pero con una conexion "imaginaria" (posicion y hacia donde mira).</summary>
    private static void PoseAt(CorridorSegment proto, int via, Vector3 targetPos, Vector3 targetForward,
        out Vector3 pos, out Quaternion rot)
    {
        Transform s = proto.GetSocket(via);
        Vector3 localFwd = s.localRotation * Vector3.forward;
        Vector3 want = -targetForward;
        float yaw = Mathf.Atan2(want.x, want.z) * Mathf.Rad2Deg - Mathf.Atan2(localFwd.x, localFwd.z) * Mathf.Rad2Deg;
        yaw = Mathf.Round(yaw / 90f) * 90f; // siempre angulos rectos: evita errores de redondeo
        rot = Quaternion.Euler(0f, yaw, 0f);
        pos = targetPos - rot * s.localPosition;
    }

    private Node PlaceKind(Node parent, int socket, SegmentKind kind, int via)
    {
        CorridorSegment proto = Prototype(kind);
        if (proto == null || !pool.TryGetValue(kind, out var list) || list.Count == 0) return null;
        Pose(proto, via, parent.seg.GetSocket(socket), out Vector3 pos, out Quaternion rot);
        if (!CellsFree(proto, pos, rot)) return null;
        return Place(parent, socket, Take(kind), via, pos, rot);
    }

    private Node Place(Node parent, int socket, CorridorSegment seg, int via, Vector3 pos, Quaternion rot)
    {
        if (seg == null) return null;
        seg.transform.SetPositionAndRotation(pos, rot);
        Node node = Activate(seg, parent, socket, via);
        // Distancia provisoria hasta el proximo calculo (la usa quien escuche PiecePlaced).
        node.dist = parent.dist + Vector3.Distance(parent.seg.transform.position, pos);

        // Cuantos rectos seguidos lleva este camino, y cuantos faltan para el proximo giro.
        if (seg.Kind == SegmentKind.Recto)
        {
            node.run = parent.run + 1;
            node.straightNeeded = parent.straightNeeded;
        }
        else
        {
            node.run = 0;
            node.straightNeeded = rng.Next(minStraight, maxStraight + 1);
        }

        if (seg.Kind == SegmentKind.Fondo && parent.seg.Kind == SegmentKind.Cruce) parent.hasDeadEnd = true;
        return node;
    }

    private Node PlaceExit(Node parent, int socket, int via, Vector3 pos, Quaternion rot)
    {
        exitModule.transform.SetPositionAndRotation(pos, rot);
        exitNode = Activate(exitSegment, parent, socket, via);
        Debug.Log("[CorridorTreadmill] La salida aparecio en el camino.");
        onExitPlaced?.Invoke();
        return exitNode;
    }

    private Node Activate(CorridorSegment seg, Node parent, int parentSocket, int via)
    {
        seg.ResetForReuse();
        seg.gameObject.SetActive(true);

        var node = new Node
        {
            seg = seg,
            links = new Node[seg.SocketCount],
            caps = new GameObject[seg.SocketCount],
        };
        for (int i = 0; i < seg.CellCount; i++)
        {
            Vector2Int c = CellOf(seg.transform.TransformPoint(seg.LocalCellCenter(i)));
            node.cells.Add(c);
            occupied[c] = node;
            reserved.Remove(c); // si era una celda reservada por un bucle, ya tiene su pieza
        }
        nodes[seg] = node;

        if (parent != null)
        {
            parent.links[parentSocket] = node;
            node.links[via] = parent;
        }
        return node;
    }

    private void RecycleFar(int max)
    {
        var candidates = new List<Node>();
        foreach (Node n in nodes.Values)
        {
            if (n == current || n.dist <= keepRadius) continue;
            int linked = 0;
            foreach (Node l in n.links) if (l != null) linked++;
            if (linked <= 1) candidates.Add(n); // solo "hojas": nunca se parte un camino al medio
        }
        candidates.Sort((a, b) => b.dist.CompareTo(a.dist)); // primero las mas lejanas

        int done = 0;
        foreach (Node n in candidates)
        {
            if (done >= max) break;
            bool hidden = n == exitNode
                ? visibility.IsInBlindSpot(n.seg)
                : n.seg.State == SegmentVisibility.BlindSpot;
            if (!hidden) continue;
            Recycle(n);
            done++;
        }
    }

    private void Recycle(Node node)
    {
        for (int i = 0; i < node.links.Length; i++)
        {
            Node other = node.links[i];
            if (other == null) continue;
            for (int k = 0; k < other.links.Length; k++)
                if (other.links[k] == node) other.links[k] = null;
        }
        for (int i = 0; i < node.caps.Length; i++)
            if (node.caps[i] != null) ReleaseCap(node.caps[i]);

        foreach (Vector2Int c in node.cells) occupied.Remove(c);
        nodes.Remove(node.seg);

        if (node == exitNode)
        {
            exitNode = null;
            exitModule.SetActive(false);
            Debug.Log("[CorridorTreadmill] El jugador se alejo de la salida: va a aparecer en otro lado.");
        }
        else
        {
            PieceRecycled?.Invoke(node.seg);
            ReturnToPool(node.seg);
        }

        RecycledCount++;
        onSegmentRecycled?.Invoke();
    }

    // ------------------------------------------------------------------ persianas (tapas)

    private void Cap(Node node, int socket)
    {
        GameObject cap;
        if (capPool.Count > 0)
        {
            cap = capPool[capPool.Count - 1];
            capPool.RemoveAt(capPool.Count - 1);
        }
        else if (capPrefab != null)
        {
            cap = Instantiate(capPrefab, transform);
        }
        else
        {
            // Sin persiana asignada: igual se marca como tapada para no intentarlo cada frame.
            cap = new GameObject("TapaVacia");
            cap.transform.SetParent(transform, false);
        }

        // Un poco hacia adentro de la pieza, para no quedar pegada a la pared de la vecina.
        Transform s = node.seg.GetSocket(socket);
        cap.transform.SetPositionAndRotation(s.position - s.forward * 0.25f, s.rotation);
        cap.SetActive(true);
        node.caps[socket] = cap;
    }

    private void ReleaseCap(GameObject cap)
    {
        cap.SetActive(false);
        capPool.Add(cap);
    }

    // ------------------------------------------------------------------ que nunca quede encerrado

    /// <summary>
    /// true si desde la pieza del jugador se puede llegar a "lo desconocido" (una conexion libre
    /// o la salida) sin atravesar pasillos cerrados. 'without' se trata como si estuviera cerrado.
    /// </summary>
    private bool HasEscape(Node without)
    {
        if (current == null) return true;        // todavia no se sabe donde esta: no tocar nada
        if (current == without) return false;    // nunca se cierra la pieza donde esta parado
        var seen = new HashSet<Node> { current };
        var stack = new Stack<Node>();
        stack.Push(current);
        while (stack.Count > 0)
        {
            Node n = stack.Pop();
            if (n == exitNode) return true;
            for (int i = 0; i < n.links.Length; i++)
            {
                Node next = n.links[i];
                if (next == null)
                {
                    if (n.caps[i] == null) return true; // conexion libre: hay camino por descubrir
                    continue;
                }
                if (next == without || seen.Contains(next) || next.seg.IsBlocking) continue;
                seen.Add(next);
                stack.Push(next);
            }
        }
        return false;
    }

    /// <summary>Para MazeDirector: si cerrar esa pieza dejaria al jugador sin salida.</summary>
    public bool IsSafeToBlock(CorridorSegment segment)
    {
        if (!nodes.TryGetValue(segment, out Node node)) return true;
        return HasEscape(node);
    }

    /// <summary>
    /// Plan de emergencia: si el jugador quedo encerrado, abre a escondidas un pasillo cerrado
    /// que este en punto ciego. Para el jugador es un cambio mas del laberinto.
    /// </summary>
    private void OpenHiddenBlock()
    {
        foreach (Node n in nodes.Values)
        {
            if (!n.seg.IsBlocking || n.seg.State != SegmentVisibility.BlindSpot) continue;
            n.seg.SetStartVariant();
            n.seg.MarkChanged();
            Debug.Log($"[CorridorTreadmill] El jugador estaba encerrado: se abrio '{n.seg.name}'.");
            return;
        }
    }

    // ------------------------------------------------------------------ salida

    /// <summary>
    /// Pide terminar el loop. La salida se pone en la proxima conexion que se rellene
    /// adelante del jugador, mas alla de la niebla.
    /// </summary>
    public void RequestExit()
    {
        if (ExitRequested) return;
        ExitRequested = true;
        Debug.Log("[CorridorTreadmill] Salida solicitada.");
    }

    // ------------------------------------------------------------------ utilidades

    private Vector2Int CellOf(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world) - gridOrigin;
        return new Vector2Int(Mathf.RoundToInt(local.x / CellSize), Mathf.RoundToInt(local.z / CellSize));
    }

    /// <summary>
    /// true si las celdas de la pieza estan libres. Las celdas reservadas por un bucle cuentan
    /// como ocupadas, salvo para las piezas de ese mismo bucle ('owner').
    /// </summary>
    private bool CellsFree(CorridorSegment proto, Vector3 pos, Quaternion rot, LoopPlan owner = null)
    {
        for (int i = 0; i < proto.CellCount; i++)
        {
            Vector2Int c = CellOf(pos + rot * proto.LocalCellCenter(i));
            if (occupied.ContainsKey(c)) return false;
            if (reserved.TryGetValue(c, out LoopPlan plan) && plan != owner) return false;
        }
        return true;
    }

    private static Bounds PoseBounds(CorridorSegment proto, Vector3 pos, Quaternion rot)
    {
        var b = new Bounds(pos + rot * proto.LocalCellCenter(0) + Vector3.up * 1.5f, Vector3.one * CellSize);
        for (int i = 1; i < proto.CellCount; i++)
            b.Encapsulate(new Bounds(pos + rot * proto.LocalCellCenter(i) + Vector3.up * 1.5f, Vector3.one * CellSize));
        return b;
    }

    private CorridorSegment Prototype(SegmentKind kind) =>
        prototypes.TryGetValue(kind, out CorridorSegment p) ? p : null;

    private CorridorSegment Take(SegmentKind kind)
    {
        if (!pool.TryGetValue(kind, out var list) || list.Count == 0) return null;
        CorridorSegment s = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
        return s;
    }

    private void ReturnToPool(CorridorSegment s)
    {
        s.gameObject.SetActive(false);
        if (!pool.TryGetValue(s.Kind, out var list))
        {
            list = new List<CorridorSegment>();
            pool[s.Kind] = list;
        }
        list.Add(s);
    }

    private int BackwardSocket(Node n)
    {
        for (int i = 0; i < n.seg.SocketCount; i++)
            if (Vector3.Dot(n.seg.GetSocket(i).forward, forwardAxis) < -0.5f) return i;
        return -1;
    }

    private static int FirstFreeSocket(Node n)
    {
        for (int i = 0; i < n.links.Length; i++) if (n.links[i] == null) return i;
        return -1;
    }

    private static long Key(Node n, int socket) => ((long)n.seg.GetInstanceID() << 8) | (long)socket;

    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible || !ready) return;
        int stored = 0;
        foreach (var list in pool.Values) stored += list.Count;
        GUI.Label(new Rect(12, 144, 500, 22),
            $"Camino: {nodes.Count} piezas activas, {stored} en deposito, actual: {(current != null ? current.seg.name : "-")}");
        GUI.Label(new Rect(12, 166, 700, 22), LoopDebugText());
    }
}
