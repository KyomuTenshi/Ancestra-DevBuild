// Фасад системы сохранений. Оркестрирует SaveFileService (I/O), SaveMigrationService (версии),
// SaveableRegistry (снимки объектов) и AutoSaveService (периодическое сохранение).
// WorldStateData сохраняется в _pendingWorldData при LoadSlot и применяется к объектам
// поэтапно: через TryRestoreIfPending при OnEnable каждого SaveableComponent.

using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Центральный менеджер сохранений. Хранит активный прогресс (ActiveProgress) и слот (ActiveSlotIndex).
/// Асинхронное сохранение (SaveCoroutine) сериализует JSON на main thread, пишет файл в фоне.
/// </summary>
public class SaveManager : PersistentSingleton<SaveManager>
{
    public GameProgressData ActiveProgress  { get; private set; }
    public int              ActiveSlotIndex { get; private set; } = -1;

    public bool HasActiveSave =>
        ActiveProgress != null && !string.IsNullOrEmpty(ActiveProgress.savedSceneName);

    // Ожидает применения после загрузки сцены. Null = нет отложенного restore.
    private WorldStateData _pendingWorldData;

    private SaveFileService      _fileService;
    private SaveMigrationService _migrationService;
    private AutoSaveService      _autoSaveService;
    private SaveableRegistry     _registry;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        _fileService = new SaveFileService(
            Application.persistentDataPath,
            SaveConstants.SlotFileTemplate
        );

        _migrationService = new SaveMigrationService(
            new IMigrationStep[] { new MigrationStep_V1_to_V2() }
        );

        // AutoSave передаёт слот через замыкание: если слот -1, SaveCoroutine тихо выходит.
        _autoSaveService = new AutoSaveService(
            owner:  this,
            onSave: () => SaveCoroutine(ActiveSlotIndex)
        );

        _registry = new SaveableRegistry("WorldRegistry");

