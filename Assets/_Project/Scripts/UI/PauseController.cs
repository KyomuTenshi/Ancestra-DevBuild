// Контроллер паузы с context-стеком. Несколько систем могут паузить независимо:
// Pause(context) добавляет контекст в стек, Resume(context) убирает его.
// timeScale возвращается к 1 только когда стек полностью пуст.
// AudioSystem уведомляется через MessageBus — нет прямой зависимости.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Управляет паузой игры через context-стек. Каждый вызов Pause(ctx) блокирует timeScale;
/// Resume(ctx) разблокирует его только если все контексты сняли паузу.
/// Синглтон: живёт между сценами, безопасен при переходах через SceneLoader.
/// </summary>
public class PauseController : PersistentSingleton<PauseController>
{
    // ─── Инспектор ────────────────────────────────────────────────────────────

    [Header("Панели")]
    [SerializeField] private UIPanel _pauseMenuPanel;
    [SerializeField] private UIPanel _settingsMenuPanel;

    [Header("Input")]
    [Tooltip("Перетащи Action 'Pause' из Input Action Asset. " +
             "Создай: Project → Create → Input Actions, добавь Action Map 'UI', Action 'Pause'.")]
    [SerializeField] private InputActionReference _pauseAction;

    [Header("Зависимости")]
    [SerializeField] private SettingsUIController _settingsUIController;

    [Header("Переходы")]
    [SerializeField] private string _mainMenuScene    = "MainMenu";
    [SerializeField] private float  _fadeOutDuration  = 1.5f;

    // Каждый вызов Pause(context) добавляет объект в стек.
    // Resume(context) убирает его. timeScale = 0 пока стек не пуст.
    // Дедупликация: один контекст не может паузить дважды подряд.
    private readonly List<UnityEngine.Object> _pauseContextStack = new List<UnityEngine.Object>(4);

    /// <summary>True если хотя бы один контекст запросил паузу.</summary>
    public bool IsPaused => _pauseContextStack.Count > 0;

    /// <summary>Верхний контекст стека (последний запросивший паузу).</summary>
    public UnityEngine.Object TopContext => _pauseContextStack.Count > 0
        ? _pauseContextStack[_pauseContextStack.Count - 1]
        : null;

    // ─── События ──────────────────────────────────────────────────────────────

    /// <summary>true = пауза, false = возобновление.</summary>
    public static event Action<bool> OnPauseToggled;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        InitUI();
    }

    private void Start()
    {
        if (Instance != this) return;
        _settingsUIController?.Init();
    }

    private void OnEnable()
    {
        if (_pauseAction == null) return;
        _pauseAction.action.performed += OnPauseActionPerformed;
        _pauseAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_pauseAction == null) return;
        _pauseAction.action.performed -= OnPauseActionPerformed;
        _pauseAction.action.Disable();
    }

    private void OnDestroy()
    {
        // Без этого guard: дубликат (уничтожаемый PersistentSingleton) затирал бы OnPauseToggled
        // для всех подписчиков — они теряли бы колбэки без каких-либо ошибок.
        if (Instance != this) return;
        if (IsPaused) ForceResumeAll();
        OnPauseToggled = null;
    }

    // ─── Публичный API ────────────────────────────────────────────────────────

    /// <summary>
    /// Ставит игру на паузу от имени context.
    /// Несколько вызовов с разными context накапливаются в стеке.
    /// timeScale = 0 пока стек не пуст.
    /// </summary>
    public void Pause(UnityEngine.Object context = null)
    {
        if (SceneLoader.IsTransitioning) return;

        context ??= this;

        // Дедупликация — один контекст не может паузить дважды.
        _pauseContextStack.Remove(context);
        _pauseContextStack.Add(context);

        bool wasFirstPause = _pauseContextStack.Count == 1;

        Time.timeScale = 0f;

        if (wasFirstPause)
        {
            _pauseMenuPanel?.Show();
            ReleaseCursor();
            OnPauseToggled?.Invoke(true);

            // Уведомляем AudioSystem — он приглушит музыку.
            MessageBus<GamePausedAudioMessage>.Dispatch(new GamePausedAudioMessage(fade: 0.3f));
        }
    }

    /// <summary>
    /// Снимает паузу для данного context.
    /// timeScale возвращается к 1 только когда ВСЕ контексты сняли паузу.
    /// </summary>
    public void Resume(UnityEngine.Object context = null)
    {
        context ??= this;

        if (!_pauseContextStack.Remove(context)) return;

        if (_pauseContextStack.Count > 0) return; // другие контексты ещё паузят

        Time.timeScale = 1f;
        _pauseMenuPanel?.Hide();
        _settingsMenuPanel?.Hide();
        LockCursor();
        OnPauseToggled?.Invoke(false);

        MessageBus<GameResumedAudioMessage>.Dispatch(new GameResumedAudioMessage(fade: 0.3f));
    }

    /// <summary>Открывает настройки из меню паузы.</summary>
    public void OpenSettings()
    {
        _pauseMenuPanel?.Hide();
        _settingsMenuPanel?.Show();
        _settingsUIController?.OpenSettingsMainMenu();
    }

    /// <summary>Возвращает в меню паузы из настроек.</summary>
    public void BackToPauseMenu()
    {
        _settingsMenuPanel?.Hide();
        _pauseMenuPanel?.Show();
    }

    /// <summary>Загружает главное меню, гарантированно снимая паузу перед переходом.</summary>
    public void LoadMainMenu()
    {
        if (SceneLoader.IsTransitioning) return;
        ForceResumeAll();
        ReleaseCursor();
        SceneLoader.Load(_mainMenuScene, _fadeOutDuration);
    }

    // ─── Приватные методы ─────────────────────────────────────────────────────

    private void InitUI()
    {
        _pauseContextStack.Clear();
        Time.timeScale = 1f;
        _pauseMenuPanel?.Hide();
        _settingsMenuPanel?.Hide();
        LockCursor();
    }

    /// <summary>Принудительно снимает ВСЕ контексты паузы. Для OnDestroy и LoadMainMenu.</summary>
    private void ForceResumeAll()
    {
        _pauseContextStack.Clear();
        Time.timeScale = 1f;
        OnPauseToggled?.Invoke(false);
        MessageBus<GameResumedAudioMessage>.Dispatch(new GameResumedAudioMessage());
    }

    /// <summary>
    /// Обработчик InputAction.performed.
    /// Навигация: Настройки → Пауза → Игра → Пауза.
    /// </summary>
    private void OnPauseActionPerformed(InputAction.CallbackContext _)
    {
        if (_settingsMenuPanel != null && _settingsMenuPanel.IsVisible)
            BackToPauseMenu();
        else if (IsPaused)
            Resume(this);
        else
            Pause(this);
    }

    private static void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }
}