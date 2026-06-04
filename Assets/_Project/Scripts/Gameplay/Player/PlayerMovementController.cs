using System;
using UnityEngine;

/// <summary>
/// Контроллер физического перемещения игрока (CharacterController).
/// Отвечает за: инерцию, гравитацию, дистанцию шагов, систему страха,
/// и хранит публичное состояние (скорость, страх, флаги).
///
/// Принцип SRP: здесь только физика перемещения. Нет логики ввода,
/// нет логики камеры, нет логики звука.
///
/// Как расширять:
///   — Бег (Sprint): включи флаг _isSprinting по IsSprintHeld и выбирай targetSpeed.
///   — Приседание (Crouch): меняй _characterController.height через корутину.
///   — Наклон (Lean): добавь смещение по оси X к трансформу отдельным компонентом.
///   — Взаимодействие: в DoorInteraction подпишись на inputHandler.OnInteractPerformed.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovementController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // ЗАВИСИМОСТИ
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Зависимости")]
    [SerializeField] private MovementSettings   settings;
    [SerializeField] private PlayerInputHandler inputHandler;

    // ══════════════════════════════════════════════════════════════════════════
    // СОБЫТИЯ
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Вызывается при каждом шаге. Параметр — множитель громкости (1 = нормально, >1 = испуг).
    /// Подпишись в AudioManager или FootstepSystem.
    /// </summary>
    public event Action<float> OnStepTaken;

    /// <summary>
    /// Вызывается при приземлении после падения. Параметр — вертикальная скорость в момент удара.
    /// </summary>
    public event Action<float> OnLanded;

    // ══════════════════════════════════════════════════════════════════════════
    // ПУБЛИЧНОЕ СОСТОЯНИЕ (только чтение)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Разрешено ли движение (внешний контроль, например из DialogueSystem).</summary>
    public bool CanMove { get; set; } = true;

    /// <summary>
    /// Текущий страх [0..1]. Оценивается по AnimationCurve из MovementSettings.
    /// Влияет на звук шагов и эффекты камеры.
    /// </summary>
    public float CurrentFear { get; private set; }

    /// <summary>Текущая скорость, нормализованная по walkSpeed [0..1].</summary>
    public float CurrentNormalizedSpeed { get; private set; }

    /// <summary>Движется ли игрок прямо сейчас.</summary>
    public bool IsMoving => _currentSpeed > SpeedEpsilon || _horizontalVelocity.sqrMagnitude > InputEpsilonSq;

    /// <summary>Стоит ли игрок на земле.</summary>
    public bool IsGrounded => _characterController.isGrounded;

    /// <summary>Приседает ли игрок (подготовлено для Crouch системы).</summary>
    public bool IsCrouching { get; private set; }

    // ══════════════════════════════════════════════════════════════════════════
    // КОНСТАНТЫ
    // ══════════════════════════════════════════════════════════════════════════

    private const float InputEpsilonSq = 0.0001f; // квадрат порога ввода (избегаем sqrt)
    private const float SpeedEpsilon   = 0.05f;   // минимальная скорость для определения IsMoving

    // ══════════════════════════════════════════════════════════════════════════
    // ПРИВАТНЫЕ ПОЛЯ
    // ══════════════════════════════════════════════════════════════════════════

    private CharacterController _characterController;
    private Transform           _transform;

    // Горизонтальное движение
    private Vector2 _smoothedInput;
    private Vector2 _inputSmoothVelocity;
    private Vector3 _horizontalVelocity;
    private float   _currentSpeed;

    // Вертикальное движение
    private float _verticalVelocityY;
    private bool  _wasGrounded;

    // Шаги
    private float _stepDistanceAccumulator;
    private float _currentStepDistanceTarget;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _transform           = transform;

        _currentStepDistanceTarget = settings != null ? settings.stepDistance : 1.2f;

        ValidateDependencies();
    }

    private void Update()
    {
        UpdateFear();
        UpdateCrouch();
        UpdateHorizontalMovement();
        UpdateVerticalMovement();

        Vector3 finalVelocity = _horizontalVelocity + Vector3.up * _verticalVelocityY;
        _characterController.Move(finalVelocity * Time.deltaTime);

        UpdateStepCounter();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // СТРАХ
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateFear()
    {
        // Нормализуем время в [0..1] по fearMaxTime и передаём в кривую.
        // Позволяет задать любой профиль роста страха прямо в инспекторе
        // без изменения кода (медленный старт, экспоненциальный рост и т.д.).
        float normalizedTime = Mathf.Clamp01(Time.timeSinceLevelLoad / settings.fearMaxTime);
        CurrentFear = Mathf.Clamp01(settings.fearCurve.Evaluate(normalizedTime));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ПРИСЕДАНИЕ (заготовка)
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateCrouch()
    {
        // Когда реализуешь Crouch — раскомментируй и добавь анимацию камеры.
        // IsCrouching = inputHandler.IsCrouchHeld;
        //
        // float targetHeight = IsCrouching ? settings.crouchHeight : settings.standingHeight;
        // _characterController.height = Mathf.Lerp(
        //     _characterController.height, targetHeight,
        //     settings.crouchTransitionSpeed * Time.deltaTime);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ГОРИЗОНТАЛЬНОЕ ДВИЖЕНИЕ
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateHorizontalMovement()
    {
        // Если движение запрещено — плавно тормозим до нуля
        Vector2 rawInput = CanMove ? inputHandler.MoveInput : Vector2.zero;

        // Сглаживаем ввод чтобы убрать дёрганье при нажатии клавиш (особенно на клавиатуре)
        _smoothedInput = Vector2.SmoothDamp(
            _smoothedInput, rawInput,
            ref _inputSmoothVelocity, settings.inputSmoothing);

        bool hasInput = rawInput.sqrMagnitude > InputEpsilonSq;

        // Выбираем целевую скорость.
        // Расширение Sprint: вместо walkSpeed используй CanSprint ? settings.sprintSpeed : settings.walkSpeed
        float targetSpeed = hasInput ? settings.walkSpeed : 0f;
        float accelRate   = hasInput ? settings.accelerationRate : settings.decelerationRate;

        _currentSpeed          = Mathf.Lerp(_currentSpeed, targetSpeed, accelRate * Time.deltaTime);
        CurrentNormalizedSpeed = settings.walkSpeed > 0f
            ? Mathf.Clamp01(_currentSpeed / settings.walkSpeed)
            : 0f;

        // Направление движения в мировом пространстве (относительно поворота игрока)
        Vector3 moveDirection = (_transform.right   * _smoothedInput.x
                               + _transform.forward * _smoothedInput.y).normalized;

        // Плавно интерполируем горизонтальную скорость — это даёт ощущение веса и инерцию
        _horizontalVelocity = Vector3.Lerp(
            _horizontalVelocity,
            moveDirection * _currentSpeed,
            accelRate * Time.deltaTime);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ВЕРТИКАЛЬНОЕ ДВИЖЕНИЕ (ГРАВИТАЦИЯ)
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateVerticalMovement()
    {
        bool grounded = _characterController.isGrounded;

        if (grounded)
        {
            if (!_wasGrounded && _verticalVelocityY < -1f)
            {
                OnLanded?.Invoke(_verticalVelocityY);
                _verticalVelocityY *= (1f - settings.landingDampFactor);
            }

            // Маленькая постоянная прижимная сила: без неё CharacterController
            // "прыгает" при движении по ступенькам и склонам (isGrounded = false на один кадр)
            _verticalVelocityY = settings.groundAnchorForce;
        }
        else
        {
            _verticalVelocityY = Mathf.Max(
                _verticalVelocityY + settings.gravityStrength * Time.deltaTime,
                settings.terminalFallSpeed);
        }

        _wasGrounded = grounded;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // СИСТЕМА ШАГОВ
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateStepCounter()
    {
        if (_characterController.isGrounded && IsMoving)
        {
            AccumulateAndFireStep();
        }
        else if (!HasMoveInput())
        {
            // Плавно сбрасываем накопленное расстояние при остановке,
            // чтобы не было случайного шага сразу после старта движения
            _stepDistanceAccumulator = Mathf.Lerp(_stepDistanceAccumulator, 0f, 2f * Time.deltaTime);
        }
    }

    private void AccumulateAndFireStep()
    {
        _stepDistanceAccumulator += _horizontalVelocity.magnitude * Time.deltaTime;

        if (_stepDistanceAccumulator < _currentStepDistanceTarget) return;

        _stepDistanceAccumulator = 0f;

        // Громкость шага немного рандомизируется в зависимости от страха
        float volumeFactor = 1f + UnityEngine.Random.Range(0f, CurrentFear * 0.5f);
        OnStepTaken?.Invoke(volumeFactor);

        // Рандомизируем расстояние до следующего шага:
        // при высоком страхе игрок "спотыкается" — шаги становятся неравномерными
        float randomness               = Mathf.Lerp(settings.baseStepRandomness, settings.maxStepRandomness, CurrentFear);
        float variation                = 1f + UnityEngine.Random.Range(-randomness, randomness);
        _currentStepDistanceTarget     = Mathf.Max(0.1f, settings.stepDistance * variation);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // УТИЛИТЫ
    // ══════════════════════════════════════════════════════════════════════════

    private bool HasMoveInput() => CanMove && inputHandler.MoveInput.sqrMagnitude > InputEpsilonSq;

    private void ValidateDependencies()
    {
        if (settings     == null) Debug.LogError($"[{nameof(PlayerMovementController)}] MovementSettings не назначен!", this);
        if (inputHandler == null) Debug.LogError($"[{nameof(PlayerMovementController)}] PlayerInputHandler не назначен!", this);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ПУБЛИЧНЫЙ API — ВНЕШНЕЕ ВОЗДЕЙСТВИЕ
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Мгновенно добавляет вертикальный импульс (например, при взрыве или отдаче).
    /// Не используй для прыжка — лучше добавить отдельную систему прыжков.
    /// </summary>
    public void AddVerticalImpulse(float impulse)
    {
        _verticalVelocityY += impulse;
        _verticalVelocityY  = Mathf.Max(_verticalVelocityY, settings.terminalFallSpeed);
    }

    /// <summary>
    /// Устанавливает страх вручную — для кат-сцен, триггеров или тестирования.
    /// После вызова автоматический рост страха продолжается с того же значения
    /// (если время не соответствует кривой — выберется ближайшее).
    /// Используй осторожно: не вызывай каждый кадр, иначе перебьёшь кривую.
    /// </summary>
    public void SetFearOverride(float fear)
    {
        CurrentFear = Mathf.Clamp01(fear);
    }
}
