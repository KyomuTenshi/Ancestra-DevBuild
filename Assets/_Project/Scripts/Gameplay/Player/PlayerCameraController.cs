using UnityEngine;

/// <summary>
/// Управляет вращением камеры, Head Bob, Focus-зумом и кинематографичным шумом.
///
/// Принцип SRP: этот скрипт отвечает только за визуальное поведение камеры.
/// Физика игрока — в PlayerMovementController, ввод — в PlayerInputHandler.
///
/// Порядок компонентов:
///   Camera (этот компонент) — дочерний объект CameraHolder.
///   CameraHolder — дочерний объект PlayerBody (CharacterController).
///   Это стандартная иерархия: Body вращается по Y, Camera по X.
///
/// Как расширять:
///   — Lean (наклон): добавь смещение по Z к cameraHolder.localEulerAngles из отдельного компонента.
///   — Drunk/Concussion: временно увеличь noiseAmplitude через публичный метод AddNoisePulse().
///   — CinematicBars: добавь отдельный компонент, который слушает событие OnFocusChanged.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PlayerCameraController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // ЗАВИСИМОСТИ
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Зависимости")]
    [SerializeField] private MovementSettings         settings;
    [SerializeField] private PlayerInputHandler       inputHandler;
    [SerializeField] private PlayerMovementController movementController;

    [Tooltip("Объект, который поворачивается по вертикали (Pitch). Обычно прямой родитель Camera.")]
    [SerializeField] private Transform cameraHolder;

    [Tooltip("Тело игрока — поворачивается по горизонтали (Yaw).")]
    [SerializeField] private Transform playerBody;

    // ══════════════════════════════════════════════════════════════════════════
    // НАСТРОЙКИ ЧЕРЕЗ ИНСПЕКТОР
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Мышь")]
    [Tooltip("Чувствительность мыши. При зуме автоматически снижается до 40%.")]
    public float mouseSensitivity = 60f;

    [Header("Focus-зум")]
    [Tooltip("FOV при максимальном зуме")]
    public float focusTargetFOV = 35f;

    [Tooltip("Скорость перехода в/из зума (чем больше — тем резче)")]
    public float focusTransitionSpeed = 5f;

    // ══════════════════════════════════════════════════════════════════════════
    // ПУБЛИЧНОЕ СОСТОЯНИЕ
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Разрешено ли вращение камеры (внешний контроль из DialogSystem, CutsceneSystem).</summary>
    public bool CanLook { get; set; } = true;

    /// <summary>Текущий вес зума [0..1]. Используется FlashlightSway для демпфирования.</summary>
    public float FocusWeight => _focusWeight;

    // ══════════════════════════════════════════════════════════════════════════
    // КОНСТАНТЫ
    // ══════════════════════════════════════════════════════════════════════════

    private const float MinPitchAngle = -65f;
    private const float MaxPitchAngle =  70f;

    // Семена для шума Перлина: разные значения дают независимые оси
    private const float NoiseSeedX = 0f;
    private const float NoiseSeedY = 10f;
    private const float NoiseSeedZ = 20f;

    // Коэффициент снижения чувствительности при зуме
    private const float FocusSensitivityMultiplier = 0.4f;

    // Быстрое дрожание для пульса
    private const float PulseNoiseSpeed = 15f;

    // ══════════════════════════════════════════════════════════════════════════
    // ПРИВАТНЫЕ ПОЛЯ
    // ══════════════════════════════════════════════════════════════════════════

    private Camera _mainCamera;
    private float  _defaultFOV;

    // Вращение
    private float   _targetPitch;
    private Vector2 _smoothedLook;
    private Vector2 _lookSmoothVelocity;

    // Focus
    private float _focusWeight;
    private float _focusDelayTimer;

    // Head Bob
    private float   _bobTimer;
    private Vector3 _bobOffset;

    // Idle Noise
    private Vector3 _noiseOffset;

    // Внешний пульс шума (drunk, concussion и т.д.)
    private float _noisePulseAmplitude;
    private float _noisePulseDuration;
    private float _noisePulseTimer;

    // Кешируем флаг движения чтобы не обращаться к movementController дважды за кадр
    private bool  _isMoving;
    private float _currentFear;

    // ══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _mainCamera = GetComponent<Camera>();
        _defaultFOV = _mainCamera.fieldOfView;

        // Нормализуем начальный угол из диапазона Unity [0..360] в [-180..180]
        _targetPitch = NormalizeAngle(cameraHolder.localEulerAngles.x);

        ValidateDependencies();
    }

    private void LateUpdate()
    {
        // LateUpdate гарантирует, что камера обновляется ПОСЛЕ движения игрока в Update().
        // Это полностью исключает дёрганье (jitter).

        // Кешируем состояние один раз — все методы читают из кеша
        if (movementController != null)
        {
            _currentFear = movementController.CurrentFear;
            _isMoving    = movementController.IsMoving;
        }

        UpdateFocus();
        UpdateRotation();
        UpdateHeadBob();
        UpdateIdleNoise();
        UpdateNoisePulse();

        // Применяем все смещения разом в одном вызове Euler
        cameraHolder.localRotation = Quaternion.Euler(
            _targetPitch + _noiseOffset.x,
            _noiseOffset.y,
            _noiseOffset.z);

        // Позиция камеры = только Head Bob (шум применён через rotation)
        transform.localPosition = _bobOffset;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ФОКУС (ЗУМ)
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateFocus()
    {
        // Таймер задержки: зум начинается только если кнопка удерживается дольше порога.
        // Исключает случайные срабатывания при одиночных нажатиях.
        if (inputHandler.IsFocusing)
            _focusDelayTimer += Time.deltaTime;
        else
            _focusDelayTimer = 0f;

        float targetWeight = _focusDelayTimer >= settings.focusInputDelay ? 1f : 0f;
        _focusWeight = Mathf.MoveTowards(_focusWeight, targetWeight, focusTransitionSpeed * Time.deltaTime);

        // Дрожание при зуме: нарастает со страхом
        float jitterAmplitude = Mathf.Lerp(0f, settings.focusJitterMaxAmplitude, _currentFear) * _focusWeight;
        float fovJitter       = SampleNoise1D(Time.time * settings.focusJitterSpeed) * jitterAmplitude;

        _mainCamera.fieldOfView = Mathf.Lerp(_defaultFOV, focusTargetFOV, _focusWeight) + fovJitter;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ВРАЩЕНИЕ КАМЕРЫ
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateRotation()
    {
        if (!CanLook)
        {
            // При запрете взгляда — сбрасываем скорость сглаживания, чтобы не было рывка при разблокировке
            _lookSmoothVelocity = Vector2.zero;
            return;
        }

        // Снижаем чувствительность при зуме для точного прицеливания
        float sensitivity  = mouseSensitivity * Mathf.Lerp(1f, FocusSensitivityMultiplier, _focusWeight);
        Vector2 targetLook = inputHandler.LookInput * (sensitivity * Time.deltaTime);

        // SmoothDamp убирает "рывок" при резком движении мыши
        _smoothedLook = Vector2.SmoothDamp(
            _smoothedLook, targetLook,
            ref _lookSmoothVelocity, settings.cameraLookSmoothTime);

        // Горизонталь — поворачиваем всё тело игрока (Yaw)
        playerBody.Rotate(0f, _smoothedLook.x, 0f, Space.Self);

        // Вертикаль — накапливаем и зажимаем угол (Pitch)
        _targetPitch = Mathf.Clamp(_targetPitch - _smoothedLook.y, MinPitchAngle, MaxPitchAngle);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HEAD BOB
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateHeadBob()
    {
        if (!_isMoving)
        {
            // Плавно возвращаем камеру в нейтральную позицию
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, settings.headBobReturnSpeed * Time.deltaTime);
            _bobTimer  = 0f;
            return;
        }

        // Частота покачивания растёт со страхом и со скоростью игрока
        float fearMultiplier = 1f + _currentFear * 0.5f;
        float speedFactor    = movementController != null
            ? 0.8f + movementController.CurrentNormalizedSpeed * 0.2f
            : 1f;

        _bobTimer += Time.deltaTime * settings.headBobFrequency * fearMultiplier * speedFactor;

        float phase    = _bobTimer * Mathf.PI * 2f;
        float lateral  = Mathf.Sin(phase)             * settings.headBobLateralAmplitude;
        float vertical = -Mathf.Abs(Mathf.Cos(phase)) * settings.headBobVerticalAmplitude;

        // При зуме демпфируем боб: прицеливание = стабильность
        float bobDamp  = Mathf.Lerp(1f, 0.3f, _focusWeight);
        float bobScale = bobDamp * (movementController != null ? movementController.CurrentNormalizedSpeed : 1f);

        _bobOffset = new Vector3(lateral, vertical, 0f) * bobScale;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // IDLE NOISE (ДРОЖАНИЕ КАМЕРЫ)
    // ══════════════════════════════════════════════════════════════════════════

    private void UpdateIdleNoise()
    {
        float amplitude = Mathf.Lerp(
            settings.baseCameraNoiseAmplitude,
            settings.maxCameraNoiseAmplitude,
            _currentFear);

        if (_isMoving)
            amplitude *= settings.cameraNoiseMovingMultiplier;

        float t = Time.time * settings.cameraNoiseSpeed;

        var targetNoise = new Vector3(
            SampleNoise1D(t + NoiseSeedX) * amplitude,
            SampleNoise1D(t + NoiseSeedY) * amplitude,
            SampleNoise1D(t + NoiseSeedZ) * amplitude);

        _noiseOffset = Vector3.Lerp(_noiseOffset, targetNoise, 10f * Time.deltaTime);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ШОКОВЫЙ ПУЛЬС ШУМА (публичный API)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Добавляет временный импульс дрожания камеры.
    /// Используй для: удар по голове, взрыв рядом, резкий испуг.
    /// </summary>
    /// <param name="amplitude">Максимальная амплитуда дополнительного шума</param>
    /// <param name="duration">Длительность импульса в секундах</param>
    public void AddNoisePulse(float amplitude, float duration)
    {
        // Если пульс уже активен — берём максимум, не сбрасываем таймер полностью
        _noisePulseAmplitude = Mathf.Max(_noisePulseAmplitude, amplitude);
        _noisePulseDuration  = duration;
        _noisePulseTimer     = 0f;
    }

    private void UpdateNoisePulse()
    {
        if (_noisePulseDuration <= 0f) return;

        _noisePulseTimer += Time.deltaTime;
        float progress    = Mathf.Clamp01(_noisePulseTimer / _noisePulseDuration);

        // Амплитуда пульса спадает по кривой (1 → 0)
        float pulseAmplitude = _noisePulseAmplitude * (1f - progress);

        float t = Time.time * PulseNoiseSpeed;
        _noiseOffset += new Vector3(
            SampleNoise1D(t + 100f) * pulseAmplitude,
            SampleNoise1D(t + 200f) * pulseAmplitude,
            SampleNoise1D(t + 300f) * pulseAmplitude);

        if (_noisePulseTimer >= _noisePulseDuration)
        {
            _noisePulseDuration  = 0f;
            _noisePulseAmplitude = 0f;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ПУБЛИЧНЫЙ API — ВНЕШНЕЕ ВОЗДЕЙСТВИЕ
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Мгновенно сбрасывает накопленный Pitch (вертикальный угол) до заданного значения.
    /// Полезно при телепортации или смене точки спавна.
    /// </summary>
    public void SetPitch(float pitchDegrees)
    {
        _targetPitch = Mathf.Clamp(pitchDegrees, MinPitchAngle, MaxPitchAngle);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // УТИЛИТЫ
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Выборка 1D шума Перлина в диапазоне [-1..1].
    /// Перлин возвращает [0..1], поэтому делаем ремаппинг.
    /// </summary>
    private static float SampleNoise1D(float t) => (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;

    /// <summary>
    /// Unity хранит углы в диапазоне [0..360].
    /// Нормализуем в [-180..180] для корректных математических операций.
    /// </summary>
    private static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;

    private void ValidateDependencies()
    {
        if (settings           == null) Debug.LogError($"[{nameof(PlayerCameraController)}] MovementSettings не назначен!", this);
        if (inputHandler       == null) Debug.LogError($"[{nameof(PlayerCameraController)}] PlayerInputHandler не назначен!", this);
        if (movementController == null) Debug.LogWarning($"[{nameof(PlayerCameraController)}] PlayerMovementController не назначен — Head Bob отключён.", this);
        if (cameraHolder       == null) Debug.LogError($"[{nameof(PlayerCameraController)}] CameraHolder не назначен!", this);
        if (playerBody         == null) Debug.LogError($"[{nameof(PlayerCameraController)}] PlayerBody не назначен!", this);
    }
}
