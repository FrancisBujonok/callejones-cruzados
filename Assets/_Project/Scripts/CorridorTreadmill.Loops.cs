using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Parte de CorridorTreadmill: los BUCLES (pasillos que se repiten).
///
/// IDEA: cada tanto, el generador deja de sortear piezas y arma adelante un tramo fijo, la
/// "plantilla": unos rectos, un giro, mas rectos, un cruce con un callejon sin salida al costado,
/// mas rectos y el giro contrario. Cuando el tramo termina, vuelve a armar el MISMO tramo, con la
/// misma decoracion. El jugador sigue caminando hacia adelante y vuelve a pasar por el mismo cruce.
/// Despues de 'identicalPasses' pasadas iguales llega una con una anomalia (farol apagado,
/// persiana a medio subir u otros objetos) y despues el azar sigue como siempre.
///
/// Por que copias y no teletransporte: el generador ya arma el mundo a medida que el jugador
/// avanza, asi que alcanza con armar otra vez lo mismo adelante. Al jugador nunca se lo mueve
/// a escondidas: no hay saltos de un frame ni problemas con el CharacterController.
///
/// Por que el tramo dobla dos veces: con un giro y su contrario, cada copia queda en diagonal
/// respecto de la anterior (nunca se ven dos cruces iguales a la vez) y el tramo termina mirando
/// para el mismo lado que empezo, asi todas las copias quedan iguales.
///
/// Que nada se rompa:
/// - Antes de empezar se calcula donde caeria cada pieza de todas las copias. Si algo ya ocupa
///   ese lugar se prueba otra plantilla; si ninguna entra, el bucle espera a otra conexion.
///   Las celdas que van a usar las copias quedan reservadas: el resto del laberinto no las toca.
/// - Mientras dura, sus piezas quedan bloqueadas para MazeDirector (Locked).
/// - Si una pieza no entra o no quedan en el deposito, el bucle se corta y sigue el azar.
/// - Si el jugador se va por otro camino y el bucle queda atras sin piezas, se cancela solo.
/// </summary>
public partial class CorridorTreadmill
{
    [Header("Bucles (pasillos que se repiten)")]
    [Tooltip("Apagar para que nunca haya bucles.")]
    [SerializeField] private bool loopsEnabled = true;
    [Tooltip("Cuantas veces se pasa por el mismo tramo, identico, antes de la pasada que cambia.")]
    [SerializeField, Range(2, 5)] private int identicalPasses = 3;
    [Tooltip("Metros caminados minimos entre un bucle y el siguiente.")]
    [SerializeField] private float loopGapMin = 90f;
    [Tooltip("Metros caminados maximos entre un bucle y el siguiente (se sortea entre los dos). El primero llega a la mitad.")]
    [SerializeField] private float loopGapMax = 180f;

    /// <summary>true mientras hay un bucle en curso.</summary>
    public bool LoopActive => activePlan != null;
    /// <summary>Cuantos bucles empezaron en la partida.</summary>
    public int LoopsStarted => loopCount;

    // Valores especiales de LoopStep.next.
    private const int NoStep = -1;     // nada (por ejemplo, la conexion por donde se entra)
    private const int EndOfCopy = -2;  // aca termina la copia: sigue la proxima (o el azar)

    /// <summary>Un paso de la plantilla: que pieza va y que va enganchado en cada conexion.</summary>
    private class LoopStep
    {
        public SegmentKind kind;
        public int via;      // por cual de SUS conexiones se engancha
        public int[] next;   // por conexion: el paso que va ahi, NoStep o EndOfCopy
        public int[] decor;  // juego de cada DecorRandomizer de la pieza (igual en todas las copias)
    }

