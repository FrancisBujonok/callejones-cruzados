using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Modo demostracion" del inicio (Paso 17): mientras se ve el menu, el jugador camina solo por
/// el laberinto y la ventana del inicio (MenuCamera) muestra lo que ve.
///
/// Camina el Player de verdad, asi que el laberinto se arma, cambia y miente igual que en una
/// partida: la ventana nunca muestra dos veces el mismo callejon. No importa por donde termine:
/// al tocar Entrar la escena se recarga y la partida empieza de cero.
///
/// Como elige el camino: el piso es una grilla de celdas de 3 m (la misma del generador).
/// Desde el centro de una celda tira un rayo hacia adelante, a la izquierda y a la derecha:
/// si el rayo no choca contra una pared o una persiana, por ahi se puede seguir. Elige al azar,
/// prefiriendo seguir derecho. Si no hay paso (callejon sin salida), da la vuelta.
///
/// Tomas: cada tanto la ventana corta a negro, el caminante salta unos metros hacia adelante y
/// cambia la "toma": caminar mirando adelante, mirar las fachadas de un costado, mirar los
/// faroles, quedarse quieto mirando alrededor, o caminar bajito, a la altura de un gato.
///
/// Va en el Player. Esta apagado salvo en el inicio (lo prende GameFlow con Begin).
/// </summary>
public class TitleWalker : MonoBehaviour
{
    private enum Shot { Caminar, Fachadas, Faroles, Quieto, Bajito }

    [Header("Referencias")]
    [Tooltip("La Main Camera (mira arriba y abajo).")]
    [SerializeField] private Transform head;
    [SerializeField] private CorridorTreadmill treadmill;
    [SerializeField] private MenuCamera view;

    [Header("Caminata")]
    [Tooltip("Metros por segundo. Mas lento que el jugador: es un paseo.")]
    [SerializeField] private float walkSpeed = 1.3f;
    [Tooltip("Grados por segundo al doblar.")]
    [SerializeField] private float turnSpeed = 70f;
    [SerializeField, Range(0f, 1f)] private float straightChance = 0.6f;
    [Tooltip("Metros hacia adelante a los que mira mientras camina: asi empieza a girar antes de la esquina.")]
    [SerializeField] private float lookAhead = 2.2f;

    [Header("Tomas")]
    [Tooltip("Duracion de cada toma en segundos (minimo y maximo).")]
    [SerializeField] private Vector2 shotLength = new Vector2(8f, 12f);
    [Tooltip("Celdas de 3 m que salta hacia adelante en cada corte (minimo y maximo).")]
    [SerializeField] private Vector2Int skipCells = new Vector2Int(1, 3);
    [SerializeField] private float cutOut = 0.45f;
    [SerializeField] private float cutIn = 0.8f;
    [Tooltip("Cuanto baja la camara en la toma bajita, en metros.")]
    [SerializeField] private float catHeight = 0.95f;

    private const float Cell = 3f;
    private const float RayLength = 2.6f;

    private readonly List<Vector3> points = new List<Vector3>(); // centros de celda por recorrer
    private Vector3 lastDir;      // direccion del ultimo tramo planeado
    private Vector3 currentDir;   // direccion del tramo que se esta caminando
    private float floorY, rayY;
    private float travelYaw;      // hacia donde va el cuerpo (sigue el camino, suave)
    private float shotYaw, shotPitch, shotSpeed = 1f;
    private float yawOffset, pitchNow;
    private float sideSign = 1f;
    private float seed;
    private Shot shot = Shot.Caminar;
    private Coroutine shots;
    private bool planned;

    public void Setup(Transform headTransform, CorridorTreadmill corridor, MenuCamera menuCamera)
    {
        head = headTransform;
        treadmill = corridor;
        view = menuCamera;
    }

    /// <summary>Empieza a caminar y prende la ventana del inicio.</summary>
    public void Begin()
    {
        // Mientras camina solo, el CharacterController no hace falta (nadie lo empuja contra
        // las paredes) y apagado no dispara la salida ni otros sensores por contacto.
        var body = GetComponent<CharacterController>();
        if (body != null) body.enabled = false;

        // Altura del piso: los rayos van a la altura del pecho, por debajo de los carteles colgados.
        floorY = transform.position.y;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            floorY = hit.point.y;
        rayY = floorY + 1.1f;

        travelYaw = transform.eulerAngles.y;
        seed = Random.value * 100f;
        points.Clear();
        planned = false;
        enabled = true;

        if (view != null) view.ShowWindow(true);
        shots = StartCoroutine(Shots());
    }

    public void Stop()
    {
        enabled = false;
        if (shots != null) StopCoroutine(shots);
        if (view != null) view.ShowWindow(false);
    }

