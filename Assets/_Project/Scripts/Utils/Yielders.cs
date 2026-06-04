// Кешированные объекты ожидания для корутин. Каждый new WaitForSeconds(t) аллоцирует объект
// в heap — при частых вызовах это давит на GC. Yielders кешируют экземпляры по значению float.
//
// ВНИМАНИЕ — ограничения кеша:
//   1. Не используй с динамическими значениями (Random.Range) — каждый уникальный float
//      создаёт новую запись в словаре.
//   2. UnscaledTime() (WaitForSecondsRealtime) НЕЛЬЗЯ переиспользовать в цикле.
//      WaitForSecondsRealtime хранит абсолютное время m_WaitUntilTime; после первого
//      завершения это время в прошлом, и второй yield return вернётся немедленно.
//      Для цикла создавай new WaitForSecondsRealtime(_interval) на каждой итерации.
//   3. Time() (WaitForSeconds) безопасен в цикле — Unity сбрасывает его нативно.

using System.Collections.Generic;
using UnityEngine;

public static class Yielders
{
    // Кастомный компаратор избегает boxing, который даёт стандартный EqualityComparer<float>.
    private sealed class FloatComparer : IEqualityComparer<float>
    {
        public static readonly FloatComparer Instance = new FloatComparer();

        bool IEqualityComparer<float>.Equals(float x, float y)   => x == y;
        int  IEqualityComparer<float>.GetHashCode(float obj)     => obj.GetHashCode();
    }

    private static readonly Dictionary<float, WaitForSeconds>
        _scaledCache = new Dictionary<float, WaitForSeconds>(16, FloatComparer.Instance);

    private static readonly Dictionary<float, WaitForSecondsRealtime>
        _unscaledCache = new Dictionary<float, WaitForSecondsRealtime>(16, FloatComparer.Instance);

    private static readonly WaitForEndOfFrame  _endOfFrame  = new WaitForEndOfFrame();
    private static readonly WaitForFixedUpdate _fixedUpdate = new WaitForFixedUpdate();

    /// <summary>Ожидание N секунд игрового времени (масштабируется с timeScale).</summary>
    public static WaitForSeconds Time(float seconds)
    {
        if (!_scaledCache.TryGetValue(seconds, out WaitForSeconds wfs))
        {
            wfs = new WaitForSeconds(seconds);
            _scaledCache[seconds] = wfs;
        }
        return wfs;
    }

    /// <summary>Ожидание N секунд реального времени. Используй для логики, работающей на паузе.</summary>
    public static WaitForSecondsRealtime UnscaledTime(float seconds)
    {
        if (!_unscaledCache.TryGetValue(seconds, out WaitForSecondsRealtime wfsr))
        {
            wfsr = new WaitForSecondsRealtime(seconds);
            _unscaledCache[seconds] = wfsr;
        }
        return wfsr;
    }

    public static WaitForEndOfFrame  EndOfFrame  => _endOfFrame;
    public static WaitForFixedUpdate FixedUpdate => _fixedUpdate;

    /// <summary>Сбрасывает кеши. Нужен только при горячей перезагрузке в редакторе.</summary>
    public static void ClearCache()
    {
        _scaledCache.Clear();
        _unscaledCache.Clear();
    }
}