    /// <summary>Un bucle: la plantilla y en que va.</summary>
    private class LoopPlan
    {
        public int number;
        public LoopStep[] steps;
        public int crossStep;        // el cruce: ahi se cuentan las pasadas y va la anomalia
        public int lastCopy;         // la copia con la anomalia (las anteriores son identicas)
        public Anomaly anomaly;
        public int anomalyDecor;     // para OtraDecoracion: el juego distinto
        public int reachedCopy = -1; // la ultima copia cuyo cruce piso el jugador
        public bool finished;        // ya se vio la anomalia, o se cancelo
        public bool canceled;        // se corto antes de tiempo: no se arman mas copias
        public string summary;       // descripcion corta, para el debug
    }

    private readonly Dictionary<Vector2Int, LoopPlan> reserved = new Dictionary<Vector2Int, LoopPlan>();
    private readonly List<int> mainOut = new List<int>();
    private LoopPlan activePlan;
    private bool loopArmed;
    private int loopCount;
    private float walked;
    private float nextLoopAt;
    private Vector3 lastPlayerPos;

    // ------------------------------------------------------------------ cuando hay un bucle

    private void InitLoops()
    {
        lastPlayerPos = player.position;
        nextLoopAt = RandomGap() * 0.5f; // el primero llega antes
    }

    private float RandomGap() => Mathf.Lerp(loopGapMin, loopGapMax, (float)rng.NextDouble());

    /// <summary>Para probar: el proximo tramo que se arme adelante empieza un bucle.</summary>
    [ContextMenu("Probar bucle ahora")]
    public void ArmLoopNow()
    {
        if (activePlan != null || rng == null) return;
        loopArmed = true;
        Debug.Log("[CorridorTreadmill] Bucle pedido a mano: empieza en la proxima conexion adelante.");
    }

    /// <summary>Cada frame: metros caminados, pasadas, y si el bucle quedo abandonado.</summary>
    private void UpdateLoops()
    {
        // Metros caminados en el plano. Un salto grande no cuenta: seria un teletransporte.
        Vector3 d = player.position - lastPlayerPos;
        d.y = 0f;
        if (d.magnitude < 2f) walked += d.magnitude;
        lastPlayerPos = player.position;

        if (activePlan == null)
        {
            if (loopsEnabled && !loopArmed && !ExitRequested && walked >= nextLoopAt) loopArmed = true;
            if (reserved.Count > 0) PruneReservations();
            return;
        }

        // Una pasada mas: el jugador piso el cruce de una copia nueva.
        if (current != null && current.loop == activePlan && current.loopStep == activePlan.crossStep
            && current.loopCopy > activePlan.reachedCopy)
        {
            activePlan.reachedCopy = current.loopCopy;
            Debug.Log($"[CorridorTreadmill] Bucle #{activePlan.number}: pasada {current.loopCopy + 1} de {activePlan.lastCopy + 1}.");
            if (current.loopCopy >= activePlan.lastCopy) EndLoop(activePlan, "el jugador llego a la pasada distinta", false);
            return;
        }

        // Abandonado: ya no queda ninguna pieza del bucle (el jugador se fue por otro camino).
        foreach (Node n in nodes.Values) if (n.loop == activePlan) return;
        EndLoop(activePlan, "quedo atras sin piezas", true);
    }

