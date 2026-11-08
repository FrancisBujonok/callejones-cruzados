using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 27: LA FIGURA (prototipo). Una figura demasiado alta y flaca, quieta, que aparece
/// DETRAS del jugador cuando el miedo esta alto. No persigue, no toca, no hace perder: solo
/// esta ahi. Lo que la hace inquietante son sus reglas, no un susto:
///
/// 1. Aparece donde no la ves. Se ubica 9 a 13 m atras, sobre el camino que ya caminaste, en un
///    punto que ahora esta fuera de la camara pero que se ve si te das vuelta. Nunca aparece
///    delante de tus ojos.
/// 2. Mientras la miras, no se mueve. Solo la cabeza: empieza mirando hacia un costado y gira
///    hacia vos, muy despacio, inclinandose un poco. Solo gira mientras la miras.
/// 3. Cuando dejas de mirarla, decide: a veces da un paso hacia vos (2 m) y queda mirandote
///    de frente; otras veces ya no esta. Como mucho dos pasos, y nunca a menos de 5.5 m.
/// 4. Si la miras fijo 3 s, o te acercas, se va: los faroles cercanos se cortan un instante y
///    cuando vuelven ya no esta.
/// 5. Respira. Una respiracion cada 6.5 s, mas lenta que la de una persona. Se oye solo de cerca.
///
/// Suma miedo al verla (8), al volver a verla despues de un paso (5) y al desaparecer (3).
/// Para probar: F6 la pone detras ahora mismo, sin importar el miedo (si hay lugar).
///
/// Va en el objeto "Figura" (raiz de la escena). Lo arma Callejones > Figura y trote.
/// </summary>
public class FigurePresence : MonoBehaviour
{
    public enum State { Hidden, Waiting, Watched, Unwatched }

    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FearMeter fear;
    [Tooltip("Para no aparecer mientras suena una pista.")]
    [SerializeField] private LookBackLures lures;
    [Tooltip("El cuerpo (pies en y = 0, mirando hacia +Z).")]
    [SerializeField] private Transform body;
    [Tooltip("El pivote de la cabeza, en la punta del cuello.")]
    [SerializeField] private Transform head;
    [SerializeField] private AudioSource breath;

    [Header("Cuando aparece")]
    [Tooltip("Miedo (0 a 1) desde el que puede aparecer.")]
    [SerializeField, Range(0f, 1f)] private float appearFromFear = 0.4f;
    [Tooltip("Segundos de juego antes de la primera vez.")]
    [SerializeField] private float firstAfter = 45f;
    [Tooltip("Segundos entre una aparicion y la siguiente.")]
    [SerializeField] private float cooldown = 75f;
    [SerializeField] private float checkEvery = 2f;
    [SerializeField, Range(0f, 1f)] private float chancePerCheck = 0.3f;

    [Header("Donde")]
    [Tooltip("Metros de camino detras del jugador: entre min y max.")]
    [SerializeField] private float minBehind = 9f;
    [SerializeField] private float maxBehind = 13f;
    [Tooltip("La niebla tapa todo a 16 m: mas lejos no se ve.")]
    [SerializeField] private float viewDistance = 15.5f;

    [Header("Que hace")]
    [Tooltip("Segundos mirandola sin parar hasta que se va.")]
    [SerializeField] private float stareToVanish = 3f;
    [Tooltip("Si te acercas a menos de esto, se va.")]
    [SerializeField] private float vanishIfCloser = 5.5f;
    [SerializeField] private float stepCloser = 2f;
    [SerializeField] private int maxSteps = 2;
    [SerializeField, Range(0f, 1f)] private float stepChance = 0.65f;
    [Tooltip("Segundos sin verla antes de decidir si da un paso o se va.")]
    [SerializeField] private float unseenToDecide = 0.6f;
    [Tooltip("Si pasa este tiempo sin que la veas, se va sola.")]
    [SerializeField] private float forgetAfter = 25f;
    [Tooltip("Grados por segundo que gira la cabeza, solo mientras la miras.")]
    [SerializeField] private float headTurnSpeed = 9f;
    [SerializeField] private float headTiltMax = 22f;

