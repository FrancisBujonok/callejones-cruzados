using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 23: la plazoleta. Va en la raiz de la pieza Plaza, junto a CorridorSegment.
///
/// Respiro: despues de tanto pasillo de 3 m, la plaza se abre. Mientras el jugador esta adentro,
/// el miedo baja un poco mas rapido (reliefPerSecond, sin cortar la calma de FearMeter).
///
/// Pero mirar todos los pasillos es paranoia: cada vez que la vista pasa de una boca a otra se
/// cuenta una "ojeada". Muchas ojeadas en poco tiempo suman miedo (como mirar atras, ver
/// ParanoiaSensor) y cortan el respiro un rato. Lo mismo si salta ParanoiaSensor.
///
/// Trampa (solo con miedo alto, una vez por visita, y siempre fuera de la vista):
/// - Se cierra el callejon por el que llego: baja una persiana en esa boca. Solo si queda otro
///   camino (CorridorTreadmill.CanCloseSocket).
/// - El altar del centro aparece del otro lado del arbol, de espaldas al jugador.
/// Cuando el jugador mira lo que cambio, cuenta como un cambio descubierto (sube el miedo y
/// suena, igual que con VisibilityChecker).
/// - Al salir con miedo alto, a veces pide que la proxima plaza sea igual a esta
///   (CorridorTreadmill.ArmPlazaRepeat): la plaza repetida.
/// </summary>
[DisallowMultipleComponent]
public class Plaza : MonoBehaviour
{
    private const float Half = 4.5f;

    [Header("Partes (las pone la herramienta de editor)")]
    [Tooltip("Pivote del altar, en el centro de la plaza: girarlo lleva el altar a otro lado del arbol.")]
    [SerializeField] private Transform altarPivot;
    [Tooltip("El altar (hijo del pivote). Se usa para saber si el jugador lo esta mirando.")]
    [SerializeField] private Transform altar;

    [Header("Respiro")]
    [Tooltip("Miedo que baja por segundo mientras estas en la plaza (ademas de la calma normal).")]
    [SerializeField] private float reliefPerSecond = 2f;
    [Tooltip("Segundos en la plaza antes de que empiece el respiro.")]
    [SerializeField] private float reliefDelay = 2f;
    [Tooltip("Segundos sin paranoia (ni ojeadas de mas) para que vuelva el respiro.")]
    [SerializeField] private float quietSeconds = 6f;

    [Header("Ojeadas: mirar todas las bocas")]
    [Tooltip("Grados entre el centro de la vista y una boca para contar que la estas mirando.")]
    [SerializeField] private float glanceAngle = 18f;
    [Tooltip("Ventana en la que se cuentan las ojeadas, en segundos.")]
    [SerializeField] private float glanceWindow = 6f;
    [Tooltip("A partir de esta cantidad de ojeadas en la ventana, cada nueva suma paranoia.")]
    [SerializeField] private int glancesToTrigger = 5;
    [Tooltip("Miedo que suma cada ojeada de mas (mirar atras, en ParanoiaSensor, suma 4).")]
    [SerializeField] private float fearPerGlance = 2.5f;
    [Tooltip("Segundos minimos entre dos ojeadas que suman miedo.")]
    [SerializeField] private float glanceCooldown = 1.2f;

    [Header("Trampa")]
    [SerializeField, Range(0f, 1f)] private float trapFromFear = 0.6f;
    [Tooltip("Segundos en la plaza antes de que pueda pasar.")]
    [SerializeField] private float trapDelay = 5f;
    [Tooltip("Mas de estos grados entre la vista y lo que cambia = no lo esta viendo.")]
    [SerializeField] private float hiddenAngle = 75f;
    [Tooltip("Menos de estos grados (y sin nada en el medio) = lo vio.")]
    [SerializeField] private float seenAngle = 28f;
    [Tooltip("Probabilidad de pedir la plaza repetida al salir con miedo alto.")]
    [SerializeField, Range(0f, 1f)] private float repeatChance = 0.5f;

    /// <summary>true si esta plaza es la copia de la anterior (la trampa de la plaza repetida).</summary>
    public bool IsRepeat { get; set; }
    public bool PlayerInside => inside;
    public bool Relieving { get; private set; }
    public string LastTrap { get; private set; } = "-";

    private CorridorSegment segment;
    private CorridorTreadmill paths;
    private VisibilityChecker visibility;
    private FearMeter fear;
    private ParanoiaSensor paranoia;
    private Transform player;
    private Camera view;

