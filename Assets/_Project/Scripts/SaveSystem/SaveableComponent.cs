// Базовый класс для компонентов, участвующих в системе сохранений.
// Регистрирует себя в SaveableRegistry при OnEnable и отписывается при OnDisable.
// Подкласс реализует только Capture() и Restore() — всё остальное берёт на себя этот класс.

using UnityEngine;

/// <summary>
/// Абстрактный MonoBehaviour с автоматической интеграцией в SaveManager.
/// Для работы требует SaveableId на том же GameObject — без него объект не будет сохранён.
/// </summary>
public abstract class SaveableComponent<TData> : MonoBehaviour, ISaveableComponent
    where TData : new()
{
    private SaveableId _saveableId;

    public abstract TData Capture();
    public abstract void Restore(TData data);

    string ISaveableComponent.SaveableGuid
    {
        get
        {
            EnsureSaveableId();
            return _saveableId != null ? _saveableId.Id : string.Empty;
        }
    }

    string ISaveableComponent.DebugName   => name;
    bool   ISaveableComponent.IsActiveInScene => gameObject.activeSelf;
    Vector3 ISaveableComponent.WorldPosition  => transform.position;

    string ISaveableComponent.CaptureToJson()
    {
        TData data = Capture();
        return JsonUtility.ToJson(data);
    }

    void ISaveableComponent.RestoreFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        TData data = JsonUtility.FromJson<TData>(json);
        Restore(data);
    }

    void ISaveableComponent.SetActiveInScene(bool active)
    {
        gameObject.SetActive(active);
    }

    protected virtual void OnEnable()
    {
        EnsureSaveableId();

        if (_saveableId == null || !_saveableId.HasValidId)
        {
            Debug.LogWarning(
                $"[SaveableComponent] '{name}': нет SaveableId. " +
                "Добавь компонент SaveableId на этот GameObject.",
                this
            );
            return;
        }

        SaveManager.Instance?.GetRegistry()?.Register(this);

        // Если LoadSlot вызвали до загрузки этой сцены — _pendingWorldData ждёт.
        // Восстанавливаем немедленно, не дожидаясь OnSceneLoaded.
        SaveManager.Instance?.TryRestoreIfPending(this);
    }

    protected virtual void OnDisable()
    {
        SaveManager.Instance?.GetRegistry()?.Unregister(this);
    }

    private void EnsureSaveableId()
    {
        if (_saveableId == null)
            _saveableId = GetComponent<SaveableId>();
    }

    [ContextMenu("Save state now")]
    public void SaveStateNow()
    {
        var registry = SaveManager.Instance?.GetRegistry();
        if (registry == null)
        {
            Debug.LogWarning($"[SaveableComponent] '{name}': SaveableRegistry недоступен.", this);
            return;
        }
        Debug.Log($"[SaveableComponent] '{name}': используй SaveManager.Save() для сохранения всех данных.");
    }
}
