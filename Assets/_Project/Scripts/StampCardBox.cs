using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 37: la caja con tarjetas de sellos, en el cartel del programa antes del arco. Con E el
/// personaje se lleva una (FairPocket.TakeCard) y se abre el bolsillo un momento para verla.
/// </summary>
public class StampCardBox : MonoBehaviour
{
    [SerializeField] private float promptDistance = 2f;
    [SerializeField] private string takeLine = "Una tarjeta para sellar en los puestos. Seis casilleros. Tab para verla.";

    private Transform player;
    private Camera cam;

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        cam = Camera.main;
    }

    private void Update()
    {
        if (player == null || FairPocket.HasCard) return;
        if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Playing) return;
        Vector3 d = transform.position - player.position;
        d.y = 0f;
        if (d.magnitude > promptDistance) return;
        Vector3 look = cam != null ? cam.transform.forward : player.forward;
        look.y = 0f;
        if (Vector3.Dot(look.normalized, d.normalized) < 0.6f) return;
        InteractPrompt.Show("E  -  Agarrar una tarjeta de sellos");
#if ENABLE_INPUT_SYSTEM
        bool use = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        bool use = Input.GetKeyDown(KeyCode.E);
#endif
        if (!use) return;
        FairPocket.TakeCard();
        PocketSounds.Play("Papel", 1.6f);
        ThoughtText.Show(takeLine, 4.5f);
        PocketView.Flash(3.5f);
    }
}
