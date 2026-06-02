// Интерфейс источника локализационных данных.
// Реализуется LocalizationData (ScriptableObject) и runtime-источниками (JSON, Remote).
// LocalizationManager выбирает нужный источник по индексу из AppSettings.

using UnityEngine;

/// <summary>
/// Контракт источника локализации. Менеджер обращается только через этот интерфейс,
/// что позволяет подменять источники без изменения остального кода.
/// </summary>
public interface ILocalizationSource
{
    SystemLanguage Language { get; }

    /// <summary>
    /// Возвращает локализованный текст по ключу.
    /// Возвращает null если ключ не найден — менеджер сам решает что делать (fallback или лог).
    /// </summary>
    string GetText(string key);

    /// <summary>Инициализирует внутренний кеш. Менеджер вызывает один раз при активации языка.</summary>
    void Initialize();
}
