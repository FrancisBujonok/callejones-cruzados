using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Conecta el medidor de miedo con el post-procesado de la camara.
/// Con mas miedo: bordes mas oscuros (con un pulso como latido), mas grano,
/// separacion de colores en los bordes y colores mas apagados.
/// Va en el objeto "Global Volume". Trabaja sobre una COPIA del perfil,
/// asi lo que cambia durante Play nunca modifica el archivo del perfil.
/// </summary>
[RequireComponent(typeof(Volume))]
public class FearPostFX : MonoBehaviour
{
    [Tooltip("Si se deja vacio, lo busca en la escena.")]
    [SerializeField] private FearMeter fear;

    [Header("Valores con miedo 0 (x) y con miedo al maximo (y)")]
    [SerializeField] private Vector2 vignette = new Vector2(0.32f, 0.55f);
    [SerializeField] private Vector2 chromaticAberration = new Vector2(0f, 0.4f);
    [SerializeField] private Vector2 filmGrain = new Vector2(0.25f, 0.55f);
    [SerializeField] private Vector2 saturation = new Vector2(10f, -25f);

    [Header("Respuesta")]
    [Tooltip("Que tan rapido siguen los efectos al miedo. Mas alto = mas brusco.")]
    [SerializeField] private float response = 1.5f;
    [Tooltip("Cuanto late el borde oscuro con el miedo al maximo.")]
    [SerializeField] private float pulseAmount = 0.06f;
    [Tooltip("Latidos por minuto del pulso.")]
    [SerializeField] private float pulseBpm = 70f;

    private Vignette vignetteFx;
    private ChromaticAberration chromaticFx;
    private FilmGrain grainFx;
    private ColorAdjustments colorFx;
    private float current;

    private void Start()
    {
        if (fear == null) fear = FindAnyObjectByType<FearMeter>();

        // volume.profile (no sharedProfile) crea una copia solo para este Volume.
        VolumeProfile profile = GetComponent<Volume>().profile;
        vignetteFx = GetOrAdd<Vignette>(profile);
        chromaticFx = GetOrAdd<ChromaticAberration>(profile);
        grainFx = GetOrAdd<FilmGrain>(profile);
        colorFx = GetOrAdd<ColorAdjustments>(profile);
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component)) component = profile.Add<T>();
        component.active = true;
        return component;
    }

    private void Update()
    {
        float target = fear != null ? fear.Normalized : 0f;

        // MoveTowards: el valor se acerca al objetivo a velocidad fija, sin saltos.
        current = Mathf.MoveTowards(current, target, response * Time.deltaTime);

        float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseBpm / 60f * Mathf.PI * 2f);
        float pulse = pulseAmount * current * beat;

        Set(vignetteFx.intensity, Mathf.Lerp(vignette.x, vignette.y, current) + pulse);
        Set(chromaticFx.intensity, Mathf.Lerp(chromaticAberration.x, chromaticAberration.y, current));
        Set(grainFx.intensity, Mathf.Lerp(filmGrain.x, filmGrain.y, current));
        Set(colorFx.saturation, Mathf.Lerp(saturation.x, saturation.y, current));
    }

    private static void Set(VolumeParameter<float> parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }
}
