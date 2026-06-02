// Универсальная сериализуемая строка с двумя режимами: хардкод и ключ локализации.
// Используй вместо string в ScriptableObjects и MonoBehaviours, когда строка
// может быть как константой, так и локализованным текстом.

using System;
using UnityEngine;

/// <summary>
/// Строка с двумя режимами: RawText (хардкод) и LocalizedKey (через LocalizationManager).
/// Неявное приведение к string автоматически разрешает значение без явного вызова Resolve().
/// </summary>
[Serializable]
public struct LocalizableString
{
    [Tooltip("False = использовать RawText, True = использовать LocalizedKey")]
    public bool IsLocalized;

    [Tooltip("Текст напрямую (если IsLocalized = false). Для прототипов или нелокализованных элементов.")]
    public string RawText;

    [Tooltip("Ключ из LocKeys (если IsLocalized = true). Пример: ui.menu.play")]
    public string LocalizedKey;

    /// <summary>Хардкод-строка без локализации.</summary>
    public static LocalizableString Raw(string text) => new LocalizableString
    {
        IsLocalized  = false,
        RawText      = text,
        LocalizedKey = string.Empty,
    };

    /// <summary>Строка по ключу из LocKeys.</summary>
    public static LocalizableString Localized(string key) => new LocalizableString
    {
        IsLocalized  = true,
        RawText      = string.Empty,
        LocalizedKey = key,
    };

    /// <summary>
    /// Разрешает строку в текст.
    /// Если менеджер недоступен, а ключ задан — возвращает ключ в угловых скобках как fallback.
    /// </summary>
    public string Resolve()
    {
        if (!IsLocalized) return RawText ?? string.Empty;

        if (string.IsNullOrEmpty(LocalizedKey)) return string.Empty;

        return LocalizationManager.Instance != null
            ? LocalizationManager.Get(LocalizedKey)
            : $"<{LocalizedKey}>";
    }

    /// <summary>Разрешает строку с форматированием: string.Format(resolved, args).</summary>
    public string Resolve(params object[] args)
    {
        string raw = Resolve();
        if (args == null || args.Length == 0) return raw;

        try   { return string.Format(raw, args); }
        catch { return raw; }
    }

    /// <summary>Неявное приведение к string — позволяет использовать LocalizableString как обычную строку.</summary>
    public static implicit operator string(LocalizableString s) => s.Resolve();

    public static implicit operator LocalizableString(string raw) => Raw(raw);

    public bool IsEmpty => !IsLocalized
        ? string.IsNullOrEmpty(RawText)
        : string.IsNullOrEmpty(LocalizedKey);

    public override string ToString() => Resolve();
}
