// ScriptableObject с таблицей локализации для одного языка.
// Строит внутренний словарь из списка ключ-значение при первом обращении.
// При изменении данных в Editor кеш сбрасывается через OnValidate.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реализует ILocalizationSource на основе ScriptableObject.
/// Список pairs конвертируется в словарь при Initialize() и кешируется до следующего OnEnable.
/// </summary>
[CreateAssetMenu(fileName = "NewLanguage", menuName = "Localization/Language Data")]
public class LocalizationData : ScriptableObject, ILocalizationSource
{
    [Tooltip("Язык этого набора данных")]
    public SystemLanguage language;

    [Serializable]
    public class LocalizationPair
    {
        [Tooltip("Уникальный ключ, например: menu.play, hud.health")]
        public string key;

        [TextArea(1, 4)]
        [Tooltip("Локализованный текст. Поддерживает {0}, {1} для форматирования.")]
        public string value;
    }

    [Tooltip("Список ключ-значение для этого языка")]
    public List<LocalizationPair> pairs = new List<LocalizationPair>();

    private Dictionary<string, string> _cache;
    private bool _isInitialized;

    public SystemLanguage Language => language;

    /// <summary>
    /// Строит словарь из списка pairs. Повторный вызов — no-op, кеш пересобирается только
    /// после OnEnable или OnValidate (горячая перезагрузка в Editor).
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;
        _cache = new Dictionary<string, string>(pairs.Count, StringComparer.Ordinal);
        int duplicateCount = 0;

        foreach (var pair in pairs)
        {
            if (string.IsNullOrWhiteSpace(pair.key))
            {
                Debug.LogWarning($"[LocalizationData] '{name}': пропущен элемент с пустым ключом.");
                continue;
            }

            if (!_cache.TryAdd(pair.key, pair.value ?? string.Empty))
            {
                Debug.LogWarning(
                    $"[LocalizationData] '{name}': дубликат ключа '{pair.key}'. " +
                    "Оставлено первое значение."
                );
                duplicateCount++;
            }
        }

        _isInitialized = true;

        Debug.Log(
            $"[LocalizationData] '{name}' ({language}): " +
            $"инициализировано {_cache.Count} ключей" +
            (duplicateCount > 0 ? $", пропущено дубликатов: {duplicateCount}." : ".")
        );
    }

    /// <summary>Возвращает текст или null. Null — сигнал менеджеру применить fallback-язык.</summary>
    public string GetText(string key)
    {
        if (!_isInitialized) Initialize();
        return _cache.TryGetValue(key, out string value) ? value : null;
    }

    private void OnEnable()
    {
        // Сбрасываем кеш при загрузке SO — данные в памяти могут не совпадать с диском.
        _isInitialized = false;
        _cache         = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _isInitialized = false;
        _cache         = null;
    }
#endif
}
