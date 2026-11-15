using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 37: el bolsillo (Tab): las monedas, la tarjeta de sellos y lo que el jugador lleva.
/// Tab lo abre y lo cierra. Cuando le sellan la tarjeta, se abre solo un momento para ver el
/// sello nuevo (Flash).
///
/// Cuando se completa la tarjeta, a los dos segundos aparece un septimo sello en el rincon que
/// estaba vacio: una patita de gato. Nadie la puso. El personaje lo piensa y FairPocket.PawStamp
/// queda en true (FairUnease sube un poco lo raro).
///
/// Va en el panel "Bolsillo" de la interfaz. Lo arma Callejones > Menus.
/// </summary>
public class PocketView : MonoBehaviour
{
    private static PocketView instance;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text coins;
    [SerializeField] private TMP_Text items;
    [SerializeField] private TMP_Text hint;
    [SerializeField] private GameObject card;
    [SerializeField] private GameObject noCard;
    [Tooltip("Los sellos, en el orden de FairPocket.StampIds.")]
    [SerializeField] private GameObject[] stamps;
    [SerializeField] private GameObject paw;

    private bool open;
    private float flashUntil, pawAt = -1f;

    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        { "Peluche", "un peluche" }, { "Mascara", "una máscara de zorro" }, { "Pez", "un pez en una bolsita" },
        { "MascaraGato", "una máscara de gato" }, { "Bolita", "la bolita de vidrio de una ramune" },
    };

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(CanvasGroup g, TMP_Text coinText, TMP_Text itemText, TMP_Text hintText, GameObject cardRoot,
        GameObject noCardText, GameObject[] stampMarks, GameObject pawMark)
    {
        group = g; coins = coinText; items = itemText; hint = hintText; card = cardRoot; noCard = noCardText;
        stamps = stampMarks; paw = pawMark;
    }

    private void Awake()
    {
        instance = this;
        if (group != null) group.alpha = 0f;
    }

    private void OnEnable()
    {
        FairPocket.Changed += Redraw;
        FairPocket.Stamped += OnStamped;
        Redraw();
    }

    private void OnDisable()
    {
        FairPocket.Changed -= Redraw;
        FairPocket.Stamped -= OnStamped;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Abre el bolsillo unos segundos (por ejemplo, al agarrar la tarjeta).</summary>
    public static void Flash(float seconds)
    {
        if (instance == null) return;
        instance.Redraw();
        instance.flashUntil = Time.unscaledTime + seconds;
    }

    private void OnStamped(string id)
    {
        PocketSounds.Play("Sello");
        Flash(2.8f);
        if (FairPocket.CardComplete && !FairPocket.PawStamp) pawAt = Time.unscaledTime + 2.2f;
    }

    private void Update()
    {
        bool playing = GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;
        if (playing && TabPressed()) { open = !open; Redraw(); }
        if (!playing) open = false;

        // La patita: aparece sola, con el bolsillo abierto, despues del ultimo sello.
        if (pawAt > 0f && Time.unscaledTime >= pawAt && playing)
        {
            pawAt = -1f;
            FairPocket.SetPaw();
            PocketSounds.Play("Sello", 0.8f);
            flashUntil = Time.unscaledTime + 4f;
            ThoughtText.Show("Hay un sello más: una patita de gato. Ese no me lo puso nadie.", 5f);
        }

        bool show = playing && (open || Time.unscaledTime < flashUntil);
        if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
    }

    private void Redraw()
    {
        if (coins != null) coins.text = "Monedas: " + FairPocket.Coins;
        if (card != null) card.SetActive(FairPocket.HasCard);
        if (noCard != null) noCard.SetActive(!FairPocket.HasCard);
        if (stamps != null)
            for (int i = 0; i < stamps.Length && i < FairPocket.StampIds.Length; i++)
                if (stamps[i] != null) stamps[i].SetActive(FairPocket.HasStamp(FairPocket.StampIds[i]));
        if (paw != null) paw.SetActive(FairPocket.PawStamp);

        var list = new List<string>();
        foreach (string p in FairPrizes.All) list.Add(Names.TryGetValue(p, out string n) ? n : p);
        foreach (string p in FairPocket.Items) list.Add(Names.TryGetValue(p, out string n) ? n : p);
        if (items != null) items.text = list.Count == 0 ? "Nada más." : "Llevo " + Join(list) + ".";
        if (hint != null) hint.text = MaskView.Available() > 0 ? "M: ponerme o sacarme la máscara" : "";
    }

    private static string Join(List<string> list)
    {
        if (list.Count == 1) return list[0];
        return string.Join(", ", list.GetRange(0, list.Count - 1)) + " y " + list[list.Count - 1];
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TabPressed() => Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
    private static bool TabPressed() => Input.GetKeyDown(KeyCode.Tab);
#endif
}