    // ------------------------------------------------------------------ cada frame

    private void Update()
    {
        if (treadmill != null && !treadmill.Ready) return; // el laberinto todavia se esta armando
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (!planned)
        {
            // El "adelante" del laberinto: las celdas estan alineadas con el.
            lastDir = currentDir = Flat(treadmill != null ? treadmill.ForwardAxis : transform.forward).normalized;
            // El jugador empieza en el borde entre dos celdas (el centro del primer recto):
            // primero camina hasta el centro de la celda de adelante, y desde ahi planea.
            if (treadmill != null)
            {
                Vector3 first = treadmill.CellCenter(transform.position + lastDir * 0.2f);
                points.Add(new Vector3(first.x, transform.position.y, first.z));
            }
            Plan();
            planned = true;
        }

        // 1. Hacia donde va: un punto unos metros mas adelante sobre el camino planeado.
        Vector3 pos = transform.position;
        Vector3 ahead = Flat(PointAhead(pos, lookAhead) - pos);
        if (ahead.sqrMagnitude > 0.0001f)
            travelYaw = Mathf.MoveTowardsAngle(travelYaw, Yaw(ahead), turnSpeed * dt);

        // 2. Avanzar. Si el tramo que sigue va muy de costado o para atras (dar la vuelta),
        //    primero gira casi quieto y despues camina: nadie camina de espaldas.
        float misalign = Mathf.Abs(Mathf.DeltaAngle(travelYaw, Yaw(Flat(points[0] - pos))));
        float speed = walkSpeed * shotSpeed * Mathf.Clamp01(1f - (misalign - 35f) / 50f);
        transform.position = Advance(pos, speed * dt);

        // 3. La toma: mirada al costado o arriba, que se acomoda despacio, mas un vaiven chico.
        if (shot == Shot.Quieto) shotYaw = 32f * Mathf.Sin(Time.time * 0.32f); // mira alrededor
        float blend = 1f - Mathf.Exp(-1.6f * dt);
        yawOffset += Mathf.DeltaAngle(yawOffset, shotYaw) * blend;
        pitchNow += (shotPitch - pitchNow) * blend;

        float t = Time.time;
        float yawNoise = (Mathf.PerlinNoise(seed, t * 0.15f) - 0.5f) * 6f;
        float pitchNoise = (Mathf.PerlinNoise(seed + 7.3f, t * 0.13f) - 0.5f) * 4f;
        transform.rotation = Quaternion.Euler(0f, travelYaw + yawOffset + yawNoise, 0f);
        if (head != null) head.localRotation = Quaternion.Euler(pitchNow + pitchNoise, 0f, 0f);
    }

    /// <summary>Camina 'distance' metros por los puntos planeados (puede pasar mas de uno por frame).</summary>
    private Vector3 Advance(Vector3 pos, float distance)
    {
        int guard = 0;
        while (distance > 0f && points.Count > 0 && guard++ < 8)
        {
            Vector3 to = Flat(points[0] - pos);
            float d = to.magnitude;
            if (d <= distance)
            {
                pos = new Vector3(points[0].x, pos.y, points[0].z);
                distance -= d;
                Arrive(pos);
            }
            else
            {
                pos += to / d * distance;
                distance = 0f;
            }
        }
        return pos;
    }

    /// <summary>Llego al centro de una celda: confirma que el proximo tramo sigue abierto y planea mas.</summary>
    private void Arrive(Vector3 reached)
    {
        points.RemoveAt(0);
        if (points.Count > 0)
        {
            Vector3 next = Flat(points[0] - reached).normalized;
            // El laberinto pudo cambiar desde que se planeo (por ejemplo, una persiana que bajo):
            // si el tramo ya no esta abierto, se vuelve a planear desde aca.
            if (!Open(reached, next))
            {
                points.Clear();
                lastDir = currentDir;
            }
            else currentDir = next;
        }
        Plan();
    }

    // ------------------------------------------------------------------ camino

    /// <summary>Mantiene tres celdas planeadas por delante.</summary>
    private void Plan()
    {
        while (points.Count < 3)
        {
            Vector3 from = points.Count > 0 ? points[points.Count - 1] : transform.position;
            Vector3 dir = Choose(from, lastDir);
            points.Add(new Vector3(from.x, transform.position.y, from.z) + dir * Cell);
            lastDir = dir;
        }
    }

