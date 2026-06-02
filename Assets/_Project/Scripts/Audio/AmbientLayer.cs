// Менеджер именованных ambient-слоёв. Поддерживает одновременное воспроизведение
// нескольких слоёв, кроссфейд при смене клипа и Mute/Unmute при паузе игры.
// Каждый слой идентифицируется строковым layerId ('wind', 'horror', 'forest').

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Управляет набором ambient-слоёв: создаёт AudioSource на лету, фейдит при старте/стопе,
/// кроссфейдит при смене клипа на уже играющем слое. Максимум слоёв ограничен аппаратно.
/// </summary>
public class AmbientLayer
{
    private const string LogPrefix = "[AmbientLayer]";

    // iOS поддерживает ~32 hardware audio channels.
    // Резервируем место для музыки и SFX — ambient не должен их вытеснять.
    private const int MaxLayerCount = 12;

    private class LayerState
    {
        public AudioSource Source;
        public AudioData   Data;
        public Coroutine   FadeRoutine;
        public string      LayerId;
        public string      OwnerScene;
    }

    private readonly MonoBehaviour                    _owner;
    private readonly Transform                        _sourceParent;
    private readonly Dictionary<string, LayerState>  _layers = new Dictionary<string, LayerState>(4);
    private readonly bool                             _verbose;

    public AmbientLayer(MonoBehaviour owner, Transform sourceParent, bool verbose = false)
    {
        _owner        = owner;
        _sourceParent = sourceParent;
        _verbose      = verbose;
    }

    /// <summary>
    /// Воспроизводит ambient-слой. Если слой с таким layerId уже играет — кроссфейдит в новый клип.
    /// ownerScene используется только для предупреждения о конфликтах layerId между сценами.
    /// </summary>
    public void Play(AudioData data, string layerId = "default", float fadeDuration = 1f,
                     string ownerScene = null)
    {
        if (data == null || !data.IsValid())
        {
            Debug.LogWarning($"{LogPrefix} Play '{layerId}': AudioData null или без клипа.");
            return;
        }

        if (_layers.TryGetValue(layerId, out LayerState existing))
        {
            // Две разные сцены используют одинаковый layerId — вторая перезапишет первую.
            if (ownerScene != null && existing.OwnerScene != null && existing.OwnerScene != ownerScene)
            {
                Debug.LogWarning(
                    $"{LogPrefix} LayerId '{layerId}' уже используется сценой '{existing.OwnerScene}'. " +
                    $"Сцена '{ownerScene}' перезапишет его. Используйте уникальные layerId в каждой сцене."
                );
            }

            if (existing.FadeRoutine != null)
                _owner.StopCoroutine(existing.FadeRoutine);

            existing.Data       = data;
            existing.OwnerScene = ownerScene;
            existing.FadeRoutine = _owner.StartCoroutine(
                CrossfadeLayer(existing, data, fadeDuration)
            );
        }
        else
        {
            if (_layers.Count >= MaxLayerCount)
            {
                Debug.LogWarning(
                    $"{LogPrefix} Достигнут лимит слоёв ({MaxLayerCount}). " +
                    $"Слой '{layerId}' не создан. Остановите неиспользуемые слои."
                );
                return;
            }

            var state = new LayerState
            {
                LayerId    = layerId,
                Data       = data,
                OwnerScene = ownerScene,
                Source     = CreateSource(layerId, data),
            };

            _layers[layerId] = state;

            state.Source.volume = 0f;
            state.Source.Play();
            state.FadeRoutine = _owner.StartCoroutine(
                FadeInRoutine(state, fadeDuration)
            );
        }

        Log($"Play '{data.name}' на слое '{layerId}'");
    }

    /// <summary>Останавливает конкретный слой с fade out.</summary>
    public void Stop(string layerId = "default", float fadeDuration = 1f)
    {
        if (!_layers.TryGetValue(layerId, out LayerState state)) return;

        if (state.FadeRoutine != null)
            _owner.StopCoroutine(state.FadeRoutine);

        state.FadeRoutine = _owner.StartCoroutine(
            FadeOutAndRemove(state, layerId, fadeDuration)
        );
    }

