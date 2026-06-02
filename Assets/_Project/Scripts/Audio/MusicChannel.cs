// Канал воспроизведения музыки с кроссфейдом и стеком треков.
// Использует два AudioSource (A/B) для плавного перехода: A играет текущий трек,
// B нарастает во время кроссфейда, после чего источники меняются ролями.
// Стек позволяет возвращаться к предыдущему треку с сохранённой позицией.

using System.Collections;
using UnityEngine;

/// <summary>
/// Управляет музыкой через стек треков: Push добавляет трек поверх текущего,
/// Pop возвращает предыдущий с его сохранённой позицией воспроизведения.
/// Все переходы реализованы через кроссфейд (sourcesA/B swap).
/// </summary>
public class MusicChannel
{
    private const string LogPrefix = "[MusicChannel]";

    private readonly MonoBehaviour _owner;
    private AudioSource _sourceA; // активный источник (всегда после любой операции)
    private AudioSource _sourceB; // резервный источник, молчит вне кроссфейда
    private readonly AudioStack _stack;
    private readonly bool _verbose;

    private Coroutine _fadeRoutine;
    private bool      _paused;
    private float     _pausedVolume;

    // Отслеживает фазу кроссфейда для корректного прерывания через CancelFade.
    // FadeOut: _sourceA затихает, _sourceB молчит.
    // FadeIn:  _sourceA уже остановлен, _sourceB нарастает (swap ещё не произошёл).
    private enum CrossfadePhase { None, FadeOut, FadeIn }
    private CrossfadePhase _crossfadePhase;

    public AudioData CurrentData { get; private set; }
    public bool      IsPlaying   => _sourceA != null && _sourceA.isPlaying;
    public float     CurrentTime => _sourceA?.time ?? 0f;

    public MusicChannel(MonoBehaviour owner, AudioSource sourceA, AudioSource sourceB, bool verbose = false)
    {
        _owner   = owner;
        _sourceA = sourceA;
        _sourceB = sourceB;
        _verbose = verbose;
        _stack   = new AudioStack("MusicStack", verbose);

        _sourceA.loop   = true;
        _sourceB.loop   = true;
        _sourceA.volume = 0f;
        _sourceB.volume = 0f;
    }

    /// <summary>Добавляет трек поверх текущего. Pop вернёт предыдущий с сохранённой позицией.</summary>
    public void Push(AudioData data, float fadeDuration = -1f)
    {
        if (!ValidateData(data, "Push")) return;

        float actualFade = ResolveFade(fadeDuration, data.fadeInDuration);
        var transition   = _stack.Push(data, currentPlaybackTime: CurrentTime);
        ApplyTransition(transition, actualFade);
    }

    /// <summary>Убирает верхний трек из стека и возвращает к предыдущему с его позицией воспроизведения.</summary>
    public void Pop(float fadeDuration = -1f)
    {
        float actualFade = ResolveFade(fadeDuration, CurrentData?.fadeOutDuration ?? 1f);
        var transition   = _stack.Pop(currentPlaybackTime: CurrentTime);
        ApplyTransition(transition, actualFade);
    }

    /// <summary>Очищает стек и устанавливает единственный трек. Используй при смене сцены.</summary>
    public void Replace(AudioData data, float fadeDuration = -1f)
    {
        // Push() валидирует через ValidateData и отказывает при невалидных данных.
        // Replace() трактует невалидный data как null — выполняет fade out вместо NRE в ConfigureSource.
        if (data != null && !data.IsValid())
        {
            Debug.LogWarning($"{LogPrefix} Replace: AudioData без клипа — выполняем fade out.");
            data = null;
        }

        float actualFade = data != null
            ? ResolveFade(fadeDuration, data.fadeInDuration)
            : ResolveFade(fadeDuration, CurrentData?.fadeOutDuration ?? 1f);

        var transition = _stack.Replace(data, currentPlaybackTime: CurrentTime);
        ApplyTransition(transition, actualFade);
    }

    /// <summary>Плавно затихнуть и остановить воспроизведение. Стек не очищается.</summary>
    public void FadeOut(float duration = -1f)
    {
        float actualFade = ResolveFade(duration, CurrentData?.fadeOutDuration ?? 1.5f);
        StartFade(0f, actualFade, stopOnComplete: true);
    }

    /// <summary>Немедленная остановка. Очищает стек и сбрасывает CurrentData.</summary>
    public void Stop()
    {
        CancelFade();
        _sourceA.Stop();
        _sourceB.Stop();
        _sourceA.volume = 0f;
        _sourceB.volume = 0f;
        _stack.Clear();
        CurrentData = null;
    }

    /// <summary>Приглушает канал (громкость → 0). Не останавливает воспроизведение — Unmute восстановит.</summary>
    public void Mute(float fadeDuration = 0.3f)
    {
        if (_paused) return;
        _paused       = true;
        _pausedVolume = _sourceA.volume;
        StartFade(0f, fadeDuration, stopOnComplete: false);
    }

    /// <summary>Восстанавливает громкость после Mute до значения на момент приглушения.</summary>
    public void Unmute(float fadeDuration = 0.3f)
    {
        if (!_paused) return;
        _paused = false;
        StartFade(_pausedVolume, fadeDuration, stopOnComplete: false);
    }