        // Подписываемся в Awake — RestoreAll должен знать когда сцена полностью загружена.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _autoSaveService?.Stop();
    }

    public SaveableRegistry GetRegistry() => _registry;

    /// <summary>
    /// Синхронное сохранение. Блокирует main thread.
    /// Используй ТОЛЬКО в OnApplicationQuit — там нет возможности запустить корутину.
    /// </summary>
    public bool Save(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;

        if (ActiveProgress == null)
        {
            Debug.LogWarning($"{SaveConstants.LogPrefix} Попытка сохранить пустой прогресс. Отменено.");
            MessageBus<GameSavedMessage>.Dispatch(new GameSavedMessage(slotIndex, false));
            return false;
        }

        WorldStateData worldData = _registry.CaptureAll();
        var file = new SaveFile { progress = ActiveProgress, world = worldData };

        bool success = _fileService.Write(slotIndex, file);
        MessageBus<GameSavedMessage>.Dispatch(new GameSavedMessage(slotIndex, success));
        return success;
    }

    /// <summary>
    /// Асинхронное сохранение: сериализация на main thread, I/O в фоновом потоке.
    /// JsonUtility не thread-safe — PrepareJson всегда вызывается до Task.Run.
    /// </summary>
    public IEnumerator SaveCoroutine(int slotIndex)
    {
        if (slotIndex < 0) yield break;

        if (!IsValidSlot(slotIndex) || ActiveProgress == null)
        {
            if (ActiveProgress == null)
                Debug.LogWarning($"{SaveConstants.LogPrefix} SaveCoroutine: нет активного прогресса.");
            MessageBus<GameSavedMessage>.Dispatch(new GameSavedMessage(slotIndex, false));
            yield break;
        }

        WorldStateData worldData = _registry.CaptureAll();
        var file = new SaveFile { progress = ActiveProgress, world = worldData };

        string json = _fileService.PrepareJson(slotIndex, file);
        if (json == null)
        {
            MessageBus<GameSavedMessage>.Dispatch(new GameSavedMessage(slotIndex, false));
            yield break;
        }

        string path = _fileService.GetPath(slotIndex);
        Task<bool> task = Task.Run(() => _fileService.WriteJson(path, json));

        while (!task.IsCompleted)
            yield return null;

        bool success = task.Status == TaskStatus.RanToCompletion && task.Result;
        MessageBus<GameSavedMessage>.Dispatch(new GameSavedMessage(slotIndex, success));
    }

    /// <summary>
    /// Загружает прогресс из слота.
    /// WorldStateData сохраняется в _pendingWorldData и применяется к объектам сцены
    /// либо немедленно (если объекты уже зарегистрированы), либо через TryRestoreIfPending
    /// по мере их включения.
    /// </summary>
    public GameProgressData LoadSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return null;

        SaveFile file = _fileService.Read(slotIndex);

        if (file == null)
        {
            ActiveProgress    = new GameProgressData();
            ActiveProgress.Reset();
            ActiveSlotIndex   = slotIndex;
            _pendingWorldData = null;
            Debug.Log($"{SaveConstants.LogPrefix} Слот {slotIndex} не найден. Новая игра.");
        }
        else
        {
            _migrationService.Migrate(file, SaveConstants.CurrentVersion);

            ActiveProgress    = file.progress;
            ActiveSlotIndex   = slotIndex;
            _pendingWorldData = file.world;

            // Если сцена уже загружена и объекты зарегистрированы — restore немедленно.
            // Иначе — каждый объект восстановится сам в OnEnable через TryRestoreIfPending.
            if (_registry.RegisteredObjectCount > 0)
            {
                _registry.RestoreAll(_pendingWorldData);
                _pendingWorldData = null;
            }
        }

        MessageBus<GameLoadedMessage>.Dispatch(new GameLoadedMessage(slotIndex));
        return ActiveProgress;
    }

    /// <summary>
    /// Восстанавливает отдельный компонент из _pendingWorldData.
    /// Вызывается из SaveableComponent.OnEnable() — для объектов, включающихся до OnSceneLoaded.
    /// </summary>
    public void TryRestoreIfPending(ISaveableComponent component)
    {
        if (_pendingWorldData == null || component == null) return;

        ObjectStateEntry entry = _pendingWorldData.GetEntry(component.SaveableGuid);
        if (entry == null) return;

        string typeName = component.GetType().Name;
        ComponentStateEntry compEntry = entry.components.Find(c => c.typeName == typeName);
        if (compEntry == null) return;

        try
        {
            component.RestoreFromJson(compEntry.jsonData);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"{SaveConstants.LogPrefix} TryRestoreIfPending: ошибка для {typeName}: {ex.Message}"
            );
        }
    }

    public SaveMetadata GetSlotMetadata(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return null;
        return _fileService.ReadMetadata(slotIndex);
    }

    public T GetSlot<T>(int slotIndex) where T : class, ISaveSlot, new()
    {
        if (!IsValidSlot(slotIndex)) return null;

        SaveFile file = _fileService.Read(slotIndex);
        if (file == null)
        {
            var fresh = new T();
            fresh.Reset();
            return fresh;
        }

        if (file.progress is T typedProgress) return typedProgress;

        var result = new T();
        result.Reset();
        return result;
    }

    public bool SlotExists(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;
        return _fileService.Exists(slotIndex);
    }

    public DateTime? GetSlotSaveTime(int slotIndex)
    {
        var meta = GetSlotMetadata(slotIndex);
        return meta?.SavedAt;
    }

    public void DeleteSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;

        _fileService.Delete(slotIndex);

        if (slotIndex == ActiveSlotIndex)
            ActiveProgress?.Reset();
    }

    public void EnableAutoSave(float intervalSeconds = 300f)
        => _autoSaveService.Start(intervalSeconds);

    public void DisableAutoSave() => _autoSaveService.Stop();

    public void SetServices(
        SaveFileService      fileService      = null,
        SaveMigrationService migrationService = null,
        SaveableRegistry     registry         = null)
    {
        if (fileService      != null) _fileService      = fileService;
        if (migrationService != null) _migrationService = migrationService;
        if (registry         != null) _registry         = registry;
    }

    private void OnApplicationQuit()
    {
        if (Instance != this) return;
        if ((_autoSaveService?.IsEnabled ?? false) && ActiveSlotIndex >= 0 && ActiveProgress != null)
        {
            Save(ActiveSlotIndex);
            Debug.Log($"{SaveConstants.LogPrefix} Финальное сохранение при выходе.");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Аддитивные сцены не инициируют RestoreAll — только основная сцена.
        if (_pendingWorldData == null || mode == LoadSceneMode.Additive) return;

        // К этому моменту все активные SaveableComponent уже вызвали TryRestoreIfPending в OnEnable.
        // Повторный RestoreAll здесь был бы двойным — просто очищаем.
        _pendingWorldData = null;

        Debug.Log($"{SaveConstants.LogPrefix} WorldState применён для сцены '{scene.name}'.");
    }

    private bool IsValidSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < SaveConstants.SlotCount) return true;

        Debug.LogError(
            $"{SaveConstants.LogPrefix} Неверный индекс слота: {slotIndex}. " +
            $"Допустимо: 0–{SaveConstants.SlotCount - 1}."
        );
        return false;
    }
}
