using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.Narrative
{
    /// <summary>
    /// Контроллер пролога. Показывает кадры один за другим.
    /// Тап/клик — перейти к следующему кадру.
    ///
    /// Fade между сценами делегирован SceneLoader.
    /// Fade между кадрами делегирован UIPanel (_framePanel).
    /// Эффекты текста (Perlin-цвет, jitter, глиф-коррупция) — на AncestraGlyphEffect рядом с contentLabel.
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        // ─── Данные ───────────────────────────────────────────────────────────

        [Header("Данные")]
        [Tooltip("ScriptableObject с текстами и настройками пролога.")]
        [SerializeField] private CutsceneData cutsceneData;

        // ─── UI ───────────────────────────────────────────────────────────────

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI contentLabel;

        [Tooltip("UIPanel с контентом кадра — fade между кадрами через Show/Hide.")]
        [SerializeField] private UIPanel framePanel;

        [Tooltip("Невидимая кнопка на весь экран для перехода по тапу.")]
        [SerializeField] private Button screenButton;

        // ─── Аудио ────────────────────────────────────────────────────────────

        [Header("Аудио")]
        [SerializeField] private AudioSource voiceSource;

        // ─── Переходы ─────────────────────────────────────────────────────────

        [Header("Переходы")]
        [SerializeField, Min(0f)] private float startDelay = 0.5f;

        [Tooltip("Длительность fade при выходе в следующую сцену.")]
        [SerializeField, Min(0.1f)] private float exitFadeDuration = 0.8f;

        [Tooltip("Имя следующей сцены. Если пусто — берётся из CutsceneData.nextSceneName.")]
        [SerializeField] private string overrideNextScene;

        // ─── Состояние ────────────────────────────────────────────────────────

        private int  _frameIndex;
        private bool _isTransitioning;
        private bool _isFinishing;

        // ─── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            framePanel?.HideImmediate();

            if (screenButton != null)
                screenButton.onClick.AddListener(OnScreenTapped);
        }

        private void Start()
        {
            if (cutsceneData == null || !cutsceneData.HasFrames)
            {
                Debug.LogError("[CutsceneController] CutsceneData пуст или не назначен.");
                return;
            }

            if (contentLabel == null)
            {
                Debug.LogError("[CutsceneController] ContentLabel не назначен.");
                return;
            }

            StartCoroutine(RunPrologue());
        }

        private void OnDestroy()
        {
            if (screenButton != null)
                screenButton.onClick.RemoveListener(OnScreenTapped);
        }

        // ─── Главная последовательность ───────────────────────────────────────

        private IEnumerator RunPrologue()
        {
            _isTransitioning = true;

            if (startDelay > 0f)
                yield return new WaitForSecondsRealtime(startDelay);

            _isTransitioning = false;
            ShowFrame(0);
        }

        // ─── Управление кадрами ───────────────────────────────────────────────

        private void ShowFrame(int index)
        {
            _frameIndex = index;
            CutsceneFrame frame = cutsceneData.Frames[index];

            if (voiceSource != null)
            {
                voiceSource.Stop();
                if (frame.voice != null)
                    voiceSource.PlayOneShot(frame.voice);
            }

            contentLabel.text = frame.text ?? string.Empty;
            framePanel?.Show();
        }

        private void NextFrame()
        {
            int next = _frameIndex + 1;

            if (next >= cutsceneData.Frames.Count)
            {
                StartCoroutine(FinishPrologue());
                return;
            }

            StartCoroutine(TransitionToFrame(next));
        }

        private IEnumerator TransitionToFrame(int nextIndex)
        {
            _isTransitioning = true;

            framePanel?.Hide();
            yield return new WaitUntil(() => framePanel == null || !framePanel.IsAnimating);

            contentLabel.text = string.Empty;

            _isTransitioning = false;
            ShowFrame(nextIndex);
        }

        private IEnumerator FinishPrologue()
        {
            if (_isFinishing) yield break;
            _isFinishing     = true;
            _isTransitioning = true;

            framePanel?.Hide();
            yield return new WaitUntil(() => framePanel == null || !framePanel.IsAnimating);

            string scene = !string.IsNullOrWhiteSpace(overrideNextScene)
                ? overrideNextScene
                : cutsceneData.nextSceneName;

            if (string.IsNullOrWhiteSpace(scene))
                scene = "00_Level";

            SceneLoader.Load(scene, exitFadeDuration);
        }

        // ─── Ввод ─────────────────────────────────────────────────────────────

        private void OnScreenTapped()
        {
            if (_isFinishing || _isTransitioning) return;
            NextFrame();
        }
    }
}