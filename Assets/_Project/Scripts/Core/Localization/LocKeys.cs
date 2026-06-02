// Централизованный реестр всех ключей локализации.
// Никаких строк-ключей в других файлах — только ссылки на эти константы.
// Чтобы добавить новый ключ: объяви константу здесь + добавь запись в каждый LocalizationData SO.

/// <summary>
/// Все ключи локализации проекта. Иерархия (UI.Menu, HUD, Notify, Common) отражает UI-структуру.
/// Используй эти константы вместо строк напрямую — переименование не сломает ни один вызов.
/// </summary>
public static class LocKeys
{
    public static class UI
    {
        public static class Menu
        {
            public const string Play       = "ui.menu.play";
            public const string Continue   = "ui.menu.continue";
            public const string Settings   = "ui.menu.settings";
            public const string Quit       = "ui.menu.quit";
            public const string NewGame    = "ui.menu.new_game";
            public const string LoadGame   = "ui.menu.load_game";
            // Шаблон "v{0}". Добавь запись в каждый LocalizationData ScriptableObject со значением "v{0}".
            public const string Version    = "ui.menu.version";
        }

        public static class Settings
        {
            public const string Title      = "ui.settings.title";
            public const string Language   = "ui.settings.language";
            public const string Volume     = "ui.settings.volume";
            public const string Quality    = "ui.settings.quality";
            public const string Apply      = "ui.settings.apply";
            public const string Back       = "ui.settings.back";
        }

        public static class Save
        {
            public const string SlotEmpty  = "ui.save.slot_empty";
            public const string SaveGame   = "ui.save.save";
            public const string Load       = "ui.save.load";
            public const string Delete     = "ui.save.delete";
            public const string Saving     = "ui.save.saving";
            public const string Saved      = "ui.save.saved";
        }

        public static class Pause
        {
            public const string Title      = "ui.pause.title";
            public const string Resume     = "ui.pause.resume";
            public const string MainMenu   = "ui.pause.main_menu";
        }
    }

    public static class HUD
    {
        public const string Health        = "hud.health";
        public const string Ammo          = "hud.ammo";
        public const string Interact      = "hud.interact";

        public static class Item
        {
            // Plural key: используй LocalizationManager.GetPlural(LocKeys.HUD.Item.Picked, count).
            // Требует записей hud.item.picked.one, hud.item.picked.few, hud.item.picked.many в каждом LocalizationData.
            public const string Picked = "hud.item.picked";
        }
    }

    public static class Notify
    {
        public const string SaveSuccess   = "notify.save_success";
        public const string SaveFailed    = "notify.save_failed";
        public const string LoadFailed    = "notify.load_failed";
        public const string AutoSaved     = "notify.auto_saved";
    }

    public static class Common
    {
        public const string Yes           = "common.yes";
        public const string No            = "common.no";
        public const string Confirm       = "common.confirm";
        public const string Cancel        = "common.cancel";
        public const string Close         = "common.close";
        public const string Ok            = "common.ok";
    }
}
