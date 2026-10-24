using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// El "director" del laberinto. Cuando el jugador completa una racha de pasos,
/// elige un modulo en punto ciego (por defecto, a su espalda) y le cambia la variante.
/// Si en ese momento no hay ningun punto ciego, espera hasta que aparezca uno:
/// el cambio ocurre en cuanto el jugador aparta la mirada.
/// Va en el objeto "Laberinto", junto a VisibilityChecker.
/// </summary>
public class MazeDirector : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El StepCounter del Player. El director escucha su evento de racha completa.")]
    [SerializeField] private StepCounter stepCounter;
    [Tooltip("Si se deja vacio, usa el VisibilityChecker de este mismo objeto.")]
    [SerializeField] private VisibilityChecker visibility;

    [Header("Reglas")]
    [Tooltip("Solo cambiar modulos que esten detras del jugador.")]
    [SerializeField] private bool onlyBehind = true;
    [Tooltip("Si no hay punto ciego al completar la racha, esperar a que aparezca uno.")]
    [SerializeField] private bool waitForBlindSpot = true;
    [Tooltip("Segundos maximos esperando un punto ciego antes de cancelar el cambio.")]
    [SerializeField] private float maxWaitTime = 10f;

    [Header("Eventos")]
    [Tooltip("Se dispara cada vez que el laberinto cambia. Aca se conecta el medidor de miedo.")]
    public UnityEvent onMutation;

    public int MutationCount { get; private set; }

    private bool pending;
    private float pendingTimer;

    private void Awake()
    {
        if (visibility == null) visibility = GetComponent<VisibilityChecker>();

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
    }

    private void OnDisable()
    {
        if (stepCounter != null) stepCounter.onStreakReached.RemoveListener(RequestMutation);
    }

    /// <summary>Pide un cambio. Lo llama StepCounter al completar la racha.</summary>
    public void RequestMutation()
    {
        if (TryMutate()) return;

        if (waitForBlindSpot)
        {
            pending = true;
            pendingTimer = 0f;
            Debug.Log("[MazeDirector] No hay punto ciego ahora. Esperando a que el jugador aparte la mirada...");
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
        List<CorridorSegment> candidates = visibility.GetBlindSpotSegments(onlyBehind);
        candidates.RemoveAll(s => s.VariantCount < 2); // sin alternativas no hay nada que cambiar

        if (candidates.Count == 0) return false;

        CorridorSegment target = candidates[Random.Range(0, candidates.Count)];
        int newVariant = PickDifferentVariant(target);
        target.SetVariant(newVariant);

        MutationCount++;
        Debug.Log($"[MazeDirector] Cambio #{MutationCount}: '{target.name}' pasa a la variante {newVariant}.");
        onMutation?.Invoke();
        return true;
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