    /// <summary>
    /// Si la conexion le toca a un bucle (porque es parte de uno, o porque hay que empezar uno),
    /// pone la pieza de la plantilla y devuelve true. Si no, devuelve false y sigue el azar normal.
    /// </summary>
    private bool FillLoopSocket(Node parent, int socket, bool initial, bool urgent)
    {
        LoopPlan plan;
        int copy, stepIndex;
        List<Vector2Int> footprint = null;
        bool starting = parent.loop == null;
        Transform target = parent.seg.GetSocket(socket);

        if (!starting)
        {
            // La pieza de al lado es de un bucle: la plantilla dice que va aca.
            plan = parent.loop;
            copy = parent.loopCopy;
            stepIndex = plan.steps[parent.loopStep].next[socket];
            if (stepIndex == EndOfCopy)
            {
                // Termino una copia: empieza la proxima, salvo que fuera la ultima.
                if (plan.canceled || copy >= plan.lastCopy) return false;
                copy++;
                stepIndex = 0;
            }
            if (stepIndex < 0) return false;
        }
        else
        {
            // Empezar un bucle: solo si toca, y en una conexion bien adelante del jugador.
            if (!loopArmed || activePlan != null || initial || ExitRequested) return false;
            if (Vector3.Dot(target.forward, forwardAxis) < -0.5f || !IsAhead(parent, socket)) return false;
            plan = CreatePlan(target, out footprint);
            if (plan == null) return false;
            copy = 0;
            stepIndex = 0;
        }

        LoopStep step = plan.steps[stepIndex];
        CorridorSegment proto = Prototype(step.kind);
        bool fits = proto != null && pool.TryGetValue(step.kind, out var stock) && stock.Count > 0;
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        if (fits)
        {
            Pose(proto, step.via, target, out pos, out rot);
            fits = CellsFree(proto, pos, rot, plan);
        }
        if (!fits)
        {
            // No entra o no quedan piezas: el bucle se corta aca y sigue el azar normal.
            if (!starting && !plan.canceled)
                EndLoop(plan, $"no entra la pieza {step.kind} de la pasada {copy + 1}", true);
            return false;
        }

        // Igual que con cualquier pieza: si se veria aparecer, se espera.
        if (!initial && !urgent && visibility.CouldBeSeen(PoseBounds(proto, pos, rot), RenderSettings.fogEndDistance + 2f))
        {
            deferred.Add(Key(parent, socket));
            return true;
        }

        Node node = Place(parent, socket, Take(step.kind), step.via, pos, rot);
        node.loop = plan;
        node.loopCopy = copy;
        node.loopStep = stepIndex;

        if (starting) StartLoop(plan, footprint);
        DressLoopPiece(node, plan, step, copy, stepIndex);
        return true;
    }

    private void StartLoop(LoopPlan plan, List<Vector2Int> footprint)
    {
        plan.number = ++loopCount;
        foreach (Vector2Int c in footprint)
            if (!occupied.ContainsKey(c)) reserved[c] = plan;
        activePlan = plan;
        loopArmed = false;
        Debug.Log($"[CorridorTreadmill] Bucle #{plan.number} empieza: {plan.summary}. " +
                  $"{plan.lastCopy} pasadas iguales y despues: {plan.anomaly}.");
    }

    /// <summary>Misma decoracion en todas las copias, y la anomalia en el cruce de la ultima.</summary>
    private void DressLoopPiece(Node node, LoopPlan plan, LoopStep step, int copy, int stepIndex)
    {
        DecorRandomizer[] decor = node.seg.GetComponentsInChildren<DecorRandomizer>(true);
        for (int i = 0; i < decor.Length && i < step.decor.Length; i++) decor[i].Show(step.decor[i]);

        if (!plan.finished) node.seg.Locked = true;

        if (copy != plan.lastCopy || stepIndex != plan.crossStep) return;

        if (plan.anomaly == Anomaly.OtraDecoracion)
        {
            if (decor.Length > 0) decor[0].Show(plan.anomalyDecor);
        }
        else
        {
            SegmentAnomalies anomalies = node.seg.GetComponent<SegmentAnomalies>();
            if (anomalies != null) anomalies.Apply(plan.anomaly);
        }
        // Para el resto del juego es un cambio mas: cuando el jugador lo ve, sube el miedo.
        node.seg.MarkChanged();
    }

    /// <summary>Termina un bucle: porque se vio la anomalia, o cancelado.</summary>
    private void EndLoop(LoopPlan plan, string reason, bool canceled)
    {
        bool wasFinished = plan.finished;
        plan.finished = true;
        if (canceled)
        {
            plan.canceled = true;
            var drop = new List<Vector2Int>();
            foreach (var kv in reserved) if (kv.Value == plan) drop.Add(kv.Key);
            foreach (Vector2Int c in drop) reserved.Remove(c);
        }
        foreach (Node n in nodes.Values) if (n.loop == plan) n.seg.Locked = false;

        if (activePlan == plan)
        {
            activePlan = null;
            // Si el jugador casi no lo vio, se reintenta pronto. Si no, la pausa normal.
            nextLoopAt = walked + (canceled && plan.reachedCopy < 1 ? 15f : RandomGap());
        }
        if (!wasFinished) Debug.Log($"[CorridorTreadmill] Bucle #{plan.number} termina: {reason}.");
    }

