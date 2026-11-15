using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paso 37: lo que el jugador lleva encima en la feria: monedas, la tarjeta de sellos y las cosas
/// que compra (la mascara de gato, la bolita de la ramune). Los premios de los juegos siguen en
/// FairPrizes.
///
/// Es "static": sobrevive al cambio de escena, asi el capitulo 2 puede preguntar, por ejemplo,
/// FairPocket.HasItem("Bolita") o FairPocket.PawStamp. Se vacia al cargar la feria (FairUnease).
///
/// La tarjeta tiene seis sellos (StampIds). Cuando se completa, aparece un septimo que no puso
/// nadie: una patita de gato (PawStamp). Lo muestra PocketView.
/// </summary>
public static class FairPocket
{
    public const int StartCoins = 20;
    public static readonly string[] StampIds = { "Aros", "Latas", "Peces", "Comida", "Bebida", "Mascara" };

    private static readonly HashSet<string> items = new HashSet<string>();
    private static readonly HashSet<string> stamps = new HashSet<string>();

    public static int Coins { get; private set; } = StartCoins;
    public static bool HasCard { get; private set; }
    /// <summary>La patita: el sello que aparecio solo al completar la tarjeta.</summary>
    public static bool PawStamp { get; private set; }
    public static bool CardComplete => HasCard && stamps.Count >= StampIds.Length;

    /// <summary>Avisa cada vez que cambia algo (lo usa PocketView para redibujar).</summary>
    public static event System.Action Changed;
    /// <summary>Avisa cuando se pone un sello nuevo (id del sello).</summary>
    public static event System.Action<string> Stamped;

    public static void ResetAll()
    {
        Coins = StartCoins;
        items.Clear();
        stamps.Clear();
        HasCard = false;
        PawStamp = false;
        FairPrizes.Clear();
        Changed?.Invoke();
    }

    public static bool CanPay(int price) => Coins >= price;

    public static bool Pay(int price)
    {
        if (Coins < price) return false;
        Coins -= price;
        Changed?.Invoke();
        return true;
    }

    public static bool HasItem(string id) => items.Contains(id);
    public static void AddItem(string id) { if (items.Add(id)) Changed?.Invoke(); }
    public static IEnumerable<string> Items => items;

    public static void TakeCard()
    {
        if (HasCard) return;
        HasCard = true;
        Changed?.Invoke();
    }

    public static bool HasStamp(string id) => stamps.Contains(id);

    /// <summary>Sella la tarjeta (si la tiene y el sello es nuevo). Devuelve si sello.</summary>
    public static bool Stamp(string id)
    {
        if (!HasCard || string.IsNullOrEmpty(id) || !stamps.Add(id)) return false;
        Changed?.Invoke();
        Stamped?.Invoke(id);
        return true;
    }

    public static void SetPaw()
    {
        if (PawStamp) return;
        PawStamp = true;
        Changed?.Invoke();
    }

    /// <summary>Para el editor: llenar la tarjeta de una (prueba).</summary>
    public static void DebugFillCard()
    {
        HasCard = true;
        foreach (string id in StampIds) Stamp(id);
    }
}
