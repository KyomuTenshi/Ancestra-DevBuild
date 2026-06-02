// Обёртка над AudioMixer. Хранит линейные значения громкости в кеше и
// конвертирует их в дБ при записи. Имена параметров должны точно совпадать
// с Exposed Parameters в AudioMixer Inspector.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Конвертирует линейную громкость [0,1] в дБ для AudioMixer через logarithm.
/// Кешированные значения позволяют читать текущую громкость без AudioMixer.GetFloat().
/// </summary>
public class AudioMixerRouter
{
    // Имена должны точно совпадать с Exposed Parameters в AudioMixer Inspector.
    public const string ParamMaster  = "MasterVolume";
    public const string ParamMusic   = "MusicVolume";
    public const string ParamAmbient = "AmbientVolume";
    public const string ParamSfx     = "SFXVolume";
    public const string ParamUI      = "UIVolume";

    // Ниже этого порога линейная громкость считается тишиной — логарифм от нуля не определён.
    private const float SilenceThreshold = 0.0001f;
    private const float SilenceDb        = -80f;
    private const string LogPrefix       = "[AudioMixerRouter]";

    private readonly AudioMixer _mixer;
    private readonly bool _isValid;

    // Кеш линейных значений: избегает обращений к AudioMixer.GetFloat() при каждом запросе.
    private readonly Dictionary<string, float> _linearCache =
        new Dictionary<string, float>(8);

    public AudioMixerRouter(AudioMixer mixer)
    {
        _mixer   = mixer;
        _isValid = mixer != null;

        if (!_isValid)
            Debug.LogError($"{LogPrefix} AudioMixer не задан — все операции будут проигнорированы.");
    }

    public void SetMasterVolume(float linear)  => Set(ParamMaster,  linear);
    public void SetMusicVolume(float linear)   => Set(ParamMusic,   linear);
    public void SetAmbientVolume(float linear) => Set(ParamAmbient, linear);
    public void SetSfxVolume(float linear)     => Set(ParamSfx,     linear);
    public void SetUiVolume(float linear)      => Set(ParamUI,      linear);

    public float GetMasterVolume()  => GetCached(ParamMaster);
    public float GetMusicVolume()   => GetCached(ParamMusic);
    public float GetAmbientVolume() => GetCached(ParamAmbient);
    public float GetSfxVolume()     => GetCached(ParamSfx);

    /// <summary>Устанавливает линейную громкость [0,1], конвертируя её в дБ для AudioMixer.</summary>
    public void Set(string paramName, float linear)
    {
        if (!_isValid) return;

        linear = Mathf.Clamp01(linear);
        _linearCache[paramName] = linear;

        if (!_mixer.SetFloat(paramName, LinearToDb(linear)))
            Debug.LogError($"{LogPrefix} SetFloat('{paramName}') не выполнен. " +
                           "Убедитесь, что параметр добавлен в Exposed Parameters в AudioMixer Inspector " +
                           "с именем строго как в константах ParamMaster/ParamMusic/ParamAmbient/ParamSfx.");
    }

    /// <summary>Возвращает кешированное значение без обращения к AudioMixer. O(1).</summary>
    public float GetCached(string paramName)
    {
        return _linearCache.TryGetValue(paramName, out float val) ? val : 1f;
    }

    /// <summary>Читает значение напрямую из AudioMixer. Используй для первоначальной синхронизации.</summary>
    public float GetFromMixer(string paramName)
    {
        if (!_isValid) return 0f;
        if (_mixer.GetFloat(paramName, out float db))
            return DbToLinear(db);
        return 0f;
    }

    /// <summary>Снимок текущих значений. Используй для сохранения громкости перед паузой.</summary>
    public Dictionary<string, float> GetSnapshot()
    {
        return new Dictionary<string, float>(_linearCache);
    }

    public void ApplySnapshot(Dictionary<string, float> snapshot)
    {
        if (snapshot == null) return;
        foreach (var kv in snapshot)
            Set(kv.Key, kv.Value);
    }

    public static float LinearToDb(float linear)
    {
        linear = Mathf.Clamp01(linear);
        return linear <= SilenceThreshold ? SilenceDb : Mathf.Log10(linear) * 20f;
    }

    public static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);
}
