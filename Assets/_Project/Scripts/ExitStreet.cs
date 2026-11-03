using UnityEngine;

/// <summary>
/// Paso 22: la calle que se ve al salir del callejon, detras del torii. Hay tres calles armadas
/// (hijos Calle0, Calle1 y Calle2 de este objeto); en cada partida se prende una sola, y nunca
/// la misma que en la partida anterior (se guarda en PlayerPrefs). Reiniciar recarga la escena,
/// asi que cada partida nueva elige otra vez.
///
/// No es un RandomPick a proposito: la salida se "recicla" (ResetForReuse) cuando aparece en el
/// camino, y la calle no tiene que cambiar en medio de una partida.
///
/// Va en el objeto "Calle" del ModuloSalida. Lo arma Callejones > Calle de la salida.
/// </summary>
public class ExitStreet : MonoBehaviour
{
    private const string LastKey = "Callejones.Calle";

    [Tooltip("Las calles posibles. Solo una queda prendida.")]
    [SerializeField] private GameObject[] streets = new GameObject[0];

    /// <summary>La calle de esta partida (-1 si todavia no se eligio).</summary>
    public int Current { get; private set; } = -1;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(GameObject[] newStreets)
    {
        streets = newStreets;
    }

    private void Awake()
    {
        Pick();
    }

    /// <summary>Elige una calle distinta de la de la partida anterior y apaga las demas.</summary>
    public void Pick()
    {
        int n = streets == null ? 0 : streets.Length;
        if (n == 0) return;

        int last = PlayerPrefs.GetInt(LastKey, -1);
        int pick;
        if (n == 1 || last < 0 || last >= n)
        {
            pick = Random.Range(0, n);
        }
        else
        {
            // Una de las n - 1 que no son la anterior: se sortea entre 0 y n - 2 y se saltea 'last'.
            pick = Random.Range(0, n - 1);
            if (pick >= last) pick++;
        }
        Show(pick);
        PlayerPrefs.SetInt(LastKey, pick);
        PlayerPrefs.Save();
    }

    /// <summary>Prende la calle i y apaga las otras (tambien sirve para probar a mano).</summary>
    public void Show(int index)
    {
        for (int i = 0; i < streets.Length; i++)
            if (streets[i] != null) streets[i].SetActive(i == index);
        Current = index;
    }
}
