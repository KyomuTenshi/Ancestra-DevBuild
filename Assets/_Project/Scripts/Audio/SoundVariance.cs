// Вспомогательная структура для рандомизации параметров воспроизведения.
// Применяется AudioData.GetPlaybackParams() как множитель к базовым volume и pitch.

using UnityEngine;

/// <summary>
/// Диапазоны рандомизации [Min, Max] для громкости и pitch.
/// Когда Min == Max, <see cref="Evaluate"/> пропускает Random.Range и не создаёт GC-давления.
/// </summary>
[System.Serializable]
public struct SoundVariance
{
    [Tooltip("Минимальный множитель громкости (0–1). Итоговая громкость = volume × [Min,Max].")]
    [Range(0f, 1f)]
    public float MinVolume;

    [Tooltip("Максимальный множитель громкости (0–1).")]
    [Range(0f, 1f)]
    public float MaxVolume;

    [Tooltip("Минимальный pitch.")]
    [Range(0.1f, 3f)]
    public float MinPitch;

    [Tooltip("Максимальный pitch.")]
    [Range(0.1f, 3f)]
    public float MaxPitch;

    /// <summary>Создаёт SoundVariance без рандомизации — для обратной совместимости с AudioData без вариативности.</summary>
    public static SoundVariance Fixed(float volume = 1f, float pitch = 1f) =>
        new SoundVariance
        {
            MinVolume = volume,
            MaxVolume = volume,
            MinPitch  = pitch,
            MaxPitch  = pitch,
        };

    /// <summary>
    /// Возвращает случайные громкость и pitch в заданных диапазонах.
    /// Когда Min == Max, пропускает Random.Range — нет аллокаций и нет GC-давления.
    /// </summary>
    public void Evaluate(out float volume, out float pitch)
    {
        volume = Mathf.Approximately(MinVolume, MaxVolume)
            ? MinVolume
            : Random.Range(MinVolume, MaxVolume);

        pitch = Mathf.Approximately(MinPitch, MaxPitch)
            ? MinPitch
            : Random.Range(MinPitch, MaxPitch);
    }

    /// <summary>Гарантирует Min ≤ Max. Вызывай из OnValidate().</summary>
    public SoundVariance Validated()
    {
        return new SoundVariance
        {
            MinVolume = Mathf.Min(MinVolume, MaxVolume),
            MaxVolume = Mathf.Max(MinVolume, MaxVolume),
            MinPitch  = Mathf.Min(MinPitch, MaxPitch),
            MaxPitch  = Mathf.Max(MinPitch, MaxPitch),
        };
    }
}
