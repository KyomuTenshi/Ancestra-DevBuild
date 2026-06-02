// ScriptableObject-ассет с параметрами аудиоклипа.
// Используется MusicChannel, AmbientLayer и SfxPool через AudioSystem.
// Вариативность pitch/volume включается флагом useVariance — SoundVariance рандомизирует
// параметры при каждом воспроизведении, не затрагивая базовые значения.

using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Конфигурация аудиоресурса: клип, громкость, pitch, маршрут в AudioMixer и параметры fade.
/// Передаётся в AudioSystem через MessageBus-сообщения или напрямую для SFX.
/// </summary>
[CreateAssetMenu(fileName = "NewAudioData", menuName = "Audio/Audio Data")]
public class AudioData : ScriptableObject
{
    [Tooltip("Аудиоклип для воспроизведения")]
    public AudioClip clip;

    [Tooltip("Базовая громкость трека (0–1). Умножается на громкость микшера.")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Базовый pitch. 1 = нормальная, 0.5 = октава вниз, 2 = октава вверх.")]
    [Range(0.1f, 3f)]
    public float pitch = 1f;

    [Tooltip("Зациклить воспроизведение (для музыки — обычно true)")]
    public bool loop = true;

    [Tooltip("AudioMixer Group для маршрутизации. Null = Master.")]
    public AudioMixerGroup mixerGroup;

    [Header("Параметры фейда")]
    [Tooltip("Длительность Fade In при старте этого трека (секунды)")]
    [Min(0f)]
    public float fadeInDuration = 1f;

    [Tooltip("Длительность Fade Out при завершении этого трека (секунды)")]
    [Min(0f)]
    public float fadeOutDuration = 1f;

    [Header("Вариативность воспроизведения")]
    [Tooltip("Включить рандомизацию громкости и pitch при каждом воспроизведении.\n" +
             "Полезно для SFX шагов, ударов и ambient-звуков — делает их живее.")]
    public bool useVariance;

    [Tooltip("Диапазоны рандомизации. Базовые volume и pitch умножаются на случайное значение из диапазона.")]
    public SoundVariance variance = SoundVariance.Fixed(1f, 1f);

    /// <summary>
    /// Проверяет что клип назначен и успешно загружен.
    /// Состояние Loading считается валидным — Unity воспроизведёт клип по готовности.
    /// </summary>
    public bool IsValid()
    {
        if (clip == null) return false;

        switch (clip.loadState)
        {
            case UnityEngine.AudioDataLoadState.Loaded:
                return true;
            case UnityEngine.AudioDataLoadState.Loading:
                // Клип ещё грузится — возвращаем true, Unity воспроизведёт его когда загрузится.
                Debug.LogWarning($"[AudioData] '{name}': клип '{clip.name}' ещё загружается — возможна тишина.");
                return true;
            case UnityEngine.AudioDataLoadState.Failed:
                Debug.LogError($"[AudioData] '{name}': клип '{clip.name}' не загружен (Failed).");
                return false;
            case UnityEngine.AudioDataLoadState.Unloaded:
                // Клип выгружен — инициируем загрузку. AudioSystem повторит попытку после Load.
                clip.LoadAudioData();
                Debug.LogWarning($"[AudioData] '{name}': клип '{clip.name}' выгружен (Unloaded) — воспроизведение невозможно.");
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// Возвращает итоговые громкость и pitch для воспроизведения.
    /// SoundVariance применяется как множитель к базовым значениям, а не как абсолютные —
    /// это сохраняет смысл базового volume при рандомизации.
    /// </summary>
    public void GetPlaybackParams(out float outVolume, out float outPitch)
    {
        if (!useVariance)
        {
            outVolume = volume;
            outPitch  = pitch;
            return;
        }

        variance.Evaluate(out float vMul, out float pMul);
        outVolume = volume * vMul;
        outPitch  = pitch  * pMul;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);
        pitch  = Mathf.Clamp(pitch, 0.1f, 3f);

        if (useVariance)
            variance = variance.Validated();
    }
#endif
}
