// Реестр сохраняемых компонентов без FindObjectOfType.
// Компоненты регистрируются сами через OnEnable/OnDisable в SaveableComponent.
// Один GUID может иметь несколько компонентов (например, DoorState + LockState на одном объекте).

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Словарь GUID → список ISaveableComponent. CaptureAll() снимает JSON-снимок всех объектов,
/// RestoreAll() восстанавливает их из WorldStateData. Объекты без записи не трогаются.
/// </summary>
public class SaveableRegistry
{
    private readonly Dictionary<string, List<ISaveableComponent>> _registry =
        new Dictionary<string, List<ISaveableComponent>>(64);

    private readonly string _logPrefix;

    public SaveableRegistry(string debugName = "SaveableRegistry")
    {
        _logPrefix = $"[{debugName}]";
    }

    /// <summary>Вызывается из SaveableComponent.OnEnable().</summary>
    public void Register(ISaveableComponent component)
    {
        if (component == null) return;

        string guid = component.SaveableGuid;
        if (!GuidGenerator.IsValid(guid))
        {
            Debug.LogWarning($"{_logPrefix} Компонент {component.GetType().Name} " +
                             "имеет невалидный GUID — не будет сохранён.");
            return;
        }

        if (!_registry.TryGetValue(guid, out List<ISaveableComponent> list))
        {
            list = new List<ISaveableComponent>(2);
            _registry[guid] = list;
        }

        if (!list.Contains(component))
            list.Add(component);
    }

    /// <summary>Вызывается из SaveableComponent.OnDisable().</summary>
    public void Unregister(ISaveableComponent component)
    {
        if (component == null) return;

        string guid = component.SaveableGuid;
        if (_registry.TryGetValue(guid, out List<ISaveableComponent> list))
        {
            list.Remove(component);
            if (list.Count == 0)
                _registry.Remove(guid);
        }
    }

    /// <summary>Снимает состояние всех зарегистрированных компонентов.</summary>
    public WorldStateData CaptureAll()
    {
        var worldData = new WorldStateData();
        int capturedObjects = 0;

        foreach (var kv in _registry)
        {
            string guid = kv.Key;
            List<ISaveableComponent> components = kv.Value;

            if (components.Count == 0) continue;

            // Первый компонент даёт объектные данные (активность, позиция).
            var first = components[0];
            var entry = new ObjectStateEntry
            {
                guid          = guid,
                debugName     = first.DebugName,
                isActive      = first.IsActiveInScene,
                worldPosition = first.WorldPosition,
                components    = new System.Collections.Generic.List<ComponentStateEntry>(components.Count),
            };

            foreach (var comp in components)
            {
                try
                {
                    string json = comp.CaptureToJson();
                    entry.components.Add(new ComponentStateEntry
                    {
                        typeName = comp.GetType().Name,
                        jsonData = json,
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{_logPrefix} Ошибка Capture для {comp.GetType().Name} " +
                                  $"(GUID: {GuidGenerator.Short(guid)}): {ex.Message}");
                }
            }

            worldData.SetEntry(entry);
            capturedObjects++;
        }

        Debug.Log($"{_logPrefix} CaptureAll: сохранено {capturedObjects} объектов, " +
                  $"{worldData.Count} записей.");
        return worldData;
    }

    /// <summary>
    /// Восстанавливает состояние из WorldStateData.
    /// Объекты без записи в данных не трогаются.
    /// </summary>
    public void RestoreAll(WorldStateData worldData)
    {
        if (worldData == null) return;

        int restoredComponents = 0;

        foreach (var kv in _registry)
        {
            string guid = kv.Key;
            ObjectStateEntry entry = worldData.GetEntry(guid);

            if (entry == null) continue;

            List<ISaveableComponent> components = kv.Value;

            if (components.Count > 0)
                components[0].SetActiveInScene(entry.isActive);

            foreach (var comp in components)
            {
                string typeName = comp.GetType().Name;
                ComponentStateEntry compEntry = entry.components.Find(
                    c => c.typeName == typeName
                );

                if (compEntry == null) continue;

                try
                {
                    comp.RestoreFromJson(compEntry.jsonData);
                    restoredComponents++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{_logPrefix} Ошибка Restore для {typeName} " +
                                  $"(GUID: {GuidGenerator.Short(guid)}): {ex.Message}");
                }
            }
        }

        Debug.Log($"{_logPrefix} RestoreAll: восстановлено {restoredComponents} компонентов.");
    }

    public int RegisteredObjectCount => _registry.Count;

    public void Clear() => _registry.Clear();
}

/// <summary>
/// Внутренний интерфейс для не-generic взаимодействия с SaveableRegistry.
/// Используется только SaveableRegistry — не часть публичного API проекта.
/// </summary>
public interface ISaveableComponent
{
    string SaveableGuid   { get; }
    string DebugName      { get; }
    bool   IsActiveInScene { get; }
    Vector3 WorldPosition { get; }

    string CaptureToJson();
    void   RestoreFromJson(string json);
    void   SetActiveInScene(bool active);
}
