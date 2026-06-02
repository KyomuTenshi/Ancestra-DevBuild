// Сервис I/O для файлов сохранений.
// Атомарная запись (write → .tmp, rename → final) предотвращает повреждение файла при краше.
// При старте восстанавливает оборванные .tmp файлы. Контрольная сумма обнаруживает ручное изменение.
// JsonUtility не thread-safe: PrepareJson вызывается на main thread, WriteJson — в фоне.

using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Читает и записывает SaveFile в JSON на диск. Поддерживает legacy-формат через LegacyEnvelope.
/// Атомарная запись защищает от частичной записи при аварийном завершении приложения.
/// </summary>
public class SaveFileService
{
    private readonly string _directory;
    private readonly string _fileTemplate;
    private readonly string _logPrefix = "[SaveFileService]";

    public SaveFileService(string directory, string fileTemplate)
    {
        _directory    = directory;
        _fileTemplate = fileTemplate;
        EnsureDirectory();
        RecoverOrphanedTempFiles();
    }

    /// <summary>
    /// Синхронная запись. Блокирует main thread.
    /// Допустимо только в OnApplicationQuit — там нет возможности запустить корутину.
    /// </summary>
    public bool Write(int slotIndex, SaveFile file)
    {
        string json = PrepareJson(slotIndex, file);
        if (json == null) return false;
        return WriteJson(GetPath(slotIndex), json);
    }

    /// <summary>
    /// Сериализует SaveFile в JSON на main thread (JsonUtility не thread-safe).
    /// Вызывай до Task.Run — передавай готовую строку в фоновый поток.
    /// </summary>
    public string PrepareJson(int slotIndex, SaveFile file)
    {
        if (file == null)
        {
            Debug.LogError($"{_logPrefix} PrepareJson: null SaveFile для слота {slotIndex}.");
            return null;
        }

        int checksum = ComputeChecksum(file.progress, file.world);
        file.metadata.Stamp(SaveConstants.CurrentVersion, checksum);
        file.metadata.slotIndex = slotIndex;

        try
        {
            return JsonUtility.ToJson(file, prettyPrint: true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"{_logPrefix} Ошибка сериализации слота {slotIndex}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Записывает JSON атомарно: сначала в .tmp, затем переименовывает.
    /// На POSIX — rename(), на Windows — ReplaceFile(). Безопасно вызывать из фонового потока.
    /// </summary>
    public bool WriteJson(string finalPath, string json)
    {
        string tempPath = finalPath + ".tmp";

        try
        {
            File.WriteAllText(tempPath, json);

            if (File.Exists(finalPath))
                File.Replace(tempPath, finalPath, null);
            else
                File.Move(tempPath, finalPath);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{_logPrefix} Ошибка записи '{finalPath}': {ex.Message}");
            TryDelete(tempPath);
            return false;
        }
    }

    public SaveFile Read(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (!File.Exists(path)) return null;

        string json = ReadText(path);
        if (json == null) return null;

        SaveFile file = TryDeserialize<SaveFile>(json, slotIndex);
        if (file != null && file.metadata != null && file.metadata.dataVersion > 0)
            return ValidateChecksum(file, slotIndex) ? file : null;

        return TryReadLegacy(json, slotIndex);
    }

    public SaveMetadata ReadMetadata(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (!File.Exists(path)) return null;

        string json = ReadText(path);
        if (json == null) return null;

        SaveFile file = TryDeserialize<SaveFile>(json, slotIndex);
        if (file?.metadata != null && file.metadata.dataVersion > 0)
            return file.metadata;

        var legacy = TryDeserialize<LegacyEnvelope>(json, slotIndex);
        if (legacy != null)
            return SaveMetadata.FromLegacyEnvelope(slotIndex, legacy.savedAtUtc, legacy.version);

        return null;
    }

    public bool   Exists(int slotIndex)  => File.Exists(GetPath(slotIndex));
    public string GetPath(int slotIndex) =>
        Path.Combine(_directory, string.Format(_fileTemplate, slotIndex));

    public bool Delete(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (!File.Exists(path)) return true;

        try
        {
            File.Delete(path);
            Debug.Log($"{_logPrefix} Слот {slotIndex} удалён.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{_logPrefix} Ошибка удаления слота {slotIndex}: {ex.Message}");
            return false;
        }
    }

    // При запуске восстанавливаем .tmp файлы, оставшиеся от прерванной записи.
    // Если main-файл цел — tmp является мусором и удаляется.
    private void RecoverOrphanedTempFiles()
    {
        for (int i = 0; i < SaveConstants.SlotCount; i++)
        {
            string finalPath = GetPath(i);
            string tempPath  = finalPath + ".tmp";

            if (!File.Exists(tempPath)) continue;

            if (!File.Exists(finalPath))
            {
                try
                {
                    File.Move(tempPath, finalPath);
                    Debug.Log($"{_logPrefix} Слот {i} восстановлен из temp-файла.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{_logPrefix} Не удалось восстановить слот {i}: {ex.Message}");
                }
            }
            else
            {
                TryDelete(tempPath);
            }
        }
    }

    private int ComputeChecksum(GameProgressData progress, WorldStateData world)
    {
        string progressJson = JsonUtility.ToJson(progress);
        string worldJson    = JsonUtility.ToJson(world);

        int hash = 17;
        foreach (char c in progressJson) hash = hash * 31 + c;
        foreach (char c in worldJson)    hash = hash * 31 + c;
        return hash;
    }

    private bool ValidateChecksum(SaveFile file, int slotIndex)
    {
        // -1 означает что checksum не записывался (legacy или отключён).
        if (file.metadata.dataChecksum == -1) return true;

        int computed = ComputeChecksum(file.progress, file.world);
        if (computed != file.metadata.dataChecksum)
        {
            Debug.LogWarning(
                $"{_logPrefix} Слот {slotIndex}: checksum не совпадает. " +
                "Файл повреждён или изменён вручную."
            );
            return false;
        }
        return true;
    }

    private string ReadText(string path)
    {
        try
        {
            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning($"{_logPrefix} Файл пуст: {path}");
                return null;
            }
            return text;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{_logPrefix} Ошибка чтения {path}: {ex.Message}");
            return null;
        }
    }

    private T TryDeserialize<T>(string json, int slotIndex) where T : class, new()
    {
        try
        {
            var obj = new T();
            JsonUtility.FromJsonOverwrite(json, obj);
            return obj;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"{_logPrefix} Слот {slotIndex}: ошибка десериализации как {typeof(T).Name}: {ex.Message}"
            );
            return null;
        }
    }

    private SaveFile TryReadLegacy(string json, int slotIndex)
    {
        var legacy = TryDeserialize<LegacyEnvelope>(json, slotIndex);
        if (legacy?.data == null) return null;

        Debug.Log($"{_logPrefix} Слот {slotIndex}: загружен legacy-формат. Конвертируем.");

        return new SaveFile
        {
            metadata = SaveMetadata.FromLegacyEnvelope(slotIndex, legacy.savedAtUtc, legacy.version),
            progress = legacy.data,
            world    = new WorldStateData(),
        };
    }

    private void EnsureDirectory()
    {
        if (!Directory.Exists(_directory))
        {
            try { Directory.CreateDirectory(_directory); }
            catch (Exception ex)
            {
                Debug.LogError($"{_logPrefix} Не удалось создать директорию {_directory}: {ex.Message}");
            }
        }
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* удаляем по возможности */ }
    }

    [Serializable]
    private class LegacyEnvelope
    {
        public int              version;
        public string           savedAtUtc;
        public int              checksum;
        public GameProgressData data;
    }
}
