using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Процедурный эффект кнопки главного меню: prefix на hover, цветовой шум через Perlin,
/// jitter позиции и редкая глиф-коррупция текста. Использует unscaled time — работает при timeScale=0.
/// Реагирует на смену языка через LanguageChangedMessage: обновляет кешированный _originalText.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class AncestraMenuButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] TextMeshProUGUI _label;

    [Header("Prefix")]
    [SerializeField] string prefix = "— ";
    [SerializeField] float prefixDelay = 0.06f;

    [Header("Jitter")]
    [SerializeField] float jitterStrength = 0.8f;
    [SerializeField] float jitterInterval = 0.14f;
    [SerializeField] float jitterChance   = 0.4f;

    [Header("Color Noise")]
    [SerializeField] Color baseColor  = new Color(0.40f, 0.38f, 0.34f);
    [SerializeField] Color hoverColor = new Color(0.88f, 0.84f, 0.72f);
    [SerializeField] float noiseFreq  = 3.5f;

    [Header("Glyph Corruption")]
    [SerializeField] bool   enableCorruption  = true;
    [SerializeField] float  corruptionChance  = 0.08f;
    [SerializeField] string corruptGlyphs     = "—·†_";

    RectTransform _rect;
    string        _originalText;
    Vector2       _basePos;
    bool          _isHovered;
    float         _jitterTimer;
    Coroutine     _corruptCoroutine;

    void Awake()
    {
        _rect    = GetComponent<RectTransform>();
        _basePos = _rect.anchoredPosition;
        if (_label != null) _label.color = baseColor;
    }

    void OnEnable()
    {
        MessageBus<LanguageChangedMessage>.Subscribe(OnLanguageChanged);
    }

    void OnDisable()
    {
        MessageBus<LanguageChangedMessage>.Unsubscribe(OnLanguageChanged);
        // Сбрасываем состояние hover при отключении объекта
        if (_isHovered)
        {
            _isHovered = false;
            if (_label != null) _label.text = _originalText;
        }
    }

    // Захватываем текст после того как LocalizedText.OnEnable() обновил его
    void Start()
    {
        if (_label != null)
            _originalText = _label.text;
    }

    // Обновляем _originalText при смене языка — иначе после смены языка
    // hover показывал бы строку предыдущего языка как «оригинальный» текст
    private void OnLanguageChanged(LanguageChangedMessage _)
    {
        if (_label == null) return;
        if (_isHovered)
        {
            // Во время hover текст содержит prefix — сохраняем обновлённый оригинал,
            // но не меняем видимый текст (он обновится при следующем OnPointerExit)
            string current = _label.text;
            bool hasPrefix = current.StartsWith(prefix);
            _originalText = hasPrefix ? current.Substring(prefix.Length) : current;
        }
        else
        {
            _originalText = _label.text;
        }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        // Захватываем актуальный текст при наведении — локализация к этому моменту уже применена
        _originalText = _label != null ? _label.text : _originalText;
        _isHovered    = true;
        StartCoroutine(ShowPrefixDelayed());
        if (enableCorruption)
            _corruptCoroutine = StartCoroutine(CorruptionLoop());
    }

    public void OnPointerExit(PointerEventData e)
    {
        _isHovered = false;
        if (_label != null)
        {
            _label.text  = _originalText;
            _label.color = baseColor;
        }
        _rect.anchoredPosition = _basePos;
        if (_corruptCoroutine != null)
        {
            StopCoroutine(_corruptCoroutine);
            _corruptCoroutine = null;
        }
    }

    public void OnPointerClick(PointerEventData e)
    {
        StartCoroutine(ClickJolt());
    }

    IEnumerator ShowPrefixDelayed()
    {
        yield return new WaitForSecondsRealtime(prefixDelay);
        if (_isHovered && _label != null)
            _label.text = prefix + _originalText;
    }

    IEnumerator CorruptionLoop()
    {
        while (_isHovered)
        {
            yield return new WaitForSecondsRealtime(Random.Range(1.2f, 3.0f));
            if (!_isHovered || Random.value > corruptionChance || _label == null) continue;

            string current  = _label.text;
            if (current.Length == 0) continue;

            int    idx      = Random.Range(0, current.Length);
            char   glitch   = corruptGlyphs[Random.Range(0, corruptGlyphs.Length)];
            char   original = current[idx];

            _label.text = current.Substring(0, idx) + glitch + current.Substring(idx + 1);
            yield return new WaitForSecondsRealtime(Random.Range(0.06f, 0.18f));

            if (_isHovered && _label != null)
                _label.text = current.Substring(0, idx) + original + current.Substring(idx + 1);
        }
    }

    IEnumerator ClickJolt()
    {
        _rect.anchoredPosition = _basePos + new Vector2(Random.Range(-2f, 2f), 0);
        yield return new WaitForSecondsRealtime(0.05f);
        _rect.anchoredPosition = _basePos;
    }

    void Update()
    {
        if (!_isHovered || _label == null) return;

        // Time.unscaledTime: анимация работает при timeScale=0 (меню паузы, загрузка)
        float n = Mathf.PerlinNoise(Time.unscaledTime * noiseFreq, 42f);
        float t = Mathf.Clamp01(n * 1.2f - 0.1f);
        _label.color = Color.Lerp(baseColor, hoverColor, t);

        _jitterTimer -= Time.unscaledDeltaTime;
        if (_jitterTimer <= 0f)
        {
            _jitterTimer = jitterInterval;
            if (Random.value < jitterChance)
                _rect.anchoredPosition = _basePos + (Vector2)(Random.insideUnitCircle * jitterStrength);
            else
                _rect.anchoredPosition = _basePos;
        }
    }
}
