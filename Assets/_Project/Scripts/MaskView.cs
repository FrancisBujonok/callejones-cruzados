using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 37: la mascara puesta. Con M se pone la mascara que se tenga (la de zorro, premio del
/// tiro al blanco, o la de gato, comprada); si tiene las dos, M pasa de una a la otra y despues
/// se la saca. La vista queda tapada salvo por los agujeros de los ojos (una imagen negra con dos
/// huecos, encima del juego).
///
/// Con la mascara puesta, la gente de la feria que "mira de mas" deja de mirar (FairPerson lee
/// MaskView.On), salvo los vendedores al final. Funciona tambien en el capitulo 2.
///
/// Va en el panel "Mascara" de la interfaz. Lo arma Callejones > Menus.
/// </summary>
public class MaskView : MonoBehaviour
{
    public static bool On => worn != null;
    /// <summary>Cual tiene puesta: "Mascara" (zorro), "MascaraGato" o null.</summary>
    public static string Worn => worn;
    private static string worn;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private RawImage overlay;

    private bool saidOnce;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(CanvasGroup g, RawImage image) { group = g; overlay = image; }

    /// <summary>Cuantas mascaras tiene.</summary>
    public static int Available() => (FairPrizes.Has("Mascara") ? 1 : 0) + (FairPocket.HasItem("MascaraGato") ? 1 : 0);

    private void Awake()
    {
        worn = null;
        if (group != null) group.alpha = 0f;
    }

    private void Update()
    {
        bool playing = GameFlow.Instance == null || GameFlow.Instance.Current == GameFlow.State.Playing;
        if (playing && MaskPressed() && Available() > 0) Next();
        if (worn != null && !(worn == "Mascara" ? FairPrizes.Has("Mascara") : FairPocket.HasItem(worn))) worn = null;
        if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, worn != null && playing ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        if (overlay != null) overlay.color = worn == "MascaraGato" ? new Color(0.85f, 0.85f, 0.9f, 1f) : Color.white;
    }

    private void Next()
    {
        bool fox = FairPrizes.Has("Mascara"), cat = FairPocket.HasItem("MascaraGato");
        if (worn == null) worn = fox ? "Mascara" : "MascaraGato";
        else if (worn == "Mascara" && cat) worn = "MascaraGato";
        else worn = null;
        PocketSounds.Play("Papel", 0.7f);
        if (worn != null && !saidOnce)
        {
            saidOnce = true;
            ThoughtText.Show("Se ve poco por los agujeros. Huele a plástico.", 3.5f);
        }
    }

#if ENABLE_INPUT_SYSTEM
    private static bool MaskPressed() => Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
    private static bool MaskPressed() => Input.GetKeyDown(KeyCode.M);
#endif
}
