using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 38 (provisorio): la adivina. Delante de su mesita aparece "E - Sentarse con la adivina
/// (3 monedas)". Con E se paga, el personaje queda quieto mirandola, suenan las campanillas y la
/// bola de cristal se prende. Ella le mira las manos y le habla... en castellano: es la unica de la
/// feria que lo hace. Le dice una frase (una de 'fortunes', al azar) que tendria que tener sentido
/// en los callejones (capitulo 2).
///
/// La frase queda en el bolsillo como un papelito (FairPocket.Fortune, item "Papelito"): se lee
/// con Tab y el capitulo 2 la puede preguntar. Una sola vez por partida; si vuelve, ella le hace
/// un gesto para que se vaya.
///
/// Va en el puesto de la adivina. Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class FortuneTeller : MonoBehaviour
{
    [SerializeField] private int price = 3;
    [Tooltip("Donde se para el jugador (delante de la mesita, mirando a +Z de este punto).")]
    [SerializeField] private Transform seat;
    [SerializeField] private float promptDistance = 2f;
    [Tooltip("La luz de la bola de cristal (apagada hasta que lee).")]
    [SerializeField] private Light ballLight;
    [SerializeField] private AudioSource chimes;
    [Tooltip("Lo que pasa antes de la frase (lo que piensa y lo que ella dice, entre comillas).")]
    [SerializeField] private string[] before =
    {
        "Me agarra las manos y las da vuelta. Las mira un rato largo.",
        "Después me habla. En castellano.",
        "«No sos de acá. Pero vas a conocer estas calles mejor que yo.»",
    };
    [Tooltip("Las frases posibles (sale una).")]
    [SerializeField] private string[] fortunes =
    {
        "«Cuando las calles cambien, no te des vuelta para comprobarlo.»",
        "«El gato sabe dónde está la salida. Vos todavía no.»",
        "«Vas a pasar tres veces por el mismo lugar. A la tercera, mirá arriba.»",
        "«Lo que te sigue no camina. Espera.»",
    };
    [SerializeField] private string after = "Nadie más en la feria me habló en castellano.";
    [SerializeField] private string paperLine = "Me escribe la frase en un papelito y me lo da.";
    [SerializeField] private string againLine = "La adivina me hace un gesto con la mano: ya está, que me vaya.";

    private Transform player;
    private Camera cam;
    private FirstPersonController fpc;
    private CharacterController controller;
    private bool reading;
    private int step;
    private float nextAt, againAt;
    private string[] script;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Transform seatSpot, Light light, AudioSource bells)
    {
        seat = seatSpot; ballLight = light; chimes = bells;
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            fpc = p.GetComponent<FirstPersonController>();
            controller = p.GetComponent<CharacterController>();
        }
        cam = Camera.main;
        if (ballLight != null) ballLight.enabled = false;
    }

    private void Update()
    {
        if (player == null || seat == null) return;
        if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Playing) return;

        if (reading)
        {
            if (ballLight != null) ballLight.intensity = 1.2f + 0.5f * Mathf.Sin(Time.time * 3f) + 0.2f * Mathf.Sin(Time.time * 11f);
            if (Time.time >= nextAt) NextStep();
            return;
        }

        Vector3 d = seat.position - player.position;
        d.y = 0f;
        if (d.magnitude > promptDistance || Time.time < againAt) return;
        Vector3 look = cam != null ? cam.transform.forward : player.forward;
        Vector3 to = transform.position - player.position;
        look.y = 0f; to.y = 0f;
        if (Vector3.Dot(look.normalized, to.normalized) < 0.6f) return;

        if (FairPocket.HasItem("Papelito"))
        {
            InteractPrompt.Show("E  -  La adivina");
            if (UsePressed()) { ThoughtText.Show(againLine, 3.5f); againAt = Time.time + 4f; }
            return;
        }
        if (!FairPocket.CanPay(price))
        {
            InteractPrompt.Show("Sentarse con la adivina (" + price + " monedas)  -  no me alcanza");
            return;
        }
        InteractPrompt.Show("E  -  Sentarse con la adivina (" + price + " monedas)");
        if (UsePressed()) Begin();
    }

    private void Begin()
    {
        if (!FairPocket.Pay(price)) return;
        PocketSounds.Play("Moneda");
        reading = true;
        // Delante de la mesita, mirandola, un poco hacia abajo (la bola).
        if (controller != null) controller.enabled = false;
        player.position = new Vector3(seat.position.x, player.position.y, seat.position.z);
        player.rotation = Quaternion.Euler(0f, seat.eulerAngles.y, 0f);
        if (controller != null) controller.enabled = true;
        if (fpc != null) { fpc.MovementLocked = true; fpc.SetPitch(14f); }
        if (chimes != null) chimes.Play();
        if (ballLight != null) ballLight.enabled = true;

        string fortune = fortunes != null && fortunes.Length > 0 ? fortunes[Random.Range(0, fortunes.Length)] : "";
        var list = new System.Collections.Generic.List<string>(before ?? new string[0]) { fortune, paperLine, after };
        script = list.ToArray();
        FairPocket.SetFortune(fortune);
        step = -1;
        nextAt = Time.time + 1.2f; // primero las campanillas
    }

    private void NextStep()
    {
        step++;
        if (script == null || step >= script.Length) { End(); return; }
        bool isFortune = script[step] == FairPocket.Fortune;
        float seconds = isFortune ? 5.5f : 3.8f;
        ThoughtText.Show(script[step], seconds);
        nextAt = Time.time + seconds + 0.3f;
        if (isFortune && chimes != null) chimes.Play();
    }

    private void End()
    {
        reading = false;
        if (fpc != null) fpc.MovementLocked = false;
        if (ballLight != null) ballLight.enabled = false;
        againAt = Time.time + 3f;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool UsePressed() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
    private static bool UsePressed() => Input.GetKeyDown(KeyCode.E);
#endif
}
