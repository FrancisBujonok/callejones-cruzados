using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 33: el transito alrededor de la feria. Tiene un deposito de autos (hijos apagados) y
/// varios carriles. Cada tanto saca un auto al azar, lo pone al principio de un carril (lejos, en
/// la niebla) y lo guarda cuando llega al otro extremo.
///
/// - Al empezar ya hay algunos autos andando, para que la calle no arranque vacia.
/// - El colectivo de la parada: al empezar el capitulo esta parado en la parada (el jugador acaba
///   de bajarse) y arranca a los pocos segundos. Despues vuelve al deposito como cualquier otro.
/// - Ningun auto sale si el ultimo de ese carril todavia esta cerca del principio.
///
/// Paso 35: hay un cruce con semaforos (TrafficSignals) detras del comienzo. Los carriles pueden
/// ir a lo largo de X (la calle de la feria) o de Z (la transversal); cada carril que pasa por el
/// cruce tiene su linea de detencion y su grupo de semaforo.
///
/// Va en el objeto "Transito". Lo arma Callejones > Feria de dia (capitulo 1). Los carriles se
/// pueden ajustar en el Inspector.
/// </summary>
public class FairTraffic : MonoBehaviour
{
    [System.Serializable]
    public class Lane
    {
        public string name;
        [Tooltip("Prendido: el carril va a lo largo de Z (la transversal). Apagado: a lo largo de X.")]
        public bool alongZ;
        [Tooltip("Centro del carril en el otro eje (Z si va a lo largo de X, X si va a lo largo de Z).")]
        public float z;
        [Tooltip("1 = hacia +X (o +Z), -1 = hacia -X (o -Z).")]
        public float direction = 1f;
        [Tooltip("Donde aparecen y donde desaparecen los autos (a lo largo del carril).")]
        public float fromX = -115f, toX = 115f;
        [Tooltip("Segundos entre un auto y el siguiente (minimo y maximo).")]
        public Vector2 every = new Vector2(3f, 9f);
        [Tooltip("Grupo de semaforo: -1 = ninguno, 0 = calle principal, 1 = transversal.")]
        public int signalGroup = -1;
        [Tooltip("Donde tiene que quedar la trompa con el semaforo en rojo (a lo largo del carril).")]
        public float stopAt;
        [System.NonSerialized] public float next;
    }

    [SerializeField] private Lane[] lanes;
    [SerializeField] private TrafficCar[] cars;
    [SerializeField] private TrafficSignals signals;

    [Header("El colectivo de la parada")]
    [SerializeField] private TrafficCar startBus;
    [SerializeField] private int busLane;
    [SerializeField] private float busStopX = -64f;
    [Tooltip("Segundos parado antes de arrancar (la tarjeta del capitulo dura unos 3.5).")]
    [SerializeField] private float busDeparts = 5.5f;

    [Tooltip("Cuantos autos ya andando al empezar, en cada carril.")]
    [SerializeField] private int prefillPerLane = 2;

