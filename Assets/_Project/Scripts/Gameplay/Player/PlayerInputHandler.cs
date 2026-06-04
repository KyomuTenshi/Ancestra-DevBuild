using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Читает ввод игрока через Unity New Input System и предоставляет
/// абстрактные свойства остальным системам.
///
/// Принцип SRP: этот скрипт ТОЛЬКО читает ввод и хранит его текущее состояние.
/// Никакой логики движения, камеры и т.д. здесь нет.
///
/// Как расширять:
///   — Добавь новое действие в PlayerInputActions (Input Actions Asset).
///   — Добавь публичное свойство ниже и читай его в нужном Update().
///   — Подпишись на событие (performed/canceled) если нужна реакция в момент нажатия.
///
/// Поддержка геймпада:
///   Реализована через Input System — достаточно добавить геймпад-биндинг
///   в Input Actions Asset. Никаких изменений в этом скрипте не нужно.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // ПУБЛИЧНЫЕ СВОЙСТВА (только чтение)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Аналоговый вектор движения [-1..1] по X и Y. Нормализован для диагоналей.</summary>
    public Vector2 MoveInput { get; private set; }

    /// <summary>Вектор взгляда мыши / правого стика. Единицы зависят от биндинга.</summary>
    public Vector2 LookInput { get; private set; }

    /// <summary>Удерживается ли кнопка зума прямо сейчас.</summary>
    public bool IsFocusing { get; private set; }

    /// <summary>Нажата ли кнопка переключения фонарика именно в этом кадре.</summary>
    public bool FlashlightTogglePressedThisFrame { get; private set; }

    /// <summary>Удерживается ли кнопка бега прямо сейчас (подготовлено для Sprint).</summary>
    public bool IsSprintHeld { get; private set; }

    /// <summary>Удерживается ли кнопка приседания прямо сейчас (подготовлено для Crouch).</summary>
    public bool IsCrouchHeld { get; private set; }

    // ══════════════════════════════════════════════════════════════════════════
    // СОБЫТИЯ
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Вызывается в момент начала взаимодействия (нажатие E).</summary>
    public event Action OnInteractPerformed;

    /// <summary>Вызывается в момент отпускания кнопки взаимодействия.</summary>
    public event Action OnInteractCanceled;

    // ══════════════════════════════════════════════════════════════════════════
    // ПРИВАТНЫЕ ПОЛЯ
    // ══════════════════════════════════════════════════════════════════════════

    private PlayerInputActions _inputActions;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        _inputActions.Enable();

        // Подписываемся только на события (performed / canceled).
        // Состояние (IsPressed, ReadValue) опрашиваем каждый кадр в Update().
        _inputActions.Player.Interact.performed += HandleInteractPerformed;
        _inputActions.Player.Interact.canceled  += HandleInteractCanceled;
    }

    private void OnDisable()
    {
        _inputActions.Player.Interact.performed -= HandleInteractPerformed;
        _inputActions.Player.Interact.canceled  -= HandleInteractCanceled;

        _inputActions.Disable();
    }

    private void OnDestroy()
    {
        // КРИТИЧНО: Input System создаёт неуправляемые нативные ресурсы.
        // Без Dispose() при переходе между сценами будет утечка памяти.
        _inputActions?.Dispose();
    }

    private void Update()
    {
        // Опрашиваем все состояния ввода один раз в начале кадра.
        // Остальные системы читают уже готовые свойства — без обращений к Input System напрямую.

        MoveInput  = _inputActions.Player.Movement.ReadValue<Vector2>();
        LookInput  = _inputActions.Player.Look.ReadValue<Vector2>();

        IsFocusing   = _inputActions.Player.Focus.IsPressed();
        IsSprintHeld = _inputActions.Player.Sprint.IsPressed();
        IsCrouchHeld = _inputActions.Player.Crouch.IsPressed();

        // WasPressedThisFrame() — безопасный способ поймать одиночное нажатие без флага.
        FlashlightTogglePressedThisFrame = _inputActions.Player.Flashlight.WasPressedThisFrame();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ОБРАБОТЧИКИ СОБЫТИЙ
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleInteractPerformed(InputAction.CallbackContext ctx) => OnInteractPerformed?.Invoke();
    private void HandleInteractCanceled(InputAction.CallbackContext ctx)  => OnInteractCanceled?.Invoke();
}
