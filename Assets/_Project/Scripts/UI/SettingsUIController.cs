// UI-контроллер экрана настроек. Управляет тремя категориями: аудио, графика, язык.
// Все изменения накапливаются в _pending-полях и применяются атомарно только через Apply().
// Discard() откатывает _pending-поля к значениям из AppSettings и возвращает язык
// к _committedLanguageIndex — индексу, зафиксированному последним вызовом Apply().
// При уходе из подменю с несохранёнными изменениями показывается ConfirmDialog.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран настроек с явным подтверждением (Apply / Discard).
/// Pending-состояние гарантирует, что изменения видны только в UI и не попадают
/// в AppSettings до явного Apply(). Это позволяет безопасно отменить любые правки
/// кнопкой Discard вне зависимости от того, по какому подменю навигировал пользователь.
/// </summary>
public class SettingsUIController : MonoBehaviour
{
    // ─── Инспектор ────────────────────────────────────────────────────────────

    [Header("Подменю")]
    [SerializeField] private UIPanel _settingsMainPanel;
    [SerializeField] private UIPanel _videoPanel;
    [SerializeField] private UIPanel _audioPanel;
    [SerializeField] private UIPanel _gameplayPanel;

    [Header("Аудио")]
    [SerializeField] private Slider _masterVolSlider;
    [SerializeField] private Slider _musicVolSlider;
    [SerializeField] private Slider _ambientVolSlider;
    [SerializeField] private Slider _sfxVolSlider;

    [Header("Качество")]
    [SerializeField] private Button _lowQualityBtn;
    [SerializeField] private Button _medQualityBtn;
    [SerializeField] private Button _highQualityBtn;

    // Цвет текста активной (выбранной) кнопки качества и цвет для остальных.
    [SerializeField] private Color _activeButtonColor  = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color _defaultButtonColor = Color.white;

    [Header("Графика")]
    [SerializeField] private TMP_Dropdown _resolutionDropdown;
    [SerializeField] private Toggle       _vSyncToggle;

    [Header("Язык")]
    [SerializeField] private TMP_Dropdown _languageDropdown;

    [Header("Диалог подтверждения")]
    [SerializeField] private ConfirmDialog _confirmDialog;

    // ─── Pending-состояние ───────────────────────────────────────────────────
    // «Черновик» изменений. В AppSettings записывается только через Apply(),
    // что позволяет свободно менять значения в UI, не затрагивая сохранённые настройки.

    private float _pendingMasterVol;
    private float _pendingMusicVol;
    private float _pendingAmbientVol;
    private float _pendingSfxVol;

    // Индекс языка в pending-состоянии: обновляется при изменении dropdown.
    // Сравнивается с _committedLanguageIndex, чтобы понять нужен ли откат при Discard().
    private int _pendingLanguageIndex;

    // Индекс языка, зафиксированный последним Apply() или инициализацией.
    // Discard() возвращает язык именно к этому значению, а не к AppSettings.LanguageIndex,
    // потому что AppSettings обновляется только внутри Apply() — до него там лежит
    // предыдущее committed-значение, а не текущий pending.
    private int _committedLanguageIndex;

    private int  _pendingQualityLevel;
    private bool _hasUnsavedChanges;

    // ─── Внутреннее состояние ─────────────────────────────────────────────────

    // Список уникальных разрешений (без дублей по refresh rate) для заполнения dropdown.
    private Resolution[] _uniqueResolutions;

    // Init() идемпотентен — повторный вызов при возврате из сцены безопасен.
    private bool _isInitialized;

    // Ссылки на TMP_Text внутри кнопок качества — нужны для смены цвета активной кнопки.
    private TMP_Text _lowQualityLabel;
    private TMP_Text _medQualityLabel;
    private TMP_Text _highQualityLabel;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void OnEnable()
    {
        // При повторном включении объекта язык мог измениться извне —
        // синхронизируем dropdown чтобы UI не показывал устаревшее значение.
        if (_isInitialized) SyncLanguageDropdown();
        MessageBus<LanguageChangedMessage>.Subscribe(OnLanguageChanged);
    }

    private void OnDisable()
    {
        MessageBus<LanguageChangedMessage>.Unsubscribe(OnLanguageChanged);
    }

