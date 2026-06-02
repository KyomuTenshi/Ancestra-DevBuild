// Сервис периодического автосохранения.
// WaitForSecondsRealtime гарантирует работу при timeScale = 0 (пауза, меню).
// Каждый цикл ждёт завершения корутины сохранения перед следующим отсчётом интервала.

using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Запускает сохранение (через переданную корутину-фабрику) каждые N секунд реального времени.
/// Start() можно вызывать повторно для изменения интервала — текущая корутина перезапускается.
/// </summary>
public class AutoSaveService
{
    public const float MinIntervalSeconds = 10f;

    private readonly MonoBehaviour      _owner;
    private readonly Func<IEnumerator>  _onSave;
    private readonly string             _logPrefix = "[AutoSaveService]";

    private float     _intervalSeconds;
    private bool      _enabled;
    private Coroutine _routine;

    /// <param name="owner">MonoBehaviour для запуска корутин.</param>
    /// <param name="onSave">Фабрика корутины сохранения. AutoSaveLoop ждёт её завершения перед следующим циклом.</param>
    public AutoSaveService(MonoBehaviour owner, Func<IEnumerator> onSave)
    {
        _owner  = owner  ?? throw new ArgumentNullException(nameof(owner));
        _onSave = onSave ?? throw new ArgumentNullException(nameof(onSave));
    }

    public bool  IsEnabled       => _enabled;
    public float IntervalSeconds => _intervalSeconds;

    /// <summary>Запускает автосохранение. Если уже запущено — перезапускает с новым интервалом.</summary>
    public void Start(float intervalSeconds = 300f)
    {
        _intervalSeconds = Mathf.Max(MinIntervalSeconds, intervalSeconds);

        Stop();          // Сначала останавливаем старую корутину (Stop сбрасывает _enabled в false)
        _enabled = true; // Только после Stop() — иначе AutoSaveLoop увидит false и сразу выйдет
        _routine = _owner.StartCoroutine(AutoSaveLoop());

        Debug.Log($"{_logPrefix} Запущено (каждые {_intervalSeconds}с).");
    }

    /// <summary>Останавливает автосохранение. Текущее сохранение (если идёт) завершится.</summary>
    public void Stop()
    {
        _enabled = false;

        if (_routine != null)
        {
            _owner.StopCoroutine(_routine);
            _routine = null;
        }
    }

    public void SetInterval(float seconds)
    {
        if (!_enabled) return;
        Start(seconds);
    }

    private IEnumerator AutoSaveLoop()
    {
        while (_enabled)
        {
            // WaitForSecondsRealtime нельзя взять из Yielders-кеша для цикла:
            // после первого завершения m_WaitUntilTime остаётся в прошлом,
            // и каждый последующий yield return вернулся бы немедленно — I/O-спам.
            // Для редкого автосохранения (≥10 с) аллокация одного объекта несущественна.
            yield return new WaitForSecondsRealtime(_intervalSeconds);

            if (!_enabled) break;

            Debug.Log($"{_logPrefix} Автосохранение...");
            yield return _owner.StartCoroutine(_onSave());
        }

        _routine = null;
    }
}
