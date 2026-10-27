using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Detecta cuando el jugador mira hacia atras de golpe ("mirada atras")
/// y, si lo hace muchas veces en poco tiempo, dispara paranoia.
///
/// - Mirada atras: girar al menos lookBackAngle grados en menos de lookBackTime segundos.
///   Girar despacio para explorar NO cuenta.
/// - Paranoia: a partir de lookBacksToTrigger miradas atras dentro de paranoiaWindow
///   segundos, cada nueva mirada dispara onParanoia.
///
/// Va en el Player: mide el giro del cuerpo, que es lo que rota FirstPersonController.
/// </summary>
public class ParanoiaSensor : MonoBehaviour
{
    [Header("Que cuenta como mirar atras")]
    [Tooltip("Grados de giro horizontal necesarios.")]
    [SerializeField] private float lookBackAngle = 120f;
    [Tooltip("Tiempo maximo para completar ese giro, en segundos. Los giros lentos no cuentan.")]
    [SerializeField] private float lookBackTime = 0.8f;

    [Header("Cuando es paranoia")]
    [Tooltip("Ventana de tiempo en la que se cuentan las miradas atras, en segundos.")]
    [SerializeField] private float paranoiaWindow = 12f;
    [Tooltip("Cantidad de miradas atras dentro de la ventana a partir de la cual cada nueva suma paranoia.")]
    [SerializeField] private int lookBacksToTrigger = 3;

    [Header("Eventos")]
    [Tooltip("Cada mirada atras, sea o no paranoia.")]
    public UnityEvent onLookBack;
    [Tooltip("Mirada atras de mas: aca se conecta FearMeter.OnParanoia.")]
    public UnityEvent onParanoia;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;

    public int RecentLookBacks => lookBackTimes.Count;

    // Ventana corta: giros de cada frame en los ultimos lookBackTime segundos.
    private readonly Queue<(float time, float delta)> turnSamples = new Queue<(float time, float delta)>();
    private float turnSum;

    // Ventana larga: momentos de cada mirada atras en los ultimos paranoiaWindow segundos.
    private readonly Queue<float> lookBackTimes = new Queue<float>();

    private float lastYaw;

    private void Start()
    {
        lastYaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        float now = Time.time;

        // 1. Cuanto giro el cuerpo este frame. DeltaAngle resuelve el salto de 359 a 0 grados.
        float yaw = transform.eulerAngles.y;
        float delta = Mathf.DeltaAngle(lastYaw, yaw);
        lastYaw = yaw;

        // 2. Sumar el giro a la ventana corta y descartar lo que ya es viejo.
        turnSamples.Enqueue((now, delta));
        turnSum += delta;
        while (turnSamples.Count > 0 && now - turnSamples.Peek().time > lookBackTime)
        {
            turnSum -= turnSamples.Dequeue().delta;
        }

        // 3. Giro suficiente en poco tiempo = mirada atras.
        if (Mathf.Abs(turnSum) >= lookBackAngle)
        {
            RegisterLookBack(now);
            turnSamples.Clear();
            turnSum = 0f;
        }

        // 4. Olvidar miradas atras fuera de la ventana larga.
        while (lookBackTimes.Count > 0 && now - lookBackTimes.Peek() > paranoiaWindow)
        {
            lookBackTimes.Dequeue();
        }
    }

    private void RegisterLookBack(float now)
    {
        lookBackTimes.Enqueue(now);
        onLookBack?.Invoke();

        if (lookBackTimes.Count >= lookBacksToTrigger)
        {
            Debug.Log($"[ParanoiaSensor] Paranoia: {lookBackTimes.Count} miradas atras en {paranoiaWindow:0} s.");
            onParanoia?.Invoke();
        }
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || !DebugOverlay.Visible) return;
        GUI.Label(new Rect(12, 100, 400, 22), $"Miradas atras recientes: {RecentLookBacks}");
    }
}