    private void OnDestroy()
    {
        // Снимаем все runtime-листенеры, чтобы избежать утечек памяти при перезагрузке сцены.
        _languageDropdown?.onValueChanged.RemoveAllListeners();
        _resolutionDropdown?.onValueChanged.RemoveAllListeners();
        _vSyncToggle?.onValueChanged.RemoveAllListeners();
        _masterVolSlider?.onValueChanged.RemoveAllListeners();
        _musicVolSlider?.onValueChanged.RemoveAllListeners();
        _ambientVolSlider?.onValueChanged.RemoveAllListeners();
        _sfxVolSlider?.onValueChanged.RemoveAllListeners();
        _lowQualityBtn?.onClick.RemoveAllListeners();
        _medQualityBtn?.onClick.RemoveAllListeners();
        _highQualityBtn?.onClick.RemoveAllListeners();
    }

    // ─── Публичный API ────────────────────────────────────────────────────────

    /// <summary>
    /// Инициализирует все секции настроек. Идемпотентен — повторный вызов игнорируется.
    /// Вызывается из MainMenuController.Start() и PauseController.Start().
    /// </summary>
    public void Init()
    {
        if (_isInitialized) return;

        InitAudioSection();
        InitGraphicsSection();
        InitLanguageSection();
        _isInitialized = true;
    }

    /// <summary>Открывает подменю видео, скрывая остальные.</summary>
    public void OpenVideoMenu()    => ShowPanel(_videoPanel);

    /// <summary>Открывает подменю аудио, скрывая остальные.</summary>
    public void OpenAudioMenu()    => ShowPanel(_audioPanel);

    /// <summary>Открывает подменю геймплея, скрывая остальные.</summary>
    public void OpenGameplayMenu() => ShowPanel(_gameplayPanel);

    /// <summary>
    /// Возвращает в главное меню настроек. При наличии несохранённых изменений
    /// показывает ConfirmDialog, иначе переходит напрямую.
    /// Вызывается кнопкой Back в подменю аудио, видео и геймплея.
    /// </summary>
    public void OpenSettingsMainMenu()
    {
        if (_hasUnsavedChanges && _confirmDialog != null)
        {
            _confirmDialog.Show(
                onConfirm: () =>
                {
                    Apply();
                    // Apply() уже обновил _committedLanguageIndex внутри себя,
                    // но синхронизируем явно на случай задержки в SetLanguageByIndex.
                    _committedLanguageIndex = AppSettings.LanguageIndex;
                    ShowPanel(_settingsMainPanel);
                },
                onDiscard: () =>
                {
                    Discard();
                    // После отката синхронизируем committed с фактическим состоянием AppSettings,
                    // чтобы следующий ConfirmDialog стартовал с корректного базиса.
                    _committedLanguageIndex = AppSettings.LanguageIndex;
                    ShowPanel(_settingsMainPanel);
                }
            );
        }
        else
        {
            // _confirmDialog не назначен или нет несохранённых изменений:
            // откатываем pending-изменения и переходим напрямую.
            if (_hasUnsavedChanges)
            {
                Debug.LogWarning("[SettingsUIController] ConfirmDialog не назначен — несохранённые изменения отброшены.", this);
                Discard();
                _committedLanguageIndex = AppSettings.LanguageIndex;
            }
            ShowPanel(_settingsMainPanel);
        }
    }

