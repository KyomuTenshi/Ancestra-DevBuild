// Структуры данных стека музыкальных треков.
// AudioStackEntry — запись в стеке (трек + сохранённая позиция воспроизведения).
// AudioTransition — описание перехода (откуда, куда, позиция возобновления).
// AudioStack — сам стек с операциями Push/Pop/Replace.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Запись в стеке: трек и позиция воспроизведения на момент прерывания.</summary>
public struct AudioStackEntry
{
    public AudioData Data;
    public float     SavedTime;    // позиция воспроизведения на момент, когда трек был прерван

    public bool IsEmpty => Data == null;

    public static readonly AudioStackEntry Empty = new AudioStackEntry { Data = null, SavedTime = 0f };
}

/// <summary>
/// Описывает переход между треками. MusicChannel читает его и запускает корутину фейда.
/// </summary>
public readonly struct AudioTransition
{
    public readonly AudioData From;         // null = ничего не играло
    public readonly AudioData To;           // null = стек пуст, нужна остановка
    public readonly float     ToResumeTime; // позиция, с которой возобновляется To
    public readonly bool      SameClip;     // From == To, только громкость меняется

    public bool HasFrom => From != null;
    public bool HasTo   => To   != null;

    public AudioTransition(AudioData from, AudioData to, float resumeTime)
    {
        From         = from;
        To           = to;
        ToResumeTime = resumeTime;
        SameClip     = from != null && from == to;
    }
}

public class AudioStack
{
    private readonly List<AudioStackEntry> _entries = new List<AudioStackEntry>(8);
    private readonly string _debugName;
    private readonly bool   _verbose;

    public AudioStack(string debugName = "AudioStack", bool verbose = false)
    {
        _debugName = debugName;
        _verbose   = verbose;
    }

    /// <summary>
    /// Добавляет трек поверх текущего, сохраняя позицию воспроизведения прерванного трека.
    /// Если пушим тот же клип — обновляем его сохранённую позицию и не дублируем запись.
    /// </summary>
    public AudioTransition Push(AudioData data, float currentPlaybackTime, float startTime = 0f)
    {
        if (data == null)
        {
            Debug.LogWarning($"[{_debugName}] Push: data == null, пропуск.");
            return new AudioTransition(Current.Data, null, 0f);
        }

        if (_entries.Count > 0)
        {
            var top = _entries[_entries.Count - 1];

            if (top.Data == data)
            {
                top.SavedTime = currentPlaybackTime;
                _entries[_entries.Count - 1] = top;

                if (_verbose) Debug.Log($"[{_debugName}] Push same clip '{data.name}' — обновлена позиция {currentPlaybackTime:F2}s");
                return new AudioTransition(data, data, currentPlaybackTime);
            }

            top.SavedTime = currentPlaybackTime;
            _entries[_entries.Count - 1] = top;
        }

        float resolvedStart = startTime;
        _entries.Add(new AudioStackEntry { Data = data, SavedTime = resolvedStart });

        AudioData prevData = _entries.Count > 1 ? _entries[_entries.Count - 2].Data : null;
        if (_verbose) Debug.Log($"[{_debugName}] Push '{data.name}'. Глубина стека: {_entries.Count}");

        return new AudioTransition(prevData, data, resolvedStart);
    }

    /// <summary>
    /// Убирает верхний трек, возвращая переход к предыдущему с его сохранённой позицией.
    /// </summary>
    public AudioTransition Pop(float currentPlaybackTime)
    {
        if (_entries.Count == 0)
        {
            Debug.LogWarning($"[{_debugName}] Pop на пустом стеке.");
            return new AudioTransition(null, null, 0f);
        }

        var removed = _entries[_entries.Count - 1];
        _entries.RemoveAt(_entries.Count - 1);

        AudioStackEntry next = _entries.Count > 0 ? _entries[_entries.Count - 1] : AudioStackEntry.Empty;

        if (_verbose) Debug.Log($"[{_debugName}] Pop '{removed.Data?.name}' → '{next.Data?.name}' " +
                  $"resume {next.SavedTime:F2}s. Глубина стека: {_entries.Count}");

        return new AudioTransition(removed.Data, next.Data, next.SavedTime);
    }

    /// <summary>
    /// Полностью очищает стек и устанавливает один трек. Используется при смене сцены.
    /// </summary>
    public AudioTransition Replace(AudioData data, float currentPlaybackTime)
    {
        AudioData from = Current.Data;
        Clear();

        if (data != null)
            _entries.Add(new AudioStackEntry { Data = data, SavedTime = 0f });

        if (_verbose) Debug.Log($"[{_debugName}] Replace '{from?.name}' → '{data?.name}'");
        return new AudioTransition(from, data, 0f);
    }

    /// <summary>Удаляет конкретный трек из стека без воспроизведения перехода.</summary>
    public void RemoveSilent(AudioData data)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Data == data)
                _entries.RemoveAt(i);
        }
    }

    public void Clear() => _entries.Clear();

    public AudioStackEntry Current =>
        _entries.Count > 0 ? _entries[_entries.Count - 1] : AudioStackEntry.Empty;

    public bool IsEmpty => _entries.Count == 0;
    public int  Depth   => _entries.Count;

    public IReadOnlyList<AudioStackEntry> Entries => _entries;
}
