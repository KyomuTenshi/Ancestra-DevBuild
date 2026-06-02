// Сериализуемый контейнер прогресса игрока.
// Добавляй новые поля прямо сюда. При breaking-изменениях — увеличивай
// SaveConstants.CurrentVersion и добавляй ветку в SaveMigrationService (не в этот класс).

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Прогресс игрока: последняя сцена, позиция, здоровье и инвентарь.
/// При добавлении полей увеличивай SaveConstants.CurrentVersion и добавляй шаг в SaveMigrationService.
/// </summary>
[Serializable]
public class GameProgressData : ISaveSlot
{
    [Tooltip("Имя сцены, на которой игрок сохранился")]
    public string savedSceneName = string.Empty;

    public Vector3 playerPosition = Vector3.zero;
    public Vector3 playerRotation = Vector3.zero;
    public float   playerHealth   = 100f;

    public List<string> inventoryItems = new List<string>();

    /// <summary>
    /// Сбрасывает все поля к значениям по умолчанию.
    /// inventoryItems.Clear() вместо new List — сохраняем уже аллоцированный объект.
    /// </summary>
    public void Reset()
    {
        savedSceneName = string.Empty;
        playerPosition = Vector3.zero;
        playerRotation = Vector3.zero;
        playerHealth   = 100f;
        inventoryItems.Clear();
    }
}