    [Header("Miedo")]
    [SerializeField] private float fearOnSight = 8f;
    [SerializeField] private float fearOnStep = 5f;
    [SerializeField] private float fearOnVanish = 3f;

    [Header("Al irse")]
    [SerializeField] private float flickerRadius = 9f;
    [SerializeField] private float flickerTime = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;
    [Tooltip("Prototipo: F6 la pone detras del jugador ahora, sin importar el miedo.")]
    [SerializeField] private bool forceKey = true;

    public State Current { get; private set; } = State.Hidden;
    public int Appearances { get; private set; }
    public string LastEvent { get; private set; } = "-";

    // Migas: por donde camino el jugador, una cada 0.3 m (las ultimas 48 m).
    private const float CrumbStep = 0.3f;
    private const int MaxCrumbs = 160;
    private readonly List<Vector3> crumbs = new List<Vector3>();
    private readonly List<Vector3> candidates = new List<Vector3>();
    private Renderer[] parts;

    private float playTime, nextCheck, availableAt;
    private float aliveTime, stare, unseenTime, losTimer;
    private bool losClear, decided, stepPending;
    private int steps;
    private float headYaw, headRoll, tiltSign = 1f;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Transform playerTransform, Camera cam, FearMeter fearMeter, LookBackLures lookBackLures,
        Transform bodyTransform, Transform headPivot, AudioSource breathSource)
    {
        player = playerTransform;
        playerCamera = cam;
        fear = fearMeter;
        lures = lookBackLures;
        body = bodyTransform;
        head = headPivot;
        breath = breathSource;
    }

    private void Awake()
    {
        if (player == null || playerCamera == null || body == null)
        {
            Debug.LogWarning("[FigurePresence] Faltan referencias. Ejecuta Callejones > Figura y trote.", this);
            enabled = false;
            return;
        }
        parts = body.GetComponentsInChildren<Renderer>(true);
        availableAt = firstAfter;
        Hide(null);
    }

    private static bool Playing() =>
        GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;

    private void Update()
    {
        if (!Playing())
        {
            if (Current != State.Hidden) Hide(null); // titulo, final: no queda nada
            crumbs.Clear();
            return;
        }

        float dt = Time.deltaTime;
        playTime += dt;
        RecordCrumb();

        if (forceKey && ForcePressed()) TryAppear(true);

        if (Current == State.Hidden)
        {
            if (playTime < availableAt || playTime < nextCheck) return;
            nextCheck = playTime + checkEvery;
            if (fear != null && fear.Normalized >= appearFromFear && Random.value < chancePerCheck) TryAppear(false);
            return;
        }

        UpdatePresent(dt);
    }

    // ------------------------------------------------------------------ aparecer

    private void RecordCrumb()
    {
        Vector3 p = player.position;
        if (crumbs.Count > 0 && (p - crumbs[crumbs.Count - 1]).sqrMagnitude < CrumbStep * CrumbStep) return;
        // Un salto grande (teletransporte al volver a empezar) corta el camino: lo viejo no sirve.
        if (crumbs.Count > 0 && (p - crumbs[crumbs.Count - 1]).sqrMagnitude > 4f) crumbs.Clear();
        crumbs.Add(p);
        if (crumbs.Count > MaxCrumbs) crumbs.RemoveAt(0);
    }

    private void TryAppear(bool forced)
    {
        if (Current != State.Hidden) Hide(null);
        if (!forced && (SignInspector.IsOpen || (lures != null && lures.Active != LookBackLures.Cue.None))) return;

        if (!FindSpotBehind(out Vector3 feet))
        {
            if (forced) LastEvent = "no hay lugar atras (camino corto o con giros)";
            return;
        }

        body.position = feet;
        FacePlayer();
        headYaw = (Random.value < 0.5f ? -1f : 1f) * Random.Range(60f, 85f); // mirando hacia un costado
        headRoll = 0f;
        tiltSign = Random.value < 0.5f ? -1f : 1f;
        ApplyHead();
        SetVisible(true);
        if (breath != null && breath.clip != null)
        {
            breath.Play();
            breath.timeSamples = Random.Range(0, breath.clip.samples);
        }

        Current = State.Waiting;
        Appearances++;
        aliveTime = stare = unseenTime = 0f;
        steps = 0;
        decided = stepPending = false;
        losTimer = 0f;
        LastEvent = forced ? "puesta con F6" : "aparecio atras";
        Debug.Log($"[FigurePresence] {LastEvent} (a {FlatDistance():0.0} m).");
    }

    /// <summary>
    /// Recorre las migas desde la mas nueva hacia atras, sumando metros de camino. Sirven las que
    /// quedan entre minBehind y maxBehind, tienen piso, estan fuera de la camara ahora y se ven
    /// desde los ojos del jugador (si se da vuelta, la ve). Elige una al azar.
    /// </summary>
    private bool FindSpotBehind(out Vector3 feet)
    {
        feet = Vector3.zero;
        candidates.Clear();
        Vector3 eye = playerCamera.transform.position;
        Vector3 prev = player.position;
        float path = 0f;

        for (int i = crumbs.Count - 1; i >= 0; i--)
        {
            path += Vector3.Distance(prev, crumbs[i]);
            prev = crumbs[i];
            if (path < minBehind) continue;
            if (path > maxBehind) break;

            if (!Physics.Raycast(crumbs[i] + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                continue;
            Vector3 p = hit.point;
            float d = Flat(p - player.position).magnitude;
            if (d < minBehind * 0.8f || d > viewDistance - 1f) continue;
            if (InFrame(p, 0.15f)) continue;                     // nunca delante de tus ojos
            if (!ClearLine(eye, p + Vector3.up * 1.6f)) continue; // si te das vuelta, se ve
            candidates.Add(p);
        }

        if (candidates.Count == 0) return false;
        feet = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    // ------------------------------------------------------------------ mientras esta

    private void UpdatePresent(float dt)
    {
        aliveTime += dt;
        bool seen = IsSeen(dt);
        float distance = FlatDistance();

        if (seen)
        {
            if (Current == State.Waiting) { AddFear(fearOnSight); LastEvent = "la viste"; }
            else if (stepPending) { AddFear(fearOnStep); LastEvent = "la viste mas cerca"; }
            stepPending = false;
            Current = State.Watched;
            unseenTime = 0f;
            decided = false;
            stare += dt;

            // La cabeza gira hacia vos y se inclina, despacio. Solo mientras la miras.
            headYaw = Mathf.MoveTowards(headYaw, 0f, headTurnSpeed * dt);
            headRoll = Mathf.MoveTowards(headRoll, tiltSign * headTiltMax, headTurnSpeed * 0.6f * dt);
            ApplyHead();

            if (stare >= stareToVanish) Vanish("la miraste fijo");
            else if (distance < vanishIfCloser) Vanish("te acercaste");
            return;
        }

        stare = 0f;
        if (Current == State.Waiting)
        {
            if (aliveTime > forgetAfter) Hide("nunca la viste");
            return;
        }

        Current = State.Unwatched;
        unseenTime += dt;
        if (!decided && unseenTime >= unseenToDecide)
        {
            decided = true;
            Decide();
        }
        else if (Current != State.Hidden && unseenTime > forgetAfter)
        {
            Hide("se fue sin que la vieras");
        }
    }

    /// <summary>Cuando dejas de mirarla: un paso hacia vos (y te mira de frente), o se va.</summary>
    private void Decide()
    {
        if (steps < maxSteps && Random.value < stepChance && TryStep()) return;
        Hide("ya no esta");
    }

    private bool TryStep()
    {
        Vector3 to = Flat(player.position - body.position);
        float d = to.magnitude;
        if (d - stepCloser < vanishIfCloser + 0.5f) return false;
        Vector3 target = body.position + to / d * stepCloser;
        if (!Physics.Raycast(target + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        target = hit.point;
        if (InFrame(target, 0.15f)) return false; // no se mueve donde la podes ver
        if (!ClearLine(playerCamera.transform.position, target + Vector3.up * 1.6f)) return false;

        body.position = target;
        FacePlayer();
        headYaw = 0f; // ahora te mira de frente, con la cabeza ya inclinada
        ApplyHead();
        steps++;
        stepPending = true;
        LastEvent = $"dio un paso ({steps})";
        return true;
    }

    private void Vanish(string reason)
    {
        // Los faroles cercanos se cortan un instante; cuando vuelven, ya no esta.
        foreach (LanternLight lantern in FindObjectsByType<LanternLight>(FindObjectsSortMode.None))
            if ((lantern.transform.position - body.position).sqrMagnitude < flickerRadius * flickerRadius)
                lantern.Dropout(flickerTime);
        AddFear(fearOnVanish);
        Hide(reason);
    }

    private void Hide(string reason)
    {
        SetVisible(false);
        if (breath != null) breath.Stop();
        bool wasPresent = Current != State.Hidden;
        Current = State.Hidden;
        if (wasPresent) availableAt = playTime + cooldown;
        if (reason != null)
        {
            LastEvent = reason;
            Debug.Log($"[FigurePresence] Se fue: {reason}.");
        }
    }

    // ------------------------------------------------------------------ vista

    /// <summary>
    /// La ves si esta en la camara, mas cerca que la niebla y sin nada en el medio (hasta la
    /// cabeza o el pecho). La linea se revisa 10 veces por segundo.
    /// </summary>
    private bool IsSeen(float dt)
    {
        Vector3 chest = body.position + Vector3.up * 1.3f;
        if (!InFrame(body.position, 0f)) return false;
        if (FlatDistance() > viewDistance) return false;
        losTimer -= dt;
        if (losTimer <= 0f)
        {
            losTimer = 0.1f;
            Vector3 eye = playerCamera.transform.position;
            losClear = ClearLine(eye, chest) || ClearLine(eye, chest + Vector3.up * 0.7f);
        }
        return losClear;
    }

    /// <summary>El punto (o la figura parada ahi) cae dentro de la imagen, con un margen.</summary>
    private bool InFrame(Vector3 feet, float margin)
    {
        for (int i = 0; i < 3; i++)
        {
            Vector3 v = playerCamera.WorldToViewportPoint(feet + Vector3.up * (i * 1.05f));
            if (v.z > 0f && v.x > -margin && v.x < 1f + margin && v.y > -margin && v.y < 1f + margin) return true;
        }
        return false;
    }

    private static bool ClearLine(Vector3 from, Vector3 to) =>
        !Physics.Linecast(from, to, ~0, QueryTriggerInteraction.Ignore);

    // ------------------------------------------------------------------ utilidades

    private void FacePlayer()
    {
        Vector3 to = Flat(player.position - body.position);
        if (to.sqrMagnitude > 0.01f) body.rotation = Quaternion.LookRotation(to);
    }

    private void ApplyHead()
    {
        if (head != null) head.localRotation = Quaternion.Euler(6f, headYaw, headRoll);
    }

    private void SetVisible(bool value)
    {
        if (parts == null) return;
        foreach (Renderer r in parts) if (r != null) r.enabled = value;
    }

    private void AddFear(float amount)
    {
        if (fear != null && amount > 0f) fear.AddFear(amount);
    }

    private float FlatDistance() => Flat(player.position - body.position).magnitude;
    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    [ContextMenu("Probar: aparecer atras")]
    private void TestAppear() => TryAppear(true);

#if ENABLE_INPUT_SYSTEM
    private static bool ForcePressed() => Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame;
#else
    private static bool ForcePressed() => Input.GetKeyDown(KeyCode.F6);
#endif

    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible || !Playing()) return;
        string state = Current == State.Hidden
            ? (playTime < availableAt ? $"oculta (disponible en {availableAt - playTime:0} s)" : "oculta")
            : $"{Current} a {FlatDistance():0.0} m, pasos {steps}, mirada {stare:0.0} s";
        GUI.Label(new Rect(12, 298, 900, 22), $"Figura: {state}, veces {Appearances}, ultimo: {LastEvent} (F6 la pone)");
    }
}
