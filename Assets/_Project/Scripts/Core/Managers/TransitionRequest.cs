// Параметры одного перехода между сценами.
// Передаётся в SceneTransitionManager.LoadScene() или через SceneLoader.Load().
//
// Быстрое создание через фабричные методы:
//   TransitionRequest.Default("Game")           — стандартный переход с fade
//   TransitionRequest.Immediate("Game")         — без fade, мгновенно
//   TransitionRequest.WithKey("Game", action)   — ждёт нажатия клавиши перед активацией

using UnityEngine;
using UnityEngine.InputSystem;

public class TransitionRequest
{
    /// <summary>Имя целевой сцены (должна быть в Build Settings).</summary>
    public string SceneName { get; }

    /// <summary>Длительность затемнения экрана (секунды). -1 = брать из инспектора STM.</summary>
    public float FadeOutDuration { get; set; } = -1f;

    /// <summary>Длительность проявления экрана (секунды). -1 = брать из инспектора STM.</summary>
    public float FadeInDuration  { get; set; } = -1f;

    /// <summary>
    /// Если true — сцена загружается до 90% и останавливается.
    /// Активация происходит при нажатии AnyKeyAction или вызове ConfirmSceneActivation().
    /// Используй для экрана загрузки типа "нажмите любую клавишу".
    /// </summary>
    public bool WaitForAnyKey { get; set; } = false;

    /// <summary>
    /// Если true — после активации сцены SceneTransitionManager удерживает оверлей
    /// и диспатчит ReadyToHandOffMessage вместо запуска FadeIn.
    /// Получатель обязан вызвать ReleaseOverlay() — иначе сработает страховочный таймаут.
    /// Используй для cutscene-сцен где нужна бесшовная передача оверлея.
    /// </summary>
    public bool UseHandOff { get; set; } = false;

    /// <summary>
    /// InputAction для подтверждения активации сцены (WaitForAnyKey = true).
    /// Приоритет: это поле → _anyKeyActionRef в инспекторе STM → ConfirmSceneActivation().
    /// Не задавай если хочешь управлять активацией вручную из кода.
    /// </summary>
    public InputAction AnyKeyAction { get; set; } = null;

    /// <summary>Кривая fade out. Null = кривая из инспектора SceneTransitionManager.</summary>
    public AnimationCurve FadeOutCurve { get; set; } = null;

    /// <summary>Кривая fade in. Null = кривая из инспектора SceneTransitionManager.</summary>
    public AnimationCurve FadeInCurve  { get; set; } = null;

    public TransitionRequest(string sceneName)
    {
        SceneName = sceneName;
    }

    // ─── Фабричные методы ─────────────────────────────────────────────────────

    /// <summary>Стандартный переход с fade по умолчанию.</summary>
    public static TransitionRequest Default(string sceneName) =>
        new TransitionRequest(sceneName);

    /// <summary>Мгновенная смена сцены без fade. Используй для рестарта или отладки.</summary>
    public static TransitionRequest Immediate(string sceneName) =>
        new TransitionRequest(sceneName)
        {
            FadeOutDuration = 0f,
            FadeInDuration  = 0f,
        };

    /// <summary>Переход с экраном загрузки: сцена ждёт нажатия клавиши перед активацией.</summary>
    public static TransitionRequest WithKey(string sceneName, InputAction anyKeyAction) =>
        new TransitionRequest(sceneName)
        {
            WaitForAnyKey = true,
            AnyKeyAction  = anyKeyAction,
        };
}
