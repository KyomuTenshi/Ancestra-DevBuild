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
- [Описание подсистем](#описание-подсистем)
- [Паттерны и соглашения](#паттерны-и-соглашения)
- [Утилиты](#утилиты)

---

## Фичи

### Аудиосистема
- Кроссфейд музыки через два `AudioSource` (A/B swap) с поддержкой стека треков — `Push`/`Pop` возвращает предыдущий трек с сохранённой позицией воспроизведения
- Именованные ambient-слои с кроссфейдом при смене клипа; до 12 одновременных слоёв
- Object pool для SFX с автоматическим расширением до `maxSize`; при переполнении прерывает самый старый источник
- Маппинг «имя сцены → аудиоконфиг» через `SceneMusicConfig` ScriptableObject — без кода
- `SceneAudioTrigger` — MonoBehaviour-альтернатива `SceneMusicConfig` для GameObject-уровня: режим `Replace` заменяет трек, режим `Push` добавляет в стек и делает `Pop` при `OnDisable` (для катсцен, аддитивных сцен)
- Рандомизация pitch/volume через `SoundVariance`; при `Min == Max` пропускает `Random.Range`
- Конвертация линейной громкости [0,1] → дБ для `AudioMixer` с кешом значений

### Локализация
- Поддержка множественного числа (one/few/many/zero) для русского, украинского, польского и fallback для остальных языков
- Fallback-язык: отсутствующие ключи берутся из резервного источника с логом
- Кеш разрешённых строк сбрасывается при смене языка
- `LocalizableString` — сериализуемая структура с двумя режимами (`RawText` / `LocalizedKey`); неявное приведение к `string`
- `LocalizedText` и `LocalizedLabel` — MonoBehaviour-компоненты для TMP и legacy Text с поддержкой форматирования и plural
- `LocalizationCallbackReceiver` — MonoBehaviour-прокси для реакции на смену языка без написания кода; `UnityEvent<int>` в Inspector вызывается при `LanguageChangedMessage`; флаг `invokeOnEnable` синхронизирует UI при первом включении объекта
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

### Система игрока
- `PlayerInputHandler` — единственная точка чтения ввода (New Input System); экспонирует свойства (`MoveInput`, `LookInput`, `IsFocusing`, `FlashlightTogglePressedThisFrame` и др.) — остальные системы не обращаются к Input System напрямую
- `PlayerMovementController` — движение, гравитация, head bob, приседание; параметры управляются через `MovementSettings` ScriptableObject
- `PlayerCameraController` — сглаживание взгляда (SmoothDamp), Perlin-шум камеры, зависящий от уровня страха, focus-зум с дрожанием, покачивание фонарика
- `MovementSettings` ScriptableObject — все числовые параметры игрока в одном месте: скорости, инерция, гравитация, head bob, sway фонарика, батарея, система страха (`AnimationCurve fearCurve`)

### Нарративная система (Cutscene)
- `CutsceneData` ScriptableObject — список кадров (`CutsceneFrame`): текст, опциональный `AudioClip`, override скорости печати; создаётся через `Ancestra/Narrative/Cutscene Data`
- `CutsceneController` — последовательный показ кадров пролога; тап/клик → следующий кадр; fade между кадрами через `UIPanel`; переход в следующую сцену через `SceneLoader`; поддерживает `overrideNextScene` для гибкой маршрутизации

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

### SceneAudioTrigger

Альтернатива `SceneMusicConfig` для случаев, когда аудио привязано к конкретному GameObject, а не к имени сцены (аддитивные сцены, катсцены, отдельные зоны).

```csharp
// Режимы:
// TriggerMode.Replace — заменяет текущую музыку (стандартная смена сцены)
// TriggerMode.Push    — добавляет в стек; OnDisable делает Pop (для катсцен и аддитивных сцен)

// Вызов вручную (если _playOnStart = false):
sceneAudioTrigger.Play();
sceneAudioTrigger.Stop();
```

Настройки в инспекторе:
- `_music` — `AudioData` для трека (`null` = не менять)
- `_mode` — `Replace` / `Push`
- `_fadeIn` / `_fadeOut` — длительность; `-1` = брать из `AudioData`
- `_ambientLayers` — список `AmbientEntry` (data, layerId, fadeIn, fadeOut)
- `_playOnStart` — автозапуск при `Start()`

---

### LocalizationCallbackReceiver

MonoBehaviour-прокси: реакция на смену языка **без написания кода** — через `UnityEvent<int>` в Inspector.

```csharp
// Inspector-настройки:
// _onLanguageChanged  — UnityEvent<int>, вызывается с индексом нового языка
// _invokeOnEnable     — true: сразу вызвать событие при включении для синхронизации UI
```

Типичные применения: переключение спрайтов флага, воспроизведение SFX при смене языка, скрытие/показ объектов, зависящих от локали.

---

### MainMenuController

```csharp
// Публичные методы (привязываются к кнопкам через Inspector):
OpenSettings();     // меню → настройки
BackToMainMenu();   // настройки → меню; вызывает Discard() перед скрытием панели

OnClickNewGame();   // удаляет слот 0, загружает _prologueScene
OnClickContinue();  // загружает savedSceneName из слота 0
OnClickExit();      // Application.Quit() / EditorApplication.isPlaying = false в Editor
```

Кнопка Continue автоматически становится некликабельной при отсутствии сохранения — состояние обновляется через `MessageBus<GameSavedMessage>` и `MessageBus<GameLoadedMessage>`.

Поле `_prologueScene` (default: `"Prologue"`) задаёт имя сцены для New Game.

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

### SettingsUIController

Экран настроек с **pending-состоянием**: все изменения накапливаются в `_pending`-полях и попадают в `AppSettings` только через `Apply()`. `Discard()` откатывает изменения, в том числе возвращает язык к `_committedLanguageIndex` — индексу, зафиксированному последним `Apply()`.

```csharp
// Публичный API:
Init();                   // идемпотентная инициализация; вызывать из MainMenuController/PauseController

OpenVideoMenu();          // открыть подменю видео
OpenAudioMenu();          // открыть подменю аудио
OpenGameplayMenu();       // открыть подменю геймплея
OpenSettingsMainMenu();   // назад в список настроек; показывает ConfirmDialog при несохранённых изменениях

Apply();                  // фиксирует _pending → AppSettings, применяет громкость, качество, язык
Discard();                // откатывает _pending к AppSettings, синхронизирует слайдеры и dropdown
SyncLanguageDropdown();   // синхронизирует dropdown языка без вызова onValueChanged
```

Секции настроек:
- **Аудио** — четыре слайдера (master, music, ambient, sfx); превью в реальном времени через `AudioVolumeChangedMessage`
- **Качество** — три кнопки (Low / Medium / High); активная кнопка некликабельна и окрашена
- **Графика** — dropdown разрешений с дедупликацией по `width×height`, toggle VSync (применяется немедленно, не через pending)
- **Язык** — dropdown из `LocalizationManager.GetAvailableLanguageNames()`

При уходе из подменю с несохранёнными изменениями показывается `ConfirmDialog` с выбором Apply/Discard.

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

### PlayerInputHandler

Единственная точка доступа к вводу. Все системы читают свойства — ни одна не обращается к `InputSystem` напрямую.

```csharp
// Другие системы читают готовые свойства:
Vector2 move   = _input.MoveInput;
Vector2 look   = _input.LookInput;
bool focusing  = _input.IsFocusing;
bool flashlight = _input.FlashlightTogglePressedThisFrame;

// События для реакции в момент нажатия:
_input.OnInteractPerformed += HandleInteract;
_input.OnInteractCanceled  += HandleInteractEnd;
```

Поддержка геймпада — добавить биндинг в Input Actions Asset, код менять не нужно.

---

### MovementSettings

ScriptableObject с полным балансом игрока. Создаётся через **правый клик → Ancestra → Movement Settings**.

Секции: базовые скорости, инерция (`accelerationRate` / `decelerationRate`), гравитация, приседание, head bob, Perlin-шум камеры, focus-зум, шаги, батарея фонарика, sway фонарика, система страха.

```csharp
// Система страха — нелинейная кривая нарастания:
// X = нормированное время [0..1], Y = страх [0..1]
// fearMaxTime задаёт полный цикл в секундах (default: 200)
public AnimationCurve fearCurve;
public float fearMaxTime = 200f;
```

---

### CutsceneController / CutsceneData

Показывает кадры пролога один за другим. Тап/клик — следующий кадр. Fade между кадрами через `UIPanel`, переход в следующую сцену через `SceneLoader`.

```csharp
// CutsceneData — назначить в инспекторе:
// Ancestra/Narrative/Cutscene Data
// nextSceneName — сцена после последнего кадра (default: "00_Level")

// CutsceneFrame:
// text                        — текст кадра
// voice                       — AudioClip (опционально)
// charactersPerSecondOverride — 0 = глобальное значение контроллера
```

Приоритет следующей сцены: `overrideNextScene` (поле контроллера) → `CutsceneData.nextSceneName` → `"00_Level"`.

---

## Паттерны и соглашения

| Паттерн | Применение |
|---|---|
| `PersistentSingleton<T>` | `AudioSystem`, `LocalizationManager`, `SaveManager`, `PauseController` |
| Input Abstraction | `PlayerInputHandler` — одна точка чтения, остальные системы не трогают Input System |
| ScriptableObject-баланс | `MovementSettings` — все числовые параметры игрока без перекомпиляции |
| Frame Sequencer | `CutsceneController` + `CutsceneData` — нарративный пролог без кода |
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
- Ввод читать только через `PlayerInputHandler` — прямые обращения к `InputSystem` из других систем запрещены
- `MovementSettings` ассет создавать через меню, не дублировать поля в инспекторе MonoBehaviour
- Следующую сцену после катсцены задавать в `CutsceneData.nextSceneName`; `overrideNextScene` — только для одноразовых исключений
- `SceneAudioTrigger` в режиме `Push` рассчитывает на симметричный `OnDisable` — не уничтожай объект без `Stop()`, иначе трек останется в стеке
- Инициализировать `SettingsUIController.Init()` один раз из `MainMenuController.Start()` или `PauseController.Start()` — метод идемпотентен, повторный вызов безопасен, но избыточен

---

## Утилиты

### GuidGenerator

Тонкая обёртка над `System.Guid` для работы с `SaveableId`.

```csharp
string id    = GuidGenerator.New();        // стандартный GUID (8-4-4-4-12)
string short = GuidGenerator.Short(id);    // первые 8 символов — только для логов
bool   valid = GuidGenerator.IsValid(id);  // проверка формата
```

`Short()` не гарантирует уникальности — использовать исключительно для вывода в лог.

---

### Yielders

Кеш coroutine-объектов для снижения нагрузки на GC: каждый `new WaitForSeconds(t)` аллоцирует объект; при частых корутинах это создаёт давление на сборщик мусора.

```csharp
yield return Yielders.Time(0.5f);          // WaitForSeconds (кешируется по значению float)
yield return Yielders.UnscaledTime(0.5f);  // WaitForSecondsRealtime (работает при timeScale = 0)
yield return Yielders.EndOfFrame;          // WaitForEndOfFrame
yield return Yielders.FixedUpdate;         // WaitForFixedUpdate

Yielders.ClearCache();                     // сброс кешей — только при горячей перезагрузке в Editor
```

**Ограничения кеша:**
- `UnscaledTime()` нельзя переиспользовать в цикле — `WaitForSecondsRealtime` хранит абсолютное время `m_WaitUntilTime`; после первого завершения второй `yield return` вернётся немедленно. В цикле создавай `new WaitForSecondsRealtime(_interval)` на каждой итерации.
- Не передавай в `Time()` динамические значения (`Random.Range`) — каждый уникальный `float` создаёт новую запись в словаре.
- `Yielders.Time()` (WaitForSeconds) безопасен в цикле — Unity сбрасывает его нативно.
