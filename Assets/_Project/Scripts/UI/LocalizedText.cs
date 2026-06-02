// MonoBehaviour для отображения локализованного текста. Поддерживает TMP_Text и legacy Text,
// plural forms (SetCount) и форматирование (SetArgs). Ищет текстовый компонент на себе
// и в дочерних объектах — работает с кнопками, у которых Text-чайлд.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Привязывает ключ из LocKeys к текстовому компоненту (TMP_Text или legacy Text).
/// Автоматически обновляется при смене языка через MessageBus&lt;LanguageChangedMessage&gt;.
/// debugRenameInEditor переименовывает GameObject по ключу для удобной отладки в иерархии.
/// </summary>
[AddComponentMenu("Localization/Localized Text")]
public class LocalizedText : MonoBehaviour
{
    // ─── Инспектор ────────────────────────────────────────────────────────────

    [Tooltip("Ключ локализации. Используй константы из LocKeys, например: LocKeys.UI.Menu.Play")]
    [SerializeField] private string localizationKey;

    [Tooltip("Plural mode: если true — использует GetPlural(key, _count) вместо Get(key)")]
    [SerializeField] private bool pluralMode;

    [Tooltip("В Editor переименовывает GameObject по ключу — удобно для отладки в иерархии")]
    [SerializeField] private bool debugRenameInEditor;

    // ─── Внутреннее состояние ─────────────────────────────────────────────────

    private object[] _formatArgs;
    private int      _count;

    // Поддерживаем TMP_Text и legacy Text без define symbols.
    private TMP_Text _tmpText;
    private Text     _uiText;
    private bool     _isTextFound;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Ищем сначала на себе, затем в дочерних объектах (кнопки с Text-чайлдом).
        _isTextFound = TryGetComponent(out _tmpText);
        if (!_isTextFound)
        {
            _tmpText = GetComponentInChildren<TMP_Text>();
            _isTextFound = _tmpText != null;
        }

        if (!_isTextFound)
        {
            _isTextFound = TryGetComponent(out _uiText);
            if (!_isTextFound)
            {
                _uiText = GetComponentInChildren<Text>();
                _isTextFound = _uiText != null;
            }
        }

        if (!_isTextFound)
        {
            Debug.LogWarning(
                $"[LocalizedText] TMP_Text или Text не найден на '{gameObject.name}'. " +
                "Компонент отключён.", this
            );
            enabled = false;
        }
    }

    private void OnEnable()
    {
        MessageBus<LanguageChangedMessage>.Subscribe(OnLanguageChanged);
        // Если менеджер ещё не инициализирован (порядок Awake), текст придёт через
        // LanguageChangedMessage из LocalizationManager.Start() — UpdateText() пропускаем.
        if (LocalizationManager.Instance != null)
            UpdateText();
    }

    private void OnDisable()
    {
        MessageBus<LanguageChangedMessage>.Unsubscribe(OnLanguageChanged);
    }

    // ─── Публичный API ────────────────────────────────────────────────────────

    /// <summary>Задаёт ключ и обновляет текст. Совместим с UITextLocalizer.SetKey().</summary>
    public void SetKey(string key)
    {
        localizationKey = key;
        UpdateText();
    }

    /// <summary>Форматирующие аргументы: SetArgs(name, score) → "Игрок {0}: {1} очков".</summary>
    public void SetArgs(params object[] args)
    {
        _formatArgs = args;
        UpdateText();
    }

    /// <summary>Ключ + аргументы за один вызов.</summary>
    public void SetKeyWithArgs(string key, params object[] args)
    {
        localizationKey = key;
        _formatArgs     = args;
        UpdateText();
    }

    /// <summary>
    /// Plural mode: SetCount(5) → GetPlural(key, 5).
    /// Для строк "У вас {0} монет" с формами .one / .few / .many в LocalizationData.
    /// </summary>
    public void SetCount(int count)
    {
        _count      = count;
        pluralMode  = true;
        UpdateText();
    }

    /// <summary>Обновляет текст вручную. Аналог UITextLocalizer.RefreshText().</summary>
    public void UpdateText()
    {
        if (!_isTextFound || string.IsNullOrEmpty(localizationKey)) return;

        string result;

        if (pluralMode)
        {
            result = LocalizationManager.GetPlural(localizationKey, _count);
        }
        else if (_formatArgs is { Length: > 0 })
        {
            result = LocalizationManager.Get(localizationKey, _formatArgs);
        }
        else
        {
            result = LocalizationManager.Get(localizationKey);
        }

        ApplyText(result);

#if UNITY_EDITOR
        if (debugRenameInEditor && Application.isPlaying)
            gameObject.name = $"[Loc] {localizationKey}";
#endif
    }

    public string Key => localizationKey;

    // ─── Внутренние методы ────────────────────────────────────────────────────

    private void OnLanguageChanged(LanguageChangedMessage _) => UpdateText();

    private void ApplyText(string text)
    {
        if (_tmpText != null) { _tmpText.text = text; return; }
        if (_uiText  != null) { _uiText.text  = text; }
    }
}
