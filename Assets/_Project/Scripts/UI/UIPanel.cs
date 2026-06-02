// Assets/_Project/Scripts/UI/Core/UIPanel.cs
// Базовый класс для всех UI-панелей.
// Анимация через корутины — нет зависимости от Animator Controller.
//
// Настройка в инспекторе:
//   FadeDuration — длительность появления/исчезновения (0 = мгновенно)
//   FadeCurve    — кривая плавности (по умолчанию EaseInOut)
//
// Подкласс может переопределить:
//   OnShowStarted()  — вызывается до начала появления (сбросить состояние, включить объекты)
//   OnShowFinished() — вызывается после полного появления
//   OnHideStarted()  — вызывается до начала исчезновения
//   OnHideFinished() — вызывается после полного исчезновения (отключить звук, остановить эффекты)
//
// Использование:
//   panel.Show();              — плавное появление
//   panel.Hide();              — плавное исчезновение
//   panel.ShowImmediate();     — мгновенно (без анимации)
//   panel.HideImmediate();     — мгновенно
//   panel.IsVisible            — true если Show() был вызван (intent-based, не alpha)
//   panel.IsFullyVisible       — true если alpha == 1 и не идёт анимация
//   panel.IsAnimating          — true пока идёт fade

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    // ─── Инспектор ────────────────────────────────────────────────────────────

    [Tooltip("Длительность fade in/out в секундах. 0 = мгновенно.")]
    [Min(0f)]
    [SerializeField] private float _fadeDuration = 0.25f;

    [Tooltip("Кривая плавности fade. X = прогресс [0,1], Y = alpha [0,1].")]
    [SerializeField] private AnimationCurve _fadeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ─── Компоненты ───────────────────────────────────────────────────────────

    private CanvasGroup _canvasGroup;

    // ─── Состояние ────────────────────────────────────────────────────────────

    private Coroutine _fadeRoutine;

    // Флаг намерения, а не текущего alpha. alpha > 0 возвращал бы true во время fade-out,
    // из-за чего PauseController неверно маршрутизировал нажатие Escape пока панель
    // ещё затухала. _isVisible отражает намерение (был ли вызван Show/Hide), не альфу.
    private bool _isVisible;

    /// <summary>True если Show() был вызван и Hide() ещё не вызван.</summary>
    public bool IsVisible      => _isVisible;

    /// <summary>True если alpha == 1 и анимация не идёт.</summary>
    public bool IsFullyVisible => _canvasGroup != null &&
                                  _canvasGroup.alpha >= 1f &&
                                  _fadeRoutine == null;

    /// <summary>True пока идёт fade-анимация.</summary>
    public bool IsAnimating    => _fadeRoutine != null;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        SetAlpha(0f, interactable: false);
    }

    // ─── Публичный API ────────────────────────────────────────────────────────

    /// <summary>Плавное появление. Если уже видима — ничего не делает.</summary>
    public virtual void Show()
    {
        if (IsFullyVisible) return;

        _isVisible = true;  // намерение фиксируем до старта анимации
        OnShowStarted();
        StartFade(targetAlpha: 1f, onComplete: OnShowFinished);
    }

    /// <summary>Плавное исчезновение.</summary>
    public virtual void Hide()
    {
        if (!_isVisible && _fadeRoutine == null) return;

        _isVisible = false;  // намерение сбрасываем сразу — input-навигация реагирует мгновенно
        OnHideStarted();

        // Клики блокируем до конца анимации — не ждём пока alpha дойдёт до 0.
        SetInteractable(false);

        StartFade(targetAlpha: 0f, onComplete: OnHideFinished);
    }

    /// <summary>Мгновенное появление без анимации.</summary>
    public virtual void ShowImmediate()
    {
        StopFade();
        _isVisible = true;
        OnShowStarted();
        SetAlpha(1f, interactable: true);
        OnShowFinished();
    }

    /// <summary>Мгновенное исчезновение без анимации.</summary>
    public virtual void HideImmediate()
    {
        StopFade();
        _isVisible = false;
        OnHideStarted();
        SetAlpha(0f, interactable: false);
        OnHideFinished();
    }

    /// <summary>
    /// Переключает видимость: Show если скрыта, Hide если видима.
    /// </summary>
    public void Toggle()
    {
        if (IsVisible) Hide();
        else           Show();
    }

    // ─── Хуки для подклассов ──────────────────────────────────────────────────

    protected virtual void OnShowStarted()  { }
    protected virtual void OnShowFinished() { }
    protected virtual void OnHideStarted()  { }
    protected virtual void OnHideFinished() { }

    // ─── Внутренняя логика ────────────────────────────────────────────────────

    private void StartFade(float targetAlpha, System.Action onComplete)
    {
        StopFade();

        if (_fadeDuration <= 0f)
        {
            SetAlpha(targetAlpha, interactable: targetAlpha > 0f);
            onComplete?.Invoke();
            return;
        }

        _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, onComplete));
    }

    private IEnumerator FadeRoutine(float targetAlpha, System.Action onComplete)
    {
        float startAlpha = _canvasGroup.alpha;
        float elapsed    = 0f;

        // При появлении — включаем interactable сразу, не ждём полного fade.
        // При исчезновении — уже выключили в Hide() до старта корутины.
        if (targetAlpha > 0f)
            SetInteractable(true);

        while (elapsed < _fadeDuration)
        {
            // unscaledDeltaTime — работает при timeScale = 0 (пауза).
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / _fadeDuration);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, _fadeCurve.Evaluate(t));
            yield return null;
        }

        // Гарантируем точное финальное значение — накопленная ошибка Lerp не оставляет artефактов.
        SetAlpha(targetAlpha, interactable: targetAlpha > 0f);

        _fadeRoutine = null;
        onComplete?.Invoke();
    }

    private void StopFade()
    {
        if (_fadeRoutine == null) return;
        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
    }

    private void SetAlpha(float alpha, bool interactable)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = alpha;
        SetInteractable(interactable);
    }

    private void SetInteractable(bool interactable)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.interactable   = interactable;
        _canvasGroup.blocksRaycasts = interactable;
    }
}