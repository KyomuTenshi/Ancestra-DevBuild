// ScriptableObject: маппинг «имя сцены → аудио-конфигурация».
// Дизайнер настраивает аудио для каждой сцены без кода.
// Кеш строится лениво и сбрасывается при изменении данных в Editor.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Глобальная таблица «имя сцены → аудиоконфиг». Дизайнер настраивает музыку и ambient
/// для каждой сцены без кода. AudioSystem читает этот ассет при sceneLoaded.
/// </summary>
[CreateAssetMenu(fileName = "SceneMusicConfig", menuName = "Audio/Scene Music Config")]
public class SceneMusicConfig : ScriptableObject
{
    public enum UnmappedBehavior
    {
        Continue,      // текущий трек продолжает играть
        FadeOut,       // плавно затихнуть
        StopImmediate, // немедленная остановка
    }

    [Header("Поведение для неизвестных сцен")]
    [Tooltip("Что делать с музыкой если для загружаемой сцены не задан трек")]
    public UnmappedBehavior unmappedBehavior = UnmappedBehavior.Continue;

    [Tooltip("Длительность fade out при UnmappedBehavior.FadeOut")]
    [Min(0f)]
    public float unmappedFadeOutDuration = 1.5f;

    [Serializable]
    public class AmbientEntry
    {
        [Tooltip("ID слоя, напр. 'wind', 'horror', 'forest'")]
        public string layerId = "default";

        public AudioData data;

        [Min(0f)]
        public float fadeInDuration = 1f;
    }

    [Serializable]
    public class SceneEntry
    {
        [Tooltip("Имя сцены — точное совпадение с BuildSettings")]
        public string sceneName;

        [Tooltip("Музыкальный трек. Null = не менять музыку.")]
        public AudioData music;

        [Tooltip("True = Push в стек (аддитивная сцена, меню поверх). " +
                 "False = полная замена (новая игровая сцена).")]
        public bool pushMode;

        [Tooltip("Fade In при входе. -1 = из AudioData.")]
        [Min(-1f)]
        public float overrideFadeIn = -1f;

        [Tooltip("Fade Out при выходе. -1 = из AudioData.")]
        [Min(-1f)]
        public float overrideFadeOut = -1f;

        [Tooltip("Ambient-слои для этой сцены. Запускаются при входе, останавливаются при выходе.")]
        public List<AmbientEntry> ambientLayers = new List<AmbientEntry>();
    }

    [Tooltip("Таблица соответствия сцен и треков")]
    public List<SceneEntry> scenes = new List<SceneEntry>();

    private Dictionary<string, SceneEntry> _cache;

    public SceneEntry GetEntry(string sceneName)
    {
        EnsureCache();
        return _cache.TryGetValue(sceneName, out SceneEntry entry) ? entry : null;
    }

    private void EnsureCache()
    {
        if (_cache != null) return;

        // Регистронезависимый словарь — имя сцены в BuildSettings может различаться по регистру.
        _cache = new Dictionary<string, SceneEntry>(
            scenes.Count, StringComparer.OrdinalIgnoreCase
        );

        foreach (var entry in scenes)
        {
            if (string.IsNullOrWhiteSpace(entry.sceneName))
            {
                Debug.LogWarning($"[SceneMusicConfig] Пропущена запись с пустым именем сцены.");
                continue;
            }

            if (!_cache.TryAdd(entry.sceneName, entry))
            {
                Debug.LogWarning(
                    $"[SceneMusicConfig] Дубликат сцены '{entry.sceneName}'. Оставлена первая запись."
                );
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate() => _cache = null;
#endif
}