    /// <summary>
    /// Записывает все накопленные pending-изменения в AppSettings и применяет их.
    /// Вызывается кнопкой Apply в подменю или через колбэк onConfirm в ConfirmDialog.
    /// Ничего не делает если несохранённых изменений нет.
    /// </summary>
    public void Apply()
    {
        if (!_hasUnsavedChanges) return;

        AppSettings.GeneralVolume    = _pendingMasterVol;
        AppSettings.BackgroundVolume = _pendingMusicVol;
        AppSettings.AmbientVolume    = _pendingAmbientVol;
        AppSettings.SfxVolume        = _pendingSfxVol;
        AppSettings.QualityLevel     = _pendingQualityLevel;

        // Уведомляем AudioSystem об изменении громкости через MessageBus —
        // это исключает прямую зависимость между SettingsUIController и AudioSystem.
        MessageBus<AudioVolumeChangedMessage>.Dispatch(new AudioVolumeChangedMessage(
            master:  _pendingMasterVol,
            music:   _pendingMusicVol,
            ambient: _pendingAmbientVol,
            sfx:     _pendingSfxVol
        ));

        // Язык применяем только при реальном изменении — SetLanguageByIndex дорогая операция,
        // которая сбрасывает кеш локализации и рассылает LanguageChangedMessage всем подписчикам.
        if (_pendingLanguageIndex != AppSettings.LanguageIndex)
            LocalizationManager.Instance?.SetLanguageByIndex(_pendingLanguageIndex);

        QualitySettings.SetQualityLevel(_pendingQualityLevel, applyExpensiveChanges: true);

        // Фиксируем применённый язык. Discard() будет откатывать именно к этому значению,
        // а не к исходному из Init() — иначе повторный Apply → Discard откатывался бы слишком далеко.
        _committedLanguageIndex = AppSettings.LanguageIndex;
        _hasUnsavedChanges = false;

        // Гарантированно сбрасываем буфер PlayerPrefs на диск.
        // SetFloat/SetInt буферизуют данные — без явного Save() крэш теряет настройки.
        // Если язык менялся, PlayerPrefs.Save() уже вызван внутри AppSettings.LanguageIndex setter,
        // но для изменений только громкости/качества явный вызов обязателен.
        PlayerPrefs.Save();
#if UNITY_EDITOR
        Debug.Log($"[Settings] Apply() done — committedLang now={_committedLanguageIndex}");
#endif
    }

    /// <summary>
    /// Откатывает все pending-изменения к последним сохранённым значениям в AppSettings.
    /// Если язык в pending отличается от committed — возвращает его через LocalizationManager.
    /// </summary>
    public void Discard()
    {
        // Сравниваем _pendingLanguageIndex с _committedLanguageIndex, а не с AppSettings.LanguageIndex.
        // Если Apply() не вызывался, AppSettings.LanguageIndex == _committedLanguageIndex,
        // и старое условие (AppSettings != committed) было бы ложным — откат не происходил.
        if (_pendingLanguageIndex != _committedLanguageIndex)
            LocalizationManager.Instance?.SetLanguageByIndex(_committedLanguageIndex);

        LoadPendingFromSaved();
        SyncSlidersToState();
        SyncLanguageDropdown();
        UpdateQualityButtonsVisual(_pendingQualityLevel);
        _hasUnsavedChanges = false;
        DispatchPendingVolume();
    }

    /// <summary>
    /// Синхронизирует dropdown языка с текущим AppSettings без вызова onValueChanged.
    /// Используется при смене языка извне (LocalizationManager) и при Discard().
    /// </summary>
    public void SyncLanguageDropdown()
    {
        if (_languageDropdown == null) return;
        // SetValueWithoutNotify не вызывает onValueChanged — pending-состояние
        // не помечается грязным во время синхронизации.
        _languageDropdown.SetValueWithoutNotify(AppSettings.LanguageIndex);
        _languageDropdown.RefreshShownValue();
    }

    // ─── Инициализация секций ─────────────────────────────────────────────────

    /// <summary>Инициализирует слайдеры громкости и подключает к ним pending-листенеры.</summary>
    private void InitAudioSection()
    {
        LoadPendingFromSaved();
        SyncSlidersToState();

        AddSliderListener(_masterVolSlider,  v => { _pendingMasterVol  = v; MarkDirty(); DispatchPendingVolume(); });
        AddSliderListener(_musicVolSlider,   v => { _pendingMusicVol   = v; MarkDirty(); DispatchPendingVolume(); });
        AddSliderListener(_ambientVolSlider, v => { _pendingAmbientVol = v; MarkDirty(); DispatchPendingVolume(); });
        AddSliderListener(_sfxVolSlider,     v => { _pendingSfxVol     = v; MarkDirty(); DispatchPendingVolume(); });
    }

