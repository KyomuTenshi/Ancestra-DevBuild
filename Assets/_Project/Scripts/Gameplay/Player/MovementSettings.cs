using UnityEngine;

/// <summary>
/// ScriptableObject с настройками всех систем игрока.
/// Вынесен отдельно, чтобы баланс можно было менять прямо в инспекторе,
/// не трогая код и не пересобирая префаб.
///
/// Как расширять:
///   — Добавь новую [Header] секцию и поля для своей системы (например, Sprint или Crouch).
///   — Создай ассет через правый клик → Ancestra → Movement Settings.
///   — Перетащи ассет в нужный компонент в инспекторе.
/// </summary>
[CreateAssetMenu(fileName = "SO_MovementSettings", menuName = "Ancestra/Movement Settings")]
public class MovementSettings : ScriptableObject
{
    // ══════════════════════════════════════════════════════════════════════════
    // ДВИЖЕНИЕ
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Базовые скорости")]
    [Tooltip("Скорость обычной ходьбы (м/с)")]
    [Min(0f)] public float walkSpeed = 1.35f;

    [Tooltip("Скорость бега (м/с). Используется когда добавишь Sprint.")]
    [Min(0f)] public float sprintSpeed = 2.8f;

    [Tooltip("Скорость приседания (м/с). Используется когда добавишь Crouch.")]
    [Min(0f)] public float crouchSpeed = 0.7f;

    [Header("Инерция")]
    [Tooltip("Насколько быстро игрок набирает скорость (чем больше — тем резче старт)")]
    [Min(0f)] public float accelerationRate = 2.5f;

    [Tooltip("Насколько быстро игрок тормозит (чем больше — тем резче стоп)")]
    [Min(0f)] public float decelerationRate = 6f;

    [Tooltip("Сглаживание аналогового ввода (снижает дёрганье на клавиатуре)")]
    [Range(0.01f, 1f)] public float inputSmoothing = 0.15f;

    // ══════════════════════════════════════════════════════════════════════════
    // ГРАВИТАЦИЯ
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Гравитация")]
    [Tooltip("Сила гравитации (отрицательное значение тянет вниз)")]
    public float gravityStrength = -18f;

    [Tooltip("Маленькая прижимная сила, не даёт скользить по склонам пока стоим на земле")]
    public float groundAnchorForce = -2f;

    [Tooltip("Максимальная скорость падения")]
    public float terminalFallSpeed = -20f;

    [Tooltip("Коэффициент гашения вертикальной скорости при приземлении (0 = никакого гашения, 1 = полная остановка)")]
    [Range(0f, 1f)] public float landingDampFactor = 0.85f;

    // ══════════════════════════════════════════════════════════════════════════
    // ПРИСЕДАНИЕ (подготовлено для расширения)
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Приседание (Crouch)")]
    [Tooltip("Высота CharacterController стоя")]
    [Min(0.1f)] public float standingHeight = 1.8f;

    [Tooltip("Высота CharacterController сидя")]
    [Min(0.1f)] public float crouchHeight = 0.9f;

    [Tooltip("Скорость перехода между стоя/сидя")]
    [Min(0f)] public float crouchTransitionSpeed = 8f;

    // ══════════════════════════════════════════════════════════════════════════
    // КАМЕРА
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Камера — сглаживание")]
    [Tooltip("Время сглаживания вращения камеры (SmoothDamp). Меньше = резче.")]
    [Min(0f)] public float cameraLookSmoothTime = 0.05f;

    [Header("Head Bob (покачивание камеры при ходьбе)")]
    [Tooltip("Боковая амплитуда покачивания (м)")]
    [Min(0f)] public float headBobLateralAmplitude = 0.02f;

    [Tooltip("Вертикальная амплитуда покачивания (м)")]
    [Min(0f)] public float headBobVerticalAmplitude = 0.035f;

    [Tooltip("Частота покачивания (циклов в секунду при максимальной скорости)")]
    [Min(0f)] public float headBobFrequency = 1.0f;

    [Tooltip("Скорость возврата к нейтральной позиции когда стоим")]
    [Min(0f)] public float headBobReturnSpeed = 3f;

    [Header("Шум камеры (Idle)")]
    [Tooltip("Базовая амплитуда покачивания когда стоим (без страха)")]
    [Min(0f)] public float baseCameraNoiseAmplitude = 0.1f;

    [Tooltip("Максимальная амплитуда покачивания при максимальном страхе")]
    [Min(0f)] public float maxCameraNoiseAmplitude = 1.2f;

