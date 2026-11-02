using UnityEngine;

/// <summary>
/// Niebla del color del cielo (Paso 20). Va en la Main Camera.
///
/// Antes la niebla era de un solo color: el del cielo cerca del horizonte. Con edificios de
/// 30 m, lo alto y lejano quedaba de ese color marron contra un cielo violeta, y las piezas que
/// el generador pone a lo lejos aparecian como siluetas. Ahora el shader PS1 Lit pinta la niebla
/// con el mismo degrade que el cielo, segun la altura a la que se mira: lo lejano se funde con el
/// cielo que tiene detras.
///
/// Este componente solo copia los colores y las alturas del degrade del material del cielo a
/// variables globales (ExecuteAlways: tambien se ve en la vista Scene). Apagado, el shader vuelve
/// a la niebla de siempre.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SkyFog : MonoBehaviour
{
    private static readonly int OnId = Shader.PropertyToID("_CallejonesSkyFogOn");
    private static readonly int HorizonId = Shader.PropertyToID("_CallejonesSkyHorizon");
    private static readonly int GlowId = Shader.PropertyToID("_CallejonesSkyGlow");
    private static readonly int MidId = Shader.PropertyToID("_CallejonesSkyMid");
    private static readonly int ZenithId = Shader.PropertyToID("_CallejonesSkyZenith");
    private static readonly int HeightsId = Shader.PropertyToID("_CallejonesSkyHeights");

    [Tooltip("Cuanto cuenta la altura en la distancia de la niebla. 1 = como la distancia comun; " +
             "menos de 1 = lo alto se ve un poco mas, asi se distinguen las alturas de los edificios.")]
    [SerializeField, Range(0.2f, 1f)] private float verticalWeight = 0.6f;

    private void OnEnable() => Push();
    private void Update() => Push();
    private void OnValidate() => Push();
    private void OnDisable() => Shader.SetGlobalFloat(OnId, 0f);

    private void Push()
    {
        if (!isActiveAndEnabled) return;
        Material sky = RenderSettings.skybox;
        bool usable = sky != null && sky.HasProperty("_HorizonColor") && RenderSettings.fog &&
                      RenderSettings.fogMode == FogMode.Linear;
        Shader.SetGlobalFloat(OnId, usable ? 1f : 0f);
        if (!usable) return;

        // SetGlobalColor convierte a espacio lineal, igual que las propiedades de un material.
        Shader.SetGlobalColor(HorizonId, sky.GetColor("_HorizonColor"));
        Shader.SetGlobalColor(GlowId, sky.GetColor("_GlowColor"));
        Shader.SetGlobalColor(MidId, sky.GetColor("_MidColor"));
        Shader.SetGlobalColor(ZenithId, sky.GetColor("_ZenithColor"));
        Shader.SetGlobalVector(HeightsId, new Vector4(sky.GetFloat("_HorizonHold"), sky.GetFloat("_GlowHeight"),
            sky.GetFloat("_MidHeight"), verticalWeight));
    }
}