    private Vector3 Choose(Vector3 from, Vector3 arrive)
    {
        Vector3 left = new Vector3(-arrive.z, 0f, arrive.x);
        Vector3 right = -left;
        float side = (1f - straightChance) * 0.5f;

        // Las tres salidas posibles con su peso. Hacia "atras" del laberinto casi nunca:
        // el generador no arma caminos para ese lado, solo callejones cortos.
        Vector3[] dirs = { arrive, left, right };
        float[] weights = { straightChance, side, side };
        float total = 0f;
        for (int i = 0; i < 3; i++)
        {
            if (!Open(from, dirs[i])) weights[i] = 0f;
            else if (treadmill != null && Vector3.Dot(dirs[i], treadmill.ForwardAxis) < -0.5f) weights[i] *= 0.1f;
            total += weights[i];
        }
        if (total <= 0f) return -arrive; // callejon sin salida: dar la vuelta

        float r = Random.value * total;
        for (int i = 0; i < 3; i++)
        {
            if (r < weights[i]) return dirs[i];
            r -= weights[i];
        }
        return dirs[0];
    }

    /// <summary>
    /// Hay paso si en la celda de al lado hay una pieza puesta (si no, ahi todavia no hay nada)
    /// y un rayo a la altura del pecho no choca con una pared o una persiana en el camino.
    /// </summary>
    private bool Open(Vector3 from, Vector3 dir)
    {
        if (treadmill != null && !treadmill.IsBuilt(from + dir * Cell)) return false;
        Vector3 origin = new Vector3(from.x, rayY, from.z);
        return !Physics.Raycast(origin, dir, RayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>El punto que esta 'distance' metros mas adelante siguiendo los puntos planeados.</summary>
    private Vector3 PointAhead(Vector3 pos, float distance)
    {
        Vector3 from = pos;
        foreach (Vector3 p in points)
        {
            Vector3 seg = Flat(p - from);
            float d = seg.magnitude;
            if (d >= distance) return from + seg / Mathf.Max(d, 0.0001f) * distance;
            distance -= d;
            from = new Vector3(p.x, pos.y, p.z);
        }
        return from;
    }

    // ------------------------------------------------------------------ tomas

    private IEnumerator Shots()
    {
        SetShot(Shot.Caminar);
        // Esperar a que el laberinto este listo antes de mostrar la primera toma.
        while (treadmill != null && !treadmill.Ready) yield return null;
        if (view != null) yield return view.FadeWindow(0f, 1.5f);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(shotLength.x, shotLength.y));
            if (view != null) yield return view.FadeWindow(1f, cutOut);

            // En negro: salta unas celdas hacia adelante (otro lugar) y cambia la toma.
            int skip = Random.Range(skipCells.x, skipCells.y + 1);
            for (int i = 0; i < skip && points.Count > 0; i++)
            {
                Vector3 p = points[0];
                transform.position = new Vector3(p.x, transform.position.y, p.z);
                Arrive(transform.position);
            }
            if (points.Count > 0) travelYaw = Yaw(Flat(PointAhead(transform.position, lookAhead) - transform.position));

            SetShot(NextShot());
            yawOffset = shotYaw;   // la toma nueva empieza ya encuadrada
            pitchNow = shotPitch;
            yield return null;     // un frame para que la camara se acomode
            if (view != null) yield return view.FadeWindow(0f, cutIn);
        }
    }

    private Shot NextShot()
    {
        // Pesos: caminar es lo mas comun; quedarse quieto y la toma bajita, lo mas raro.
        Shot[] all = { Shot.Caminar, Shot.Fachadas, Shot.Faroles, Shot.Quieto, Shot.Bajito };
        float[] weights = { 3f, 2f, 1.5f, 1f, 1f };
        float total = 0f;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == shot) weights[i] = 0f; // nunca dos veces la misma seguida
            total += weights[i];
        }
        float r = Random.value * total;
        for (int i = 0; i < all.Length; i++)
        {
            if (r < weights[i]) return all[i];
            r -= weights[i];
        }
        return Shot.Caminar;
    }

    private void SetShot(Shot next)
    {
        shot = next;
        sideSign = Random.value < 0.5f ? -1f : 1f;
        float height = 0f;
        switch (next)
        {
            case Shot.Caminar:  shotYaw = 0f;             shotPitch = 4f;   shotSpeed = 1f;    break;
            case Shot.Fachadas: shotYaw = 38f * sideSign; shotPitch = 2f;   shotSpeed = 0.65f; break;
            case Shot.Faroles:  shotYaw = 10f * sideSign; shotPitch = -24f; shotSpeed = 0.5f;  break;
            case Shot.Quieto:   shotYaw = 0f;             shotPitch = -3f;  shotSpeed = 0f;    break;
            case Shot.Bajito:   shotYaw = 0f;             shotPitch = -6f;  shotSpeed = 0.9f;  height = catHeight; break;
        }
        if (view != null) view.SetHeightOffset(height);
    }

    // ------------------------------------------------------------------ utilidades

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
}
