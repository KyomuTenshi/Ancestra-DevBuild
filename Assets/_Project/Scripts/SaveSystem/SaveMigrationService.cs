// Миграция данных при изменении формата сохранений.
// Добавить новую миграцию:
//   1. Увеличь SaveConstants.CurrentVersion.
//   2. Создай класс, реализующий IMigrationStep.
//   3. Зарегистрируй его в SaveManager (или через RegisterStep).

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Один шаг миграции между двумя версиями формата.</summary>
public interface IMigrationStep
{
    int FromVersion { get; }
    int ToVersion   { get; }

    /// <summary>Применяет изменения к SaveFile. Меняй только поля, изменившиеся между версиями.</summary>
    void Apply(SaveFile file);
}

/// <summary>
/// Применяет цепочку IMigrationStep к SaveFile, пока его версия не достигнет targetVersion.
/// Безопасен на актуальных файлах — если версия уже целевая, ничего не происходит.
/// </summary>
public class SaveMigrationService
{
    private readonly List<IMigrationStep> _steps;
    private readonly string _logPrefix = "[SaveMigrationService]";

    /// <param name="steps">Шаги миграции. Сортируются по fromVersion автоматически.</param>
    public SaveMigrationService(IEnumerable<IMigrationStep> steps = null)
    {
        _steps = steps != null
            ? new List<IMigrationStep>(steps)
            : new List<IMigrationStep>();

        _steps.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
    }

    public void RegisterStep(IMigrationStep step)
    {
        if (step == null) return;
        _steps.Add(step);
        _steps.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
    }

    /// <summary>
    /// Применяет все нужные шаги. Безопасно вызывать на актуальных файлах — ничего не произойдёт.
    /// Если файл новее текущей версии — пропускает миграцию с предупреждением.
    /// </summary>
    public bool Migrate(SaveFile file, int targetVersion)
    {
        if (file == null) return false;

        int currentVersion = file.metadata.dataVersion;

        if (currentVersion > targetVersion)
        {
            Debug.LogWarning(
                $"{_logPrefix} Файл версии {currentVersion} новее текущей {targetVersion}. " +
                "Возможна несовместимость — пропускаем миграцию."
            );
            return false;
        }

        if (currentVersion == targetVersion)
            return false;

        Debug.Log($"{_logPrefix} Миграция {currentVersion} → {targetVersion}...");

        bool anyApplied = false;

        foreach (var step in _steps)
        {
            if (step.FromVersion < currentVersion) continue;
            if (step.FromVersion >= targetVersion) break;

            try
            {
                Debug.Log($"{_logPrefix}   Шаг: v{step.FromVersion} → v{step.ToVersion}");
                step.Apply(file);
                anyApplied = true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"{_logPrefix} Ошибка при миграции v{step.FromVersion}: {ex.Message}. " +
                    "Миграция прервана — данные могут быть частично обновлены."
                );
                break;
            }
        }

        if (anyApplied)
        {
            file.metadata.dataVersion = targetVersion;
            Debug.Log($"{_logPrefix} Миграция завершена. Версия: {targetVersion}.");
        }

        return anyApplied;
    }

    public int StepCount => _steps.Count;
}

/// <summary>
/// Шаблон шага миграции v1→v2. Скопируй, переименуй и заполни Apply() для каждой новой версии.
/// Пример изменения: перенос поля из старой структуры в новую.
/// </summary>
public class MigrationStep_V1_to_V2 : IMigrationStep
{
    public int FromVersion => 1;
    public int ToVersion   => 2;

    public void Apply(SaveFile file)
    {
        // Версия 2 ещё не введена — заглушка. Добавь реальные преобразования полей здесь.
        // Пример: file.progress.startScene = file.progress.savedSceneName;
    }
}