    /// <summary>
    /// Инициализирует кнопки качества, VSync и dropdown разрешений.
    /// Уровень качества применяется сразу — он должен соответствовать сохранённому с первого кадра.
    /// </summary>
    private void InitGraphicsSection()
    {
        _lowQualityLabel  = _lowQualityBtn?.GetComponentInChildren<TMP_Text>();
        _medQualityLabel  = _medQualityBtn?.GetComponentInChildren<TMP_Text>();
        _highQualityLabel = _highQualityBtn?.GetComponentInChildren<TMP_Text>();

        AddQualityButton(_lowQualityBtn,  0);
        AddQualityButton(_medQualityBtn,  1);
        AddQualityButton(_highQualityBtn, 2);

        _pendingQualityLevel = AppSettings.QualityLevel;
        QualitySettings.SetQualityLevel(_pendingQualityLevel, applyExpensiveChanges: true);
        UpdateQualityButtonsVisual(_pendingQualityLevel);

        InitVSyncToggle();
        if (_resolutionDropdown != null) InitResolutionDropdown();
    }

    /// <summary>
    /// Заполняет dropdown языков и инициализирует оба индекса (_pendingLanguageIndex
    /// и _committedLanguageIndex) из текущего AppSettings.
    /// </summary>
    private void InitLanguageSection()
    {
        if (_languageDropdown == null) return;

        List<string> names = LocalizationManager.GetAvailableLanguageNames();
        if (names.Count == 0)
        {
            Debug.LogWarning("[SettingsUIController] InitLanguageSection: LocalizationManager не инициализирован или availableLanguages пуст. Dropdown языков не заполнен.");
            return;
        }

        _languageDropdown.ClearOptions();
        _languageDropdown.AddOptions(names);
        SyncLanguageDropdown();

        // Оба индекса инициализируются одинаково — нет pending-изменений при старте.
        _pendingLanguageIndex   = AppSettings.LanguageIndex;
        _committedLanguageIndex = AppSettings.LanguageIndex;

        // Listener только обновляет pending и помечает изменение грязным.
        // Реальная смена языка происходит только внутри Apply().
        _languageDropdown.onValueChanged.AddListener(idx =>
        {
            _pendingLanguageIndex = idx;
            MarkDirty();
        });
    }

    // ─── Обработчики ──────────────────────────────────────────────────────────

    private void OnLanguageChanged(LanguageChangedMessage _) => SyncLanguageDropdown();

    /// <summary>Устанавливает pending-уровень качества и обновляет визуал кнопок.</summary>
    private void SetQuality(int level)
    {
        _pendingQualityLevel = level;
        UpdateQualityButtonsVisual(level);
        MarkDirty();
    }

    /// <summary>Обновляет визуальное состояние всех трёх кнопок качества.</summary>
    private void UpdateQualityButtonsVisual(int activeLevel)
    {
        UpdateQualityBtnVisual(_lowQualityBtn,  _lowQualityLabel,  activeLevel, 0);
        UpdateQualityBtnVisual(_medQualityBtn,  _medQualityLabel,  activeLevel, 1);
        UpdateQualityBtnVisual(_highQualityBtn, _highQualityLabel, activeLevel, 2);
    }

    /// <summary>
    /// Делает активную кнопку качества неинтерактивной и окрашивает её текст.
    /// Неактивные кнопки остаются кликабельными с дефолтным цветом.
    /// </summary>
    private void UpdateQualityBtnVisual(Button btn, TMP_Text label, int activeLevel, int target)
    {
        if (btn == null) return;
        bool isActive    = activeLevel == target;
        // Активная кнопка некликабельна — нельзя выбрать уже выбранный уровень.
        btn.interactable = !isActive;
        if (label != null)
            label.color  = isActive ? _activeButtonColor : _defaultButtonColor;
    }

    /// <summary>
    /// Инициализирует Toggle VSync. VSync применяется немедленно, минуя pending-систему,
    /// поэтому Discard() его не откатывает.
    /// </summary>
    private void InitVSyncToggle()
    {
        if (_vSyncToggle == null) return;
        _vSyncToggle.isOn = QualitySettings.vSyncCount > 0;
        _vSyncToggle.onValueChanged.AddListener(isOn =>
            QualitySettings.vSyncCount = isOn ? 1 : 0
        );
    }

