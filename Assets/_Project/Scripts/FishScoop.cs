using UnityEngine;

/// <summary>
/// Paso 37: pescar el pez dorado (kingyo sukui). Una pileta con pececitos que nadan; el jugador
/// tiene un poi (un aro con papel) que sigue a la mira sobre el agua.
///
/// - Mantener apretado el clic (o E): el poi entra al agua. Soltar: lo saca. Si al sacarlo hay un
///   pez debajo del papel, se lo lleva al cuenco.
/// - El papel se moja y se rompe: aguanta unos segundos bajo el agua, menos si se mueve rapido, y
///   sacar un pez le cuesta un poco. Roto, termina la partida.
/// - Con un pez ya gana (se lleva un pez en una bolsita, FairPrizes "Pez"); puede seguir hasta
///   tres o hasta que se rompa el papel. Q deja el juego.
/// - Los peces nadan al azar dentro de la pileta y se apartan del poi si se mueve rapido cerca.
///
/// Espacio del puesto: la pileta de x -1.05 a 1.05 y z -1.1 a -0.2; el agua en y 0.4.
/// Va en el puesto "PescaPeces". Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class FishScoop : StallGame
{
    [Header("Peces")]
    [SerializeField] private Transform[] fish;
    [Tooltip("El poi (el aro con papel) y el papel solo (se esconde al romperse).")]
    [SerializeField] private Transform poi;
    [SerializeField] private GameObject paper;
    [Tooltip("Donde quedan los peces pescados.")]
    [SerializeField] private Transform bowl;
    [SerializeField] private float waterY = 0.4f;
    [SerializeField] private Vector2 poolMin = new Vector2(-1.05f, -1.1f), poolMax = new Vector2(1.05f, -0.2f);
    [SerializeField] private float poiRadius = 0.06f;
    [Tooltip("Segundos que aguanta el papel quieto bajo el agua.")]
    [SerializeField] private float paperSeconds = 7f;

    private Vector2[] pos, vel;
    private float[] turnAt;
    private bool[] caught;
    private Vector3[] fishHome;
    private int caughtCount;
    private float paperLeft, dippedFor;
    private bool dipped, torn, waitRelease;
    private Vector2 poiAt, poiLast;

    protected override void Start()
    {
        if (fish != null)
        {
            fishHome = new Vector3[fish.Length];
            for (int i = 0; i < fish.Length; i++) fishHome[i] = fish[i].localPosition;
        }
        base.Start();
        if (poi != null) poi.gameObject.SetActive(false);
    }

    protected override void ResetTargets()
    {
        if (fish == null) return;
        int n = fish.Length;
        if (pos == null || pos.Length != n)
        {
            pos = new Vector2[n]; vel = new Vector2[n]; turnAt = new float[n]; caught = new bool[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = new Vector2(fishHome[i].x, fishHome[i].z);
                vel[i] = Random.insideUnitCircle.normalized * Random.Range(0.08f, 0.2f);
            }
        }
        // Los pescados de antes vuelven a la pileta (el vendedor los devuelve).
        for (int i = 0; i < n; i++)
            if (caught[i])
            {
                caught[i] = false;
                pos[i] = new Vector2(Random.Range(poolMin.x + 0.1f, poolMax.x - 0.1f), Random.Range(poolMin.y + 0.1f, poolMax.y - 0.1f));
            }
        caughtCount = 0;
        paperLeft = 1f;
        torn = false;
        dipped = false;
        if (paper != null) paper.SetActive(true);
    }

    protected override void OnBegin()
    {
        if (fpc != null) fpc.SetPitch(48f); // mirar la pileta
        waitRelease = true; // la E que empezo la partida no mete el poi
        if (poi != null) poi.gameObject.SetActive(true);
    }

    protected override void PlayInput()
    {
        InteractPrompt.Show(torn ? "Se rompió el papel" :
            "Mantené clic: meter el poi   Soltá: sacarlo   (" + caughtCount + " peces)   Q: dejar");
        if (torn || poi == null) return;

        // El poi sigue a la mira sobre el agua.
        Transform c = cam != null ? cam.transform : player;
        Vector3 o = transform.InverseTransformPoint(c.position), d = transform.InverseTransformDirection(c.forward);
        if (d.y < -0.05f)
        {
            float t = (waterY - o.y) / d.y;
            Vector3 hit = o + d * t;
            poiAt = new Vector2(Mathf.Clamp(hit.x, poolMin.x + 0.04f, poolMax.x - 0.04f), Mathf.Clamp(hit.z, poolMin.y + 0.04f, poolMax.y - 0.04f));
        }

        bool hold = ThrowHeld() || UseHeld();
        if (waitRelease) { if (!hold) waitRelease = false; hold = false; }
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        float speed = (poiAt - poiLast).magnitude / dt;
        poiLast = poiAt;

        if (hold && !dipped) { dipped = true; dippedFor = 0f; PocketSounds.Play("Agua", Random.Range(0.9f, 1.1f)); }
        if (dipped)
        {
            dippedFor += dt;
            // El papel se moja: mas rapido si se mueve rapido.
            paperLeft -= dt / paperSeconds * (1f + Mathf.Max(0f, speed - 0.25f) * 3f);
            if (paperLeft <= 0f) { Tear(); return; }
        }
        if (!hold && dipped)
        {
            dipped = false;
            if (dippedFor > 0.12f) Lift();
        }
        poi.localPosition = new Vector3(poiAt.x, waterY + (dipped ? -0.015f : 0.1f), poiAt.y);
        poi.localRotation = Quaternion.Euler(dipped ? 0f : 12f, 0f, 0f);
    }

    private void Lift()
    {
        PocketSounds.Play("Agua", 1.3f);
        for (int i = 0; i < fish.Length; i++)
        {
            if (caught[i] || (pos[i] - poiAt).magnitude > poiRadius) continue;
            // Sacar un pez le cuesta al papel; si ya estaba muy mojado, se rompe con el pez.
            paperLeft -= 0.22f;
            if (paperLeft <= 0f) { Tear(); return; }
            caught[i] = true;
            caughtCount++;
            won = true;
            fish[i].localPosition = bowl != null ? transform.InverseTransformPoint(bowl.position) + new Vector3(Random.Range(-0.03f, 0.03f), 0.04f, Random.Range(-0.03f, 0.03f)) : fish[i].localPosition;
            return; // de a uno
        }
    }

    private void Tear()
    {
        torn = true;
        dipped = false;
        if (paper != null) paper.SetActive(false);
        PocketSounds.Play("Papel");
    }

    protected override bool Done() => torn || caughtCount >= 3;

    protected override void Throw(Vector3 origin, Vector3 direction) { }
    protected override bool Busy() => false;

    protected override void Simulate(float dt) { }

    protected override void Update()
    {
        base.Update();
        if (!Playing && poi != null && poi.gameObject.activeSelf) poi.gameObject.SetActive(false);
        Swim(Time.deltaTime);
    }

    /// <summary>Los peces nadan siempre, se juegue o no.</summary>
    private void Swim(float dt)
    {
        if (fish == null || pos == null || dt <= 0f) return;
        float t = Time.time;
        for (int i = 0; i < fish.Length; i++)
        {
            if (caught[i]) continue;
            if (t >= turnAt[i])
            {
                vel[i] = Quaternion.Euler(0f, 0f, Random.Range(-70f, 70f)) * vel[i];
                vel[i] = vel[i].normalized * Random.Range(0.06f, 0.22f);
                turnAt[i] = t + Random.Range(0.6f, 2.2f);
            }
            // Se apartan del poi metido en el agua.
            if (Playing && dipped)
            {
                Vector2 away = pos[i] - poiAt;
                if (away.magnitude < 0.18f) vel[i] += away.normalized * 0.6f * dt;
            }
            pos[i] += vel[i] * dt;
            if (pos[i].x < poolMin.x + 0.05f || pos[i].x > poolMax.x - 0.05f) vel[i].x = -vel[i].x;
            if (pos[i].y < poolMin.y + 0.05f || pos[i].y > poolMax.y - 0.05f) vel[i].y = -vel[i].y;
            pos[i] = new Vector2(Mathf.Clamp(pos[i].x, poolMin.x + 0.05f, poolMax.x - 0.05f), Mathf.Clamp(pos[i].y, poolMin.y + 0.05f, poolMax.y - 0.05f));
            vel[i] = Vector2.ClampMagnitude(vel[i], 0.35f);

            fish[i].localPosition = new Vector3(pos[i].x, waterY + 0.006f, pos[i].y);
            if (vel[i].sqrMagnitude > 0.0001f)
                fish[i].localRotation = Quaternion.Euler(0f, Mathf.Atan2(vel[i].x, vel[i].y) * Mathf.Rad2Deg + Mathf.Sin(t * 9f + i) * 12f, 0f);
        }
    }

#if ENABLE_INPUT_SYSTEM
    private static bool UseHeld() => UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.isPressed;
#else
    private static bool UseHeld() => Input.GetKey(KeyCode.E);
#endif
}