    private bool inside, trapDone;
    private float insideTime, lastParanoia = -999f, lastGlanceFear = -999f;
    private int arrival = -1, lastFaced = -1;
    private readonly Queue<float> glances = new Queue<float>();
    private bool pendingSeen;
    private Vector3 pendingPoint;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Transform newAltarPivot, Transform newAltar)
    {
        altarPivot = newAltarPivot;
        altar = newAltar;
    }

    private void Awake()
    {
        segment = GetComponent<CorridorSegment>();
        paths = GetComponentInParent<CorridorTreadmill>();
        visibility = paths != null ? paths.GetComponent<VisibilityChecker>() : null;
        fear = FindAnyObjectByType<FearMeter>();
        paranoia = FindAnyObjectByType<ParanoiaSensor>();
        player = paths != null ? paths.Player : null;
        view = Camera.main;
    }

    private void OnEnable()
    {
        // La pieza se reusa: todo como nuevo (el generador ya sorteo la decoracion).
        inside = trapDone = pendingSeen = false;
        insideTime = 0f;
        arrival = lastFaced = -1;
        glances.Clear();
        LastTrap = "-";
        if (altarPivot != null) altarPivot.localRotation = Quaternion.identity;
        if (paranoia != null) paranoia.onParanoia.AddListener(OnParanoia);
    }

    private void OnDisable()
    {
        if (paranoia != null) paranoia.onParanoia.RemoveListener(OnParanoia);
    }

    private void OnParanoia() => lastParanoia = Time.time;

    // ------------------------------------------------------------------ copiar la plaza

    /// <summary>La opcion de cada RandomPick de la plaza (disposicion, carteles, ventanas).</summary>
    public int[] GetPicks()
    {
        RandomPick[] picks = GetComponentsInChildren<RandomPick>(true);
        var result = new int[picks.Length];
        for (int i = 0; i < picks.Length; i++) result[i] = picks[i].Current;
        return result;
    }

    /// <summary>Muestra las mismas opciones que otra plaza: queda igual a ella.</summary>
    public void ApplyPicks(int[] values)
    {
        RandomPick[] picks = GetComponentsInChildren<RandomPick>(true);
        for (int i = 0; i < picks.Length && i < values.Length; i++)
            if (values[i] >= 0) picks[i].Show(values[i]);
    }

    // ------------------------------------------------------------------ cada frame

    private void Update()
    {
        if (player == null || view == null || fear == null) return;
        float now = Time.time;

        Vector3 local = transform.InverseTransformPoint(player.position);
        bool nowInside = Mathf.Abs(local.x) < Half && Mathf.Abs(local.z) < Half;
        if (nowInside && !inside) Enter(local);
        else if (!nowInside && inside) Leave();
        inside = nowInside;

        CheckSeen();
        if (!inside)
        {
            Relieving = false;
            return;
        }

        insideTime += Time.deltaTime;
        CountGlances(now);

        // Respiro: lugar abierto, siempre que no estes mirando para todos lados.
        Relieving = insideTime >= reliefDelay && now - lastParanoia >= quietSeconds;
        if (Relieving) fear.Relieve(reliefPerSecond * Time.deltaTime);

        if (!trapDone && insideTime >= trapDelay && fear.Normalized >= trapFromFear) TryTrap();
    }

    private void Enter(Vector3 local)
    {
        insideTime = 0f;
        // La boca por la que llego: la mas cercana al jugador.
        float best = float.MaxValue;
        for (int i = 0; i < segment.SocketCount; i++)
        {
            float d = (segment.GetSocket(i).localPosition - new Vector3(local.x, 0f, local.z)).sqrMagnitude;
            if (d < best) { best = d; arrival = i; }
        }
    }

    private void Leave()
    {
        Relieving = false;
        if (!IsRepeat && paths != null && fear.Normalized >= trapFromFear && Random.value < repeatChance)
            paths.ArmPlazaRepeat(this);
    }

    /// <summary>Cada vez que la vista pasa a otra boca, una ojeada.</summary>
    private void CountGlances(float now)
    {
        int faced = -1;
        for (int i = 0; i < segment.SocketCount; i++)
            if (FlatAngle(segment.GetSocket(i).position) < glanceAngle) { faced = i; break; }

        if (faced >= 0 && faced != lastFaced)
        {
            lastFaced = faced;
            glances.Enqueue(now);
            while (glances.Count > 0 && now - glances.Peek() > glanceWindow) glances.Dequeue();
            if (glances.Count >= glancesToTrigger)
            {
                lastParanoia = now; // sin respiro mientras sigas mirando para todos lados
                if (now - lastGlanceFear >= glanceCooldown)
                {
                    lastGlanceFear = now;
                    Debug.Log($"[Plaza] Paranoia: {glances.Count} ojeadas a las bocas en {glanceWindow:0} s.");
                    fear.AddFear(fearPerGlance);
                }
            }
        }
        while (glances.Count > 0 && now - glances.Peek() > glanceWindow) glances.Dequeue();
    }

    // ------------------------------------------------------------------ la trampa

    private void TryTrap()
    {
        var choices = new List<int>(2);
        if (CanCloseArrival()) choices.Add(0);
        if (altarPivot != null && altar != null && IsHidden(altar.position + Vector3.up * 0.8f)) choices.Add(1);
        if (choices.Count == 0) return; // ahora no se puede: se prueba en el proximo frame

        trapDone = true;
        if (choices[Random.Range(0, choices.Count)] == 0)
        {
            segment.SetSocketClosed(arrival, true);
            Transform s = segment.GetSocket(arrival);
            pendingPoint = s.position - s.forward * 0.4f + Vector3.up * 1.4f;
            LastTrap = "se cerro el callejon de llegada";
        }
        else
        {
            TurnAltarAway();
            pendingPoint = altar.position + Vector3.up * 0.8f;
            LastTrap = "el altar se dio vuelta";
        }
        pendingSeen = true;
        Debug.Log($"[Plaza] Trampa en '{name}': {LastTrap}.");
    }

    private bool CanCloseArrival()
    {
        if (arrival < 0 || paths == null || !segment.CanClose(arrival) || segment.IsSocketClosed(arrival)) return false;
        Transform s = segment.GetSocket(arrival);
        // Lejos de la boca (que no le baje la persiana encima) y sin mirarla.
        if ((s.position - player.position).sqrMagnitude < 3f * 3f) return false;
        if (!IsHidden(s.position + Vector3.up * 1.4f)) return false;
        return paths.CanCloseSocket(segment, arrival);
    }

    /// <summary>El altar pasa al lado del arbol que mira mas lejos del jugador: le da la espalda.</summary>
    private void TurnAltarAway()
    {
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        float bestDot = float.MinValue;
        Quaternion best = altarPivot.localRotation;
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = Quaternion.Euler(0f, 90f * k, 0f);
            if (Quaternion.Angle(q, altarPivot.localRotation) < 1f) continue; // tiene que cambiar
            // El altar mira hacia -Z del pivote: girado, mira hacia rot * -Z.
            Vector3 facing = transform.rotation * (q * Vector3.back);
            float dot = Vector3.Dot(facing, away.normalized);
            if (dot > bestDot) { bestDot = dot; best = q; }
        }
        altarPivot.localRotation = best;
    }

    /// <summary>Cuando el jugador mira lo que cambio: un cambio descubierto.</summary>
    private void CheckSeen()
    {
        if (!pendingSeen) return;
        Vector3 cam = view.transform.position;
        if ((pendingPoint - cam).sqrMagnitude > 18f * 18f) return;
        if (Vector3.Angle(view.transform.forward, pendingPoint - cam) > seenAngle) return;
        if (Physics.Linecast(cam, pendingPoint, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(transform)) return;

        pendingSeen = false;
        Debug.Log($"[Plaza] El jugador vio el cambio: {LastTrap}.");
        if (visibility != null) visibility.onChangeDiscovered?.Invoke(); // miedo + sonido, como cualquier cambio
        else fear.OnChangeDiscovered();
    }

    private bool IsHidden(Vector3 point) => Vector3.Angle(view.transform.forward, point - view.transform.position) > hiddenAngle;

    private float FlatAngle(Vector3 point)
    {
        Vector3 f = view.transform.forward; f.y = 0f;
        Vector3 d = point - view.transform.position; d.y = 0f;
        return Vector3.Angle(f, d);
    }

    private void OnGUI()
    {
        if (!inside || !DebugOverlay.Visible) return;
        GUI.Label(new Rect(12, 232, 700, 22),
            $"Plaza{(IsRepeat ? " (repetida)" : "")}: {(Relieving ? "respiro" : "sin respiro")}, ojeadas {glances.Count}, trampa: {LastTrap}");
    }
}
