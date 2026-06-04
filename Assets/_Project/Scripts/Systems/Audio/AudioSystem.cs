// Центральная система аудио. Управляет музыкой через стек треков (MusicChannel),
// ambient-слоями (AmbientLayer) и пулом SFX (SfxPool). Маршрутизирует события
// через MessageBus, применяет аудиоконфиг сцены из SceneMusicConfig.
// Сохраняется между сценами через PersistentSingleton.

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Фасад аудиосистемы. Создаёт подсистемы при старте, подписывается на MessageBus-сообщения
/// и применяет аудиоконфиг сцен автоматически по событиям SceneManager.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioSystem : PersistentSingleton<AudioSystem>
{
    [Header("Mixer")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("Конфиг сцен")]
    [SerializeField] private SceneMusicConfig sceneConfig;

    [Header("SFX пул")]
    [SerializeField] private int sfxPoolInitialSize = 8;
    [SerializeField] private int sfxPoolMaxSize     = 32;

    [Header("Дебаг")]
    [SerializeField] private bool verboseLogging;

    private MusicChannel     _music;
    private AmbientLayer     _ambient;
    private SfxPool          _sfxPool;
    private AudioMixerRouter _mixer;

    // Имя последней non-additive сцены — нужно чтобы при переходе остановить её ambient.
    private string _previousNonAdditiveName;

    // Имя сцены, для которой музыка уже запущена через ScreenObscuredMessage.
    // OnSceneLoaded проверяет это поле, чтобы не запустить музыку повторно.
    private string _transitionHandledScene;

    // true = у целевой сцены тот же трек что и сейчас — fade-out не нужен.
    private bool _isSameMusicTransition;

    public MusicChannel     Music   => _music;
    public AmbientLayer     Ambient => _ambient;
    public AudioMixerRouter Mixer   => _mixer;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        _mixer = new AudioMixerRouter(mainMixer);

        var sourcesRoot = new GameObject("AudioSources").transform;
        sourcesRoot.SetParent(transform);

        var musicA = CreateMusicSource("Music_A", sourcesRoot);
        var musicB = CreateMusicSource("Music_B", sourcesRoot);
        _music = new MusicChannel(this, musicA, musicB, verboseLogging);

        _ambient = new AmbientLayer(this, sourcesRoot, verboseLogging);

        var sfxRoot = new GameObject("SFX").transform;
        sfxRoot.SetParent(sourcesRoot);
        _sfxPool = new SfxPool(this, sfxRoot, sfxPoolInitialSize, sfxPoolMaxSize, verboseLogging);

        // Подписываемся в Awake (до любого Start), чтобы не пропустить AudioVolumeChangedMessage
        // из SettingsBootstrapper.Start() — порядок Start() между скриптами не гарантирован.
        // SceneManager-события тоже здесь: иначе пропустим sceneLoaded первой сцены.
        MessageBus<TransitionStartedMessage>.Subscribe(OnTransitionStarted);
        MessageBus<ScreenObscuredMessage>.Subscribe(OnScreenObscured);

        MessageBus<PlayMusicMessage>.Subscribe(OnPlayMusic);
        MessageBus<PopMusicMessage>.Subscribe(OnPopMusic);
        MessageBus<FadeMusicOutMessage>.Subscribe(OnFadeMusicOut);
        MessageBus<StopMusicMessage>.Subscribe(OnStopMusic);

        MessageBus<PlayAmbientMessage>.Subscribe(OnPlayAmbient);
        MessageBus<StopAmbientMessage>.Subscribe(OnStopAmbient);
        MessageBus<StopAllAmbientMessage>.Subscribe(OnStopAllAmbient);

        MessageBus<PlaySfxAtPointMessage>.Subscribe(OnPlaySfxAtPoint);

        MessageBus<AudioVolumeChangedMessage>.Subscribe(OnVolumeChanged);
        MessageBus<GamePausedAudioMessage>.Subscribe(OnGamePaused);
        MessageBus<GameResumedAudioMessage>.Subscribe(OnGameResumed);

        SceneManager.sceneLoaded   += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void Start()
    {
        if (Instance != this) return;

        // Подписки перенесены в Awake. Здесь только применяем аудиоконфиг начальной сцены:
        // sceneLoaded для неё уже сработал до Start(), поэтому вызываем вручную.
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        Log("AudioSystem инициализирован.");
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        MessageBus<TransitionStartedMessage>.Unsubscribe(OnTransitionStarted);
        MessageBus<ScreenObscuredMessage>.Unsubscribe(OnScreenObscured);

        MessageBus<PlayMusicMessage>.Unsubscribe(OnPlayMusic);
        MessageBus<PopMusicMessage>.Unsubscribe(OnPopMusic);
        MessageBus<FadeMusicOutMessage>.Unsubscribe(OnFadeMusicOut);
        MessageBus<StopMusicMessage>.Unsubscribe(OnStopMusic);

        MessageBus<PlayAmbientMessage>.Unsubscribe(OnPlayAmbient);
        MessageBus<StopAmbientMessage>.Unsubscribe(OnStopAmbient);
        MessageBus<StopAllAmbientMessage>.Unsubscribe(OnStopAllAmbient);

        MessageBus<PlaySfxAtPointMessage>.Unsubscribe(OnPlaySfxAtPoint);

        MessageBus<AudioVolumeChangedMessage>.Unsubscribe(OnVolumeChanged);
        MessageBus<GamePausedAudioMessage>.Unsubscribe(OnGamePaused);
        MessageBus<GameResumedAudioMessage>.Unsubscribe(OnGameResumed);

        SceneManager.sceneLoaded   -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnPlayMusic(PlayMusicMessage msg)
    {
        if (msg.Push)
            _music.Push(msg.Data, msg.FadeDuration);
        else
            _music.Replace(msg.Data, msg.FadeDuration);
    }

    private void OnPopMusic(PopMusicMessage msg)
    {
        // Пропускаем Pop если ForData задан, но сейчас играет другой трек.
        if (msg.ForData != null && _music.CurrentData != msg.ForData) return;
        _music.Pop(msg.FadeDuration);
    }

    private void OnFadeMusicOut(FadeMusicOutMessage msg)  => _music.FadeOut(msg.Duration);
    private void OnStopMusic(StopMusicMessage _)          => _music.Stop();

    private void OnPlayAmbient(PlayAmbientMessage msg)
        => _ambient.Play(msg.Data, msg.LayerId, msg.FadeDuration);

    private void OnStopAmbient(StopAmbientMessage msg)
        => _ambient.Stop(msg.LayerId, msg.FadeDuration);

    private void OnStopAllAmbient(StopAllAmbientMessage msg)
        => _ambient.StopAll(msg.FadeDuration);

    private void OnPlaySfxAtPoint(PlaySfxAtPointMessage msg)
        => _sfxPool.PlayAtPoint(msg.Data, msg.Position);

    private void OnVolumeChanged(AudioVolumeChangedMessage msg)
    {
        _mixer.SetMasterVolume(msg.MasterVolume);
        _mixer.SetMusicVolume(msg.MusicVolume);
        _mixer.SetAmbientVolume(msg.AmbientVolume);
        _mixer.SetSfxVolume(msg.SfxVolume);
        Log($"Громкость: master={msg.MasterVolume:F2} music={msg.MusicVolume:F2} " +
            $"ambient={msg.AmbientVolume:F2} sfx={msg.SfxVolume:F2}");
    }

    private void OnGamePaused(GamePausedAudioMessage msg)
    {
        _music.Mute(msg.FadeOutDuration);
        _ambient.MuteAll(msg.FadeOutDuration);
        Log("Аудио приглушено (пауза).");
    }

    private void OnGameResumed(GameResumedAudioMessage msg)
    {
        _music.Unmute(msg.FadeInDuration);
        _ambient.UnmuteAll(msg.FadeInDuration);
        Log("Аудио восстановлено (снятие паузы).");
    }

    // ─── Обработчики перехода сцены ──────────────────────────────────────────

    // Экран начинает затемняться: запускаем fade-out музыки и ambient.
    // Для одинакового трека fade-out пропускается — непрерывное воспроизведение.
    private void OnTransitionStarted(TransitionStartedMessage msg)
    {
        if (sceneConfig == null) return;

        var entry = sceneConfig.GetEntry(msg.SceneName);
        _isSameMusicTransition = entry?.music != null && entry.music == _music.CurrentData;

        if (!_isSameMusicTransition)
            _music.FadeOut(msg.FadeOutDuration);

        _ambient.StopAll(msg.FadeOutDuration);

        Log($"Переход → '{msg.SceneName}': " +
            $"музыка {(_isSameMusicTransition ? "продолжает играть" : $"FadeOut {msg.FadeOutDuration:F1}с")}, " +
            $"ambient StopAll {msg.FadeOutDuration:F1}с.");
    }

    // Экран полностью чёрный: запускаем новую музыку пока сцена грузится.
    // Ambient не трогаем — его запустит OnSceneLoaded когда сцена будет готова.
    private void OnScreenObscured(ScreenObscuredMessage msg)
    {
        if (sceneConfig == null) return;

        var entry = sceneConfig.GetEntry(msg.SceneName);

        if (entry == null)
        {
            // Сцены нет в конфиге: применяем unmapped-поведение пока экран чёрный.
            if (!_isSameMusicTransition)
                HandleUnmappedScene(msg.SceneName);
            _transitionHandledScene = msg.SceneName;
            return;
        }

        if (entry.music != null && !_isSameMusicTransition)
        {
            PreloadEntryClips(entry);
            float fadeIn = ResolveOverride(entry.overrideFadeIn, entry.music.fadeInDuration);
            StartCoroutine(PlayMusicOnlyAsync(entry.music, fadeIn));
        }

        _transitionHandledScene = msg.SceneName;
        Log($"Экран чёрный → '{msg.SceneName}': " +
            (_isSameMusicTransition ? "музыка продолжает играть." : "запуск новой музыки."));
    }

    // Запускает Replace только для музыки, без ambient (ambient стартует в OnSceneLoaded).
    private IEnumerator PlayMusicOnlyAsync(AudioData music, float fadeIn)
    {
        if (music.clip != null && music.clip.loadState == AudioDataLoadState.Unloaded)
            music.clip.LoadAudioData();

        if (music.clip != null)
        {
            float elapsed = 0f;
            while (music.clip.loadState == AudioDataLoadState.Loading && elapsed < 5f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        _music.Replace(music, fadeIn);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (sceneConfig == null) return;

        if (mode == LoadSceneMode.Additive)
        {
            var entry = sceneConfig.GetEntry(scene.name);
            if (entry != null && entry.music != null && entry.pushMode)
            {
                float fadeIn = ResolveOverride(entry.overrideFadeIn, entry.music.fadeInDuration);
                StartCoroutine(PlaySceneEntryAsync(entry, push: true, fadeIn: fadeIn));
                Log($"Аддитивная сцена '{scene.name}': Push музыки.");
            }
            return;
        }

        // Non-additive: определяем, была ли музыка уже запущена через ScreenObscuredMessage.
        bool musicHandled = scene.name == _transitionHandledScene;
        _transitionHandledScene = null;
        _isSameMusicTransition  = false;

        // Останавливаем ambient предыдущей сцены (страховка: STM-путь уже сделал StopAll,
        // но прямая загрузка без STM этого не делала).
        if (!string.IsNullOrEmpty(_previousNonAdditiveName))
        {
            var prevEntry = sceneConfig.GetEntry(_previousNonAdditiveName);
            if (prevEntry != null) StopAmbientForScene(prevEntry);
        }

        _previousNonAdditiveName = scene.name;

        var newEntry = sceneConfig.GetEntry(scene.name);
        if (newEntry != null)
        {
            if (musicHandled)
            {
                // Музыка уже нарастает с момента ScreenObscured — запускаем только ambient.
                StartAmbientForScene(newEntry);
            }
            else if (newEntry.music != null)
            {
                // Fallback: STM не было (прямая загрузка), запускаем музыку и ambient вместе.
                float fadeIn = ResolveOverride(newEntry.overrideFadeIn, newEntry.music.fadeInDuration);
                StartCoroutine(PlaySceneEntryAsync(newEntry, push: false, fadeIn: fadeIn));
            }
            else
            {
                StartAmbientForScene(newEntry);
            }
            return;
        }

        if (!musicHandled)
            HandleUnmappedScene(scene.name);
    }

    private IEnumerator PlaySceneEntryAsync(SceneMusicConfig.SceneEntry entry, bool push, float fadeIn)
    {
        PreloadEntryClips(entry);

        // Wait for music clip to finish loading before playing (max 5 s unscaled).
        if (entry.music?.clip != null)
        {
            float elapsed = 0f;
            while (entry.music.clip.loadState == AudioDataLoadState.Loading && elapsed < 5f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (push)
            _music.Push(entry.music, fadeIn);
        else
            _music.Replace(entry.music, fadeIn);

        StartAmbientForScene(entry);
    }

    private void PreloadEntryClips(SceneMusicConfig.SceneEntry entry)
    {
        if (entry.music?.clip != null && entry.music.clip.loadState == AudioDataLoadState.Unloaded)
            entry.music.clip.LoadAudioData();

        foreach (var amb in entry.ambientLayers)
            if (amb.data?.clip != null && amb.data.clip.loadState == AudioDataLoadState.Unloaded)
                amb.data.clip.LoadAudioData();
    }

    // При выгрузке аддитивной сцены убираем её музыку и ambient.
    // Без этого запушенный трек остаётся на стеке бесконечно.
    private void OnSceneUnloaded(Scene scene)
    {
        if (sceneConfig == null) return;

        var entry = sceneConfig.GetEntry(scene.name);
        if (entry == null) return;

        if (entry.pushMode && entry.music != null)
        {
            float fadeOut = ResolveOverride(entry.overrideFadeOut, entry.music.fadeOutDuration);
            _music.Pop(fadeOut);
            Log($"Аддитивная сцена '{scene.name}' выгружена: Pop музыки.");
        }

        StopAmbientForScene(entry);
    }

    private void StartAmbientForScene(SceneMusicConfig.SceneEntry entry)
    {
        foreach (var amb in entry.ambientLayers)
            if (amb.data != null)
                _ambient.Play(amb.data, amb.layerId, amb.fadeInDuration, ownerScene: entry.sceneName);
    }

    private void StopAmbientForScene(SceneMusicConfig.SceneEntry entry)
    {
        foreach (var amb in entry.ambientLayers)
            _ambient.Stop(amb.layerId);
    }

    private void HandleUnmappedScene(string sceneName)
    {
        if (sceneConfig == null) return;
        switch (sceneConfig.unmappedBehavior)
        {
            case SceneMusicConfig.UnmappedBehavior.Continue:
                Log($"Сцена '{sceneName}' не в конфиге — трек продолжает играть.");
                break;
            case SceneMusicConfig.UnmappedBehavior.FadeOut:
                _music.FadeOut(sceneConfig.unmappedFadeOutDuration);
                _ambient.StopAll(sceneConfig.unmappedFadeOutDuration);
                Log($"Сцена '{sceneName}' — fade out.");
                break;
            case SceneMusicConfig.UnmappedBehavior.StopImmediate:
                _music.Stop();
                _ambient.StopAll(0f);
                Log($"Сцена '{sceneName}' — стоп.");
                break;
        }
    }

    // Прямой вызов только для SFX. Музыку и ambient всегда меняй через MessageBus —
    // это единственный путь и он не обходит никакую логику.
    public void PlaySfx(AudioData data)                       => _sfxPool.Play(data);
    public void PlaySfxClip(AudioClip clip, float volume = 1f) => _sfxPool.PlayClip(clip, volume);

    public void SetMasterVolume(float v)  => _mixer.SetMasterVolume(v);
    public void SetMusicVolume(float v)   => _mixer.SetMusicVolume(v);
    public void SetSfxVolume(float v)     => _mixer.SetSfxVolume(v);

    private AudioSource CreateMusicSource(string goName, Transform parent)
    {
        var go     = new GameObject(goName);
        go.transform.SetParent(parent);
        var src    = go.AddComponent<AudioSource>();
        src.loop   = true;
        src.volume = 0f;
        return src;
    }

    // -1 означает «взять из AudioData», любое неотрицательное — явное override в конфиге сцены.
    private static float ResolveOverride(float overrideVal, float fromData)
        => overrideVal >= 0f ? overrideVal : fromData;

    private void Log(string msg)
    {
        if (verboseLogging) Debug.Log($"[AudioSystem] {msg}");
    }
}
