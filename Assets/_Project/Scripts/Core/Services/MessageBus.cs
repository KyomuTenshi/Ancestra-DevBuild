// Type-safe event bus без зависимостей на MonoBehaviour.
// Защищён от реентрантности: Subscribe/Unsubscribe во время Dispatch откладываются
// до завершения итерации и применяются когда глубина вызовов возвращается к нулю.

using System;
using System.Collections.Generic;

/// <summary>Маркерный интерфейс. Все сообщения MessageBus обязаны его реализовывать.</summary>
public interface IMessage { }

/// <summary>
/// Статический event bus для сообщений типа T.
/// Подписчики получают копию сообщения — struct-сообщения не создают heap-аллокаций.
/// Безопасен при вложенных вызовах Dispatch (handler → Dispatch → handler).
/// </summary>
public static class MessageBus<T> where T : IMessage
{
    private static readonly HashSet<Action<T>> _listeners         = new HashSet<Action<T>>();
    private static readonly HashSet<Action<T>> _listenersToAdd    = new HashSet<Action<T>>();
    private static readonly HashSet<Action<T>> _listenersToRemove = new HashSet<Action<T>>();

    // int, а не bool: корректно обрабатывает реентрантные вызовы Dispatch внутри обработчика.
    // Изменения коллекции откладываются до момента когда глубина вернётся в 0 —
    // активный foreach никогда не получает модификацию во время итерации.
    private static int _dispatchDepth;

    public static void Subscribe(Action<T> listener)
    {
        if (listener == null) return;

        if (_dispatchDepth > 0)
            _listenersToAdd.Add(listener);
        else
            _listeners.Add(listener);
    }

    public static void Unsubscribe(Action<T> listener)
    {
        if (listener == null) return;

        if (_dispatchDepth > 0)
            _listenersToRemove.Add(listener);
        else
            _listeners.Remove(listener);
    }

    public static void Dispatch(T message)
    {
        _dispatchDepth++;

        foreach (var listener in _listeners)
        {
            try
            {
                listener(message);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                // Уничтоженный Unity-объект не успел отписаться — автоматически убираем
                // его из списка, чтобы исключение не повторялось при каждом следующем Dispatch.
                if (ex is UnityEngine.MissingReferenceException)
                    _listenersToRemove.Add(listener);
            }
        }

        _dispatchDepth--;

        if (_dispatchDepth == 0)
        {
            foreach (var l in _listenersToRemove) _listeners.Remove(l);
            foreach (var l in _listenersToAdd)    _listeners.Add(l);
            _listenersToRemove.Clear();
            _listenersToAdd.Clear();
        }
    }

    // _listenersToAdd учитывается наравне с _listeners: Subscribe() во время активного
    // Dispatch попадает в _listenersToAdd, а не в _listeners. Без этого ListenerCount
    // возвращал бы 0 для только что подписавшегося слушателя, из-за чего
    // SceneTransitionManager пропускал бы HandOff-путь.
    public static int ListenerCount => _listeners.Count + _listenersToAdd.Count;

    /// <summary>
    /// Сбрасывает все подписки и глубину диспатча.
    /// Используй только в тестах — в рантайме вызов сломает все активные подписки.
    /// </summary>
    public static void Reset()
    {
        _listeners.Clear();
        _listenersToAdd.Clear();
        _listenersToRemove.Clear();
        _dispatchDepth = 0;
    }
}
