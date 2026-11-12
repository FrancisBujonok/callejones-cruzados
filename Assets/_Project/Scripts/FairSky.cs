using UnityEngine;

/// <summary>
/// Paso 32: el cielo de la feria. Usa el mismo shader que el de la noche (Callejones/Cielo Nocturno)
/// con los colores del atardecer, pero sin SkyDirector (el miedo no lo toca):
/// - Pone en cero las variables globales del miedo del cielo (quedan guardadas entre escenas: si se
///   viene de los callejones con miedo alto, la luna y la vuelta al mundo seguirian raras).
/// - Hace girar la vuelta al mundo del horizonte y cambiar de color sus lamparitas, despacio.
///
/// Va en la Main Camera de la escena Feria. Lo arma Callejones > Feria de dia (capitulo 1).
/// </summary>
public class FairSky : MonoBehaviour
{
    private static readonly int FearId = Shader.PropertyToID("_CallejonesSkyFear");
    private static readonly int WheelId = Shader.PropertyToID("_CallejonesWheelAngle");
    private static readonly int BulbId = Shader.PropertyToID("_CallejonesBulbStep");
    private static readonly int MoonId = Shader.PropertyToID("_CallejonesMoonOffset");

    [Tooltip("Vueltas por minuto de la vuelta al mundo.")]
    [SerializeField] private float wheelRpm = 0.6f;
    [Tooltip("Cambios de color de las lamparitas por segundo.")]
    [SerializeField] private float bulbSteps = 0.8f;

    private float wheel, bulbs;

    private void Update()
    {
        wheel += wheelRpm / 60f * Mathf.PI * 2f * Time.deltaTime;
        bulbs += bulbSteps * Time.deltaTime;
        Shader.SetGlobalFloat(FearId, 0f);
        Shader.SetGlobalFloat(MoonId, 0f);
        Shader.SetGlobalFloat(WheelId, wheel);
        Shader.SetGlobalFloat(BulbId, bulbs);
    }
}
