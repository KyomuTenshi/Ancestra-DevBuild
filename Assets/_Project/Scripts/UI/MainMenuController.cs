// Контроллер главного меню. Управляет переключением панелей (меню ↔ настройки),
// кнопками New Game, Continue и Exit. Кнопка Continue активна только при наличии сохранения —
// состояние обновляется автоматически через MessageBus<GameSavedMessage/GameLoadedMessage>.

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Главное меню: переключает между панелью меню и панелью настроек,
/// запускает новую игру или продолжает с сохранённой сцены.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // ─── Инспектор ────────────────────────────────────────────────────────────

    [Header("Панели")]
    [SerializeField] private UIPanel _mainMenuPanel;
    [SerializeField] private UIPanel _settingsPanel;

    [Header("Кнопки")]
    [SerializeField] private Button _continueButton;

    [Header("Локализованные тексты")]
    [Tooltip("Текст версии. Ключ: LocKeys.UI.Menu.Version, аргумент: {0} = Application.version")]
    [SerializeField] private LocalizedText _versionLabel;

    [Header("Зависимости")]
    [SerializeField] private SettingsUIController _settingsUIController;

    [Header("Сцены")]
    [SerializeField] private string _prologueScene = "Prologue";

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Кнопка Continue должна реагировать на сохранение/загрузку в реальном времени,
        // поэтому подписываемся при каждой активации, а не только в Start().
        MessageBus<GameSavedMessage>.Subscribe(OnSaveStateChanged);
        MessageBus<GameLoadedMessage>.Subscribe(OnSaveStateChanged);
    }

    private void OnDisable()
    {
        MessageBus<GameSavedMessage>.Unsubscribe(OnSaveStateChanged);
        MessageBus<GameLoadedMessage>.Unsubscribe(OnSaveStateChanged);
    }

    private void Start()
    {
        // SetKeyWithArgs подставляет Application.version в шаблон "v{0}".
        // LocalizedText автоматически обновляет текст при смене языка через OnEnable.
        _versionLabel?.SetKeyWithArgs(LocKeys.UI.Menu.Version, Application.version);

        _settingsUIController?.Init();
        RefreshContinueButton();
        ShowMainMenu();
    }

    // ─── Публичный API (кнопки через Inspector) ───────────────────────────────

    /// <summary>
    /// Скрывает панель меню и открывает панель настроек.
    /// OpenSettingsMainMenu() отображает главное подменю настроек.
    /// </summary>
    public void OpenSettings()
    {
        _mainMenuPanel?.Hide();
        _settingsPanel?.Show();
        _settingsUIController?.OpenSettingsMainMenu();
    }

    /// <summary>
    /// Возвращает из настроек в главное меню. Вызывает Discard() перед скрытием панели,
    /// чтобы явно откатить несохранённые pending-изменения — без этого они остались бы
    /// в _pending-полях и применились бы при следующем открытии настроек.
    /// </summary>
    public void BackToMainMenu()
    {
        _settingsUIController?.Discard();
        _settingsPanel?.Hide();
        _mainMenuPanel?.Show();
    }

    /// <summary>
    /// Стартует новую игру: удаляет слот 0 (сбрасывает прогресс) и загружает сцену пролога.
    /// Удаление выполняется до загрузки, чтобы гарантировать чистый старт.
    /// </summary>
    public void OnClickNewGame()
    {
        if (SceneLoader.IsTransitioning) return;
        SaveManager.Instance?.DeleteSlot(0);
        SceneLoader.Load(_prologueScene);
    }

    /// <summary>
    /// Продолжает игру с последней сохранённой сцены. Если слот 0 ещё не загружен
    /// в память — загружает его перед переходом.
    /// </summary>
    public void OnClickContinue()
    {
        if (SceneLoader.IsTransitioning) return;
        if (SaveManager.Instance == null) return;

        // Слот может быть не загружен если кнопка нажата впервые после запуска —
        // в этом случае нужно подгрузить прогресс перед чтением savedSceneName.
        if (!SaveManager.Instance.HasActiveSave)
            SaveManager.Instance.LoadSlot(0);

        string savedScene = SaveManager.Instance.ActiveProgress?.savedSceneName;
        if (!string.IsNullOrEmpty(savedScene))
            SceneLoader.Load(savedScene);
    }

    /// <summary>Выходит из приложения. В редакторе останавливает Play Mode.</summary>
    public void OnClickExit()
    {
        if (SceneLoader.IsTransitioning) return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─── Приватные методы ─────────────────────────────────────────────────────

    private void OnSaveStateChanged<T>(T _) where T : IMessage => RefreshContinueButton();

    /// <summary>Обновляет интерактивность кнопки Continue в зависимости от наличия сохранения.</summary>
    private void RefreshContinueButton()
    {
        if (_continueButton == null) return;
        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.SlotExists(0);
        _continueButton.interactable = hasSave;
    }

    /// <summary>Скрывает панель настроек и показывает панель главного меню.</summary>
    private void ShowMainMenu()
    {
        _settingsPanel?.Hide();
        _mainMenuPanel?.Show();
    }
}
