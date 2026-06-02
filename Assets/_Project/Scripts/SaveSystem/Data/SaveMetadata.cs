// Метаданные файла сохранения: версия формата, время, счётчик и контрольная сумма.
// Не содержит игровых данных — только техническую информацию о файле.
// savedAtUtc хранится в строке ISO 8601 ("o"), т.к. JsonUtility не поддерживает DateTime.

using System;
using UnityEngine;

/// <summary>
/// Техническая информация о файле сохранения. SaveFile.metadata обновляется при каждой записи
/// через Stamp(), который фиксирует версию, время UTC и контрольную сумму данных.
/// </summary>
[Serializable]
public class SaveMetadata
{
    public int slotIndex;

    public int dataVersion;

    // Строка вместо DateTime: JsonUtility не поддерживает DateTime напрямую.
    // Формат ISO 8601 ("o") корректно сериализуется и разбирается на любой платформе.
    public string savedAtUtc = string.Empty;

    public int saveCount;

    public string locationName = string.Empty;

    // Имя сцены для загрузки — это не игровая логика, а техническая метаинформация.
    public string sceneName = string.Empty;

    public float playTimeSeconds;

    // -1 означает «контрольная сумма не проверялась» (старые файлы или legacy-импорт).
    public int dataChecksum = -1;

    /// <summary>Распаршенное время сохранения. Null если строка отсутствует или некорректна.</summary>
    public DateTime? SavedAt
    {
        get
        {
            if (string.IsNullOrEmpty(savedAtUtc)) return null;
            if (DateTime.TryParse(savedAtUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt))
                return dt;
            return null;
        }
    }

    /// <summary>Форматированное игровое время для UI (H:MM:SS или MM:SS).</summary>
    public string FormattedPlayTime
    {
        get
        {
            var span = TimeSpan.FromSeconds(playTimeSeconds);
            return span.Hours > 0
                ? $"{span.Hours}:{span.Minutes:D2}:{span.Seconds:D2}"
                : $"{span.Minutes}:{span.Seconds:D2}";
        }
    }

    /// <summary>Заполняет поля версии, времени и контрольной суммы перед записью на диск.</summary>
    public void Stamp(int version, int checksum)
    {
        dataVersion  = version;
        savedAtUtc   = DateTime.UtcNow.ToString("o");
        dataChecksum = checksum;
        saveCount++;
    }

    /// <summary>Строит SaveMetadata из старого формата файла (без метаданных).</summary>
    public static SaveMetadata FromLegacyEnvelope(int slot, string savedAtUtc, int version)
    {
        return new SaveMetadata
        {
            slotIndex    = slot,
            dataVersion  = version,
            savedAtUtc   = savedAtUtc,
            saveCount    = 1,
            dataChecksum = -1,
        };
    }
}

/// <summary>Полный файл сохранения: метаданные + прогресс + состояние объектов мира.</summary>
[Serializable]
public class SaveFile
{
    public SaveMetadata     metadata = new SaveMetadata();
    public GameProgressData progress = new GameProgressData();
    public WorldStateData   world    = new WorldStateData();
}
