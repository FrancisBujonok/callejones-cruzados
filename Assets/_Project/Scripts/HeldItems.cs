using UnityEngine;

/// <summary>
/// Paso 37: lo que el personaje tiene en la mano (abajo a la derecha de la vista): una brocheta,
/// una manzana caramelizada o una ramune. Son objetos hijos de la camara, escondidos hasta que se
/// compran.
///
/// - Brocheta: tres mordiscos (uno cada 1.8 s, se va un cubito cada vez). Al terminar, Stamina.Feed:
///   el aire para trotar no se gasta por un rato.
/// - Manzana: se lleva mientras se camina; un mordisco cada 8 s (se achica), son cinco.
/// - Ramune: la bolita (un "plop"), la botella sube a la boca dos veces y la bolita de vidrio
///   queda en el bolsillo (FairPocket "Bolita").
///
/// Mientras tiene algo en la mano no compra otra cosa de comer (Busy). Va en la Main Camera de la
/// feria. Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class HeldItems : MonoBehaviour
{
    public enum Item { Ninguno, Brocheta, Manzana, Ramune }

    public static HeldItems Instance { get; private set; }

    [SerializeField] private Transform brocheta;
    [SerializeField] private GameObject[] brochetaBites; // los cubitos, de la punta para abajo
    [SerializeField] private Transform manzana;
    [SerializeField] private Transform ramune;
    [Tooltip("Segundos que el trote no gasta aire despues de comer brochetas.")]
    [SerializeField] private float fedSeconds = 90f;

    private Item current = Item.Ninguno;
    private float started, nextBite;
    private int bites;
    private Vector3 restB, restM, restR;
    private Stamina stamina;

    public bool Busy => current != Item.Ninguno;

    /// <summary>Lo usa la herramienta de editor.</summary>
    public void Setup(Transform stick, GameObject[] stickBites, Transform apple, Transform bottle)
    {
        brocheta = stick; brochetaBites = stickBites; manzana = apple; ramune = bottle;
    }

    private void Awake()
    {
        Instance = this;
        if (brocheta != null) { restB = brocheta.localPosition; brocheta.gameObject.SetActive(false); }
        if (manzana != null) { restM = manzana.localPosition; manzana.gameObject.SetActive(false); }
        if (ramune != null) { restR = ramune.localPosition; ramune.gameObject.SetActive(false); }
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) stamina = p.GetComponent<Stamina>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Eat(Item item)
    {
        if (Busy) return;
        current = item;
        started = Time.time;
        bites = 0;
        switch (item)
        {
            case Item.Brocheta:
                brocheta.gameObject.SetActive(true);
                foreach (GameObject b in brochetaBites) b.SetActive(true);
                nextBite = Time.time + 1.6f;
                break;
            case Item.Manzana:
                manzana.gameObject.SetActive(true);
                manzana.localScale = Vector3.one;
                nextBite = Time.time + 6f;
                break;
            case Item.Ramune:
                ramune.gameObject.SetActive(true);
                nextBite = Time.time + 1.2f;
                break;
        }
    }

    private void Update()
    {
        if (!Busy) return;
        if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Playing) return;
        float t = Time.time, since = t - started;
        // Entra desde abajo y se mece apenas.
        float rise = Mathf.SmoothStep(-0.25f, 0f, Mathf.Clamp01(since * 3f));
        Vector3 sway = new Vector3(Mathf.Sin(t * 1.3f) * 0.004f, rise + Mathf.Sin(t * 2.1f) * 0.003f, 0f);

        switch (current)
        {
            case Item.Brocheta:
                brocheta.localPosition = restB + sway + (t > nextBite - 0.3f ? new Vector3(-0.04f, 0.05f, -0.03f) : Vector3.zero);
                if (t >= nextBite)
                {
                    if (bites < brochetaBites.Length) brochetaBites[bites].SetActive(false);
                    bites++;
                    PocketSounds.Play("Mordida", Random.Range(0.9f, 1.1f));
                    nextBite = t + 1.8f;
                    if (bites >= brochetaBites.Length) { Done(brocheta); if (stamina != null) stamina.Feed(fedSeconds); }
                }
                break;
            case Item.Manzana:
                manzana.localPosition = restM + sway + (t > nextBite - 0.4f ? new Vector3(-0.05f, 0.06f, -0.04f) : Vector3.zero);
                if (t >= nextBite)
                {
                    bites++;
                    PocketSounds.Play("Mordida", Random.Range(1.05f, 1.25f));
                    manzana.localScale = Vector3.one * (1f - bites * 0.14f);
                    nextBite = t + 8f;
                    if (bites >= 5) Done(manzana);
                }
                break;
            case Item.Ramune:
                float tip = 0f;
                if (bites == 0 && t >= nextBite) { PocketSounds.Play("Ramune"); bites = 1; nextBite = t + 1.4f; }
                else if (bites >= 1 && bites <= 2)
                {
                    tip = Mathf.Clamp01(1f - Mathf.Abs(t - nextBite) / 0.7f); // sube a la boca y baja
                    if (t >= nextBite + 0.7f) { PocketSounds.Play("Trago"); bites++; nextBite = t + 1.6f; }
                }
                else if (bites > 2)
                {
                    Done(ramune);
                    FairPocket.AddItem("Bolita");
                    ThoughtText.Show("Me quedé con la bolita de vidrio de la botella.", 3.5f);
                    return;
                }
                ramune.localPosition = restR + sway + new Vector3(-0.06f, 0.09f, -0.02f) * tip;
                ramune.localRotation = Quaternion.Euler(-60f * tip, 0f, 10f * tip);
                break;
        }
    }

    private void Done(Transform held)
    {
        held.gameObject.SetActive(false);
        current = Item.Ninguno;
    }
}
