using UnityEngine;

/// <summary>Lo que puede estar distinto en la ultima pasada de un bucle.</summary>
public enum Anomaly
{
    FarolApagado,          // el farol de la pieza no tiene luz ni zumbido
    PersianaEntreabierta,  // la persiana del fondo esta levantada hasta la mitad: abajo, oscuridad
    OtraDecoracion         // los objetos de la pieza no son los mismos
}

/// <summary>
/// Va en las piezas de 3 x 3 (Esquina, Cruce, Fondo). Guarda las partes que una anomalia
/// prende o apaga y sabe dejarlas como nuevas.
/// - CorridorTreadmill llama a Apply en la ultima pasada de un bucle.
/// - CorridorSegment.ResetForReuse llama a Clear cada vez que la pieza se reutiliza.
/// La anomalia OtraDecoracion no necesita nada de aca: la resuelve el generador con DecorRandomizer.
/// Las referencias las pone la herramienta Callejones > Laberinto con giros.
/// </summary>
public class SegmentAnomalies : MonoBehaviour
{
    [Tooltip("El farol de la pieza.")]
    [SerializeField] private LanternLight lantern;
    [Tooltip("El panel de la persiana bajada (el normal).")]
    [SerializeField] private GameObject shutterDown;
    [Tooltip("El panel corto, levantado hasta la mitad. Apagado normalmente.")]
    [SerializeField] private GameObject shutterHalf;
    [Tooltip("El hueco oscuro que queda debajo de la persiana levantada. Apagado normalmente.")]
    [SerializeField] private GameObject darkGap;

    /// <summary>Si esta pieza tiene lo necesario para esa anomalia.</summary>
    public bool Supports(Anomaly anomaly) => anomaly switch
    {
        Anomaly.FarolApagado => lantern != null,
        Anomaly.PersianaEntreabierta => shutterDown != null && shutterHalf != null,
        _ => true
    };

    /// <summary>Lo usa la herramienta de editor al armar el prefab.</summary>
    public void Setup(LanternLight newLantern, GameObject down, GameObject half, GameObject gap)
    {
        lantern = newLantern;
        shutterDown = down;
        shutterHalf = half;
        darkGap = gap;
    }

    /// <summary>Todo normal: farol prendido y persiana bajada.</summary>
    public void Clear()
    {
        if (lantern != null) lantern.SetOff(false);
        if (shutterDown != null) shutterDown.SetActive(true);
        if (shutterHalf != null) shutterHalf.SetActive(false);
        if (darkGap != null) darkGap.SetActive(false);
    }

    public void Apply(Anomaly anomaly)
    {
        switch (anomaly)
        {
            case Anomaly.FarolApagado:
                if (lantern != null) lantern.SetOff(true);
                break;

            case Anomaly.PersianaEntreabierta:
                if (shutterDown != null) shutterDown.SetActive(false);
                if (shutterHalf != null) shutterHalf.SetActive(true);
                if (darkGap != null) darkGap.SetActive(true);
                break;
        }
    }
}
