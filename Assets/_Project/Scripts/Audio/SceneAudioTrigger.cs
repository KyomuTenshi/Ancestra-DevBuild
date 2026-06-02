// MonoBehaviour-триггер аудио для конкретной сцены.
// Альтернатива SceneMusicConfig.SceneEntry — удобна для прototипов и отдельных зон
// внутри большой сцены, когда глобальный конфиг избыточен.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Запускает музыку и ambient-слои при Start(), останавливает при OnDisable().
/// TriggerMode.Push добавляет трек в стек — удобно для cutscene и диалоговых сцен.
/// </summary>
public class SceneAudioTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        [Tooltip("Заменяет текущую музыку (стандартная смена сцены)")]
        Replace,
        [Tooltip("Добавляет в стек. При OnDisable — Pop (аддитивная сцена, кат-сцена)")]
        Push,
    }

    [Header("Музыка")]
    [Tooltip("Трек для этой сцены. Null = не менять.")]
    [SerializeField] private AudioData _music;

    [Tooltip("Replace: полная замена. Push: добавить в стек, Pop при выходе.")]
    [SerializeField] private TriggerMode _mode = TriggerMode.Replace;

    [Tooltip("Fade In. -1 = из AudioData.")]
    [Min(-1f)]
    [SerializeField] private float _fadeIn = -1f;

    [Tooltip("Fade Out при Push→Pop. -1 = из AudioData.")]
    [Min(-1f)]
    [SerializeField] private float _fadeOut = -1f;

    [Header("Ambient слои")]
    [Tooltip("Список ambient-слоёв для этой сцены. Запускаются при Start, останавливаются при OnDisable.")]
    [SerializeField] private List<AmbientEntry> _ambientLayers = new List<AmbientEntry>();

    [System.Serializable]
    public class AmbientEntry
    {
        public AudioData data;
        [Tooltip("ID слоя: 'wind', 'water', 'horror'. Должен быть уникален в рамках сцены.")]
        public string layerId = "default";
        [Min(0f)]
        public float fadeIn = 1f;
        [Min(0f)]
        public float fadeOut = 1f;
    }

    [Header("Настройки")]
    [Tooltip("Запустить при Start (иначе вызови Play() вручную).")]
    [SerializeField] private bool _playOnStart = true;

    private bool _isPlaying;

    private void Start()
    {
        if (_playOnStart) Play();
    }

    private void OnDisable()
    {
        if (_isPlaying) Stop();
    }

    /// <summary>
    /// Запускает музыку и ambient этой сцены. Повторный вызов игнорируется.
    /// </summary>
    public void Play()
    {
        if (_isPlaying) return;
        _isPlaying = true;

        if (_music != null)
        {
            bool doPush = _mode == TriggerMode.Push;
            float fadeIn = _fadeIn >= 0f ? _fadeIn : _music.fadeInDuration;

            MessageBus<PlayMusicMessage>.Dispatch(
                new PlayMusicMessage(_music, fadeDuration: fadeIn, push: doPush)
            );
        }

        foreach (var entry in _ambientLayers)
        {
            if (entry.data == null) continue;

            MessageBus<PlayAmbientMessage>.Dispatch(
                new PlayAmbientMessage(entry.data, entry.layerId, entry.fadeIn)
            );
        }
    }

    /// <summary>
    /// Останавливает музыку и ambient этой сцены.
    /// В режиме Push — делает Pop из стека, возвращая предыдущий трек.
    /// В режиме Replace — музыку не трогает, следующая сцена заменит её сама.
    /// </summary>
    public void Stop()
    {
        if (!_isPlaying) return;
        _isPlaying = false;

        if (_music != null)
        {
            if (_mode == TriggerMode.Push)
            {
                float fadeOut = _fadeOut >= 0f ? _fadeOut : _music.fadeOutDuration;
                MessageBus<PopMusicMessage>.Dispatch(new PopMusicMessage(fadeOut, _music));
            }
        }

        foreach (var entry in _ambientLayers)
        {
            MessageBus<StopAmbientMessage>.Dispatch(
                new StopAmbientMessage(entry.layerId, entry.fadeOut)
            );
        }
    }
}
