// Пул AudioSource для воспроизведения коротких звуковых эффектов.
// Источники переиспользуются через PlayOneShot — isPlaying=false сигнализирует о свободном слоте.
// При переполнении пул прерывает самый старый источник вместо потери SFX.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Object pool для SFX: выбирает свободный AudioSource, воспроизводит клип через PlayOneShot
/// и не требует явного возврата источника в пул — Unity обнуляет isPlaying автоматически.
/// Поддерживает 2D (Play) и 3D (PlayAtPoint) воспроизведение.
/// </summary>
public class SfxPool
{
    private const string LogPrefix = "[SfxPool]";

    private readonly List<AudioSource> _pool    = new List<AudioSource>(16);
    private readonly Transform         _parent;
    private readonly MonoBehaviour     _owner;
    private readonly int               _maxSize;
    private readonly bool              _verbose;

    public SfxPool(MonoBehaviour owner, Transform parent, int initialSize = 8, int maxSize = 32, bool verbose = false)
    {
        _owner   = owner;
        _parent  = parent;
        _maxSize = maxSize;
        _verbose = verbose;

        for (int i = 0; i < initialSize; i++)
            _pool.Add(CreateSource($"SFX_{i:D2}"));
    }

    /// <summary>Воспроизводит 2D SFX из свободного источника пула.</summary>
    public void Play(AudioData data)
    {
        if (!Validate(data, "Play")) return;

        var source = GetFreeSource();
        if (source == null) return;

        data.GetPlaybackParams(out float vol, out float pitch);

        source.transform.localPosition  = Vector3.zero;
        source.spatialBlend             = 0f;
        source.pitch                    = pitch;
        source.outputAudioMixerGroup    = data.mixerGroup;

        source.PlayOneShot(data.clip, vol);

        Log($"SFX '{data.clip.name}' vol={vol:F2} pitch={pitch:F2}");
    }

    /// <summary>Воспроизводит 3D SFX в точке мира. spatialBlend = 1f, источник перемещается в worldPosition.</summary>
    public void PlayAtPoint(AudioData data, Vector3 worldPosition)
    {
        if (!Validate(data, "PlayAtPoint")) return;

        var source = GetFreeSource();
        if (source == null) return;

        data.GetPlaybackParams(out float vol, out float pitch);

        source.transform.position    = worldPosition;
        source.spatialBlend         = 1f;
        source.pitch                = pitch;
        source.outputAudioMixerGroup = data.mixerGroup;

        source.PlayOneShot(data.clip, vol);

        Log($"SFX 3D '{data.clip.name}' @ {worldPosition}");
    }

    /// <summary>Воспроизводит сырой AudioClip без AudioData. Используй для UI-звуков и системных сигналов.</summary>
    public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        var source = GetFreeSource();
        if (source == null) return;

        source.spatialBlend = 0f;
        source.pitch        = pitch;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private AudioSource GetFreeSource()
    {
        foreach (var src in _pool)
        {
            if (!src.isPlaying)
                return src;
        }

        if (_pool.Count < _maxSize)
        {
            var src = CreateSource($"SFX_{_pool.Count:D2}");
            _pool.Add(src);
            return src;
        }

        // Пул заполнен — прерываем самый ранний источник.
        // PlayOneShot удерживает isPlaying=true пока клип играет, поэтому явный возврат не нужен.
        Debug.LogWarning($"{LogPrefix} Пул переполнен ({_maxSize}). Прерываем старый SFX.");
        var oldest = _pool[0];
        oldest.Stop();
        return oldest;
    }

    private AudioSource CreateSource(string goName)
    {
        var go  = new GameObject(goName);
        go.transform.SetParent(_parent);

        var src    = go.AddComponent<AudioSource>();
        src.loop   = false;
        src.volume = 1f;
        return src;
    }

    private bool Validate(AudioData data, string ctx)
    {
        if (data != null && data.IsValid()) return true;
        Debug.LogWarning($"{LogPrefix} {ctx}: AudioData null или без клипа.");
        return false;
    }

    private void Log(string msg) { if (_verbose) Debug.Log($"{LogPrefix} {msg}"); }

    public int PoolSize => _pool.Count;

    // Без FindAll() — нет аллокации нового List при каждом вызове.
    public int ActiveSfx
    {
        get
        {
            int count = 0;
            foreach (var s in _pool)
                if (s.isPlaying) count++;
            return count;
        }
    }
}
