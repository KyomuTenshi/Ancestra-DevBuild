// Пользовательские настройки через PlayerPrefs. Отделён от игрового прогресса намеренно:
// при полном сбросе прогресса настройки остаются нетронутыми.
// Каждое свойство-сеттер пишет напрямую в PlayerPrefs без буферизации —
// вызови PlayerPrefs.Save() явно если нужна гарантия при краше.

using UnityEngine;

/// <summary>
/// Статическое хранилище пользовательских настроек. Хранит громкость, язык, разрешение
/// и качество графики в PlayerPrefs. Намеренно отделён от GameProgressData:
/// настройки переживают сброс прогресса.
/// </summary>
public static class AppSettings
{
    private const string KeyLanguageIndex    = "App_Lang";
    private const string KeyLanguageSelected = "App_LangSelected";
    private const string KeyGeneralVolume    = "App_GenVol";
    private const string KeyBgVolume         = "App_BgVol";
    private const string KeyAmbientVolume    = "App_AmbVol";
    private const string KeySfxVolume        = "App_SfxVol";
    private const string KeyResolutionIndex  = "App_ResIdx";
    private const string KeyQualityLevel     = "App_Quality";

    public const float DefaultGeneralVolume  = 0.75f;
    public const float DefaultBgVolume       = 0.50f;
    public const float DefaultAmbientVolume  = 0.60f;
    public const float DefaultSfxVolume      = 0.80f;

    // -1 — «не задан»: система выберет текущее разрешение экрана.
    private const int DefaultResolutionIndex = -1;

    /// <summary>
    /// Индекс языка. При установке немедленно сохраняет в PlayerPrefs —
    /// критично для первого запуска, чтобы выбор не потерялся при краше до следующего Save.
    /// </summary>
    public static int LanguageIndex
    {
        get => PlayerPrefs.GetInt(KeyLanguageIndex, 0);
        set
        {
            PlayerPrefs.SetInt(KeyLanguageIndex, value);
            PlayerPrefs.SetInt(KeyLanguageSelected, 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>True если пользователь хотя бы раз явно выбрал язык. Экран выбора языка показывается только когда false.</summary>
    public static bool IsLanguageSelected =>
        PlayerPrefs.GetInt(KeyLanguageSelected, 0) == 1;

    public static float GeneralVolume
    {
        get => PlayerPrefs.GetFloat(KeyGeneralVolume, DefaultGeneralVolume);
        set => PlayerPrefs.SetFloat(KeyGeneralVolume, value);
    }

    public static float BackgroundVolume
    {
        get => PlayerPrefs.GetFloat(KeyBgVolume, DefaultBgVolume);
        set => PlayerPrefs.SetFloat(KeyBgVolume, value);
    }

    public static float AmbientVolume
    {
        get => PlayerPrefs.GetFloat(KeyAmbientVolume, DefaultAmbientVolume);
        set => PlayerPrefs.SetFloat(KeyAmbientVolume, value);
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(KeySfxVolume, DefaultSfxVolume);
        set => PlayerPrefs.SetFloat(KeySfxVolume, value);
    }

    public static int ResolutionIndex
    {
        get => PlayerPrefs.GetInt(KeyResolutionIndex, DefaultResolutionIndex);
        set => PlayerPrefs.SetInt(KeyResolutionIndex, value);
    }

    public static int QualityLevel
    {
        get => PlayerPrefs.GetInt(KeyQualityLevel, QualitySettings.GetQualityLevel());
        set => PlayerPrefs.SetInt(KeyQualityLevel, value);
    }

    /// <summary>Сбрасывает только пользовательские настройки. Прогресс игры не затрагивает.</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KeyLanguageIndex);
        PlayerPrefs.DeleteKey(KeyLanguageSelected);
        PlayerPrefs.DeleteKey(KeyGeneralVolume);
        PlayerPrefs.DeleteKey(KeyBgVolume);
        PlayerPrefs.DeleteKey(KeyAmbientVolume);
        PlayerPrefs.DeleteKey(KeySfxVolume);
        PlayerPrefs.DeleteKey(KeyResolutionIndex);
        PlayerPrefs.DeleteKey(KeyQualityLevel);
        PlayerPrefs.Save();
    }
}