    public void StopAll(float fadeDuration = 1f)
    {
        var keys = new List<string>(_layers.Keys);
        foreach (var key in keys)
            Stop(key, fadeDuration);
    }

    public void MuteAll(float fadeDuration = 0.3f)
    {
        foreach (var state in _layers.Values)
        {
            if (state.FadeRoutine != null)
                _owner.StopCoroutine(state.FadeRoutine);

            state.FadeRoutine = _owner.StartCoroutine(
                SimpleFade(state.Source, 0f, fadeDuration, stopOnComplete: false)
            );
        }
    }

    public void UnmuteAll(float fadeDuration = 0.3f)
    {
        foreach (var state in _layers.Values)
        {
            if (state.FadeRoutine != null)
                _owner.StopCoroutine(state.FadeRoutine);

            float vol = 1f;
            if (state.Data != null)
                state.Data.GetPlaybackParams(out vol, out _);

            state.FadeRoutine = _owner.StartCoroutine(
                SimpleFade(state.Source, vol, fadeDuration, stopOnComplete: false)
            );
        }
    }

    public bool HasLayer(string layerId) => _layers.ContainsKey(layerId);
    public int  LayerCount              => _layers.Count;

    private IEnumerator CrossfadeLayer(LayerState state, AudioData newData, float duration)
    {
        float halfDur = duration * 0.5f;

        yield return FadeSourceTo(state.Source, 0f, halfDur);

        state.Source.Stop();
        newData.GetPlaybackParams(out float targetVol, out float pitch);
        state.Source.clip   = newData.clip;
        state.Source.pitch  = pitch;
        state.Source.volume = 0f;
        state.Source.Play();

        if (newData.mixerGroup != null)
            state.Source.outputAudioMixerGroup = newData.mixerGroup;

        yield return FadeSourceTo(state.Source, targetVol, halfDur);

        state.FadeRoutine = null;
    }

    private IEnumerator FadeInRoutine(LayerState state, float duration)
    {
        state.Data.GetPlaybackParams(out float targetVol, out _);
        yield return FadeSourceTo(state.Source, targetVol, duration);
        state.FadeRoutine = null;
    }

    private IEnumerator FadeOutAndRemove(LayerState state, string layerId, float duration)
    {
        yield return FadeSourceTo(state.Source, 0f, duration);

        state.Source.Stop();
        Object.Destroy(state.Source.gameObject);
        _layers.Remove(layerId);
        state.FadeRoutine = null;

        Log($"Слой '{layerId}' остановлен.");
    }

    private IEnumerator SimpleFade(AudioSource source, float target, float duration, bool stopOnComplete)
    {
        yield return FadeSourceTo(source, target, duration);
        if (stopOnComplete && target <= 0f) source.Stop();
    }

    private IEnumerator FadeSourceTo(AudioSource source, float target, float duration)
    {
        float start   = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed      += Time.unscaledDeltaTime;
            float smooth  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            source.volume = Mathf.Lerp(start, target, smooth);
            yield return null;
        }

        source.volume = target;
    }

    private AudioSource CreateSource(string layerId, AudioData data)
    {
        var go  = new GameObject($"Ambient_{layerId}");
        go.transform.SetParent(_sourceParent);

        var src  = go.AddComponent<AudioSource>();
        src.loop = true;
        src.clip = data.clip; // Без этого Play() воспроизводит тишину

        data.GetPlaybackParams(out _, out float pitch);
        src.pitch = pitch;

        if (data.mixerGroup != null)
            src.outputAudioMixerGroup = data.mixerGroup;

        return src;
    }

    private void Log(string msg) { if (_verbose) Debug.Log($"{LogPrefix} {msg}"); }
}
