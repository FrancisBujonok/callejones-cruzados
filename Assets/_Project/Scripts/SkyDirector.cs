using UnityEngine;

/// <summary>
/// El cielo con el miedo (Paso 19). Va en la Main Camera.
///
/// - La ciudad lejana se apaga: con mas miedo, menos ventanas prendidas (se apagan de a una).
/// - El carnaval se detiene: la vuelta al mundo gira cada vez mas lento hasta pararse, y sus
///   lamparitas dejan de cambiar de color, se ponen rojas y algunas se apagan.
/// - La luna miente: con miedo alto, cada vez que pasa un rato sin que la mires, aparece en otro
///   lado. El que la usaba para orientarse ya no puede. Cuando el miedo baja, vuelve a su lugar,
///   tambien sin que la mires.
///
/// No toca el material del cielo (asi nada de esto queda guardado en el archivo): escribe
/// variables globales que lee el shader Callejones/Cielo Nocturno.
/// Durante un bucle el cielo se queda quieto: si cambiara entre dos pasadas, pareceria la anomalia.
///
/// Paso 21: tambien lleva el reloj de las ventanas de los edificios (_CallejonesWindowTime), que
/// se congela en los bucles por lo mismo. Las ventanas usan el miedo del cielo para apagarse.
/// </summary>
[DisallowMultipleComponent]
public class SkyDirector : MonoBehaviour
{
    private static readonly int FearId = Shader.PropertyToID("_CallejonesSkyFear");
    private static readonly int WheelId = Shader.PropertyToID("_CallejonesWheelAngle");
    private static readonly int BulbId = Shader.PropertyToID("_CallejonesBulbStep");
    private static readonly int MoonId = Shader.PropertyToID("_CallejonesMoonOffset");
    private static readonly int WindowTimeId = Shader.PropertyToID("_CallejonesWindowTime");

    [Header("Referencias")]
    [SerializeField] private FearMeter fear;
    [Tooltip("Para saber si hay un bucle en curso.")]
    [SerializeField] private CorridorTreadmill treadmill;
    [Tooltip("La camara del jugador: para saber si esta mirando la luna.")]
    [SerializeField] private Camera view;

    [Header("Ciudad y carnaval")]
    [Tooltip("Que tan rapido sigue el cielo al miedo (unidades de miedo por segundo). Lento a proposito.")]
    [SerializeField] private float response = 0.08f;
    [Tooltip("Vuelta al mundo sin miedo, en radianes por segundo.")]
    [SerializeField] private float wheelSpeed = 0.05f;
    [Tooltip("Cambios de color de las lamparitas por segundo, sin miedo.")]
    [SerializeField] private float bulbRate = 1.5f;
    [Tooltip("Con este miedo la vuelta al mundo ya esta parada (empieza a frenar 0.3 antes).")]
    [SerializeField, Range(0f, 1f)] private float wheelStopFear = 0.75f;

    [Header("La luna que miente")]
    [SerializeField, Range(0f, 1f)] private float moonLiesFromFear = 0.5f;
    [SerializeField, Range(0f, 1f)] private float moonReturnsBelowFear = 0.25f;
    [Tooltip("Segundos sin mirarla antes de que se mueva.")]
    [SerializeField] private float unseenSeconds = 6f;
    [Tooltip("Cuantos grados salta, entre estos dos valores.")]
    [SerializeField] private Vector2 jumpDegrees = new Vector2(70f, 160f);
    [Tooltip("Margen alrededor de la pantalla (en fraccion de pantalla) para decidir si la luna esta a la vista.")]
    [SerializeField] private float frameMargin = 0.1f;

    public float SkyFear { get; private set; }
    public float MoonOffset { get; private set; }
    public int MoonJumps { get; private set; }

    private float wheelAngle, bulbStep, unseen, windowTime;
    private bool seenSinceMove = true;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(FearMeter fearMeter, CorridorTreadmill corridor, Camera camera)
    {
        fear = fearMeter;
        treadmill = corridor;
        view = camera;
    }

    private void Awake()
    {
        if (view == null) view = GetComponent<Camera>();
        if (fear == null) fear = FindAnyObjectByType<FearMeter>();
        if (treadmill == null) treadmill = FindAnyObjectByType<CorridorTreadmill>();
    }

