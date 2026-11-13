using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 33: el transito de la calle de la feria. Tiene un deposito de autos (hijos apagados) y
/// dos carriles, uno por sentido. Cada tanto saca un auto al azar, lo pone al principio de un
/// carril (lejos, en la niebla) y lo guarda cuando llega al otro extremo.
///
/// - Al empezar ya hay algunos autos andando, para que la calle no arranque vacia.
/// - El colectivo de la parada: al empezar el capitulo esta parado en la parada (el jugador acaba
///   de bajarse) y arranca a los pocos segundos. Despues vuelve al deposito como cualquier otro.
/// - Ningun auto sale si el ultimo de ese carril todavia esta cerca del principio.
///
/// Va en el objeto "Transito". Lo arma Callejones > Feria de dia (capitulo 1). Los carriles se
/// pueden ajustar en el Inspector (z, sentido, desde donde y hasta donde, cada cuanto).
/// </summary>
public class FairTraffic : MonoBehaviour
{
    [System.Serializable]
    public class Lane
    {
        public string name;
        [Tooltip("Centro del carril en Z.")]
        public float z;
        [Tooltip("1 = hacia +X (este), -1 = hacia -X (oeste).")]
        public float direction = 1f;
        [Tooltip("Donde aparecen y donde desaparecen los autos (en X).")]
        public float fromX = -115f, toX = 115f;
        [Tooltip("Segundos entre un auto y el siguiente (minimo y maximo).")]
        public Vector2 every = new Vector2(3f, 9f);
        [System.NonSerialized] public float next;
    }

    [SerializeField] private Lane[] lanes;
    [SerializeField] private TrafficCar[] cars;

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
    public void Setup(Lane[] laneList, TrafficCar[] carList, TrafficCar bus, int laneOfBus, float stopX)
    {
        lanes = laneList;
        cars = carList;
        startBus = bus;
        busLane = laneOfBus;
        busStopX = stopX;
    }

    private void Start()
    {
        if (lanes == null || cars == null) return;
        foreach (TrafficCar c in cars) if (c != null) c.Park();

        if (startBus != null && busLane >= 0 && busLane < lanes.Length)
        {
            Lane l = lanes[busLane];
            startBus.Launch(this, busLane, busStopX, l.z, l.direction, l.toX, 0f, busDeparts);
            driving.Add(startBus);
        }

        for (int i = 0; i < lanes.Length; i++)
        {
            Lane l = lanes[i];
            for (int k = 0; k < prefillPerLane; k++)
            {
                // Repartidos a lo largo del carril, lejos de la parada.
                float u = (k + 0.3f + Random.value * 0.4f) / prefillPerLane;
                float x = Mathf.Lerp(l.fromX, l.toX, u);
                if (Mathf.Abs(x - busStopX) < 30f) continue;
                TrafficCar c = Pick(false);
                if (c == null) break;
                c.Launch(this, i, x, l.z, l.direction, l.toX, 99f);
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
            c.Launch(this, i, l.fromX, l.z, l.direction, l.toX, 99f);
            driving.Add(c);
        }
    }

    /// <summary>Metros libres hasta el auto de adelante en el mismo carril (sin contar largos).</summary>
    public float GapAhead(TrafficCar car)
    {
        float best = float.MaxValue;
        float x = car.transform.position.x;
        float dir = lanes[car.Lane].direction;
        foreach (TrafficCar other in driving)
        {
            if (other == car || other.Lane != car.Lane) continue;
            float d = (other.transform.position.x - x) * dir;
            if (d <= 0f) continue;
            d -= (other.Length + car.Length) * 0.5f;
            if (d < best) best = d;
        }
        return best;
    }

    public void Recycle(TrafficCar car)
    {
        driving.Remove(car);
        car.Park();
    }

    private bool EntryClear(int lane, Lane l)
    {
        foreach (TrafficCar c in driving)
            if (c.Lane == lane && Mathf.Abs(c.transform.position.x - l.fromX) < 16f + c.Length) return false;
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
            Gizmos.DrawLine(new Vector3(l.fromX, 0.3f, l.z), new Vector3(l.toX, 0.3f, l.z));
            Gizmos.DrawSphere(new Vector3(l.toX, 0.3f, l.z), 0.5f);
        }
    }
}