    /// <summary>Suelta las reservas de bucles que ya no tienen ninguna pieza puesta.</summary>
    private void PruneReservations()
    {
        var alive = new HashSet<LoopPlan>();
        foreach (Node n in nodes.Values) if (n.loop != null) alive.Add(n.loop);
        var drop = new List<Vector2Int>();
        foreach (var kv in reserved) if (!alive.Contains(kv.Value)) drop.Add(kv.Key);
        foreach (Vector2Int c in drop) reserved.Remove(c);
    }

    // ------------------------------------------------------------------ la plantilla

    /// <summary>
    /// Arma una plantilla al azar que entre entera (todas las copias) a partir de esa conexion.
    /// Prueba hasta 6 plantillas distintas. Devuelve null si ninguna entra.
    /// </summary>
    private LoopPlan CreatePlan(Transform start, out List<Vector2Int> footprint)
    {
        footprint = new List<Vector2Int>();
        for (int attempt = 0; attempt < 6; attempt++)
        {
            LoopPlan plan = BuildTemplate(start.forward);
            if (plan == null) return null;
            if (Simulate(plan, start.position, start.forward, footprint)) return plan;
        }
        return null;
    }

    private LoopPlan BuildTemplate(Vector3 h0)
    {
        CorridorSegment recto = Prototype(SegmentKind.Recto);
        CorridorSegment esquina = Prototype(SegmentKind.Esquina);
        CorridorSegment cruce = Prototype(SegmentKind.Cruce);
        CorridorSegment fondo = Prototype(SegmentKind.Fondo);
        if (recto == null || esquina == null || cruce == null || fondo == null) return null;

        var steps = new List<LoopStep>();
        mainOut.Clear();
        var plan = new LoopPlan { lastCopy = identicalPasses };

        // Largo de cada tramo recto: 1 o 2 rectos (6 o 12 m), al azar.
        int a = rng.Next(1, 3), b = rng.Next(1, 3), c = rng.Next(1, 3);

        // 1. Rectos hasta el primer giro.
        for (int i = 0; i < a; i++) AddMain(steps, recto, 0, 1);

        // 2. Primer giro, hacia un lado que no lleve hacia atras.
        int t1 = PickTurn(esquina, h0);
        if (t1 < 0) return null;
        AddMain(steps, esquina, t1, 1 - t1);
        Vector3 h1 = OutDir(esquina, t1, 1 - t1, h0);

        // 3. Rectos hasta el cruce.
        for (int i = 0; i < b; i++) AddMain(steps, recto, 0, 1);

        // 4. El cruce, atravesado de costado: se sigue derecho y queda un callejon a un lado.
        int viaC = PickCrossing(cruce, h1, out int straight, out int side);
        if (viaC < 0) return null;
        plan.crossStep = steps.Count;
        AddMain(steps, cruce, viaC, straight);

        // 5. Rectos despues del cruce.
        for (int i = 0; i < c; i++) AddMain(steps, recto, 0, 1);

        // 6. El giro contrario: el tramo termina mirando para el mismo lado que empezo.
        int t2 = PickTurnTo(esquina, h1, h0);
        if (t2 < 0) return null;
        AddMain(steps, esquina, t2, 1 - t2);

        // Enlazar la linea principal: la salida de cada paso lleva al siguiente.
        int mainCount = steps.Count;
        for (int i = 0; i < mainCount; i++)
            steps[i].next[mainOut[i]] = i < mainCount - 1 ? i + 1 : EndOfCopy;

        // El callejon del costado: a veces un nicho (solo el fondo), a veces un recto y el fondo.
        bool longAlley = rng.Next(2) == 1;
        int firstSide = steps.Count;
        if (longAlley)
        {
            AddStep(steps, recto, 0);
            steps[firstSide].next[1] = firstSide + 1;
        }
        AddStep(steps, fondo, 0);
        steps[plan.crossStep].next[side] = firstSide;

        // La anomalia de la ultima pasada, entre las que el cruce puede mostrar.
        var choices = new List<Anomaly>();
        SegmentAnomalies anomalies = cruce.GetComponent<SegmentAnomalies>();
        if (anomalies != null && anomalies.Supports(Anomaly.FarolApagado)) choices.Add(Anomaly.FarolApagado);
        if (anomalies != null && anomalies.Supports(Anomaly.PersianaEntreabierta)) choices.Add(Anomaly.PersianaEntreabierta);
        DecorRandomizer[] crossDecor = cruce.GetComponentsInChildren<DecorRandomizer>(true);
        if (crossDecor.Length > 0 && crossDecor[0].Count > 1) choices.Add(Anomaly.OtraDecoracion);
        if (choices.Count == 0) return null;
        plan.anomaly = choices[rng.Next(choices.Count)];
        if (plan.anomaly == Anomaly.OtraDecoracion)
        {
            int n = crossDecor[0].Count;
            plan.anomalyDecor = (steps[plan.crossStep].decor[0] + 1 + rng.Next(n - 1)) % n; // cualquiera menos el de siempre
        }

        plan.steps = steps.ToArray();
        bool turnRight = Vector3.Cross(h0, h1).y > 0f;
        bool sideRight = Vector3.Cross(h1, OutDir(cruce, viaC, side, h1)).y > 0f;
        plan.summary = $"{a}+{b}+{c} rectos, dobla a la {(turnRight ? "derecha" : "izquierda")}, " +
                       $"callejon {(longAlley ? "largo" : "corto")} a la {(sideRight ? "derecha" : "izquierda")}";
        return plan;
    }

