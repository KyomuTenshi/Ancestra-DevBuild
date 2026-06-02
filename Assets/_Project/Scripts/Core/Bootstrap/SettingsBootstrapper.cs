// Применяет сохранённые настройки громкости при старте сцены.
// Должен присутствовать в каждой сцене, т.к. AudioMixer сбрасывается при переключении сцен.
// Статический ApplyVolumeSettings() доступен из UI настроек для мгновенного превью.

using UnityEngine;

/// <summary>
/// Читает AppSettings при Start() и диспатчит AudioVolumeChangedMessage, чтобы AudioSystem
/// настроил AudioMixer без прямой зависимости на порядок инициализации систем.
/// </summary>
public class SettingsBootstrapper : MonoBehaviour
{
    private void Start()
    {
        ApplyVolumeSettings(
            AppSettings.GeneralVolume,
            AppSettings.BackgroundVolume,
            AppSettings.AmbientVolume,
            AppSettings.SfxVolume
        );

        Debug.Log("[SettingsBootstrapper] Настройки применены.");
    }

    /// <summary>
    /// Сохраняет громкость в AppSettings и уведомляет AudioSystem через MessageBus.
    /// Вызывай из UI настроек при изменении ползунков.
    /// ambientVolume/sfxVolume = -1 означает «не менять, взять из AppSettings».
    /// </summary>
    public static void ApplyVolumeSettings(
        float masterVolume,
        float musicVolume,
        float ambientVolume = -1f,
        float sfxVolume     = -1f)
    {
        float ambient = ambientVolume >= 0f ? ambientVolume : AppSettings.AmbientVolume;
        float sfx     = sfxVolume     >= 0f ? sfxVolume     : AppSettings.SfxVolume;

        AppSettings.GeneralVolume    = masterVolume;
        AppSettings.BackgroundVolume = musicVolume;
        AppSettings.AmbientVolume    = ambient;
        AppSettings.SfxVolume        = sfx;

        MessageBus<AudioVolumeChangedMessage>.Dispatch(
            new AudioVolumeChangedMessage(masterVolume, musicVolume, ambient, sfx)
        );
    }
}
