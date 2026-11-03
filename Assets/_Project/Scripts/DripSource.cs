using UnityEngine;

/// <summary>
/// Paso 21: un punto de donde gotea agua (el borde de un toldo, el desague de un aire
/// acondicionado, una chapa del techado, ropa mojada). Solo guarda datos: las gotas, las ondas
/// y el sonido los maneja DripSystem (en la Main Camera), que recorre los puntos activos.
///
/// Va en un objeto vacio dentro de la pieza, en el lugar de donde cae la gota. Si la pieza o la
/// altura del edificio que lo contiene se apagan, el punto deja de gotear (OnDisable).
///
/// Paso 22: solo los puntos con charco abajo dejan onda al caer en el piso (ripple); los demas
/// (los aires de los pisos altos) solo suenan.
/// </summary>
public class DripSource : MonoBehaviour
{
    [Tooltip("Segundos entre gota y gota (al azar entre estos dos valores).")]
    [SerializeField] private Vector2 interval = new Vector2(3f, 8f);
    [Tooltip("Techo que puede haber debajo (el tramo techado). Si esta prendido, la gota cae sobre la chapa.")]
    [SerializeField] private GameObject roof;
    [Tooltip("Altura de ese techo, medida desde el piso de la pieza.")]
    [SerializeField] private float roofHeight = 3.97f;
    [Tooltip("Si hay un charco abajo: al caer en el piso deja una onda. Sin charco, solo suena.")]
    [SerializeField] private bool ripple = true;

    /// <summary>Cuando cae la proxima gota (Time.time). Lo maneja DripSystem.</summary>
    public float NextTime { get; set; }
    public Vector2 Interval => interval;
    public bool Ripple => ripple;

    private Transform piece;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Vector2 newInterval, GameObject newRoof, float newRoofHeight, bool newRipple = true)
    {
        interval = newInterval;
        roof = newRoof;
        roofHeight = newRoofHeight;
        ripple = newRipple;
    }

    /// <summary>Altura del piso de la pieza (todas las piezas tienen el piso en y = 0).</summary>
    public float FloorY => piece != null ? piece.position.y : 0f;

    /// <summary>Si hay un techo prendido debajo, su altura en el mundo; si no, NaN.</summary>
    public float RoofY
    {
        get
        {
            if (roof == null || !roof.activeInHierarchy) return float.NaN;
            float y = FloorY + roofHeight;
            return transform.position.y > y + 0.05f ? y : float.NaN;
        }
    }

    private void OnEnable()
    {
        CorridorSegment segment = GetComponentInParent<CorridorSegment>();
        piece = segment != null ? segment.transform : transform.root;
        // Que no goteen todos juntos al prender la pieza.
        NextTime = Time.time + Random.Range(0.3f, interval.y);
        DripSystem.Register(this);
    }

    private void OnDisable()
    {
        DripSystem.Unregister(this);
    }
}
