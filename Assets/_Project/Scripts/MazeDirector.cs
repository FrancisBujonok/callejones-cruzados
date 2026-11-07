using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// El "director" del laberinto. Cuando el jugador completa una racha de pasos,
/// elige un modulo en punto ciego (por defecto, el mas cercano a su espalda),
/// le cambia la variante y lo marca como "cambio sin ver".
/// Si en ese momento no hay ningun punto ciego, espera hasta que aparezca uno.
///
/// Paso 24: girar sin parar ya no tiene premio asegurado.
/// - Despues de una mirada atras (ParanoiaSensor.onLookBack), el cambio espera waitAfterLookBack
///   segundos: el laberinto cambia cuando dejas de mirar, no mientras miras. Si la racha se
///   completa en ese rato, el pedido queda pendiente (la misma espera de maxWaitTime).
/// - Con "Nearest", una parte de las veces (randomChance) elige un punto ciego al azar: el
///   cambio no esta siempre en la pieza justo detras.
///
/// Va en el objeto "Laberinto", junto a VisibilityChecker.
/// </summary>
public class MazeDirector : MonoBehaviour
{
    public enum TargetSelection
    {
        Nearest, // el punto ciego mas cercano: el jugador lo nota al darse vuelta
        Random   // cualquiera: mas impredecible, pero muchos cambios pasan desapercibidos
    }

    [Header("Referencias")]
    [Tooltip("El StepCounter del Player. El director escucha su evento de racha completa.")]
    [SerializeField] private StepCounter stepCounter;
    [Tooltip("Si se deja vacio, usa el VisibilityChecker de este mismo objeto.")]
    [SerializeField] private VisibilityChecker visibility;
    [Tooltip("Si se deja vacio, usa el ParanoiaSensor del mismo objeto que el StepCounter (el Player).")]
    [SerializeField] private ParanoiaSensor paranoia;

    [Header("Reglas")]
    [Tooltip("Como elegir el modulo a cambiar entre todos los puntos ciegos.")]
    [SerializeField] private TargetSelection selection = TargetSelection.Nearest;
    [Tooltip("Paso 24: con Nearest, probabilidad de elegir al azar en vez del mas cercano.")]
    [SerializeField, Range(0f, 1f)] private float randomChance = 0.3f;
    [Tooltip("Solo cambiar modulos que esten detras del jugador.")]
    [SerializeField] private bool onlyBehind = true;
    [Tooltip("Paso 24: segundos despues de una mirada atras en los que no se cambia nada.")]
    [SerializeField] private float waitAfterLookBack = 1.5f;
    [Tooltip("Si no hay punto ciego al completar la racha, esperar a que aparezca uno.")]
    [SerializeField] private bool waitForBlindSpot = true;
    [Tooltip("Segundos maximos esperando un punto ciego antes de cancelar el cambio.")]
    [SerializeField] private float maxWaitTime = 10f;

    [Header("Eventos")]
    [Tooltip("Se dispara cada vez que el laberinto cambia (aunque el jugador no lo vea). Util para sonidos.")]
    public UnityEvent onMutation;

    public int MutationCount { get; private set; }
    /// <summary>El ultimo modulo que cambio. Lo usa el sonido para ubicar el ruido lejano.</summary>
    public CorridorSegment LastMutated { get; private set; }
    /// <summary>Paso 24: true mientras dura la espera despues de una mirada atras.</summary>
    public bool JustLookedBack => Time.time - lastLookBack < waitAfterLookBack;

    private bool pending;
    private float pendingTimer;
    private bool pickRandom;          // se sortea una vez por pedido, no en cada intento
    private float lastLookBack = -999f;
    // El generador de caminos sabe como estan conectadas las piezas: lo usamos para no
    // cerrar un pasillo que deje al jugador encerrado.
    private CorridorTreadmill paths;

    private void Awake()
    {
        if (visibility == null) visibility = GetComponent<VisibilityChecker>();
        if (paranoia == null && stepCounter != null) paranoia = stepCounter.GetComponent<ParanoiaSensor>();
        paths = GetComponent<CorridorTreadmill>();

        if (stepCounter == null || visibility == null)
        {
            Debug.LogError("[MazeDirector] Falta asignar Step Counter o Visibility en el Inspector.", this);
            enabled = false;
        }
    }