    private void OnDisable()
    {
        // Fuera de Play el cielo queda como en el material.
        Shader.SetGlobalFloat(FearId, 0f);
        Shader.SetGlobalFloat(WheelId, 0f);
        Shader.SetGlobalFloat(BulbId, 0f);
        Shader.SetGlobalFloat(MoonId, 0f);
        Shader.SetGlobalFloat(WindowTimeId, 0f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        bool loop = treadmill != null && treadmill.LoopActive;

        // 1. El miedo del cielo sigue al del jugador, despacio, y se congela en los bucles.
        float target = fear != null ? fear.Normalized : 0f;
        if (!loop) SkyFear = Mathf.MoveTowards(SkyFear, target, response * dt);

        // 2. El carnaval frena. Los angulos se acumulan aca: si el shader usara tiempo por
        //    velocidad, al cambiar la velocidad la rueda saltaria de golpe.
        float stop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(wheelStopFear - 0.3f, wheelStopFear, SkyFear));
        wheelAngle = Mathf.Repeat(wheelAngle + wheelSpeed * (1f - stop) * dt, Mathf.PI * 2f);
        bulbStep = Mathf.Repeat(bulbStep + bulbRate * (1f - stop) * dt, 300f);

        // 3. La luna.
        UpdateMoon(dt, loop);

        // 4. El reloj de las ventanas: quieto durante un bucle.
        if (!loop) windowTime += dt;

        Shader.SetGlobalFloat(FearId, SkyFear);
        Shader.SetGlobalFloat(WheelId, wheelAngle);
        Shader.SetGlobalFloat(BulbId, bulbStep);
        Shader.SetGlobalFloat(MoonId, MoonOffset);
        Shader.SetGlobalFloat(WindowTimeId, windowTime);
    }

    private void UpdateMoon(float dt, bool loop)
    {
        Material sky = RenderSettings.skybox;
        if (view == null || sky == null || !sky.HasProperty("_MoonAzimuth")) return;

        float azimuth = sky.GetFloat("_MoonAzimuth"), elevation = sky.GetFloat("_MoonElevation");
        Vector3 moon = Direction(azimuth + MoonOffset, elevation);
        // Vista = dentro de la pantalla y sin una pared en el medio.
        bool seen = InFrame(moon) && !Physics.Raycast(view.transform.position, moon, 60f, ~0, QueryTriggerInteraction.Ignore);
        if (seen)
        {
            unseen = 0f;
            seenSinceMove = true;
            return;
        }

        unseen += dt;
        if (loop || unseen < unseenSeconds) return;

        if (SkyFear >= moonLiesFromFear && seenSinceMove)
        {
            // Solo despues de que la viste en su lugar nuevo: si no, nadie nota que se movio.
            // Y nunca a un lugar que este en pantalla en este momento: se la veria aparecer.
            float jump = Random.Range(jumpDegrees.x, jumpDegrees.y);
            foreach (float sign in Random.value < 0.5f ? new[] { -1f, 1f } : new[] { 1f, -1f })
            {
                float offset = Mathf.Repeat(MoonOffset + sign * jump + 180f, 360f) - 180f;
                if (InFrame(Direction(azimuth + offset, elevation))) continue;
                MoonOffset = offset;
                MoonJumps++;
                seenSinceMove = false;
                unseen = 0f;
                break;
            }
        }
        else if (SkyFear < moonReturnsBelowFear && !Mathf.Approximately(MoonOffset, 0f)
                 && !InFrame(Direction(azimuth, elevation)))
        {
            MoonOffset = 0f;
            unseen = 0f;
        }
    }

    private bool InFrame(Vector3 direction)
    {
        Vector3 p = view.WorldToViewportPoint(view.transform.position + direction * 100f);
        return p.z > 0f && p.x > -frameMargin && p.x < 1f + frameMargin && p.y > -frameMargin && p.y < 1f + frameMargin;
    }

    /// <summary>Mismo calculo que DirFrom en el shader: rumbo 0 = +Z, en grados.</summary>
    private static Vector3 Direction(float azimuth, float elevation)
    {
        float az = azimuth * Mathf.Deg2Rad, el = elevation * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
    }
}