    [Tooltip("Скорость шума Перлина для покачивания")]
    [Min(0f)] public float cameraNoiseSpeed = 0.5f;

    [Tooltip("Множитель амплитуды во время движения (усиливает нестабильность)")]
    [Min(1f)] public float cameraNoiseMovingMultiplier = 1.5f;

    // ══════════════════════════════════════════════════════════════════════════
    // FOCUS-ЗУМ
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Focus-зум (прицеливание)")]
    [Tooltip("Задержка перед началом зума (с), чтобы случайное нажатие не зумило сразу")]
    [Min(0f)] public float focusInputDelay = 0.15f;

    [Tooltip("Максимальная амплитуда дрожания при зуме (зависит от страха)")]
    [Min(0f)] public float focusJitterMaxAmplitude = 3.5f;

    [Tooltip("Скорость случайного дрожания при зуме")]
    [Min(0f)] public float focusJitterSpeed = 8f;

    // ══════════════════════════════════════════════════════════════════════════
    // ШАГИ
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Шаги")]
    [Tooltip("Расстояние между шагами в метрах")]
    [Min(0.1f)] public float stepDistance = 1.2f;

    [Tooltip("Минимальная случайность интервала шагов (0 = равномерно)")]
    [Min(0f)] public float baseStepRandomness = 0.05f;

    [Tooltip("Максимальная случайность интервала шагов при максимальном страхе")]
    [Min(0f)] public float maxStepRandomness = 0.3f;

    // ══════════════════════════════════════════════════════════════════════════
    // ФОНАРИК
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Фонарик — батарея")]
    [Tooltip("Максимальный заряд батареи (секунды работы при полном заряде)")]
    [Min(1f)] public float batteryMaxCharge = 300f;

    [Tooltip("Скорость разряда батареи (единиц в секунду, 1/maxCharge для линейного расхода)")]
    [Min(0f)] public float batteryDrainRate = 1f;

    [Tooltip("Нижний порог заряда, ниже которого фонарик начинает мерцать")]
    [Range(0f, 1f)] public float batteryFlickerThreshold = 0.2f;

    [Tooltip("Интенсивность мерцания при низком заряде (0 = нет, 1 = сильное)")]
    [Range(0f, 1f)] public float batteryFlickerIntensity = 0.6f;

    [Header("Фонарик — анимация")]
    [Tooltip("Скорость анимации подъёма фонарика при подборе")]
    [Min(0f)] public float flashlightEquipSpeed = 4f;

    [Tooltip("Смещение модели фонарика вниз (в скрытом состоянии)")]
    public float flashlightHiddenYOffset = -1.0f;

    [Tooltip("Скорость плавного изменения интенсивности при вкл/выкл")]
    [Min(0f)] public float flashlightIntensityTransitionSpeed = 8f;

    // ══════════════════════════════════════════════════════════════════════════
    // SWAY ФОНАРИКА
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Покачивание фонарика (Sway)")]
    [Tooltip("Базовая сила покачивания от движения мыши")]
    [Min(0f)] public float swayAmount = 0.02f;

    [Tooltip("Максимальное смещение фонарика от центра (ограничитель)")]
    [Min(0f)] public float maxSwayAmount = 0.06f;

    [Tooltip("Скорость возврата фонарика к нейтральной позиции")]
    [Min(0f)] public float swaySmoothSpeed = 6f;

    [Tooltip("Дополнительная амплитуда покачивания во время ходьбы (bobbing фонарика)")]
    [Min(0f)] public float swayWalkBobAmplitude = 0.01f;

    [Tooltip("Частота покачивания фонарика при ходьбе (синхронизируй с headBobFrequency)")]
    [Min(0f)] public float swayWalkBobFrequency = 1.0f;

    // ══════════════════════════════════════════════════════════════════════════
    // СИСТЕМА СТРАХА
    // ══════════════════════════════════════════════════════════════════════════

    [Header("Система страха")]
    [Tooltip(
        "Кривая нарастания страха по времени (X = нормированное время [0..1], Y = страх [0..1]).\n" +
        "Позволяет задать нелинейный рост: медленно в начале и резко к концу.\n" +
        "Установи время в поле fearMaxTime.")]
    public AnimationCurve fearCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Время (в секундах) за которое страх достигает максимума по кривой.\n" +
             "Ранее: 1/fearGrowthRate = ~200 сек при fearGrowthRate=0.005.")]
    [Min(1f)] public float fearMaxTime = 200f;
}