    private void AddMain(List<LoopStep> steps, CorridorSegment proto, int via, int outSocket)
    {
        AddStep(steps, proto, via);
        mainOut.Add(outSocket);
    }

    /// <summary>Agrega un paso y le sortea la decoracion de una vez: la misma para todas las copias.</summary>
    private void AddStep(List<LoopStep> steps, CorridorSegment proto, int via)
    {
        var step = new LoopStep { kind = proto.Kind, via = via, next = new int[proto.SocketCount] };
        for (int i = 0; i < step.next.Length; i++) step.next[i] = NoStep;

        DecorRandomizer[] decor = proto.GetComponentsInChildren<DecorRandomizer>(true);
        step.decor = new int[decor.Length];
        for (int i = 0; i < decor.Length; i++) step.decor[i] = decor[i].Count > 0 ? rng.Next(decor[i].Count) : 0;
        steps.Add(step);
    }

    /// <summary>Esquina: por cual conexion entrar para doblar hacia un lado permitido (al azar).</summary>
    private int PickTurn(CorridorSegment esquina, Vector3 heading)
    {
        var valid = new List<int>();
        for (int v = 0; v < 2 && v < esquina.SocketCount; v++)
            if (Vector3.Dot(OutDir(esquina, v, 1 - v, heading), forwardAxis) > -0.5f) valid.Add(v);
        return valid.Count == 0 ? -1 : valid[rng.Next(valid.Count)];
    }

    /// <summary>Esquina: por cual conexion entrar para salir mirando hacia 'wanted'.</summary>
    private static int PickTurnTo(CorridorSegment esquina, Vector3 heading, Vector3 wanted)
    {
        for (int v = 0; v < 2 && v < esquina.SocketCount; v++)
            if (Vector3.Dot(OutDir(esquina, v, 1 - v, heading), wanted) > 0.9f) return v;
        return -1;
    }

