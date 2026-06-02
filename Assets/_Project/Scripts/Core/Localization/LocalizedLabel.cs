// MonoBehaviour для отображения локализованного текста на TMP_Text или Text.
// Автоматически перерисовывается при смене языка через MessageBus<LanguageChangedMessage>.
// Поддерживает форматирование ({0}, {1}), plural forms и произвольный текст через SetRaw.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Привязывает ключ из LocKeys к текстовому компоненту (TMP_Text или legacy Text).
/// SetRaw() позволяет вывести динамические данные (HP, счёт) без локализации и без аллокаций.
/// </summary>
[AddComponentMenu("Localization/Localized Label")]
public class LocalizedLabel : MonoBehaviour
{
    [Tooltip("Ключ локализации из LocKeys, например: LocKeys.UI.Menu.Play")]
    [SerializeField] private string _key;

    [Tooltip("True если строка содержит {0}, {1} и т.д. для форматирования.")]
    [SerializeField] private bool _hasFormat;

    // Компоненты ищутся автоматически в Awake — нет нужды в Inspector-ссылках.
    private TMP_Text  _tmpText;
    private Text      _uiText;

    private object[] _formatArgs;

    private void Awake()
    {
        _tmpText = GetComponent<TMP_Text>();
        if (_tmpText == null)
            _uiText = GetComponent<Text>();

        if (_tmpText == null && _uiText == null)
            Debug.LogWarning($"[LocalizedLabel] '{name}': нет TMP_Text или Text. Добавь компонент.", this);
    }

    private void OnEnable()
    {
        MessageBus<LanguageChangedMessage>.Subscribe(OnLanguageChanged);
        // Если Instance ещё null (наш Awake не успел до чужого OnEnable),
        // текст придёт через LanguageChangedMessage из LocalizationManager.Start().
        // Без этой проверки — Warning-спам и временный артефакт "<ключ>" в UI.
        if (LocalizationManager.Instance != null)
            UpdateText();
    }

    private void OnDisable()
    {
        MessageBus<LanguageChangedMessage>.Unsubscribe(OnLanguageChanged);
    }

    /// <summary>Меняет ключ и немедленно обновляет текст.</summary>
    public void SetKey(string key)
    {
        _key = key;
        UpdateText();
    }

    /// <summary>Устанавливает аргументы форматирования. Для строк вида "Игрок {0}: {1} очков".</summary>
    public void SetArgs(params object[] args)
    {
        _formatArgs  = args;
        _hasFormat   = args != null && args.Length > 0;
        UpdateText();
    }

    public void SetKeyAndArgs(string key, params object[] args)
    {
        _key        = key;
        _formatArgs = args;
        _hasFormat  = args != null && args.Length > 0;
        UpdateText();
    }

    /// <summary>
    /// Устанавливает один типизированный аргумент.
    /// Повторно использует существующий массив чтобы избежать GC-аллокации при частых вызовах (HP, счёт, таймер).
    /// </summary>
    public void SetArg<T>(T arg)
    {
        if (_formatArgs == null || _formatArgs.Length != 1)
            _formatArgs = new object[1];
        _formatArgs[0] = arg;
        _hasFormat     = true;
        UpdateText();
    }

    /// <summary>
    /// Устанавливает произвольный текст в обход локализации.
    /// Используй для меток, обновляющихся каждый кадр (HP, счёт, таймер) — нулевые аллокации.
    /// </summary>
    public void SetRaw(string text)
    {
        _key = null;
        ApplyText(text);
    }

    private void OnLanguageChanged(LanguageChangedMessage _) => UpdateText();

    private void UpdateText()
    {
        if (string.IsNullOrEmpty(_key)) return;

        string text = _hasFormat && _formatArgs != null
            ? LocalizationManager.Get(_key, _formatArgs)
            : LocalizationManager.Get(_key);

        ApplyText(text);
    }

    private void ApplyText(string text)
    {
        if (_tmpText != null)
        {
            _tmpText.text = text;
            return;
        }

        if (_uiText != null)
        {
            _uiText.text = text;
        }
    }
}
