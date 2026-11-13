using UnityEngine;

/// <summary>
/// Paso 35: los semaforos del cruce que esta detras del comienzo de la feria. Dos grupos:
/// la calle principal (la de la feria) y la transversal. Ciclo:
///
///   principal verde (20 s) - amarillo (3 s) - todo rojo (1.5 s)
///   transversal verde (10 s) - amarillo (3 s) - todo rojo (1.5 s)
///
/// Los peatones de la senda que cruza la transversal pueden pasar mientras la transversal esta en
/// rojo (verde de la principal); el muñequito verde titila los ultimos 4 s.
///
/// FairTraffic le pregunta a cada auto si puede seguir (State). Las luces son objetos que se
/// prenden y se apagan (cajitas con brillo delante de la cara del semaforo).
///
/// Va en el objeto "Semaforos". Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class TrafficSignals : MonoBehaviour
{
    public enum Signal { Go, Yellow, Stop }

    [SerializeField] private float mainGreen = 20f, crossGreen = 10f, yellow = 3f, allRed = 1.5f;
    [Header("Luces (se prenden y se apagan)")]
    [SerializeField] private GameObject[] mainRed, mainYellow, mainGreenLights;
    [SerializeField] private GameObject[] crossRed, crossYellow, crossGreenLights;
    [SerializeField] private GameObject[] walkerStop, walkerGo;
    [Tooltip("El pajarito: suena mientras los peatones pueden cruzar.")]
    [SerializeField] private AudioSource walkSound;

    private float cycle, t;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(GameObject[] mr, GameObject[] my, GameObject[] mg, GameObject[] cr, GameObject[] cy, GameObject[] cg,
        GameObject[] ws, GameObject[] wg, AudioSource walkAudio = null)
    {
        walkSound = walkAudio;
        mainRed = mr; mainYellow = my; mainGreenLights = mg;
        crossRed = cr; crossYellow = cy; crossGreenLights = cg;
        walkerStop = ws; walkerGo = wg;
    }

    private void Start()
    {
        cycle = mainGreen + yellow + allRed + crossGreen + yellow + allRed;
        t = 2f; // arranca con la principal en verde (el colectivo de la parada sale enseguida)
        Apply();
    }

    private void Update()
    {
        t = (t + Time.deltaTime) % cycle;
        Apply();
    }

    /// <summary>group 0 = calle principal, 1 = transversal.</summary>
    public Signal State(int group)
    {
        float a = mainGreen, b = a + yellow, c = b + allRed, d = c + crossGreen, e = d + yellow;
        if (group == 0) return t < a ? Signal.Go : t < b ? Signal.Yellow : Signal.Stop;
        return t >= c && t < d ? Signal.Go : t >= d && t < e ? Signal.Yellow : Signal.Stop;
    }

    private void Apply()
    {
        Signal m = State(0), x = State(1);
        Set(mainRed, m == Signal.Stop); Set(mainYellow, m == Signal.Yellow); Set(mainGreenLights, m == Signal.Go);
        Set(crossRed, x == Signal.Stop); Set(crossYellow, x == Signal.Yellow); Set(crossGreenLights, x == Signal.Go);
        // Peatones: pasan con la principal en verde (la transversal esta parada); titila al final.
        bool walk = m == Signal.Go;
        bool blink = walk && t > mainGreen - 4f && Mathf.Repeat(t * 2f, 1f) < 0.5f;
        Set(walkerGo, walk && !blink);
        Set(walkerStop, !walk);
        if (walkSound != null)
        {
            bool chirp = walk && t < mainGreen - 4f;
            if (chirp && !walkSound.isPlaying) walkSound.Play();
            else if (!chirp && walkSound.isPlaying) walkSound.Stop();
        }
    }

    private static void Set(GameObject[] lights, bool on)
    {
        if (lights == null) return;
        foreach (GameObject l in lights)
            if (l != null && l.activeSelf != on) l.SetActive(on);
    }
}
