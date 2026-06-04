// Конечный автомат переходов между сценами — единственный источник правды
// для overlay-фейда и жизненного цикла загрузки.
//
// Стандартный пайплайн:
//   LoadScene(request)
//     FadingOut      — overlay 0 → 1 (unscaled время)
//     ScreenObscured — AudioSystem меняет трек; LoadSceneAsync запускается
//     LoadingScene   — AsyncOperation на 90%, allowSceneActivation=false
//   OnSceneLoaded (Unity callback при активации сцены)
//     FadingIn       — overlay 1 → 0
//     Idle
//
// HandOff-путь (TransitionRequest.UseHandOff = true):
//   После OnSceneLoaded: диспатчит ReadyToHandOffMessage, оверлей удерживается.
//   Получатель (CutscenePlayer и т.п.) вызывает ReleaseOverlay() для запуска FadeIn.
//   Страховочный таймаут принудительно запускает FadeIn если ReleaseOverlay не вызван.
//
// Немедленная отмена (LoadSceneImmediate):
//   Останавливает активную корутину, разблокирует AsyncOperation,
//   сбрасывает всё состояние и запускает LoadSceneAsync без фейда.
//
// MonoBehaviour существует между сценами через PersistentSingleton.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : PersistentSingleton<SceneTransitionManager>
{
    // ─── Инспектор ────────────────────────────────────────────────────────────

    [Header("Оверлей")]
    [Tooltip("CanvasGroup на полноэкранном чёрном Image. " +
             "Canvas → Screen Space Overlay, Sort Order максимальный.")]
    [SerializeField] private CanvasGroup overlayCanvasGroup;

    [Header("Длительности по умолчанию")]
    [SerializeField, Min(0f)] private float defaultFadeOutDuration = 1f;
    [SerializeField, Min(0f)] private float defaultFadeInDuration  = 1f;

    [Header("Кривые затухания")]
    [SerializeField] private AnimationCurve defaultFadeOutCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private AnimationCurve defaultFadeInCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Input")]
    [Tooltip("Глобальный Action для 'нажмите любую клавишу' (WaitForAnyKey).\n" +
             "Создай в Input Action Asset: Action 'AnyKey', binding: '<Keyboard>/anyKey'.\n" +
             "Можно переопределить в TransitionRequest.AnyKeyAction.")]
    [SerializeField] private InputActionReference _anyKeyActionRef;

    [Header("HandOff")]
    [Tooltip("Максимальное время ожидания ReleaseOverlay() перед принудительным FadeIn.\n" +
             "Должно быть достаточным чтобы получатель (CutscenePlayer) установил\n" +
             "локальный оверлей через корутину. По умолчанию 2с.")]
    [SerializeField, Min(0.1f)] private float _handOffTimeoutSeconds = 2f;

    [Header("Дебаг")]
    [SerializeField] private bool verboseLogging = false;

    // ─── Состояние ────────────────────────────────────────────────────────────

    public TransitionState State                  { get; private set; } = TransitionState.Idle;
    public bool            IsTransitioning        => State != TransitionState.Idle;
    public bool            IsWaitingForActivation { get; private set; }
    public bool            IsHoldingOverlay       { get; private set; }

    private AsyncOperation _pendingLoadOperation;
    private Coroutine      _activeTransition;
    private Coroutine      _activeFadeIn;
    private float          _pendingFadeInDuration;
    private AnimationCurve _pendingFadeInCurve;

    // Устанавливается в TransitionRoutine, читается в OnSceneLoaded и ReleaseOverlay —
    // они вызываются асинхронно системой Unity и не могут получать параметры из корутины.
    private string _pendingSceneName;

    // Захватывается из TransitionRequest в начале TransitionRoutine, чтобы OnSceneLoaded
    // мог определить путь FadeIn без доступа к оригинальному объекту запроса.
    private bool _pendingUseHandOff;

    private bool _anyKeyPressed;

    private const string LogPrefix = "[SceneTransitionManager]";

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    protected override void Awake()
    {
        // Если мы — дубликат (персистентный STM уже существует), сбрасываем scene-оверлей
        // в прозрачное состояние до того как base.Awake() уничтожит наш GO.
        // Без этого Overlay с alpha=1 остаётся в сцене и перекрывает экран после FadeIn
        // персистентного STM — выглядит как "оверлей не исчезает".
        if (Instance != null && Instance != this && overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 0f;

        base.Awake();
        if (Instance != this) return;

        // overlayCanvasGroup живёт в сцене (MainMenuCanvas, PrologueCanvas и т.п.).
        // При выгрузке сцены объект уничтожается, и ссылка становится null — фейд пропадает.
        // Решение: пересаживаем Overlay под собственный Canvas-дочерний объект STM
        // до DontDestroyOnLoad, чтобы он персистировал вместе со всем менеджером.
        EnsureOverlayPersists();

        ValidateOverlay();
        // instant:true — сразу обнуляет alpha в Awake.
        // При instant:false ResetOverlay только выставляет interactable, alpha
        // обнулялась бы в Start через кадр — давая вспышку если prefab сохранён с alpha > 0.
        ResetOverlay(instant: true);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (!IsTransitioning)
            SetOverlayInteractable(false);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ─── Публичное API ────────────────────────────────────────────────────────

    /// <summary>
    /// Запускает переход с параметрами по умолчанию.
    /// Для точного управления используй перегрузку с TransitionRequest.
    /// </summary>
    public void LoadScene(string sceneName, float fadeOutDuration = -1f)
    {
        LoadScene(new TransitionRequest(sceneName) { FadeOutDuration = fadeOutDuration });
    }

    /// <summary>
    /// Запускает переход. Если переход уже активен — запрос молча отбрасывается.
    /// Проверяй SceneLoader.IsTransitioning в UI перед вызовом, чтобы избежать двойного нажатия.
    /// </summary>
    public void LoadScene(TransitionRequest request)
    {
        if (request == null)
        {
            Debug.LogError($"{LogPrefix} TransitionRequest равен null.");
            return;
        }

        if (IsTransitioning)
        {
            Log($"Переход активен (State={State}). Запрос '{request.SceneName}' отброшен.");
            return;
        }

        if (!IsSceneInBuildSettings(request.SceneName))
        {
            Debug.LogError(
                $"{LogPrefix} Сцена '{request.SceneName}' не найдена в Build Settings. Отмена."
            );
            return;
        }

        _activeTransition = StartCoroutine(TransitionRoutine(request));
    }

    /// <summary>
    /// Загружает сцену немедленно, без fade. Безопасен на любой фазе перехода.
    /// Используй для экрана смерти, аварийного восстановления или быстрого рестарта.
    /// </summary>
    public void LoadSceneImmediate(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"{LogPrefix} LoadSceneImmediate: имя сцены пустое.");
            return;
        }

        if (!IsSceneInBuildSettings(sceneName))
        {
            Debug.LogError($"{LogPrefix} LoadSceneImmediate: '{sceneName}' не в Build Settings.");
            return;
        }

        if (_activeTransition != null)
        {
            StopCoroutine(_activeTransition);
            _activeTransition = null;
        }

        if (_activeFadeIn != null)
        {
            StopCoroutine(_activeFadeIn);
            _activeFadeIn = null;
        }

        // Разблокируем нативный job до обнуления C#-ссылки.
        // Unity хранит внутреннюю ссылку на AsyncOperation независимо от C# — обнуление
        // нашей ссылки не завершает job. Операция с allowSceneActivation=false на 90%
        // зависнет навсегда, блокируя весь scene loading pipeline до перезапуска игры.
        if (_pendingLoadOperation != null)
        {
            _pendingLoadOperation.allowSceneActivation = true;
            _pendingLoadOperation = null;
        }

        SetState(TransitionState.Idle);
        IsWaitingForActivation = false;
        IsHoldingOverlay       = false;
        _pendingUseHandOff     = false;
        ResetOverlay(instant: true);

        Log($"Немедленная загрузка: '{sceneName}'");
        SceneManager.LoadSceneAsync(sceneName);
    }

    /// <summary>
    /// Активирует сцену, удерживаемую на 90% (WaitForAnyKey без InputAction).
    /// Вызывай из UI когда игрок закрывает экран "нажмите любую клавишу".
    /// </summary>
    public void ConfirmSceneActivation()
    {
        if (!IsWaitingForActivation || _pendingLoadOperation == null)
        {
            Log("ConfirmSceneActivation: нет операции ожидающей активации.");
            return;
        }

        Log("Активация подтверждена.");
        IsWaitingForActivation                     = false;
        _pendingLoadOperation.allowSceneActivation = true;
    }

    /// <summary>
    /// Сигнализирует что получатель HandOff (CutscenePlayer и т.п.) выставил свой оверлей
    /// и глобальный transition-оверлей может начать FadeIn.
    /// Вызывай в ответ на ReadyToHandOffMessage. Страховочный таймаут запустит FadeIn
    /// принудительно если ReleaseOverlay не будет вызван.
    /// </summary>
    public void ReleaseOverlay()
    {
        if (!IsHoldingOverlay)
        {
            Log("ReleaseOverlay: оверлей не удерживается. Вызов проигнорирован.");
            return;
        }

        Log("ReleaseOverlay: запуск FadeIn глобального оверлея.");
        IsHoldingOverlay = false;
        _activeFadeIn = StartCoroutine(FadeInRoutine(_pendingFadeInDuration, _pendingFadeInCurve, _pendingSceneName));
    }

    // ─── Корутина перехода ────────────────────────────────────────────────────

    private IEnumerator TransitionRoutine(TransitionRequest request)
    {
        float fadeOutDuration = request.FadeOutDuration >= 0f
            ? request.FadeOutDuration : defaultFadeOutDuration;

        float fadeInDuration = request.FadeInDuration >= 0f
            ? request.FadeInDuration : defaultFadeInDuration;

        AnimationCurve fadeOutCurve = request.FadeOutCurve ?? defaultFadeOutCurve;

        _pendingSceneName      = request.SceneName;
        _pendingFadeInDuration = fadeInDuration;
        _pendingFadeInCurve    = request.FadeInCurve ?? defaultFadeInCurve;
        _pendingUseHandOff     = request.UseHandOff;

        // ── Фаза 1: FadingOut ──────────────────────────────────────────────────
        SetState(TransitionState.FadingOut);
        SetOverlayInteractable(true);
        Log($"Начало перехода → '{request.SceneName}'. FadeOut: {fadeOutDuration:F1}с");

        MessageBus<TransitionStartedMessage>.Dispatch(
            new TransitionStartedMessage(request.SceneName, fadeOutDuration)
        );

        yield return FadeOverlayWithCurve(0f, 1f, fadeOutDuration, fadeOutCurve);

        // ── Фаза 2: ScreenObscured ─────────────────────────────────────────────
        SetState(TransitionState.ScreenObscured);
        Log($"Экран закрыт. Загрузка '{request.SceneName}'.");

        // AudioSystem меняет трек здесь — экран полностью чёрный, визуальных артефактов нет.
        MessageBus<ScreenObscuredMessage>.Dispatch(
            new ScreenObscuredMessage(request.SceneName, fadeInDuration)
        );

        // ── Фаза 3: LoadingScene ───────────────────────────────────────────────
        SetState(TransitionState.LoadingScene);

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(request.SceneName);
        if (loadOp == null)
        {
            // IsSceneInBuildSettings уже проверил имя — null здесь означает ошибку движка.
            // Прерываем переход безопасно вместо зависания в LoadingScene навсегда.
            Debug.LogError($"{LogPrefix} LoadSceneAsync вернул null для '{request.SceneName}'. Прерывание.");
            SetState(TransitionState.Idle);
            _activeTransition = null;
            ResetOverlay(instant: true);
            yield break;
        }

        // Удерживаем на 90% чтобы активировать сцену только когда готовы —
        // allowSceneActivation=true выставляется ниже по готовности.
        loadOp.allowSceneActivation = false;
        _pendingLoadOperation       = loadOp;

        while (loadOp.progress < 0.9f)
            yield return null;

        Log($"Сцена '{request.SceneName}' на 90%. Ожидание активации...");

        if (request.WaitForAnyKey)
        {
            IsWaitingForActivation = true;
            MessageBus<WaitingForActivationMessage>.Dispatch(
                new WaitingForActivationMessage(request.SceneName)
            );
            Log("WaitForAnyKey: ожидаем подтверждения.");
            yield return WaitForConfirmation(request.AnyKeyAction);
            IsWaitingForActivation      = false;
            loadOp.allowSceneActivation = true;
        }
        else
        {
            loadOp.allowSceneActivation = true;
        }

        while (!loadOp.isDone)
            yield return null;

        // FadeIn запускается из OnSceneLoaded (Unity callback), а не здесь —
        // чтобы он стартовал только после того как Awake/Start/OnEnable новой сцены выполнены.
    }

    // ─── WaitForAnyKey через InputAction ─────────────────────────────────────

    private IEnumerator WaitForConfirmation(InputAction requestAction)
    {
        // Приоритет: action из запроса → глобальный fallback → ConfirmSceneActivation().
        InputAction action = requestAction ?? _anyKeyActionRef?.action;

        if (action == null)
        {
            // InputAction не задан — ждём ConfirmSceneActivation() из внешнего кода.
            Log("WaitForAnyKey: нет InputAction, ожидаем ConfirmSceneActivation().");
            while (IsWaitingForActivation)
                yield return null;
            yield break;
        }

        _anyKeyPressed = false;
        action.performed += OnAnyKeyPressed;
        action.Enable();

        Log("WaitForAnyKey: ожидаем нажатия клавиши...");

        // try/finally гарантирует отписку даже при StopCoroutine() извне (например из
        // LoadSceneImmediate). Без этого каждый прерванный WaitForAnyKey оставляет
        // висячую подписку, которая срабатывает при каждом нажатии в будущих переходах.
        // C# iterators поддерживают try/finally — Dispose() при StopCoroutine вызывает finally.
        // action.Disable() намеренно не вызывается: lifecycle enabled-состояния не наш.
        try
        {
            while (!_anyKeyPressed)
                yield return null;
        }
        finally
        {
            action.performed -= OnAnyKeyPressed;
        }

        Log("WaitForAnyKey: клавиша нажата, активируем сцену.");
    }

    private void OnAnyKeyPressed(InputAction.CallbackContext _) => _anyKeyPressed = true;

    // ─── OnSceneLoaded ────────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (State != TransitionState.LoadingScene) return;

        // Аддитивные загрузки (audio-сцены, persistent-сцены) могут идти параллельно
        // с обычным переходом — они не должны запускать FadeIn.
        if (mode == LoadSceneMode.Additive)
        {
            Log($"Аддитивная загрузка '{scene.name}' — фейд не выполняется.");
            return;
        }

        Log($"Сцена '{scene.name}' активирована.");

        // HandOff требует явного opt-in через TransitionRequest.UseHandOff.
        // Без флага любой подписчик на ReadyToHandOffMessage молча менял бы поведение
        // перехода — нарушение принципа наименьшего удивления.
        // ListenerCount — вторичная защита: если никто не слушает, удержание оверлея
        // оставило бы экран чёрным без пути к восстановлению.
        bool shouldHandOff = _pendingUseHandOff
            && MessageBus<ReadyToHandOffMessage>.ListenerCount > 0;

        if (shouldHandOff)
        {
            IsHoldingOverlay = true;
            Log("HandOff: оверлей удерживается, ожидаем ReleaseOverlay().");
            StartCoroutine(NotifyHandOffNextFrame());
        }
        else
        {
            Log("Стандартный FadeIn.");
            _activeFadeIn = StartCoroutine(FadeInRoutine(_pendingFadeInDuration, _pendingFadeInCurve, _pendingSceneName));
        }
    }

    private IEnumerator NotifyHandOffNextFrame()
    {
        // Один кадр задержки: Awake/Start/OnEnable новой сцены должны выполниться
        // до диспатча, иначе получатель может ещё не подписаться на сообщение.
        yield return null;

        Log("Диспатч ReadyToHandOffMessage.");
        MessageBus<ReadyToHandOffMessage>.Dispatch(new ReadyToHandOffMessage());

        // Ждём вызова ReleaseOverlay() от получателя.
        // Используем таймаут по реальному времени, а не по кадрам — получатель обычно
        // запускает свою корутину в ответ на сообщение, и её первый yield выполняется
        // только на следующем кадре. Таймаут из 2 кадров сработал бы раньше, чем
        // корутина получателя успела бы вызвать ReleaseOverlay().
        float elapsed = 0f;
        while (IsHoldingOverlay && elapsed < _handOffTimeoutSeconds)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        if (IsHoldingOverlay)
        {
            Debug.LogWarning(
                $"{LogPrefix} ReleaseOverlay() не вызван за {_handOffTimeoutSeconds:F1}с. " +
                "Запускаем FadeIn принудительно."
            );
            IsHoldingOverlay = false;
            _activeFadeIn = StartCoroutine(FadeInRoutine(_pendingFadeInDuration, _pendingFadeInCurve, _pendingSceneName));
        }
    }

    private IEnumerator FadeInRoutine(float duration, AnimationCurve curve, string sceneName)
    {
        SetState(TransitionState.FadingIn);
        yield return FadeOverlayWithCurve(1f, 0f, duration, curve);

        SetOverlayInteractable(false);
        SetState(TransitionState.Idle);
        _pendingLoadOperation = null;
        _activeTransition     = null;
        _activeFadeIn         = null;

        Log($"Переход завершён. FadeIn: {duration:F1}с");

        // sceneName — параметр, не поле, чтобы избежать race condition:
        // подписчик TransitionFinishedMessage может немедленно запустить новый переход,
        // перезаписав _pendingSceneName до возврата из Dispatch. Параметр захвачен
        // по значению на сайте вызова — верное имя сцены гарантировано.
        MessageBus<TransitionFinishedMessage>.Dispatch(
            new TransitionFinishedMessage(sceneName, duration)
        );
    }

    // ─── Корутина фейда оверлея ───────────────────────────────────────────────

    private IEnumerator FadeOverlayWithCurve(float from, float to, float duration,
                                             AnimationCurve curve)
    {
        if (overlayCanvasGroup == null)
        {
            // Оверлей не назначен. Всё равно выдерживаем ожидаемую длительность —
            // системы завязанные на тайминг перехода (AudioSystem FadeOut) остаются в синхронизации.
            if (duration > 0f) yield return new WaitForSecondsRealtime(duration);
            yield break;
        }

        if (duration <= 0f)
        {
            overlayCanvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        overlayCanvasGroup.alpha = from;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            overlayCanvasGroup.alpha = Mathf.Lerp(from, to, curve.Evaluate(t));
        }

        overlayCanvasGroup.alpha = to;
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private void SetState(TransitionState newState)
    {
        Log($"State: {State} → {newState}");
        State = newState;
    }

    private void ResetOverlay(bool instant = false)
    {
        if (overlayCanvasGroup == null) return;
        if (instant) overlayCanvasGroup.alpha = 0f;
        SetOverlayInteractable(false);
    }

    private void SetOverlayInteractable(bool block)
    {
        if (overlayCanvasGroup == null) return;
        // Оба флага устанавливаются вместе: interactable — клавиатура/геймпад,
        // blocksRaycasts — мышь/тач. Установка только одного оставляет другой активным.
        overlayCanvasGroup.interactable   = block;
        overlayCanvasGroup.blocksRaycasts = block;
    }

    private static bool IsSceneInBuildSettings(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;

        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void EnsureOverlayPersists()
    {
        if (overlayCanvasGroup == null) return;
        if (overlayCanvasGroup.transform.IsChildOf(transform)) return;

        // Создаём выделенный Canvas прямо под STM (он уже DDOL-объект благодаря base.Awake).
        // Screen Space Overlay рендерится независимо от позиции в иерархии, поэтому
        // родитель с обычным Transform не мешает корректному отображению.
        var canvasGO = new GameObject("[TransitionOverlayCanvas]");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        // worldPositionStays: false — сохраняем localPosition/Scale/Rotation Overlay-объекта
        // (anchors stretch 0→1, что корректно растянет его на весь Canvas).
        overlayCanvasGroup.transform.SetParent(canvasGO.transform, false);

        Log("EnsureOverlayPersists: Overlay перемещён под DDOL-иерархию STM.");
    }

    private void ValidateOverlay()
    {
        if (overlayCanvasGroup == null)
            Debug.LogError($"{LogPrefix} CanvasGroup не назначен — переходы без затемнения.");
    }

    private void Log(string msg)
    {
        if (verboseLogging) Debug.Log($"{LogPrefix} {msg}");
    }
}
