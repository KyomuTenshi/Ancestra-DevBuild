// Константы системы сохранений. Единая точка для всех магических значений SaveSystem.
// Изменение CurrentVersion без добавления IMigrationStep в SaveMigrationService
// приведёт к тому что старые файлы будут загружаться без миграции.

/// <summary>
/// Константы формата файлов сохранений: версия, количество слотов и шаблон имени файла.
/// При изменении CurrentVersion обязательно добавь шаг миграции в SaveMigrationService.
/// </summary>
public static class SaveConstants
{
    /// <summary>
    /// Версия формата сохранений. Увеличивай при любом breaking-изменении структуры данных
    /// и добавляй соответствующую ветку в SaveMigrationService.
    /// </summary>
    public const int CurrentVersion = 1;

    public const int SlotCount = 3;

    // {0} — номер слота (0, 1, 2, ...).
    public const string SlotFileTemplate = "save_slot_{0}.json";

    public const string LogPrefix = "[SaveSystem]";
}
