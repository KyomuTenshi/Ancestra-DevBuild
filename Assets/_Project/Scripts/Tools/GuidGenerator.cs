// Утилиты для генерации и валидации GUID-идентификаторов SaveableId.
// Тонкая обёртка над System.Guid с дополнительным методом Short() для читаемых логов.

using System;

/// <summary>
/// Генерирует стандартные GUID (формат 8-4-4-4-12) и проверяет их валидность.
/// Short() возвращает первые 8 символов — только для логов, не является уникальным ID.
/// </summary>
public static class GuidGenerator
{
    public static string New() => Guid.NewGuid().ToString();

    /// <summary>Первые 8 символов GUID для краткого отображения в логах. Не является уникальным идентификатором.</summary>
    public static string Short(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return "????????";
        return guid.Length >= 8 ? guid.Substring(0, 8) : guid;
    }

    public static bool IsValid(string guid) =>
        !string.IsNullOrEmpty(guid) && Guid.TryParse(guid, out _);
}
