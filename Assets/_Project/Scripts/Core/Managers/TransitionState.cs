// Фазы пайплайна перехода между сценами.
// Внешние системы читают SceneTransitionManager.State для точного определения фазы —
// точнее чем bool IsTransitioning, который не различает FadingOut и LoadingScene.

public enum TransitionState
{
    /// <summary>Переход не активен. Система готова принять новый запрос.</summary>
    Idle,

    /// <summary>Overlay затемняется (alpha 0 → 1).</summary>
    FadingOut,

    /// <summary>
    /// Overlay полностью непрозрачен. Сцена ещё не начала загружаться.
    /// AudioSystem подписан на ScreenObscuredMessage — идеальный момент для смены трека.
    /// </summary>
    ScreenObscured,

    /// <summary>AsyncOperation запущена, сцена загружается в фоне.</summary>
    LoadingScene,

    /// <summary>Сцена активирована, overlay проявляется (alpha 1 → 0).</summary>
    FadingIn,
}
