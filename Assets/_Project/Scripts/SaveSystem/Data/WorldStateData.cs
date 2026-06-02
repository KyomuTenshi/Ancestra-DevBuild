// Контейнер состояния объектов мира: позиция, активность и JSON-данные SaveableComponent.
// Индексируется по GUID через Dictionary (строится лениво после десериализации).
// WorldStateData входит в SaveFile вместе с GameProgressData и SaveMetadata.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Состояние одного GameObject: GUID, активность, позиция и данные всех его SaveableComponent.</summary>
[Serializable]
public class ObjectStateEntry
{
    public string guid;

    // Только для читаемости JSON — при загрузке не используется.
    public string debugName;

    public bool isActive;

    public Vector3 worldPosition;

    /// <summary>Данные каждого компонента на объекте. Один объект может иметь несколько сохраняемых компонентов.</summary>
    public List<ComponentStateEntry> components = new List<ComponentStateEntry>();
}

/// <summary>JSON-снимок одного SaveableComponent. typeName используется для сопоставления при Restore.</summary>
[Serializable]
public class ComponentStateEntry
{
    /// <summary>Имя типа (typeof(T).Name). Используется для сопоставления при загрузке.</summary>
    public string typeName;

    public string jsonData;
}

/// <summary>
/// Коллекция ObjectStateEntry, индексированная по GUID. Словарь строится лениво —
/// после десериализации JSON он null, EnsureLookup пересоздаёт его при первом обращении.
/// </summary>
[Serializable]
public class WorldStateData
{
    [SerializeField]
    private List<ObjectStateEntry> _entries = new List<ObjectStateEntry>();

    // Словарь строится лениво: после десериализации JSON он null,
    // поэтому EnsureLookup пересоздаёт его при первом обращении.
    private Dictionary<string, ObjectStateEntry> _lookup;

    /// <summary>Сохраняет или обновляет запись объекта по GUID.</summary>
    public void SetEntry(ObjectStateEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.guid)) return;

        EnsureLookup();

        if (_lookup.TryGetValue(entry.guid, out ObjectStateEntry existing))
        {
            int idx = _entries.IndexOf(existing);
            if (idx >= 0) _entries[idx] = entry;
        }
        else
        {
            _entries.Add(entry);
        }

        _lookup[entry.guid] = entry;
    }

    /// <summary>Возвращает запись по GUID или null если объект не был сохранён.</summary>
    public ObjectStateEntry GetEntry(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        EnsureLookup();
        _lookup.TryGetValue(guid, out ObjectStateEntry entry);
        return entry;
    }

    public bool HasEntry(string guid)
    {
        EnsureLookup();
        return _lookup.ContainsKey(guid);
    }

    public IReadOnlyList<ObjectStateEntry> AllEntries => _entries;

    public void Clear()
    {
        _entries.Clear();
        _lookup?.Clear();
    }

    public int Count => _entries.Count;

    private void EnsureLookup()
    {
        if (_lookup != null) return;

        _lookup = new Dictionary<string, ObjectStateEntry>(_entries.Count);
        foreach (var entry in _entries)
        {
            if (!string.IsNullOrEmpty(entry.guid))
                _lookup[entry.guid] = entry;
        }
    }
}