    private readonly List<TrafficCar> driving = new List<TrafficCar>();

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Lane[] laneList, TrafficCar[] carList, TrafficCar bus, int laneOfBus, float stopX, TrafficSignals lights = null)
    {
        lanes = laneList;
        cars = carList;
        startBus = bus;
        busLane = laneOfBus;
        busStopX = stopX;
        signals = lights;
    }

    private void Start()
    {
        if (lanes == null || cars == null) return;
        foreach (TrafficCar c in cars) if (c != null) c.Park();

        if (startBus != null && busLane >= 0 && busLane < lanes.Length)
        {
            Lane l = lanes[busLane];
            startBus.Launch(this, busLane, busStopX, l.z, l.alongZ, l.direction, l.toX, 0f, busDeparts);
            driving.Add(startBus);
        }

        for (int i = 0; i < lanes.Length; i++)
        {
            Lane l = lanes[i];
            for (int k = 0; k < prefillPerLane; k++)
            {
                // Repartidos a lo largo del carril, lejos de la parada y del cruce.
                float u = (k + 0.3f + Random.value * 0.4f) / prefillPerLane;
                float along = Mathf.Lerp(l.fromX, l.toX, u);
                if (!l.alongZ && Mathf.Abs(along - busStopX) < 30f) continue;
                if (l.signalGroup >= 0 && Mathf.Abs(along - l.stopAt) < 25f) continue;
                TrafficCar c = Pick(false);
                if (c == null) break;
                c.Launch(this, i, along, l.z, l.alongZ, l.direction, l.toX, 99f);
                driving.Add(c);
            }
            l.next = Time.time + Random.Range(l.every.x, l.every.y);
        }
    }

    private void Update()
    {
        if (lanes == null) return;
        for (int i = 0; i < lanes.Length; i++)
        {
            Lane l = lanes[i];
            if (Time.time < l.next) continue;
            l.next = Time.time + Random.Range(l.every.x, l.every.y);
            if (!EntryClear(i, l)) continue;
            TrafficCar c = Pick(true);
            if (c == null) continue;
            c.Launch(this, i, l.fromX, l.z, l.alongZ, l.direction, l.toX, 99f);
            driving.Add(c);
        }
    }

    /// <summary>Metros libres hasta el auto de adelante en el mismo carril (sin contar largos).</summary>
    public float GapAhead(TrafficCar car)
    {
        float best = float.MaxValue;
        float along = car.Along;
        float dir = car.Direction;
        foreach (TrafficCar other in driving)
        {
            if (other == car || other.Lane != car.Lane) continue;
            float d = (other.Along - along) * dir;
            if (d <= 0f) continue;
            d -= (other.Length + car.Length) * 0.5f;
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>
    /// Metros hasta la linea de detencion si el semaforo de su carril no lo deja pasar; si puede
    /// pasar (verde, o amarillo y ya esta muy cerca para frenar) o ya la paso, float.MaxValue.
    /// </summary>
    public float StopDistance(TrafficCar car)
    {
        if (signals == null || car.Lane < 0) return float.MaxValue;
        Lane l = lanes[car.Lane];
        if (l.signalGroup < 0) return float.MaxValue;
        float d = (l.stopAt - car.Front) * car.Direction;
        if (d < -0.5f) return float.MaxValue; // ya la paso: sigue
        TrafficSignals.Signal s = signals.State(l.signalGroup);
        if (s == TrafficSignals.Signal.Go) return float.MaxValue;
        if (s == TrafficSignals.Signal.Yellow && d < car.Speed * 1.1f) return float.MaxValue;
        return Mathf.Max(0f, d);
    }

    public void Recycle(TrafficCar car)
    {
        driving.Remove(car);
        car.Park();
    }

    private bool EntryClear(int lane, Lane l)
    {
        foreach (TrafficCar c in driving)
            if (c.Lane == lane && Mathf.Abs(c.Along - l.fromX) < 16f + c.Length) return false;
        return true;
    }

    /// <summary>Uno del deposito al azar, segun su peso. withBus: si el colectivo puede salir.</summary>
    private TrafficCar Pick(bool withBus)
    {
        float total = 0f;
        foreach (TrafficCar c in cars)
            if (c != null && !c.Driving && (withBus || c != startBus)) total += c.Weight;
        if (total <= 0f) return null;
        float r = Random.value * total;
        foreach (TrafficCar c in cars)
        {
            if (c == null || c.Driving || (!withBus && c == startBus)) continue;
            r -= c.Weight;
            if (r <= 0f) return c;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (lanes == null) return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f);
        foreach (Lane l in lanes)
        {
            Vector3 P(float a) => l.alongZ ? new Vector3(l.z, 0.3f, a) : new Vector3(a, 0.3f, l.z);
            Gizmos.DrawLine(P(l.fromX), P(l.toX));
            Gizmos.DrawSphere(P(l.toX), 0.5f);
            if (l.signalGroup >= 0) { Gizmos.color = Color.red; Gizmos.DrawCube(P(l.stopAt), new Vector3(0.6f, 0.1f, 0.6f)); Gizmos.color = new Color(1f, 0.85f, 0.2f); }
        }
    }
}
