using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Las opciones del jugador (Paso 17): volumen, brillo y sensibilidad del mouse.
/// Cada una va de 0 a 10, como las barras del menu de opciones, y se guarda en PlayerPrefs
/// (queda guardada entre partidas, incluso despues de cerrar el juego).
///
/// Es una clase estatica (no va en ningun objeto): las opciones son del jugador, no de la escena,
/// asi que sobreviven a recargar la escena. GameFlow las carga y las aplica al empezar.
/// </summary>
public static class GameSettings
{
    public const int Max = 10;

    private const string VolumeKey = "Callejones.Volumen";
    private const string BrightnessKey = "Callejones.Brillo";
    private const string SensitivityKey = "Callejones.Sensibilidad";

    /// <summary>Volumen general: 0 = mudo, 10 = maximo.</summary>
    public static int MasterVolume { get; private set; } = 8;
    /// <summary>Brillo: 5 = como esta pensado el juego; menos oscurece, mas aclara.</summary>
    public static int Brightness { get; private set; } = 5;
    /// <summary>Sensibilidad del mouse: 5 = la de siempre.</summary>
    public static int Sensitivity { get; private set; } = 5;

    /// <summary>Lo que FirstPersonController multiplica por su sensibilidad: 1 = 0.2x, 5 = 1x, 10 = 2x.</summary>
    public static float SensitivityScale => Mathf.Max(1, Sensitivity) / 5f;

    /// <summary>Cuanto cambia la exposicion por cada punto de brillo (en pasos de camara).</summary>
    private const float ExposurePerStep = 0.2f;

    /// <summary>Pasos de exposicion que suma el brillo elegido: -1 con brillo 0, +1 con brillo 10.</summary>
    public static float ExposureOffset => (Brightness - 5) * ExposurePerStep;

    private static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        MasterVolume = Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey, MasterVolume), 0, Max);
        Brightness = Mathf.Clamp(PlayerPrefs.GetInt(BrightnessKey, Brightness), 0, Max);
        Sensitivity = Mathf.Clamp(PlayerPrefs.GetInt(SensitivityKey, Sensitivity), 1, Max);
    }

    public static void SetMasterVolume(int value)
    {
        MasterVolume = Mathf.Clamp(value, 0, Max);
        PlayerPrefs.SetInt(VolumeKey, MasterVolume);
        AudioListener.volume = MasterVolume / (float)Max;
    }

    public static void SetBrightness(int value)
    {
        Brightness = Mathf.Clamp(value, 0, Max);
        PlayerPrefs.SetInt(BrightnessKey, Brightness);
        ApplyBrightness();
    }

    public static void SetSensitivity(int value)
    {
        Sensitivity = Mathf.Clamp(value, 1, Max);
        PlayerPrefs.SetInt(SensitivityKey, Sensitivity);
    }

    /// <summary>Escribe en disco lo cambiado (se llama al cerrar el menu de opciones).</summary>
    public static void Save() => PlayerPrefs.Save();

    /// <summary>Aplica todo. Hay que llamarlo al empezar cada escena: el Volume es nuevo.</summary>
    public static void Apply()
    {
        Load();
        AudioListener.volume = MasterVolume / (float)Max;
        ApplyBrightness();
    }

    /// <summary>
    /// El brillo se suma a la exposicion del Global Volume. Se parte del valor del ARCHIVO del perfil
    /// (sharedProfile) y se escribe en la copia que usa el juego (profile, la misma que usa FearPostFX):
    /// asi el archivo nunca cambia y el brillo no se va sumando cada vez.
    /// </summary>
    private static void ApplyBrightness()
    {
        Volume volume = FindGlobalVolume();
        if (volume == null || volume.sharedProfile == null) return;
        if (!volume.sharedProfile.TryGet(out ColorAdjustments original)) return;
        if (!volume.profile.TryGet(out ColorAdjustments color)) return;

        color.postExposure.overrideState = true;
        color.postExposure.value = original.postExposure.value + ExposureOffset;
    }

    private static Volume FindGlobalVolume()
    {
        foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v.isGlobal) return v;
        return null;
    }
}
