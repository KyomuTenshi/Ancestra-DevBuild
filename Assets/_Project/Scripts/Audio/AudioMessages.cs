// MessageBus-сообщения аудиосистемы: музыка, ambient, SFX, громкость, пауза.
// Все типы — readonly struct: нет heap-аллокаций при диспатче.
// AudioSystem подписывается на все эти типы в Start() и отписывается в OnDestroy().

using UnityEngine;

/// <summary>
/// Запрос на воспроизведение музыкального трека.
/// Push=true добавляет в стек поверх текущего — можно вернуться назад через PopMusicMessage.
/// Push=false заменяет весь стек — для смены сцены.
/// </summary>
public readonly struct PlayMusicMessage : IMessage
{
    public readonly AudioData Data;
    public readonly float     FadeDuration;
    public readonly bool      Push;

    public PlayMusicMessage(AudioData data, float fadeDuration = -1f, bool push = false)
    {
        Data         = data;
        FadeDuration = fadeDuration;
        Push         = push;
    }
}

/// <summary>
/// Убирает верхний трек из стека, возвращая предыдущий с сохранённой позицией воспроизведения.
/// ForData: pop только если именно этот трек сейчас активен — защита от случайного Pop чужого трека.
/// </summary>
public readonly struct PopMusicMessage : IMessage
{
    public readonly float FadeDuration;
    public readonly AudioData ForData;

    public PopMusicMessage(float fadeDuration = 1f, AudioData forData = null)
    {
        FadeDuration = fadeDuration;
        ForData      = forData;
    }
}

/// <summary>Плавно затихнуть и остановиться.</summary>
public readonly struct FadeMusicOutMessage : IMessage
{
    public readonly float Duration;
    public FadeMusicOutMessage(float duration = 1.5f) => Duration = duration;
}

/// <summary>Немедленная остановка без фейда.</summary>
public readonly struct StopMusicMessage : IMessage { }

/// <summary>
/// Воспроизводит ambient-слой. LayerId позволяет иметь несколько слоёв одновременно.
/// Если слой с таким ID уже играет — кроссфейдит в новый клип.
/// </summary>
public readonly struct PlayAmbientMessage : IMessage
{
    public readonly AudioData Data;
    public readonly string    LayerId;
    public readonly float     FadeDuration;

    public PlayAmbientMessage(AudioData data, string layerId = "default", float fadeDuration = 1f)
    {
        Data         = data;
        LayerId      = layerId;
        FadeDuration = fadeDuration;
    }
}

/// <summary>Останавливает конкретный ambient-слой.</summary>
public readonly struct StopAmbientMessage : IMessage
{
    public readonly string LayerId;
    public readonly float  FadeDuration;

    public StopAmbientMessage(string layerId = "default", float fadeDuration = 1f)
    {
        LayerId      = layerId;
        FadeDuration = fadeDuration;
    }
}

/// <summary>Останавливает все ambient-слои.</summary>
public readonly struct StopAllAmbientMessage : IMessage
{
    public readonly float FadeDuration;
    public StopAllAmbientMessage(float fadeDuration = 1f) => FadeDuration = fadeDuration;
}

/// <summary>Воспроизводит SFX в точке мира (3D). SfxPool выбирает свободный источник из пула.</summary>
public readonly struct PlaySfxAtPointMessage : IMessage
{
    public readonly AudioData Data;
    public readonly Vector3   Position;

    public PlaySfxAtPointMessage(AudioData data, Vector3 position)
    {
        Data     = data;
        Position = position;
    }
}

/// <summary>
/// Изменение громкости. Диспатчится из SettingsBootstrapper при старте
/// и из UI настроек при изменении ползунков.
/// </summary>
public readonly struct AudioVolumeChangedMessage : IMessage
{
    public readonly float MasterVolume;
    public readonly float MusicVolume;
    public readonly float AmbientVolume;
    public readonly float SfxVolume;

    public AudioVolumeChangedMessage(float master, float music = 1f, float ambient = 1f, float sfx = 1f)
    {
        MasterVolume  = master;
        MusicVolume   = music;
        AmbientVolume = ambient;
        SfxVolume     = sfx;
    }
}

/// <summary>Игра на паузе. AudioSystem приглушает музыку и ambient.</summary>
public readonly struct GamePausedAudioMessage : IMessage
{
    public readonly float FadeOutDuration;
    public GamePausedAudioMessage(float fade = 0.3f) => FadeOutDuration = fade;
}

/// <summary>Пауза снята. AudioSystem восстанавливает громкость.</summary>
public readonly struct GameResumedAudioMessage : IMessage
{
    public readonly float FadeInDuration;
    public GameResumedAudioMessage(float fade = 0.3f) => FadeInDuration = fade;
}