    // Suscribirse al habilitarse y desuscribirse al deshabilitarse:
    // asi nunca queda un "oyente" colgado de un objeto que ya no existe.
    private void OnEnable()
    {
        if (stepCounter != null) stepCounter.onStreakReached.AddListener(RequestMutation);
        if (paranoia != null) paranoia.onLookBack.AddListener(OnLookBack);
    }

    private void OnDisable()
    {
        if (stepCounter != null) stepCounter.onStreakReached.RemoveListener(RequestMutation);
        if (paranoia != null) paranoia.onLookBack.RemoveListener(OnLookBack);
    }

    private void OnLookBack() => lastLookBack = Time.time;

    /// <summary>Pide un cambio. Lo llama StepCounter al completar la racha.</summary>
    public void RequestMutation()
    {
        pickRandom = selection == TargetSelection.Random || Random.value < randomChance;
        if (TryMutate()) return;

        if (waitForBlindSpot)
        {
            pending = true;
            pendingTimer = 0f;
            Debug.Log(JustLookedBack
                ? "[MazeDirector] Acaba de mirar atras: el cambio espera a que deje de mirar..."
                : "[MazeDirector] No hay punto ciego ahora. Esperando a que el jugador aparte la mirada...");
        }
    }

    private void Update()
    {
        if (!pending) return;

        pendingTimer += Time.deltaTime;

        if (TryMutate())
        {
            pending = false;
        }
        else if (pendingTimer >= maxWaitTime)
        {
            pending = false;
            Debug.Log("[MazeDirector] Cambio cancelado: el jugador nunca aparto la mirada.");
        }
    }

    private bool TryMutate()
    {
        // Paso 24: mientras dura la mirada atras, nada cambia.
        if (JustLookedBack) return false;

        List<CorridorSegment> candidates = visibility.GetBlindSpotSegments(onlyBehind);

        // Sin alternativas no hay nada que cambiar. Y si ya tiene un cambio sin ver,
        // cambiarlo de nuevo lo podria devolver a como estaba y el susto se perderia.
        // Las piezas de un bucle tampoco: sus copias tienen que verse identicas.
        candidates.RemoveAll(s => s.VariantCount < 2 || s.HasUnseenChange || s.Locked);

        // Se prueban candidatos hasta encontrar uno cuyo cambio no encierre al jugador.
        CorridorSegment target = null;
        int newVariant = 0;
        while (candidates.Count > 0)
        {
            CorridorSegment option = pickRandom
                ? candidates[Random.Range(0, candidates.Count)]
                : FindNearest(candidates);
            int variant = PickDifferentVariant(option);

            bool traps = option.WouldBlock(variant) && paths != null && !paths.IsSafeToBlock(option);
            if (!traps)
            {
                target = option;
                newVariant = variant;
                break;
            }
            candidates.Remove(option);
        }

        if (target == null) return false;

        target.SetVariant(newVariant);
        target.MarkChanged();

        MutationCount++;
        LastMutated = target;
        Debug.Log($"[MazeDirector] Cambio #{MutationCount} ({(pickRandom ? "al azar" : "el mas cercano")}): '{target.name}' pasa a la variante {newVariant}.");
        onMutation?.Invoke();
        return true;
    }

    private CorridorSegment FindNearest(List<CorridorSegment> candidates)
    {
        Vector3 playerPos = stepCounter.transform.position;
        CorridorSegment best = null;
        float bestDistance = float.MaxValue;

        foreach (CorridorSegment s in candidates)
        {
            float d = s.GetWorldBounds().SqrDistance(playerPos);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = s;
            }
        }
        return best;
    }

    /// <summary>
    /// Elige al azar cualquier variante menos la actual.
    /// Truco: sortea entre (cantidad - 1) opciones y, si cae en la actual o despues, suma 1.
    /// </summary>
    private static int PickDifferentVariant(CorridorSegment segment)
    {
        int r = Random.Range(0, segment.VariantCount - 1); // int: el maximo queda excluido
        return r >= segment.CurrentVariant ? r + 1 : r;
    }
}
