// Менеджер локализации. Маршрутизирует запросы к активному ILocalizationSource,
// применяет fallback-язык для отсутствующих ключей и кеширует разрешённые строки.
// При смене языка кеш сбрасывается, всем подписчикам рассылается LanguageChangedMessage.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Правила форм множественного числа для конкретного языка.</summary>
public interface IPluralRules
{
    string GetSuffix(int count, SystemLanguage language);
}

/// <summary>
/// Правила множественного числа для русского, украинского и польского. Для остальных языков —
/// упрощённый fallback (one/many). Логика: проверка исключений (11-14) перед последней цифрой.
/// </summary>
public sealed class DefaultPluralRules : IPluralRules
{
    public static readonly DefaultPluralRules Instance = new DefaultPluralRules();

    public string GetSuffix(int count, SystemLanguage language)
    {
        if (count == 0) return "zero";

        int abs     = Math.Abs(count);
        int lastTwo = abs % 100;
        int lastOne = abs % 10;

        switch (language)
        {
            case SystemLanguage.Russian:
            case SystemLanguage.Ukrainian:
                if (lastTwo >= 11 && lastTwo <= 14) return "many";
                if (lastOne == 1)                   return "one";
                if (lastOne >= 2 && lastOne <= 4)   return "few";
                return "many";

            case SystemLanguage.Polish:
                if (abs == 1)                       return "one";
                if (lastTwo >= 12 && lastTwo <= 14) return "many";
                if (lastOne >= 2 && lastOne <= 4)   return "few";
                return "many";

            default:
                return abs == 1 ? "one" : "many";
        }
    }
}

/// <summary>
/// Центральный менеджер локализации. Хранит активный и fallback источники,
/// кеширует разрешённые строки и рассылает LanguageChangedMessage при смене языка.
/// Статические методы Get/GetPlural доступны без прямой ссылки на экземпляр.
/// </summary>
public class LocalizationManager : PersistentSingleton<LocalizationManager>
{
    [Header("Источники данных")]
    [Tooltip("ScriptableObject-файлы языков. Порядок = индекс в AppSettings.")]
    [SerializeField] private LocalizationData[] availableLanguages;

    [Header("Fallback-язык")]
    [Tooltip("Язык для ключей, отсутствующих в активном. Обычно English.")]
    [SerializeField] private LocalizationData fallbackLanguage;

    private ILocalizationSource _activeSource;
    private ILocalizationSource _fallbackSource;

    private readonly Dictionary<SystemLanguage, ILocalizationSource> _runtimeSources =
        new Dictionary<SystemLanguage, ILocalizationSource>();

    // Кеш сброшенных строк: очищается при каждой смене языка.
    private readonly Dictionary<string, string> _resolvedCache =
        new Dictionary<string, string>(256, StringComparer.Ordinal);

    // Список имён языков для UI Dropdown — не меняется в runtime, строим один раз.
    private List<string> _languageNamesCache;

    private IPluralRules _pluralRules = DefaultPluralRules.Instance;

    private const string LogPrefix = "[LocalizationManager]";

    /// <summary>Язык активного источника.</summary>
    public static SystemLanguage CurrentLanguage =>
        Instance?._activeSource?.Language ?? SystemLanguage.English;

