using UnityEngine;

/// <summary>
/// Parte de CorridorTreadmill: las PLAZAS (Paso 23).
///
/// Una plaza es una pieza de 9 x 9 m (3 x 3 celdas) con cuatro bocas: se entra por una y salen
/// tres callejones. El azar la elige como a cualquier otra pieza, pero con reglas propias:
/// - Poco peso (plazaWeight) y solo cada tantos metros caminados (entre plazaGapMin y plazaGapMax).
/// - Una a la vez: si hay una plaza puesta, no aparece otra.
/// - Nunca durante la caminata del titulo, ni al armar el laberinto inicial, ni dentro de un bucle.
/// - Como cualquier pieza, solo si sus 9 celdas estan libres y ninguna boca mira hacia atras
///   (en la practica: solo cuando el camino va hacia adelante).
///
/// La trampa de la plaza repetida: si el jugador sale de una plaza con mucho miedo, Plaza puede
/// pedir (ArmPlazaRepeat) que la proxima sea IGUAL: misma disposicion, mismos carteles, mismas
/// ventanas. Esa proxima aparece enseguida (a los plazaRepeatGap metros), con un peso enorme y
/// aunque la anterior siga puesta: "otra vez la plaza del arbol", donde no deberia haber ninguna.
/// </summary>
public partial class CorridorTreadmill
{
    [Header("Plazas (Paso 23)")]
    [Tooltip("Apagar para que nunca haya plazas.")]
    [SerializeField] private bool plazasEnabled = true;
    [Tooltip("Peso de la plaza entre las demas piezas (recto 5, giro 3, cruce 2).")]
    [SerializeField] private float plazaWeight = 1.5f;
    [Tooltip("Metros caminados minimos entre una plaza y la siguiente. La primera llega a la mitad.")]
    [SerializeField] private float plazaGapMin = 70f;
    [Tooltip("Metros caminados maximos entre una plaza y la siguiente (se sortea entre los dos).")]
    [SerializeField] private float plazaGapMax = 140f;
    [Tooltip("Plaza repetida: metros despues de salir de la anterior.")]
    [SerializeField] private float plazaRepeatGap = 9f;
    [Tooltip("Plaza repetida: su peso (casi seguro que es la proxima pieza que entre).")]
    [SerializeField] private float plazaRepeatWeight = 60f;

    /// <summary>Cuantas plazas aparecieron en la partida.</summary>
    public int PlazasPlaced { get; private set; }
    /// <summary>true si la proxima plaza va a ser una copia de la ultima.</summary>
    public bool PlazaRepeatArmed => plazaRepeat != null;

    private float nextPlazaAt;
    private int[] plazaRepeat;     // las opciones de cada RandomPick de la plaza a copiar
    private float plazaRepeatAt;
    private bool plazaForced;
    private GameFlow flow;
    private bool flowSearched;

    private void InitPlazas()
    {
        nextPlazaAt = RandomPlazaGap() * 0.5f;
    }

    private float RandomPlazaGap() => Mathf.Lerp(plazaGapMin, plazaGapMax, (float)rng.NextDouble());

    /// <summary>Para probar: la proxima conexion que se rellene adelante puede ser una plaza.</summary>
    [ContextMenu("Probar plaza ahora")]
    public void ArmPlazaNow()
    {
        plazaForced = true;
        Debug.Log("[CorridorTreadmill] Plaza pedida a mano.");
    }

    /// <summary>
    /// Pide que la proxima plaza sea igual a esta (la trampa de la plaza repetida). Lo llama
    /// Plaza cuando el jugador sale con mucho miedo.
    /// </summary>
    public void ArmPlazaRepeat(Plaza source)
    {
        if (plazaRepeat != null || source == null) return;
        plazaRepeat = source.GetPicks();
        plazaRepeatAt = walked + plazaRepeatGap;
        Debug.Log($"[CorridorTreadmill] La proxima plaza va a ser igual a '{source.name}'.");
    }

    /// <summary>Peso de la plaza para la conexion que se esta rellenando (0 = no puede ir).</summary>
    private float PlazaWeight()
    {
        if (!plazasEnabled || !ready || activePlan != null) return 0f;
        if (plazaForced) return 1000f;
        if (!InPlay()) return 0f;
        if (plazaRepeat != null) return walked >= plazaRepeatAt ? plazaRepeatWeight : 0f;
        if (walked < nextPlazaAt) return 0f;
        foreach (Node n in nodes.Values) if (n.seg.Kind == SegmentKind.Plaza) return 0f; // una a la vez
        return plazaWeight;
    }

    /// <summary>Durante el titulo (la caminata sola) no hay plazas.</summary>
    private bool InPlay()
    {
        if (!flowSearched)
        {
            flow = FindAnyObjectByType<GameFlow>();
            flowSearched = true;
        }
        return flow == null || flow.Current == GameFlow.State.Playing;
    }

    private void OnPlazaPlaced(CorridorSegment segment)
    {
        PlazasPlaced++;
        plazaForced = false;
        nextPlazaAt = walked + RandomPlazaGap();
        Plaza plaza = segment.GetComponent<Plaza>();
        if (plaza == null) return;
        plaza.IsRepeat = plazaRepeat != null;
        if (plazaRepeat != null)
        {
            plaza.ApplyPicks(plazaRepeat);
            plazaRepeat = null;
            Debug.Log($"[CorridorTreadmill] Aparecio la plaza repetida: '{segment.name}'.");
        }
        else
        {
            Debug.Log($"[CorridorTreadmill] Aparecio una plaza: '{segment.name}'.");
        }
    }

    private string PlazaDebugText()
    {
        string next = plazaRepeat != null ? "la proxima es la repetida"
            : walked >= nextPlazaAt ? "puede aparecer" : $"faltan {nextPlazaAt - walked:0} m";
        return $"Plazas: {PlazasPlaced} ({next})";
    }
}