    private void ApplyTransition(AudioTransition transition, float fadeDuration)
    {
        CancelFade();
        CurrentData = transition.To;

        if (!transition.HasTo)
        {
            StartFade(0f, fadeDuration, stopOnComplete: true);
            return;
        }

        if (transition.SameClip)
        {
            float targetVol = transition.To.volume;
            StartFade(targetVol, fadeDuration, stopOnComplete: false);
            return;
        }

        _fadeRoutine = _owner.StartCoroutine(CrossfadeRoutine(transition, fadeDuration));
    }

    private IEnumerator CrossfadeRoutine(AudioTransition transition, float totalDuration)
    {
        float halfDuration = totalDuration * 0.5f;

        Log($"Кроссфейд '{transition.From?.name}' → '{transition.To?.name}' " +
            $"за {totalDuration:F1}с (resume={transition.ToResumeTime:F2}s)");

        _crossfadePhase = CrossfadePhase.FadeOut;

        if (transition.HasFrom && _sourceA.isPlaying && halfDuration > 0f)
        {
            float startVol = _sourceA.volume;
            float elapsed  = 0f;

            while (elapsed < halfDuration)
            {
                elapsed        += Time.unscaledDeltaTime;
                float smooth    = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / halfDuration));
                _sourceA.volume = Mathf.Lerp(startVol, 0f, smooth);
                yield return null;
            }
        }

        _sourceA.Stop();
        _sourceA.volume = 0f;

        _crossfadePhase = CrossfadePhase.FadeIn;

        if (transition.HasTo)
        {
            ConfigureSource(_sourceB, transition.To, transition.ToResumeTime);
            _sourceB.volume = 0f;
            _sourceB.Play();

            transition.To.GetPlaybackParams(out float targetVol, out _);

            if (halfDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < halfDuration)
                {
                    elapsed        += Time.unscaledDeltaTime;
                    float smooth    = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / halfDuration));
                    _sourceB.volume = Mathf.Lerp(0f, targetVol, smooth);
                    yield return null;
                }
            }

            _sourceB.volume = targetVol;
        }

        SwapSources();
        _crossfadePhase = CrossfadePhase.None;
        _fadeRoutine    = null;

        Log($"Кроссфейд завершён. Трек: '{_sourceA.clip?.name}'");
    }

    private void StartFade(float targetVolume, float duration, bool stopOnComplete)
    {
        CancelFade();
        _fadeRoutine = _owner.StartCoroutine(
            SimpleFadeRoutine(_sourceA, targetVolume, duration, stopOnComplete)
        );
    }

    private IEnumerator SimpleFadeRoutine(AudioSource source, float target,
                                          float duration, bool stopOnComplete)
    {
        float startVol = source.volume;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed      += Time.unscaledDeltaTime;
            float smooth  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            source.volume = Mathf.Lerp(startVol, target, smooth);
            yield return null;
        }

        source.volume = target;

        if (stopOnComplete && target <= 0f)
        {
            source.Stop();
            CurrentData = null;
        }

        _fadeRoutine = null;
    }

    private void CancelFade()
    {
        if (_fadeRoutine == null) return;

        _owner.StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;

        switch (_crossfadePhase)
        {
            case CrossfadePhase.FadeOut:
                // _sourceA затихал — останавливаем его, _sourceB уже молчит (инвариант).
                _sourceA.Stop();
                _sourceA.volume = 0f;
                break;

            case CrossfadePhase.FadeIn:
                // _sourceA уже остановлен, _sourceB нарастал. Завершаем swap немедленно,
                // иначе _sourceB продолжит играть без контроля — AudioSource leak.
                float targetVol = CurrentData?.volume ?? 1f;
                _sourceB.volume = targetVol;
                SwapSources();
                _sourceB.Stop();
                _sourceB.volume = 0f;
                break;
        }

        _crossfadePhase = CrossfadePhase.None;
    }

    private void SwapSources()
    {
        AudioSource temp = _sourceA;
        _sourceA         = _sourceB;
        _sourceB         = temp;
    }

    private static void ConfigureSource(AudioSource source, AudioData data, float resumeTime)
    {
        source.clip = data.clip;
        source.loop = data.loop;
        // Клампим resumeTime в границы клипа, чтобы не получить тишину при невалидном времени.
        source.time = (resumeTime > 0f && resumeTime < data.clip.length) ? resumeTime : 0f;

        if (data.mixerGroup != null)
            source.outputAudioMixerGroup = data.mixerGroup;

        data.GetPlaybackParams(out _, out float pitch);
        source.pitch = pitch;
    }

    // -1 = «взять из AudioData», любое неотрицательное — явное override.
    private static float ResolveFade(float requested, float fromData)
        => requested >= 0f ? requested : fromData;

    private bool ValidateData(AudioData data, string context)
    {
        if (data != null && data.IsValid()) return true;
        Debug.LogWarning($"{LogPrefix} {context}: AudioData null или без клипа.");
        return false;
    }

    private void Log(string msg) { if (_verbose) Debug.Log($"{LogPrefix} {msg}"); }
}