    public static int CurrentLanguageIndex =>
        Instance != null ? AppSettings.LanguageIndex : 0;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        InitializeFallback();
        InitializeActiveLanguage();
    }

    private void Start()
    {
        if (Instance != this) return;

        // Прямой Dispatch — LocalizedLabel подписывается в OnEnable(), гонки со Start() нет.
        MessageBus<LanguageChangedMessage>.Dispatch(
            new LanguageChangedMessage(AppSettings.LanguageIndex)
        );
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            _activeSource   = null;
            _fallbackSource = null;
        }
    }

    public static string Get(string key)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"{LogPrefix} Get('{key}'): Instance null.");
            return FormatMissing(key);
        }
        return Instance.Resolve(key);
    }

    public static string Get(string key, params object[] args)
    {
        string raw = Get(key);
        // Avoid allocating FormatMissing(key) on every formatted call.
        // Missing marker is always "<key>", so check brackets and length instead.
        if (raw.Length == key.Length + 2 && raw[0] == '<' && raw[raw.Length - 1] == '>') return raw;

        try   { return string.Format(raw, args); }
        catch (FormatException ex)
        {
            Debug.LogWarning($"{LogPrefix} Ошибка форматирования '{key}': {ex.Message}");
            return raw;
        }
    }

    /// <summary>
    /// Возвращает форму строки в зависимости от числа.
    /// Соглашение по ключам: key.zero / key.one / key.few / key.many
    /// </summary>
    public static string GetPlural(string key, int count)
    {
        if (Instance == null) return FormatMissing(key);

        string suffix     = Instance._pluralRules.GetSuffix(count, CurrentLanguage);
        string specificKey = $"{key}.{suffix}";
        string resolved    = Instance.Resolve(specificKey, suppressWarning: true);

        // Avoid allocating FormatMissing(specificKey) on every call.
        bool isMissing = resolved.Length == specificKey.Length + 2 &&
                         resolved[0] == '<' && resolved[resolved.Length - 1] == '>';
        if (!isMissing)
            return string.Format(resolved, count);

        return Get(key, count);
    }

    /// <summary>Список названий языков для Dropdown.</summary>
    public static List<string> GetAvailableLanguageNames()
    {
        if (Instance == null) return new List<string>();
        return Instance.BuildLanguageNameList();
    }

    /// <summary>
    /// Переключает язык. Рассылает LanguageChangedMessage — все LocalizedLabel обновятся.
    /// </summary>
    public void SetLanguageByIndex(int index)
    {
        if (!TryGetSourceByIndex(index, out ILocalizationSource source))
        {
            Debug.LogWarning($"{LogPrefix} SetLanguageByIndex({index}): индекс вне диапазона.");
            return;
        }

        int safeIndex = Mathf.Clamp(index, 0, availableLanguages.Length - 1);
        AppSettings.LanguageIndex = safeIndex;

        ActivateSource(source);
        MessageBus<LanguageChangedMessage>.Dispatch(new LanguageChangedMessage(safeIndex));
    }

    /// <summary>Регистрирует runtime-источник (JSON, Remote и т.д.).</summary>
    public void RegisterSource(ILocalizationSource source)
    {
        if (source == null) return;

        _runtimeSources[source.Language] = source;
        // Сбрасываем кеш: новый источник мог добавить язык, которого не было в списке.
        _languageNamesCache = null;
        Debug.Log($"{LogPrefix} Зарегистрирован источник для {source.Language}.");

        if (_activeSource?.Language == source.Language)
        {
            ActivateSource(source);
            MessageBus<LanguageChangedMessage>.Dispatch(
                new LanguageChangedMessage(AppSettings.LanguageIndex)
            );
        }
    }

    public void SetPluralRules(IPluralRules rules)
    {
        _pluralRules = rules ?? DefaultPluralRules.Instance;
    }

    private void InitializeFallback()
    {
        if (fallbackLanguage == null) return;
        fallbackLanguage.Initialize();
        _fallbackSource = fallbackLanguage;
        Debug.Log($"{LogPrefix} Fallback: {fallbackLanguage.language}.");
    }

    private void InitializeActiveLanguage()
    {
        if (availableLanguages == null || availableLanguages.Length == 0)
        {
            Debug.LogError($"{LogPrefix} availableLanguages пуст!");
            return;
        }

        int savedIndex = AppSettings.LanguageIndex;
        int safeIndex  = Mathf.Clamp(savedIndex, 0, availableLanguages.Length - 1);

        if (savedIndex != safeIndex)
        {
            Debug.LogWarning($"{LogPrefix} Индекс {savedIndex} вне диапазона, используется {safeIndex}.");
            AppSettings.LanguageIndex = safeIndex;
        }

        if (TryGetSourceByIndex(safeIndex, out ILocalizationSource source))
            ActivateSource(source);
        else
            Debug.LogError($"{LogPrefix} Не удалось получить источник для индекса {safeIndex}.");
    }

    private void ActivateSource(ILocalizationSource source)
    {
        source.Initialize();
        _activeSource = source;
        _resolvedCache.Clear();
        Debug.Log($"{LogPrefix} Активирован: {source.Language}.");
    }

    private string Resolve(string key, bool suppressWarning = false)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"{LogPrefix} Resolve: пустой ключ.");
            return string.Empty;
        }

        if (_resolvedCache.TryGetValue(key, out string cached)) return cached;

        string result = _activeSource?.GetText(key);

        if (result == null && _fallbackSource != null && _fallbackSource != _activeSource)
        {
            result = _fallbackSource.GetText(key);
            if (result != null && !suppressWarning)
                Debug.LogWarning($"{LogPrefix} Ключ '{key}' не в активном языке, используется fallback.");
        }

        if (result == null)
        {
            if (!suppressWarning)
                Debug.LogWarning($"{LogPrefix} Ключ '{key}' не найден ни в одном источнике.");
            result = FormatMissing(key);
        }

        _resolvedCache[key] = result;
        return result;
    }

    private bool TryGetSourceByIndex(int index, out ILocalizationSource source)
    {
        source = null;
        if (availableLanguages == null || index < 0 || index >= availableLanguages.Length) return false;

        var soData = availableLanguages[index];
        if (soData == null) return false;

        // Runtime-источник имеет приоритет над SO — позволяет подменить язык в runtime.
        if (_runtimeSources.TryGetValue(soData.language, out ILocalizationSource runtime))
        {
            source = runtime;
            return true;
        }

        source = soData;
        return true;
    }

    private List<string> BuildLanguageNameList()
    {
        if (_languageNamesCache != null) return _languageNamesCache;

        _languageNamesCache = new List<string>(availableLanguages?.Length ?? 0);
        if (availableLanguages != null)
            foreach (var lang in availableLanguages)
                if (lang != null) _languageNamesCache.Add(lang.language.ToString());

        return _languageNamesCache;
    }

    // <key> — визуально заметный fallback, легко найти в UI во время разработки.
    private static string FormatMissing(string key) => $"<{key}>";
}
