using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Процедурный эффект TMP-заголовка: волновой дрейф букв, температурный цвет и редкий глитч.
/// Все анимации используют unscaled time — работают при timeScale=0 (меню паузы, загрузка).
/// ForceMeshUpdate() каждый кадр — штатный паттерн TMP для vertex-эффектов.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class AncestraTitleEffect : MonoBehaviour
{
    [Header("Vertical Drift")]
    [SerializeField] float driftAmplitude = 3.5f;
    [SerializeField] float driftSpeed     = 0.4f;
    [SerializeField] float driftSpread    = 1.8f;

    [Header("Color Shift")]
    [SerializeField] Color coldColor = new Color(0.75f, 0.78f, 0.82f); // холодный — буква вверху
    [SerializeField] Color warmColor = new Color(0.92f, 0.86f, 0.74f); // тёплый — буква внизу
    [SerializeField] float colorIntensity = 0.6f; // 0 = нет смены, 1 = полная

    [Header("Rare Glitch")]
    [SerializeField] float glitchMinInterval = 4.0f;  // не чаще раза в 4 сек
    [SerializeField] float glitchMaxInterval = 12.0f; // может ждать и 12 сек
    [SerializeField] float glitchStrength    = 18.0f; // насколько сильно смещается
    [SerializeField] float glitchDuration    = 0.07f; // как долго держится

    TextMeshProUGUI _tmp;
    int   _glitchCharIndex = -1;
    float _glitchOffsetY   =  0f;

    void Awake() {
        _tmp = GetComponent<TextMeshProUGUI>();
        StartCoroutine(GlitchLoop());
    }

    IEnumerator GlitchLoop() {
        while (true) {
            // WaitForSecondsRealtime: глитч работает при timeScale=0 (пауза, загрузка)
            yield return new WaitForSecondsRealtime(Random.Range(glitchMinInterval, glitchMaxInterval));

            // Выбираем случайную видимую букву
            _tmp.ForceMeshUpdate();
            int count = _tmp.textInfo.characterCount;
            if (count == 0) continue;

            _glitchCharIndex = Random.Range(0, count);
            _glitchOffsetY   = Random.value > 0.5f ? glitchStrength : -glitchStrength;

            // WaitForSecondsRealtime: при timeScale=0 WaitForSeconds зависает и _glitchCharIndex
            // остаётся не сброшенным — буква застрянет смещённой до снятия паузы.
            yield return new WaitForSecondsRealtime(glitchDuration);
            _glitchCharIndex = -1;
            _glitchOffsetY   = 0f;
        }
    }

    void Update() {
        _tmp.ForceMeshUpdate();

        TMP_TextInfo info = _tmp.textInfo;
        int charCount = info.characterCount;
        if (charCount == 0) return;

        for (int i = 0; i < charCount; i++) {
            TMP_CharacterInfo charInfo = info.characterInfo[i];
            if (!charInfo.isVisible) continue;

            float phase  = i * driftSpread;
            // Time.unscaledTime: анимация работает при timeScale=0 (пауза, загрузка)
            float sine   = Mathf.Sin(Time.unscaledTime * driftSpeed + phase); // [-1..1]
            float offset = sine * driftAmplitude;

            // Глич — поверх обычного дрожания
            if (i == _glitchCharIndex)
                offset += _glitchOffsetY;

            int meshIndex   = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            // — Вертикальный сдвиг —
            Vector3[] verts = info.meshInfo[meshIndex].vertices;
            for (int v = 0; v < 4; v++)
                verts[vertexIndex + v].y += offset;

            // — Цвет: тёплый когда буква внизу, холодный когда вверху —
            float t = (sine + 1f) * 0.5f; // [0..1], 0 = внизу, 1 = вверху
            Color targetColor = Color.Lerp(warmColor, coldColor, t);
            Color baseColor   = _tmp.color;
            Color finalColor  = Color.Lerp(baseColor, targetColor, colorIntensity);
            Color32 c32       = finalColor;

            Color32[] colors = info.meshInfo[meshIndex].colors32;
            for (int v = 0; v < 4; v++)
                colors[vertexIndex + v] = c32;
        }

        for (int i = 0; i < info.meshInfo.Length; i++) {
            info.meshInfo[i].mesh.vertices = info.meshInfo[i].vertices;
            info.meshInfo[i].mesh.colors32 = info.meshInfo[i].colors32;
            _tmp.UpdateGeometry(info.meshInfo[i].mesh, i);
        }
    }
}