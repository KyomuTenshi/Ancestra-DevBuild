// MessageBus-сообщения системы переходов между сценами.
//
// Порядок диспатча в нормальном пайплайне:
//   1. SceneTransitionRequestedMessage — запрос перехода (SceneLoader)
//   2. TransitionStartedMessage        — экран начинает темнеть
//   3. ScreenObscuredMessage           — экран полностью чёрный, сцена ещё не загружается
//   4. WaitingForActivationMessage     — только при WaitForAnyKey=true
//   5. ReadyToHandOffMessage           — только при UseHandOff=true
//   6. TransitionFinishedMessage       — сцена полностью видима
//
// Все типы — readonly struct: нет аллокаций на heap при диспатче.

/// <summary>
/// Экран начинает темнеть. AudioSystem: запускает FadeOut музыки на FadeOutDuration секунд.
/// </summary>
public readonly struct TransitionStartedMessage : IMessage
{
    public readonly string SceneName;
    public readonly float  FadeOutDuration;

    public TransitionStartedMessage(string sceneName, float fadeOutDuration)
    {
        SceneName       = sceneName;
        FadeOutDuration = fadeOutDuration;
    }
}

/// <summary>
/// Экран полностью чёрный. Сцена ещё не загружается.
/// Идеальный момент для смены трека — нет визуальных артефактов.
/// AudioSystem: меняет трек, запускает FadeIn нового трека длиной FadeInDuration.
/// </summary>
public readonly struct ScreenObscuredMessage : IMessage
{
    public readonly string SceneName;
    public readonly float  FadeInDuration;

    public ScreenObscuredMessage(string sceneName, float fadeInDuration)
    {
        SceneName      = sceneName;
        FadeInDuration = fadeInDuration;
    }
}

/// <summary>
/// Переход завершён, сцена полностью видима.
/// AudioSystem: запускает новый трек если он ещё не был запущен в ScreenObscuredMessage.
/// UI: скрывает loading-индикатор.
/// </summary>
public readonly struct TransitionFinishedMessage : IMessage
{
    public readonly string SceneName;
    public readonly float  FadeInDuration;

    public TransitionFinishedMessage(string sceneName, float fadeInDuration)
    {
        SceneName      = sceneName;
        FadeInDuration = fadeInDuration;
    }
}

/// <summary>
/// Сцена загружена на 90%, ожидает подтверждения (WaitForAnyKey = true).
/// UI: показывает "Нажмите любую клавишу для продолжения".
/// Подтверждение: InputAction из TransitionRequest или вызов ConfirmSceneActivation().
/// </summary>
public readonly struct WaitingForActivationMessage : IMessage
{
    public readonly string SceneName;
    public WaitingForActivationMessage(string sceneName) => SceneName = sceneName;
}

/// <summary>
/// Сцена активирована, глобальный оверлей удерживается (UseHandOff = true).
/// Получатель должен выставить локальный оверлей и вызвать ReleaseOverlay().
/// Если ReleaseOverlay() не вызван в течение _handOffTimeoutSeconds — FadeIn запускается принудительно.
/// </summary>
public readonly struct ReadyToHandOffMessage : IMessage { }
