// MonoBehaviour-прокси между системой локализации и UnityEvent в инспекторе.
// Позволяет реагировать на смену языка без написания кода — через Inspector-колбэки.

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Подписывается на MessageBus&lt;LanguageChangedMessage&gt; и вызывает UnityEvent&lt;int&gt; при смене языка.
/// Используй когда нужна реакция на смену языка без кода: анимации, SFX, видимость объектов.
/// </summary>
[AddComponentMenu("Localization/Localization Callback Receiver")]
public class LocalizationCallbackReceiver : MonoBehaviour
{
    [Tooltip("Вызывается при смене языка. Передаёт новый индекс языка.")]
    [SerializeField] private UnityEvent<int> _onLanguageChanged;

    // При включении сразу вызываем событие для синхронизации начального состояния UI.
    [Tooltip("Вызвать OnChanged при включении компонента.")]
    [SerializeField] private bool _invokeOnEnable = true;

    private void OnEnable()
    {
        MessageBus<LanguageChangedMessage>.Subscribe(OnLanguageChangedHandler);

        if (_invokeOnEnable)
            _onLanguageChanged?.Invoke(AppSettings.LanguageIndex);
    }

    private void OnDisable()
    {
        MessageBus<LanguageChangedMessage>.Unsubscribe(OnLanguageChangedHandler);
    }

    private void OnLanguageChangedHandler(LanguageChangedMessage msg)
    {
        _onLanguageChanged?.Invoke(msg.LanguageIndex);
    }
}
