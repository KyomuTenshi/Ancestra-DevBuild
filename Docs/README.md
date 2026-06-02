# Ancestra — Game Framework

![Unity](https://img.shields.io/badge/Unity-2022.3%20LTS-black?logo=unity)
![Platform](https://img.shields.io/badge/Platform-PC%20%7C%20Mobile-blue)
![License](https://img.shields.io/badge/License-Proprietary-red)
![Language](https://img.shields.io/badge/Language-C%23-239120?logo=csharp)

Производственный Unity-фреймворк для одиночной игры с атмосферным главным меню и нарративным геймплеем. Включает полные подсистемы аудио, локализации, сохранений и переходов между сценами — связанные через типобезопасный MessageBus без прямых зависимостей между системами.

---

## Содержание

- [Фичи](#фичи)
- [Архитектура](#архитектура)
- [Требования](#требования)
- [Установка и запуск](#установка-и-запуск)
- [Структура проекта](#структура-проекта)
- [Описание подсистем](#описание-подсистем)
- [Паттерны и соглашения](#паттерны-и-соглашения)

---

## Фичи

### Аудиосистема
- Кроссфейд музыки через два `AudioSource` (A/B swap) с поддержкой стека треков — `Push`/`Pop` возвращает предыдущий трек с сохранённой позицией воспроизведения
- Именованные ambient-слои с кроссфейдом при смене клипа; до 12 одновременных слоёв
- Object pool для SFX с автоматическим расширением до `maxSize`; при переполнении прерывает самый старый источник
- Маппинг «имя сцены → аудиоконфиг» через `SceneMusicConfig` ScriptableObject — без кода
- Рандомизация pitch/volume через `SoundVariance`; при `Min == Max` пропускает `Random.Range`
- Конвертация линейной громкости [0,1] → дБ для `AudioMixer` с кешом значений

### Локализация
- Поддержка множественного числа (one/few/many/zero) для русского, украинского, польского и fallback для остальных языков
- Fallback-язык: отсутствующие ключи берутся из резервного источника с логом
- Кеш разрешённых строк сбрасывается при смене языка
- `LocalizableString` — сериализуемая структура с двумя режимами (`RawText` / `LocalizedKey`); неявное приведение к `string`
- `LocalizedText` и `LocalizedLabel` — MonoBehaviour-компоненты для TMP и legacy Text с поддержкой форматирования и plural
- Горячая перезагрузка языковых данных в Editor через `OnValidate`

### Система сохранений
- Атомарная запись: `write → .tmp → rename`, восстановление оборванных `.tmp` при старте
- Асинхронное сохранение: сериализация JSON на main thread, I/O в `Task.Run`
- Контрольная сумма данных для обнаружения ручного изменения файлов
- Миграция версий формата через цепочку `IMigrationStep` — добавление нового шага без изменения остального кода
- `SaveableComponent<TData>` — абстрактный базовый класс; подкласс реализует только `Capture()` и `Restore()`
- `SaveableRegistry` — реестр без `FindObjectOfType`; компоненты регистрируются сами через `OnEnable`/`OnDisable`
- Поддержка legacy-формата файлов через `LegacyEnvelope`
- Автосохранение через `AutoSaveService` с реальным временем (`WaitForSecondsRealtime`)

### Переходы между сценами
- Fade-переход с настраиваемыми кривыми (`AnimationCurve`) и длительностями через `TransitionRequest`
- Режим `WaitForAnyKey` — сцена загружается до 90% и ждёт `InputAction`
- Режим `UseHandOff` — оверлей удерживается после активации сцены; получатель вызывает `ReleaseOverlay()` или срабатывает таймаут
- Фабричные методы: `TransitionRequest.Default()`, `.Immediate()`, `.WithKey()`
- `SceneLoader` — фасад с автоматическим fallback на `LoadSceneAsync` при отсутствии `SceneTransitionManager`

### UI и архитектура
- `UIPanel` — базовый класс с fade-анимацией через корутины, работает при `timeScale = 0`; `IsVisible` — intent-based флаг, не alpha
- `PauseController` — context-стек паузы; `timeScale` возвращается к 1 только когда все контексты сняли паузу
- `ConfirmDialog` — модальный диалог с защитой от двойного срабатывания (`_hasResponded`)
- `AncestraMenuButton` — процедурный hover-эффект: prefix, Perlin-цветовой шум, jitter позиции, глиф-коррупция текста
- `AncestraTitleEffect` — волновой дрейф вершин TMP, температурный цвет, редкий глитч-символ

### MessageBus
- Типобезопасный event bus без зависимостей на MonoBehaviour
- Защита от реентрантности: `Subscribe`/`Unsubscribe` во время `Dispatch` откладываются
- Автоматическое удаление уничтоженных Unity-объектов при `MissingReferenceException`
- Все сообщения — `readonly struct`, нет heap-аллокаций при диспатче

---

## Архитектура

```
┌─────────────────────────────────────────────────────────┐
│                      MessageBus<T>                       │
│            (type-safe, reentrance-safe bus)              │
└──────────┬──────────────────────────────────────────────┘
           │  dispatch / subscribe
    ┌──────┼──────────────────────────────────────────┐
    │      │                                          │
    ▼      ▼                                          ▼
AudioSystem        LocalizationManager          SaveManager
(PersistentSingleton)  (PersistentSingleton)  (PersistentSingleton)
    │                        │                      │
    ├─ MusicChannel           ├─ LocalizationData SO  ├─ SaveFileService
    ├─ AmbientLayer           ├─ ILocalizationSource  ├─ SaveMigrationService
    ├─ SfxPool                └─ IPluralRules         ├─ SaveableRegistry
    └─ AudioMixerRouter                               └─ AutoSaveService
                                           │
                              SceneTransitionManager
                                   │
                              SceneLoader (facade)
```

Все PersistentSingleton живут между сценами. Связь — только через `MessageBus<T>`, прямые ссылки между системами отсутствуют.

---

## Требования

| Компонент | Минимум |
|---|---|
| Unity | 2022.3 LTS или новее |
| TextMeshPro | Включён в Unity (Package Manager) |
| Input System | `com.unity.inputsystem` |
| Audio | AudioMixer с Exposed Parameters: `MasterVolume`, `MusicVolume`, `AmbientVolume`, `SFXVolume`, `UIVolume` |
| .NET | .NET Standard 2.1 |
| ОС (редактор) | Windows 10+, macOS 12+, Ubuntu 20.04+ |

---

## Установка и запуск

```bash
git clone https://github.com/your-org/ancestra.git
cd ancestra
```

1. Открыть Unity Hub → **Open** → выбрать папку проекта
2. Дождаться импорта пакетов (TextMeshPro, Input System)
3. При запросе Import TMP Essentials — подтвердить
4. Открыть стартовую сцену: `Assets/_Project/Scenes/MainMenu`
5. Нажать **Play**

### Настройка AudioMixer

В Inspector `AudioSystem` назначить `Main Mixer`. В AudioMixer добавить Exposed Parameters с именами точно как в `AudioMixerRouter`:

```csharp
public const string ParamMaster  = "MasterVolume";
public const string ParamMusic   = "MusicVolume";
public const string ParamAmbient = "AmbientVolume";
public const string ParamSfx     = "SFXVolume";
public const string ParamUI      = "UIVolume";
```

### Настройка Input System

Создать Input Action Asset, добавить Action Map `UI`, Action `Pause`. Назначить ссылку в `PauseController → _pauseAction`.

---

## Структура проекта

```
Assets/
└── _Project/
    ├── Scripts/
    │   ├── Audio/
    │   │   ├── AudioSystem.cs          # Фасад аудиосистемы (PersistentSingleton)
    │   │   ├── MusicChannel.cs         # Кроссфейд + стек треков
    │   │   ├── AmbientLayer.cs         # Именованные ambient-слои
    │   │   ├── SfxPool.cs              # Object pool для SFX
    │   │   ├── AudioMixerRouter.cs     # Linear → dB, кеш значений
    │   │   ├── AudioStack.cs           # Стек треков (Push/Pop/Replace)
    │   │   ├── AudioMessages.cs        # MessageBus-сообщения аудио
    │   │   ├── AudioData.cs            # ScriptableObject параметров клипа
    │   │   ├── SceneMusicConfig.cs     # SO: маппинг сцен → аудиоконфиг
    │   │   ├── SceneAudioTrigger.cs    # MonoBehaviour-триггер аудио
    │   │   └── SoundVariance.cs        # Рандомизация pitch/volume
    │   │
    │   ├── Localization/
    │   │   ├── LocalizationManager.cs  # Менеджер (PersistentSingleton)
    │   │   ├── LocalizationData.cs     # SO с таблицей ключ-значение
    │   │   ├── ILocalizationSource.cs  # Контракт источника локализации
    │   │   ├── LocalizableString.cs    # Сериализуемая строка (Raw/Localized)
    │   │   ├── LocalizedText.cs        # MonoBehaviour для TMP/Text
    │   │   ├── LocalizedLabel.cs       # Альтернативный компонент с SetArg<T>
    │   │   ├── LocalizationCallbackReceiver.cs  # UnityEvent при смене языка
    │   │   └── LocKeys.cs              # Реестр всех ключей (константы)
    │   │
    │   ├── Save/
    │   │   ├── SaveManager.cs          # Фасад системы сохранений
    │   │   ├── SaveFileService.cs      # Атомарный I/O, checksum, legacy
    │   │   ├── SaveMigrationService.cs # Цепочка IMigrationStep
    │   │   ├── SaveableRegistry.cs     # Реестр ISaveableComponent
    │   │   ├── SaveableComponent.cs    # Абстрактный базовый класс
    │   │   ├── SaveableId.cs           # GUID-компонент для объектов
    │   │   ├── AutoSaveService.cs      # Периодическое сохранение
    │   │   ├── GameProgressData.cs     # Данные прогресса игрока
    │   │   ├── WorldStateData.cs       # Состояние объектов мира
    │   │   ├── SaveMetadata.cs         # Техническая информация о файле
    │   │   ├── SaveConstants.cs        # Константы формата
    │   │   └── ISaveSlot.cs            # Маркерный интерфейс слота
    │   │
    │   ├── SceneTransition/
    │   │   ├── SceneTransitionManager.cs  # Управление переходами
    │   │   ├── SceneLoader.cs             # Фасад загрузки сцен
    │   │   ├── TransitionRequest.cs       # Параметры перехода + фабричные методы
    │   │   ├── TransitionState.cs         # Enum фаз перехода
    │   │   └── SceneTransitionMessages.cs # MessageBus-сообщения переходов
    │   │
    │   ├── UI/
    │   │   ├── Core/
    │   │   │   ├── UIPanel.cs             # Базовый класс панелей (fade, timeScale=0)
    │   │   │   └── ConfirmDialog.cs        # Модальный диалог подтверждения
    │   │   ├── MainMenuController.cs       # Главное меню
    │   │   ├── PauseController.cs          # Context-стек паузы
    │   │   ├── SettingsUIController.cs     # UI настроек
    │   │   ├── AncestraMenuButton.cs       # Процедурный hover-эффект кнопок
    │   │   └── AncestraTitleEffect.cs      # Волновой эффект заголовка TMP
    │   │
    │   ├── Core/
    │   │   ├── MessageBus.cs           # Type-safe event bus
    │   │   ├── PersistentSingleton.cs  # DontDestroyOnLoad базовый класс
    │   │   ├── AppSettings.cs          # PlayerPrefs-хранилище настроек
    │   │   ├── SettingsBootstrapper.cs # Применение настроек при старте сцены
    │   │   ├── GameMessages.cs         # Общие MessageBus-сообщения
    │   │   ├── Yielders.cs             # Кеш WaitForSeconds / WaitForFixedUpdate
    │   │   └── GuidGenerator.cs        # Генерация и валидация GUID
    │   │
    │   └── Data/
    │       ├── ISaveable.cs            # Типизированный контракт сохранения
    │       └── ISaveSlot.cs            # Маркерный интерфейс Reset()
    │
    ├── ScriptableObjects/
    │   ├── Audio/                      # AudioData и SceneMusicConfig ассеты
    │   └── Localization/               # LocalizationData SO для каждого языка
    │
    └── Scenes/
        ├── MainMenu
        ├── Prologue
        └── ...
```

---

## Описание подсистем

### MessageBus

Центральная шина событий. Все системы общаются только через неё.

```csharp
// Подписка
MessageBus<LanguageChangedMessage>.Subscribe(OnLanguageChanged);

// Отписка
MessageBus<LanguageChangedMessage>.Unsubscribe(OnLanguageChanged);

// Диспатч — struct, нет аллокаций
MessageBus<LanguageChangedMessage>.Dispatch(new LanguageChangedMessage(index));
```

Защита от реентрантности: если `Subscribe`/`Unsubscribe` вызваны внутри обработчика, изменения откладываются в `_listenersToAdd` / `_listenersToRemove` и применяются после завершения итерации.

---

### AudioSystem

Инициализируется один раз, живёт между сценами. Автоматически применяет аудиоконфиг при `sceneLoaded`.

```csharp
// Запустить музыку (заменить стек)
MessageBus<PlayMusicMessage>.Dispatch(new PlayMusicMessage(audioData, fadeDuration: 1.5f));

// Запустить музыку поверх текущей (Push)
MessageBus<PlayMusicMessage>.Dispatch(new PlayMusicMessage(audioData, push: true));

// Вернуть предыдущий трек
MessageBus<PopMusicMessage>.Dispatch(new PopMusicMessage(fadeDuration: 1f));

// Ambient-слой
MessageBus<PlayAmbientMessage>.Dispatch(new PlayAmbientMessage(data, layerId: "wind", fadeDuration: 2f));

// SFX в точке мира
MessageBus<PlaySfxAtPointMessage>.Dispatch(new PlaySfxAtPointMessage(data, transform.position));
```

Кроссфейд музыки — два `AudioSource` (A/B). Во время перехода A затихает, B нарастает, затем источники меняются ролями (`SwapSources`).

---

### LocalizationManager

```csharp
// Простой ключ
string text = LocalizationManager.Get(LocKeys.UI.Menu.Play);

// С форматированием
string ver = LocalizationManager.Get(LocKeys.UI.Menu.Version, Application.version);

// Plural forms (one/few/many/zero)
string label = LocalizationManager.GetPlural(LocKeys.HUD.Item.Picked, count);

// Смена языка
LocalizationManager.Instance.SetLanguageByIndex(index);
```

Все ключи — константы в `LocKeys`. Строки-литералы в вызовах `Get()` запрещены соглашением.

---

### SaveManager

```csharp
// Асинхронное сохранение (корутина)
yield return StartCoroutine(SaveManager.Instance.SaveCoroutine(slotIndex));

// Синхронное — только в OnApplicationQuit
SaveManager.Instance.Save(slotIndex);

// Загрузка
GameProgressData progress = SaveManager.Instance.LoadSlot(slotIndex);

// Автосохранение каждые 5 минут
SaveManager.Instance.EnableAutoSave(intervalSeconds: 300f);
```

Кастомный компонент сохранения:

```csharp
public class DoorState : SaveableComponent<DoorState.Data>
{
    [Serializable]
    public class Data { public bool isOpen; }

    public override Data    Capture()         => new Data { isOpen = _isOpen };
    public override void    Restore(Data d)   => SetOpen(d.isOpen);
}
```

Требования: на том же `GameObject` должен быть `SaveableId` с уникальным GUID (генерируется автоматически в Editor).

---

### SceneTransition

```csharp
// Стандартный переход
SceneLoader.Load("GameLevel_01", fadeDuration: 1.5f);

// Мгновенно
SceneTransitionManager.Instance.LoadScene(TransitionRequest.Immediate("GameLevel_01"));

// С ожиданием клавиши
SceneTransitionManager.Instance.LoadScene(TransitionRequest.WithKey("GameLevel_01", anyKeyAction));
```

Порядок сообщений при переходе:

```
SceneTransitionRequestedMessage
  → TransitionStartedMessage      (экран начинает темнеть)
    → ScreenObscuredMessage        (экран полностью чёрный — смена трека)
      → [WaitingForActivationMessage]  (если WaitForAnyKey = true)
        → [ReadyToHandOffMessage]      (если UseHandOff = true)
          → TransitionFinishedMessage  (сцена видима)
```

---

### UIPanel

```csharp
panel.Show();           // плавное появление
panel.Hide();           // плавное исчезновение
panel.ShowImmediate();  // мгновенно
panel.Toggle();         // переключение

bool isVisible = panel.IsVisible;       // intent-based (не alpha)
bool isReady   = panel.IsFullyVisible;  // alpha == 1 && !IsAnimating
```

Анимация через корутины с `Time.unscaledDeltaTime` — работает при `timeScale = 0`.

---

### AppSettings

Хранит настройки в `PlayerPrefs` отдельно от игрового прогресса. Настройки не сбрасываются при удалении сохранений.

```csharp
// Чтение
float master = AppSettings.GeneralVolume;  // default: 0.75
int   lang   = AppSettings.LanguageIndex;

// Запись (немедленно в PlayerPrefs)
AppSettings.GeneralVolume = 0.8f;

// Применить громкость через AudioSystem
SettingsBootstrapper.ApplyVolumeSettings(master, music, ambient, sfx);

// Сброс только настроек (прогресс не затрагивается)
AppSettings.ResetAll();
```

---

## Паттерны и соглашения

| Паттерн | Применение |
|---|---|
| `PersistentSingleton<T>` | `AudioSystem`, `LocalizationManager`, `SaveManager`, `PauseController` |
| `MessageBus<T>` | Связь между всеми системами без прямых зависимостей |
| `ScriptableObject` | `AudioData`, `SceneMusicConfig`, `LocalizationData` |
| `readonly struct` | Все MessageBus-сообщения (нет heap-аллокаций) |
| Object Pool | `SfxPool` для AudioSource |
| Stack | `AudioStack` для треков музыки; `PauseController` для контекстов паузы |
| Template Method | `SaveableComponent<TData>` — подкласс реализует `Capture`/`Restore` |
| Strategy | `ILocalizationSource`, `IPluralRules` — подменяемые реализации |
| Chain of Responsibility | `SaveMigrationService` — цепочка `IMigrationStep` |
| Facade | `SceneLoader`, `AudioSystem` — скрывают сложность подсистем |

### Соглашения

- Ключи локализации — только через константы `LocKeys`, никаких строк-литералов
- Музыку и ambient менять **только** через `MessageBus` — прямые вызовы `MusicChannel` обходят логику системы
- `SaveableId` генерировать в Editor, не в runtime — runtime-ID не сохраняется между сессиями
- `Yielders.UnscaledTime()` нельзя переиспользовать в цикле — `WaitForSecondsRealtime` хранит абсолютное время

---

*Документация сгенерирована на основе исходного кода проекта.*
