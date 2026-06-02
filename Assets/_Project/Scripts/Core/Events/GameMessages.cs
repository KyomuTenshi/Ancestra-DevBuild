// Игровые сообщения общего назначения: локализация, сохранения, состояние игрока и переходы.
// Все типы — readonly struct: нет heap-аллокаций при диспатче.

using UnityEngine;

/// <summary>Язык изменён. Все LocalizedLabel и LocalizationCallbackReceiver обновляют текст.</summary>
public readonly struct LanguageChangedMessage : IMessage
{
    public readonly int LanguageIndex;
    public LanguageChangedMessage(int index) => LanguageIndex = index;
}

/// <summary>Игра сохранена. UI показывает иконку или уведомление.</summary>
public readonly struct GameSavedMessage : IMessage
{
    public readonly int  SlotIndex;
    public readonly bool Success;

    public GameSavedMessage(int slot, bool success)
    {
        SlotIndex = slot;
        Success   = success;
    }
}

/// <summary>Слот загружен. Системы могут обновить состояние из SaveManager.ActiveProgress.</summary>
public readonly struct GameLoadedMessage : IMessage
{
    public readonly int SlotIndex;
    public GameLoadedMessage(int slot) => SlotIndex = slot;
}

/// <summary>Игрок погиб.</summary>
public readonly struct PlayerDiedMessage : IMessage
{
    public readonly Vector3 Position;
    public PlayerDiedMessage(Vector3 pos) => Position = pos;
}

/// <summary>
/// Запрошен переход в сцену. SaveSlotIndex = -1 означает не сохранять перед переходом.
/// </summary>
public readonly struct SceneTransitionRequestedMessage : IMessage
{
    public readonly string TargetScene;
    public readonly int    SaveSlotIndex;

    public SceneTransitionRequestedMessage(string scene, int saveSlot = -1)
    {
        TargetScene   = scene;
        SaveSlotIndex = saveSlot;
    }
}
