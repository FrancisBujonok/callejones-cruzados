using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Paso 33: el recorrido del gato por la feria. Antes el gato esperaba en el callejon desde el
/// principio, y si el jugador iba derecho se salteaba toda la feria. Ahora el gato aparece en
/// varios lugares, uno despues del otro, y cada vez que el jugador se le acerca se escapa:
///
///   1. Al llegar: cruza la peatonal antes del arco y se mete detras del primer puesto.
///   2. En el escenario, en la otra punta de la feria.
///   3. Delante del puesto de la adivina; se escapa por detras de los puestos.
///   4. En la boca del callejon, que recien ahora esta libre (ver AlleyGate). Este es el que
///      entra al callejon (LeadingCat) y lleva al final del capitulo (ChapterExit).
///
/// Cada aparicion es un LeadingCat (sentado, mira, maulla, y se va caminando cuando el jugador
/// esta cerca). Solo una esta prendida a la vez. Mientras tanto, la entrada al callejon esta
/// tapada por un carro y cajones, y el final del capitulo esta apagado.
///
/// Va en el objeto "RecorridoDelGato". Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class CatTrail : MonoBehaviour
{
    [Tooltip("Las apariciones, en orden. La ultima es la del callejon.")]
    [SerializeField] private LeadingCat[] sightings;
    [Tooltip("Lo que tapa la entrada del callejon: se saca antes de la ultima aparicion.")]
    [SerializeField] private AlleyGate gate;
    [Tooltip("El final del capitulo (se prende con la ultima aparicion).")]
    [SerializeField] private GameObject chapterExit;

    [Header("Pruebas")]
    [Tooltip("Solo en el editor: F9 salta a la siguiente aparicion.")]
    [SerializeField] private bool debugKeys = true;

    private int current = -1;

    public int Current => current;
    /// <summary>Paso 36: cuantas apariciones hay (FairUnease mide cuanto se avanzo).</summary>
    public int Count => sightings != null ? sightings.Length : 0;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(LeadingCat[] cats, AlleyGate alleyGate, GameObject exit)
    {
        sightings = cats;
        gate = alleyGate;
        chapterExit = exit;
    }

    private void Start()
    {
        if (sightings == null || sightings.Length == 0) return;
        for (int i = 0; i < sightings.Length; i++)
            if (sightings[i] != null) sightings[i].gameObject.SetActive(false);
        if (chapterExit != null) chapterExit.SetActive(false);
        Show(0);
    }

    private void Update()
    {
        if (sightings == null || current < 0 || current >= sightings.Length) return;

#if UNITY_EDITOR
        if (debugKeys && SkipPressed())
        {
            sightings[current].gameObject.SetActive(false);
            Next();
            return;
        }
#endif
        LeadingCat cat = sightings[current];
        if (cat == null || cat.Gone) Next();
        else if (current == sightings.Length - 1 && !cat.gameObject.activeSelf && (gate == null || gate.IsOpen))
            cat.gameObject.SetActive(true); // la del callejon espera a que la entrada este libre
    }

    private void Next()
    {
        if (current + 1 < sightings.Length) Show(current + 1);
        else current = sightings.Length;
    }

    private void Show(int i)
    {
        current = i;
        bool last = i == sightings.Length - 1;
        if (last)
        {
            if (gate != null) gate.Open();
            if (chapterExit != null) chapterExit.SetActive(true);
            if (gate != null && !gate.IsOpen) return; // aparece cuando el carro ya no esta (Update)
        }
        if (sightings[i] != null) sightings[i].gameObject.SetActive(true);
    }

#if ENABLE_INPUT_SYSTEM
    private static bool SkipPressed() => Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame;
#else
    private static bool SkipPressed() => Input.GetKeyDown(KeyCode.F9);
#endif

    private void OnGUI()
    {
        if (!DebugOverlay.Visible) return;
        GUI.Label(new Rect(10, 362, 400, 20), "gato: aparicion " + (current + 1) + " de " + (sightings != null ? sightings.Length : 0) +
            (gate != null ? (gate.IsOpen ? " · callejon libre" : " · callejon tapado") : ""));
    }
}
