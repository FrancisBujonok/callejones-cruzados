using UnityEngine;

/// <summary>
/// Paso 36: el juego de los aros. Botellas paradas en la mesa de atras del puesto; el jugador
/// tira tres aros desde el mostrador. Si un aro cae con el pico de una botella adentro, gana un
/// peluche.
///
/// La cuenta se hace "a mano" en el espacio del puesto (sin fisica de Unity): el aro vuela con
/// gravedad, girando; cuando baja hasta la altura de las bocas, se fija si alguna quedo adentro
/// (a menos de hitRadius del centro). Si no, se queda apoyado de costado sobre las bocas (cayo
/// entre las botellas), sobre el mostrador o la mesa, o en el piso. La lona de atras lo frena.
///
/// Espacio del puesto (lo arma la herramienta de editor): el frente en z = 0 mirando a +Z, el
/// mostrador de z -0.7 a 0 (arriba en 0.94), la mesa de atras de z -1.85 a -1.35 (arriba en 0.8),
/// la lona en z -1.98.
///
/// Va en el puesto "Aros". Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class RingToss : StallGame
{
    [Header("Aros")]
    [Tooltip("Los aros (3), sobre el mostrador hasta que se tiran.")]
    [SerializeField] private Transform[] rings;
    [Tooltip("La boca (punta) de cada botella.")]
    [SerializeField] private Transform[] bottleTops;
    [Tooltip("Que tan cerca del centro de la boca tiene que caer el aro para entrar (m).")]
    [SerializeField] private float hitRadius = 0.045f;
    [Tooltip("Velocidad del tiro (m/s).")]
    [SerializeField] private float throwSpeed = 4.6f;

    private struct Flight
    {
        public bool flying, resting;
        public Vector3 pos, vel;
        public float spin, tilt;
    }

    // Superficies donde se apoya lo que cae (espacio del puesto): x0, x1, z0, z1, arriba.
    private static readonly float[,] Surfaces =
    {
        { -1.45f, 1.45f, -0.7f, 0.0f, 0.94f },   // mostrador
        { -1.30f, 1.30f, -1.85f, -1.35f, 0.80f }, // mesa de atras
    };
    private const float BackWall = -1.96f, SideWall = 1.4f, Gravity = 9.81f;

    private Flight[] flights;
    private int next;
    private float neckY;
    private Vector2 gridMin, gridMax;

    private Vector3[] home;
    private Quaternion[] homeRot;

    protected override void Start()
    {
        // Los aros esperan sobre el mostrador; al empezar una partida vuelven ahi.
        if (rings != null)
        {
            home = new Vector3[rings.Length];
            homeRot = new Quaternion[rings.Length];
            for (int i = 0; i < rings.Length; i++) { home[i] = rings[i].localPosition; homeRot[i] = rings[i].localRotation; }
        }
        base.Start();
        if (bottleTops != null && bottleTops.Length > 0)
        {
            gridMin = new Vector2(float.MaxValue, float.MaxValue);
            gridMax = new Vector2(float.MinValue, float.MinValue);
            foreach (Transform b in bottleTops)
            {
                Vector3 l = transform.InverseTransformPoint(b.position);
                neckY = l.y;
                gridMin = Vector2.Min(gridMin, new Vector2(l.x, l.z));
                gridMax = Vector2.Max(gridMax, new Vector2(l.x, l.z));
            }
        }
    }

    protected override void ResetTargets()
    {
        if (rings == null) return;
        flights = new Flight[rings.Length];
        next = 0;
        for (int i = 0; i < rings.Length; i++)
            if (rings[i] != null && home != null) { rings[i].localPosition = home[i]; rings[i].localRotation = homeRot[i]; }
    }

    protected override void Throw(Vector3 origin, Vector3 direction)
    {
        if (rings == null || next >= rings.Length) return;
        int i = next++;
        Vector3 dir = transform.InverseTransformDirection(direction).normalized;
        flights[i] = new Flight
        {
            flying = true,
            pos = transform.InverseTransformPoint(origin),
            vel = dir * throwSpeed + Vector3.up * 0.6f, // un poco hacia arriba: el aro se tira "en arco"
            spin = Random.Range(0f, 360f),
            tilt = Random.Range(-12f, 12f),
        };
        Place(i);
    }

    protected override bool Busy()
    {
        if (flights == null) return false;
        foreach (Flight f in flights) if (f.flying) return true;
        return false;
    }

    protected override void Simulate(float dt)
    {
        if (flights == null) return;
        for (int i = 0; i < flights.Length; i++)
        {
            if (!flights[i].flying) continue;
            Flight f = flights[i];
            int steps = Mathf.Clamp(Mathf.CeilToInt(f.vel.magnitude * dt / 0.02f), 2, 60);
            float h = dt / steps;
            for (int s = 0; s < steps && f.flying; s++)
            {
                Vector3 before = f.pos;
                f.vel.y -= Gravity * h;
                f.pos += f.vel * h;
                f.spin += 540f * h;
                f.tilt = Mathf.MoveTowards(f.tilt, 0f, 20f * h);

                // La lona de atras y los costados del puesto.
                if (f.pos.z < BackWall) { f.pos.z = BackWall; f.vel.z = Mathf.Abs(f.vel.z) * 0.2f; f.vel.x *= 0.5f; }
                if (Mathf.Abs(f.pos.x) > SideWall && f.pos.z < 0f) { f.pos.x = Mathf.Sign(f.pos.x) * SideWall; f.vel.x *= -0.2f; }

                // Bajando por la altura de las bocas: ¿entro en alguna?
                if (before.y >= neckY && f.pos.y < neckY && f.vel.y < 0f)
                {
                    float k = (before.y - neckY) / Mathf.Max(0.0001f, before.y - f.pos.y);
                    Vector3 at = Vector3.Lerp(before, f.pos, k);
                    Transform hit = Ringer(at);
                    if (hit != null)
                    {
                        // Entro: baja por la botella hasta la mesa, alrededor de la botella.
                        Vector3 b = transform.InverseTransformPoint(hit.position);
                        f.pos = new Vector3(b.x, 0f, b.z);
                        f.pos.y = Ground(new Vector3(b.x, neckY, b.z)) + 0.012f;
                        f.flying = false; f.resting = true; f.tilt = 0f;
                        won = true;
                        break;
                    }
                    if (at.x > gridMin.x - 0.06f && at.x < gridMax.x + 0.06f && at.z > gridMin.y - 0.06f && at.z < gridMax.y + 0.06f)
                    {
                        // Cayo entre las botellas: queda apoyado de costado sobre las bocas.
                        f.pos = new Vector3(at.x, neckY + 0.012f, at.z);
                        f.flying = false; f.resting = true; f.tilt = Random.Range(10f, 22f) * (Random.value < 0.5f ? -1f : 1f);
                        break;
                    }
                }

                // Mostrador, mesa o piso.
                float floor = Ground(f.pos);
                if (f.pos.y <= floor && f.vel.y < 0f)
                {
                    f.pos.y = floor + 0.01f;
                    f.flying = false; f.resting = true; f.tilt = Random.Range(-4f, 4f);
                }
            }
            flights[i] = f;
            Place(i);
        }
    }

    private Transform Ringer(Vector3 at)
    {
        if (bottleTops == null) return null;
        foreach (Transform b in bottleTops)
        {
            Vector3 l = transform.InverseTransformPoint(b.position);
            Vector2 d = new Vector2(at.x - l.x, at.z - l.z);
            if (d.magnitude < hitRadius) return b;
        }
        return null;
    }

    private static float Ground(Vector3 p)
    {
        float top = 0f;
        for (int s = 0; s < Surfaces.GetLength(0); s++)
            if (p.x >= Surfaces[s, 0] && p.x <= Surfaces[s, 1] && p.z >= Surfaces[s, 2] && p.z <= Surfaces[s, 3] && p.y > Surfaces[s, 4] - 0.15f)
                top = Mathf.Max(top, Surfaces[s, 4]);
        return top;
    }

    private void Place(int i)
    {
        Transform r = rings[i];
        if (r == null) return;
        r.localPosition = flights[i].pos;
        r.localRotation = Quaternion.Euler(flights[i].tilt, flights[i].spin, 0f);
    }
}