    /// <summary>Инициализирует dropdown разрешений уникальными значениями.</summary>
    private void InitResolutionDropdown()
    {
        CollectUniqueResolutions(out var options);
        _resolutionDropdown.ClearOptions();
        _resolutionDropdown.AddOptions(options);
        _resolutionDropdown.value = GetSavedResolutionIndex();
        _resolutionDropdown.RefreshShownValue();
        _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    /// <summary>
    /// Собирает уникальные разрешения экрана, исключая дубли по refresh rate.
    /// </summary>
    /// <param name="options">Список строк "ширина×высота" для заполнения dropdown.</param>
    private void CollectUniqueResolutions(out List<string> options)
    {
        var seen   = new HashSet<long>();
        var unique = new List<Resolution>();
        options    = new List<string>();

        foreach (Resolution r in Screen.resolutions)
        {
            // Unity возвращает одно разрешение несколько раз — по одной записи на каждый
            // поддерживаемый refresh rate. Упаковываем width+height в одно long для дедупликации.
            long key = ((long)r.width << 32) | (uint)r.height;
            if (!seen.Add(key)) continue;
            unique.Add(r);
            options.Add($"{r.width}×{r.height}");
        }
        _uniqueResolutions = unique.ToArray();
    }

    /// <summary>
    /// Возвращает сохранённый индекс разрешения если он в допустимом диапазоне,
    /// иначе возвращает последний индекс (обычно максимальное разрешение).
    /// </summary>
    private int GetSavedResolutionIndex()
    {
        int saved = AppSettings.ResolutionIndex;
        return saved >= 0 && saved < _uniqueResolutions.Length
            ? saved
            : _uniqueResolutions.Length - 1;
    }

    /// <summary>Немедленно применяет выбранное разрешение и сохраняет индекс в AppSettings.</summary>
    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= _uniqueResolutions.Length) return;
        Resolution res = _uniqueResolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        AppSettings.ResolutionIndex = index;
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    /// <summary>
    /// Скрывает все панели настроек и показывает только target.
    /// Используется для навигации между подменю.
    /// </summary>
    private void ShowPanel(UIPanel target)
    {
        _settingsMainPanel?.Hide();
        _videoPanel?.Hide();
        _audioPanel?.Hide();
        _gameplayPanel?.Hide();
        target?.Show();
    }

    /// <summary>Загружает все pending-поля из текущих значений AppSettings.</summary>
    private void LoadPendingFromSaved()
    {
        _pendingMasterVol     = AppSettings.GeneralVolume;
        _pendingMusicVol      = AppSettings.BackgroundVolume;
        _pendingAmbientVol    = AppSettings.AmbientVolume;
        _pendingSfxVol        = AppSettings.SfxVolume;
        _pendingQualityLevel  = AppSettings.QualityLevel;
        _pendingLanguageIndex = AppSettings.LanguageIndex;
    }

    /// <summary>
    /// Синхронизирует слайдеры с текущими pending-значениями без вызова onValueChanged.
    /// Вызывается после LoadPendingFromSaved(), чтобы UI отразил откат к сохранённым значениям.
    /// </summary>
    private void SyncSlidersToState()
    {
        SetSliderWithoutNotify(_masterVolSlider,  _pendingMasterVol);
        SetSliderWithoutNotify(_musicVolSlider,   _pendingMusicVol);
        SetSliderWithoutNotify(_ambientVolSlider, _pendingAmbientVol);
        SetSliderWithoutNotify(_sfxVolSlider,     _pendingSfxVol);
    }

    /// <summary>Устанавливает значение слайдера без уведомления listener'ов.</summary>
    private static void SetSliderWithoutNotify(Slider slider, float value)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
    }

    /// <summary>Безопасно подключает listener к слайдеру, проверяя на null.</summary>
    private static void AddSliderListener(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null) return;
        slider.onValueChanged.AddListener(action);
    }

    /// <summary>Подключает кнопку качества к SetQuality с указанным уровнем.</summary>
    private void AddQualityButton(Button btn, int level)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() => SetQuality(level));
    }

    /// <summary>Помечает настройки как изменённые — при уходе из подменю появится ConfirmDialog.</summary>
    private void MarkDirty() => _hasUnsavedChanges = true;

    private void DispatchPendingVolume()
    {
        MessageBus<AudioVolumeChangedMessage>.Dispatch(new AudioVolumeChangedMessage(
            master:  _pendingMasterVol,
            music:   _pendingMusicVol,
            ambient: _pendingAmbientVol,
            sfx:     _pendingSfxVol
        ));
    }
}
