using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 37: comprar en un puesto. Cerca del mostrador y mirandolo aparece "E - Comprar brochetas
/// (3 monedas)". Con E se paga, el vendedor atiende y pasa lo de cada cosa:
///
/// - Brochetas (comida): se comen en tres mordiscos; despues no se pierde el aire al trotar por un
///   rato (Stamina.Feed).
/// - Manzana caramelizada (frutas): se lleva en la mano mientras se camina, y se va comiendo sola
///   de a mordiscos.
/// - Ramune (bebidas): se abre empujando la bolita, se toma y la bolita de vidrio queda en el
///   bolsillo (FairPocket "Bolita").
/// - Mascara de gato (mascaras): queda en el bolsillo; con M se pone (MaskView).
///
/// Si el jugador tiene la tarjeta, le sellan el casillero de ese puesto (stampId).
/// Lo arma Callejones > Feria de dia (capitulo 1), en los puestos de comida, frutas, bebidas y
/// mascaras.
/// </summary>
public class StallShop : MonoBehaviour
{
    public enum Goods { Brochetas, Manzana, Ramune, MascaraGato }

    [SerializeField] private Goods goods = Goods.Brochetas;
    [SerializeField] private string goodsName = "brochetas";
    [SerializeField] private int price = 3;
    [Tooltip("El casillero de la tarjeta de sellos (Comida, Bebida, Mascara). Vacio = no sella.")]
    [SerializeField] private string stampId = "";
    [Tooltip("Donde se para el que compra (delante del mostrador).")]
    [SerializeField] private Transform buySpot;
    [SerializeField] private float promptDistance = 1.8f;
    [Tooltip("Lo que piensa el personaje al comprar (se elige una).")]
    [SerializeField] private string[] lines;

    private Transform player;
    private Camera cam;
    private float againAt;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Goods what, string name, int cost, string stamp, Transform spot, string[] thoughts)
    {
        goods = what; goodsName = name; price = cost; stampId = stamp; buySpot = spot; lines = thoughts;
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        cam = Camera.main;
    }

    private void Update()
    {
        if (player == null || buySpot == null || Time.time < againAt) return;
        if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Playing) return;
        if (!Near()) return;

        if (goods == Goods.MascaraGato && FairPocket.HasItem("MascaraGato"))
        {
            InteractPrompt.Show("Ya me compré una máscara acá");
            return;
        }
        if (HeldItems.Instance != null && HeldItems.Instance.Busy && goods != Goods.MascaraGato)
        {
            InteractPrompt.Show("Primero termino lo que tengo en la mano");
            return;
        }
        if (!FairPocket.CanPay(price))
        {
            InteractPrompt.Show("Comprar " + goodsName + " (" + price + " monedas)  -  no me alcanza");
            return;
        }
        InteractPrompt.Show("E  -  Comprar " + goodsName + " (" + price + " monedas)");
        if (UsePressed()) Buy();
    }

    private bool Near()
    {
        Vector3 d = buySpot.position - player.position;
        d.y = 0f;
        if (d.magnitude > promptDistance) return false;
        Vector3 look = cam != null ? cam.transform.forward : player.forward;
        Vector3 toStall = transform.position - player.position;
        toStall.y = 0f; look.y = 0f;
        return Vector3.Dot(look.normalized, toStall.normalized) > 0.6f;
    }

    private void Buy()
    {
        if (!FairPocket.Pay(price)) return;
        againAt = Time.time + 1.5f;
        PocketSounds.Play("Moneda");
        FairPerson.Serve(transform.position);
        if (lines != null && lines.Length > 0) ThoughtText.Show(lines[Random.Range(0, lines.Length)], 4f);

        switch (goods)
        {
            case Goods.Brochetas: if (HeldItems.Instance != null) HeldItems.Instance.Eat(HeldItems.Item.Brocheta); break;
            case Goods.Manzana: if (HeldItems.Instance != null) HeldItems.Instance.Eat(HeldItems.Item.Manzana); break;
            case Goods.Ramune: if (HeldItems.Instance != null) HeldItems.Instance.Eat(HeldItems.Item.Ramune); break;
            case Goods.MascaraGato: FairPocket.AddItem("MascaraGato"); break;
        }
        if (!FairPocket.HasStamp(stampId)) Invoke(nameof(GiveStamp), 1.2f);
    }

    private void GiveStamp() => FairPocket.Stamp(stampId);

#if ENABLE_INPUT_SYSTEM
    private static bool UsePressed() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
    private static bool UsePressed() => Input.GetKeyDown(KeyCode.E);
#endif
}
