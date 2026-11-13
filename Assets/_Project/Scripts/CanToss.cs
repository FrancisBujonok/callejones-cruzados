using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 36: el tiro al blanco. Tres pirámides de latas (3-2-1) en la mesa de atras; el jugador
/// tira tres pelotitas. Si voltea una piramide entera, gana una mascara.
///
/// Como RingToss, la cuenta se hace "a mano" en el espacio del puesto: la pelota vuela con un poco
/// de gravedad; si pasa por una lata, la lata sale volando para atras (y a veces arrastra a la de
/// al lado). Las latas que estaban apoyadas sobre una que se cayo, se caen tambien. Las que caen
/// rebotan contra la lona y quedan acostadas en la mesa o en el piso.
///
/// Va en el puesto "TiroAlBlanco". Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class CanToss : StallGame
{
    [Header("Latas")]
    [Tooltip("Las pelotitas (3), en la canastita del mostrador hasta que se tiran.")]
    [SerializeField] private Transform[] balls;
    [Tooltip("Las latas, de a 6 por piramide (abajo 3, al medio 2, arriba 1).")]
    [SerializeField] private Transform[] cans;
    [SerializeField] private float throwSpeed = 11f;
    [SerializeField] private float canRadius = 0.055f, canHeight = 0.13f, ballRadius = 0.04f;

    private struct Body
    {
        public bool flying, down;
        public Vector3 pos, vel, spinAxis;
        public float spin, angle, wakeAt;
    }

    private const float BackWall = -1.96f, Gravity = 9.81f;
    private static readonly float[,] Surfaces =
    {
        { -1.45f, 1.45f, -0.7f, 0.0f, 0.94f },   // mostrador
        { -1.30f, 1.30f, -1.85f, -1.35f, 0.80f }, // mesa de atras
    };

    private Body[] ballState, canState;
    private Vector3[] canHome;
    private Quaternion[] canHomeRot;
    private List<int>[] supportedBy; // de quien se apoya cada lata
    private int next;
    private Vector3[] ballHome;

    protected override void Start()
    {
        if (balls != null)
        {
            ballHome = new Vector3[balls.Length];
            for (int i = 0; i < balls.Length; i++) ballHome[i] = balls[i].localPosition;
        }
        if (cans != null)
        {
            canHome = new Vector3[cans.Length];
            canHomeRot = new Quaternion[cans.Length];
            for (int i = 0; i < cans.Length; i++)
            {
                canHome[i] = cans[i].localPosition;
                canHomeRot[i] = cans[i].localRotation;
            }
            // Quien se apoya en quien: la de arriba, sobre las dos de abajo que tiene mas cerca.
            supportedBy = new List<int>[cans.Length];
            for (int i = 0; i < cans.Length; i++)
            {
                supportedBy[i] = new List<int>();
                for (int j = 0; j < cans.Length; j++)
                {
                    if (i == j) continue;
                    Vector3 d = canHome[i] - canHome[j];
                    if (d.y > canHeight * 0.5f && d.y < canHeight * 1.5f && new Vector2(d.x, d.z).magnitude < canRadius * 2.2f)
                        supportedBy[i].Add(j);
                }
            }
        }
        base.Start();
    }

    protected override void ResetTargets()
    {
        next = 0;
        if (balls != null)
        {
            ballState = new Body[balls.Length];
            for (int i = 0; i < balls.Length; i++)
                if (balls[i] != null && ballHome != null) balls[i].localPosition = ballHome[i];
        }
        if (cans != null && canHome != null)
        {
            canState = new Body[cans.Length];
            for (int i = 0; i < cans.Length; i++)
            {
                canState[i].pos = canHome[i];
                cans[i].localPosition = canHome[i];
                cans[i].localRotation = canHomeRot[i];
            }
        }
    }

    protected override void Throw(Vector3 origin, Vector3 direction)
    {
        if (balls == null || next >= balls.Length) return;
        int i = next++;
        ballState[i] = new Body
        {
            flying = true,
            pos = transform.InverseTransformPoint(origin),
            vel = transform.InverseTransformDirection(direction).normalized * throwSpeed,
        };
        balls[i].localPosition = ballState[i].pos;
    }

    protected override bool Busy()
    {
        if (ballState != null) foreach (Body b in ballState) if (b.flying) return true;
        if (canState != null) foreach (Body c in canState) if (c.flying) return true;
        return false;
    }

    // No se tira mientras una pelota vuela, pero si mientras caen latas.
    protected override bool CanThrow()
    {
        if (ballState != null) foreach (Body b in ballState) if (b.flying) return false;
        return true;
    }

    protected override void Simulate(float dt)
    {
        if (ballState == null || canState == null) return;
        // Pasos chicos (2 cm como mucho): con un cuadro lento la pelota no atraviesa las latas.
        float fastest = 3f;
        foreach (Body b in ballState) if (b.flying) fastest = Mathf.Max(fastest, b.vel.magnitude);
        int steps = Mathf.Clamp(Mathf.CeilToInt(fastest * dt / 0.02f), 2, 80);
        float h = dt / steps;
        for (int s = 0; s < steps; s++)
        {
            for (int i = 0; i < ballState.Length; i++)
                if (ballState[i].flying) StepBall(i, h);
            for (int i = 0; i < canState.Length; i++)
                StepCan(i, h);
        }
        for (int i = 0; i < ballState.Length; i++)
            if (i < next) balls[i].localPosition = ballState[i].pos;
        for (int i = 0; i < canState.Length; i++)
        {
            if (!canState[i].down) continue;
            cans[i].localPosition = canState[i].pos;
            cans[i].localRotation = Quaternion.AngleAxis(canState[i].angle, canState[i].spinAxis) * canHomeRot[i];
        }
        if (!won) won = PyramidDown();
    }

    private void StepBall(int i, float h)
    {
        Body b = ballState[i];
        b.vel.y -= Gravity * 0.6f * h; // un poco menos de gravedad: se siente mas justo apuntar
        b.pos += b.vel * h;

        for (int c = 0; c < canState.Length; c++)
        {
            if (canState[c].down) continue;
            Vector3 p = canState[c].pos;
            Vector2 d = new Vector2(b.pos.x - p.x, b.pos.z - p.z);
            if (d.magnitude < canRadius + ballRadius && b.pos.y > p.y - ballRadius && b.pos.y < p.y + canHeight + ballRadius)
            {
                Knock(c, b.vel * 0.35f + Vector3.up * 1.2f, 0f);
                // El golpe arrastra a veces a la de al lado (misma fila).
                for (int o = 0; o < canState.Length; o++)
                    if (o != c && !canState[o].down && Mathf.Abs(canHome[o].y - canHome[c].y) < 0.01f &&
                        Vector3.Distance(canHome[o], canHome[c]) < canRadius * 2.4f && Random.value < 0.5f)
                        Knock(o, new Vector3(Random.Range(-0.8f, 0.8f), 1f, -1.5f), 0.04f);
                b.vel *= 0.45f;
                b.vel.x += Random.Range(-1f, 1f);
            }
        }

        if (b.pos.z < BackWall) { b.pos.z = BackWall; b.vel.z = Mathf.Abs(b.vel.z) * 0.15f; b.vel.x *= 0.4f; }
        float floor = Ground(b.pos) + ballRadius;
        if (b.pos.y < floor && b.vel.y < 0f)
        {
            b.pos.y = floor;
            if (Mathf.Abs(b.vel.y) < 0.8f) b.flying = false;
            b.vel = new Vector3(b.vel.x * 0.5f, -b.vel.y * 0.3f, b.vel.z * 0.5f);
        }
        if (b.pos.magnitude > 30f) b.flying = false;
        ballState[i] = b;
    }

    private void Knock(int c, Vector3 vel, float delay)
    {
        Body k = canState[c];
        if (k.down) return;
        k.down = true;
        k.flying = true;
        k.vel = vel;
        k.wakeAt = Time.time + delay;
        Vector3 axis = Vector3.Cross(Vector3.up, new Vector3(vel.x, 0f, vel.z));
        k.spinAxis = axis.sqrMagnitude > 0.001f ? axis.normalized : Vector3.right;
        k.spin = Random.Range(500f, 900f);
        canState[c] = k;
        // Las que estaban apoyadas en esta, se caen tambien (un instante despues).
        for (int j = 0; j < canState.Length; j++)
            if (supportedBy[j].Contains(c) && !canState[j].down)
                Knock(j, new Vector3(Random.Range(-0.6f, 0.6f), 0.4f, Random.Range(-1.4f, -0.4f)), delay + 0.06f);
    }

    private void StepCan(int i, float h)
    {
        Body k = canState[i];
        if (!k.flying || Time.time < k.wakeAt) return;
        k.vel.y -= Gravity * h;
        k.pos += k.vel * h;
        k.angle += k.spin * h;
        if (k.pos.z < BackWall + canRadius) { k.pos.z = BackWall + canRadius; k.vel.z = Mathf.Abs(k.vel.z) * 0.25f; }
        float floor = Ground(k.pos);
        if (k.pos.y < floor && k.vel.y < 0f)
        {
            // Queda acostada (el pivote de la lata esta en la base, en el eje).
            k.pos.y = floor + canRadius;
            k.flying = false;
            k.angle = 90f;
        }
        canState[i] = k;
    }

    private bool PyramidDown()
    {
        for (int p = 0; p + 5 < canState.Length; p += 6)
        {
            bool all = true;
            for (int i = p; i < p + 6; i++) if (!canState[i].down) { all = false; break; }
            if (all) return true;
        }
        return false;
    }

    private static float Ground(Vector3 p)
    {
        float top = 0f;
        for (int s = 0; s < Surfaces.GetLength(0); s++)
            if (p.x >= Surfaces[s, 0] && p.x <= Surfaces[s, 1] && p.z >= Surfaces[s, 2] && p.z <= Surfaces[s, 3] && p.y > Surfaces[s, 4] - 0.15f)
                top = Mathf.Max(top, Surfaces[s, 4]);
        return top;
    }
}
