using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 38 (provisorio): alguien de la feria con quien se puede "hablar". Cerca y mirandolo
/// aparece "E - Hablar". El personaje no entiende el idioma, asi que lo que pasa sale como lo
/// que el piensa: gestos, lo que le muestran, lo que cree que le dicen.
///
/// - Las lineas salen de a una en la linea de pensamiento, cada 3.4 s; E pasa a la siguiente.
///   Si se aleja, la charla se corta.
/// - La segunda vez (y las demas) dice solo la linea de "otra vez".
/// - Si lo raro de la feria esta alto (FairUnease, 0.6 o mas), no contesta: dice uneasyLine.
/// - flagId: lo que queda marcado en FairPocket al terminar la primera charla (por ejemplo
///   "DuenoGato"), para que otro script lo use.
///
/// Va en la persona (el objeto con FairPerson). Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class FairTalk : MonoBehaviour
{
    [SerializeField] private string[] lines;
    [SerializeField] private string againLine = "Me saluda con la mano.";
    [Tooltip("Si lo raro esta alto. Vacio = contesta igual.")]
    [SerializeField] private string uneasyLine = "No me contesta. Me mira, nada más.";
    [SerializeField] private string flagId = "";
    [SerializeField] private float distance = 2.3f;
    [SerializeField] private float secondsPerLine = 3.4f;

    private Transform player;
    private Camera cam;
    private bool talked;
    private int index = -1;
    private float nextAt;
    private string[] current;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(string[] talk, string again, string uneasy, string flag)
    {
        lines = talk; againLine = again; uneasyLine = uneasy; flagId = flag;
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        cam = Camera.main;
    }

    private void Update()
    {
        if (player == null) return;
        if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Playing) return;
        Vector3 d = transform.position - player.position;
        d.y = 0f;

        if (index >= 0)
        {
            // Hablando: se corta si se aleja.
            if (d.magnitude > distance + 1.5f) { index = -1; return; }
            if (Time.time >= nextAt || UsePressed()) Next();
            return;
        }

        if (d.magnitude > distance) return;
        Vector3 look = cam != null ? cam.transform.forward : player.forward;
        look.y = 0f;
        if (Vector3.Dot(look.normalized, d.normalized) < 0.75f) return;
        InteractPrompt.Show("E  -  Hablar");
        if (UsePressed()) Begin();
    }

    private void Begin()
    {
        bool uneasy = !string.IsNullOrEmpty(uneasyLine) && FairUnease.CurrentLevel >= 0.6f;
        if (uneasy) current = new[] { uneasyLine };
        else if (talked || lines == null || lines.Length == 0) current = new[] { againLine };
        else current = lines;
        if (!uneasy && !talked && lines != null && lines.Length > 0)
        {
            talked = true;
            FairPocket.SetFlag(flagId);
        }
        index = -1;
        Next();
    }

    private void Next()
    {
        index++;
        if (current == null || index >= current.Length) { index = -1; return; }
        ThoughtText.Show(current[index], secondsPerLine);
        nextAt = Time.time + secondsPerLine;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool UsePressed() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
    private static bool UsePressed() => Input.GetKeyDown(KeyCode.E);
#endif
}