    /// <summary>
    /// Cruce: por cual conexion entrar para que otra quede derecha (seguir) y la restante al
    /// costado, sin ninguna salida hacia atras. Si hay dos posibles, al azar.
    /// </summary>
    private int PickCrossing(CorridorSegment cruce, Vector3 heading, out int straight, out int side)
    {
        var vias = new List<int>();
        var straights = new List<int>();
        var sides = new List<int>();
        for (int v = 0; v < cruce.SocketCount; v++)
        {
            int st = -1, sd = -1;
            bool backward = false;
            for (int k = 0; k < cruce.SocketCount; k++)
            {
                if (k == v) continue;
                Vector3 dir = OutDir(cruce, v, k, heading);
                if (Vector3.Dot(dir, heading) > 0.9f) st = k; else sd = k;
                if (Vector3.Dot(dir, forwardAxis) < -0.5f) backward = true;
            }
            if (st < 0 || sd < 0 || backward) continue;
            vias.Add(v);
            straights.Add(st);
            sides.Add(sd);
        }

        straight = side = -1;
        if (vias.Count == 0) return -1;
        int pick = rng.Next(vias.Count);
        straight = straights[pick];
        side = sides[pick];
        return vias[pick];
    }

    /// <summary>Hacia donde mira la conexion 'socket' si la pieza entra por 'via' yendo hacia 'heading'.</summary>
    private static Vector3 OutDir(CorridorSegment proto, int via, int socket, Vector3 heading)
    {
        PoseAt(proto, via, Vector3.zero, heading, out _, out Quaternion rot);
        return rot * (proto.GetSocket(socket).localRotation * Vector3.forward);
    }

    /// <summary>
    /// Recorre todas las copias "en el aire", sin poner nada, y junta las celdas que ocuparian.
    /// false si alguna ya esta ocupada, reservada o se pisa con otra del mismo bucle.
    /// </summary>
    private bool Simulate(LoopPlan plan, Vector3 startPos, Vector3 startForward, List<Vector2Int> cells)
    {
        cells.Clear();
        var taken = new HashSet<Vector2Int>();
        var open = new Stack<(int step, Vector3 pos, Vector3 fwd)>();
        Vector3 copyPos = startPos, copyFwd = startForward;

        for (int copy = 0; copy <= plan.lastCopy; copy++)
        {
            bool hasEnd = false;
            Vector3 endPos = copyPos, endFwd = copyFwd;
            open.Clear();
            open.Push((0, copyPos, copyFwd));

            while (open.Count > 0)
            {
                var (index, tPos, tFwd) = open.Pop();
                LoopStep step = plan.steps[index];
                CorridorSegment proto = Prototype(step.kind);
                PoseAt(proto, step.via, tPos, tFwd, out Vector3 pos, out Quaternion rot);

                for (int k = 0; k < proto.CellCount; k++)
                {
                    Vector2Int c = CellOf(pos + rot * proto.LocalCellCenter(k));
                    if (occupied.ContainsKey(c) || reserved.ContainsKey(c) || !taken.Add(c)) return false;
                    cells.Add(c);
                }

                for (int s = 0; s < step.next.Length; s++)
                {
                    if (step.next[s] == NoStep) continue;
                    Transform socket = proto.GetSocket(s);
                    Vector3 sPos = pos + rot * socket.localPosition;
                    Vector3 sFwd = rot * (socket.localRotation * Vector3.forward);
                    if (step.next[s] == EndOfCopy)
                    {
                        hasEnd = true;
                        endPos = sPos;
                        endFwd = sFwd;
                    }
                    else
                    {
                        open.Push((step.next[s], sPos, sFwd));
                    }
                }
            }

            if (!hasEnd) return false;
            copyPos = endPos;
            copyFwd = endFwd;
        }
        return true;
    }

    // ------------------------------------------------------------------ debug

    private string LoopDebugText()
    {
        if (activePlan != null)
            return $"Bucle #{activePlan.number}: pasada {activePlan.reachedCopy + 1} de {activePlan.lastCopy + 1} " +
                   $"({activePlan.summary}; al final: {activePlan.anomaly})";
        if (loopArmed) return "Bucle: listo, empieza en la proxima conexion adelante";
        if (!loopsEnabled) return "Bucles: apagados";
        return $"Proximo bucle en {Mathf.Max(0f, nextLoopAt - walked):0} m";
    }
}
