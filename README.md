# ANCESTRA — Праматерь

> *«Он сказал что любит его — и в тот же день его не стало. Теперь слова застряли в горле, а дом стоит пустым.»*

Walking simulator / психологический хоррор от первого лица о горе, вине и памяти. Игрок возвращается в родительский дом — не по желанию, а по закону — и сталкивается с тем, от чего убегал всю жизнь.

Игра не пугает монстрами. Она работает через узнавание.

---

## Общее

| | |
|---|---|
| **Жанр** | Walking Simulator / Психологический хоррор |
| **Движок** | Unity (URP, LTS) |
| **Платформа** | PC (Windows), Mac/Linux по возможности |
| **Рейтинг** | PEGI 18 / AO |
| **Статус** | Разработка MVP |

---

## Структура

Шесть зон, каждая соответствует стадии горя по Кюблер-Росс: Пролог → Отрицание → Гнев → Торг → Депрессия → Принятие. Каждая зона добавляет одну новую механику. Финал — бинарный выбор без подсказок.

**Core Loop:** Исследование → Взаимодействие с артефактами → Страница завещания → Искажение пространства → Переход

Ключевой принцип: признание факта открывает следующую зону. Нельзя убегать — движение навстречу единственный путь вперёд.

---

## Реализованные системы

| Система | Статус | Описание |
|---|---|---|
| **MessageBus** | ✅ | Типобезопасная шина событий. Все системы общаются только через неё — прямых зависимостей нет |
| **AudioSystem** | ✅ | Кроссфейд музыки (A/B swap со стеком), именованные ambient-слои, object pool для SFX |
| **SceneAudioTrigger** | ✅ | MonoBehaviour-альтернатива `SceneMusicConfig`: запускает музыку и ambient-слои на GameObject; режим Push возвращает предыдущий трек при деактивации (удобно для катсцен и аддитивных сцен) |
| **Локализация** | ✅ | Поддержка plural forms (ru/uk/pl), fallback-язык, `LocalizableString` для TMP и legacy Text |
| **LocalizationCallbackReceiver** | ✅ | Прокси без кода: `UnityEvent<int>` в Inspector вызывается при смене языка; `invokeOnEnable` синхронизирует начальное состояние UI |
| **Система сохранений** | ✅ | Атомарная запись, async I/O, контрольная сумма, миграция версий, автосохранение; `SaveFile` = `SaveMetadata` + `GameProgressData` + `WorldStateData` |
| **Переходы между сценами** | ✅ | Fade с кривыми, режимы WaitForAnyKey и HandOff, фасад `SceneLoader` |
| **Система игрока** | ✅ | Движение, head bob, приседание, Perlin-шум камеры, focus-зум, фонарик, система страха |
| **Нарративная система** | ✅ | `CutsceneData` ScriptableObject — кадры пролога с текстом, аудио и переходом в сцену |
| **MainMenuController** | ✅ | New Game / Continue / Exit; кнопка Continue активна только при наличии сохранения (реагирует на `GameSavedMessage`/`GameLoadedMessage`); New Game удаляет слот 0 перед загрузкой пролога |
| **SettingsUIController** | ✅ | Экран настроек с pending-состоянием (аудио, качество, разрешение, язык); изменения видны в UI, но попадают в `AppSettings` только через Apply(); `ConfirmDialog` при уходе с несохранёнными изменениями |
| **SettingsBootstrapper** | ✅ | Применяет сохранённые настройки громкости при старте каждой сцены; **обязателен в каждой сцене**; статический `ApplyVolumeSettings()` используется UI настроек для мгновенного превью |
| **UI** | ✅ | `UIPanel` с fade на unscaled time, стек паузы, `ConfirmDialog`, эффекты меню и заголовка |
| **AppSettings** | ✅ | Настройки в `PlayerPrefs` отдельно от прогресса; громкость, язык, качество, разрешение, сброс |

Подробная документация по каждой системе — в `Game Framework README`.

---

## Технический стек

- **Unity** 2022.3 LTS, URP
- **Input System** `com.unity.inputsystem`
- **TextMeshPro** встроен в Unity
- Ассет-пак: [pizzadoggy на itch.io](https://pizzadoggy.itch.io)

---

## Утилиты

| Утилита | Описание |
|---|---|
| **GuidGenerator** | Тонкая обёртка над `System.Guid` для работы с `SaveableId`; `Short()` возвращает первые 8 символов для читаемых логов |
| **Yielders** | Кеш coroutine-объектов (`WaitForSeconds`, `WaitForSecondsRealtime`, `WaitForEndOfFrame`, `WaitForFixedUpdate`) — снижает нагрузку на GC при частых корутинах |

> **Важно:** `Yielders.UnscaledTime()` нельзя переиспользовать в цикле — `WaitForSecondsRealtime` хранит абсолютное время. Для цикла создавай `new WaitForSecondsRealtime(_interval)` на каждой итерации.

---

## Структура папок

```
Assets/_Project/
├── Animations/              # Аниматоры и клипы
├── Audio/                   # AudioMixer, SceneMusicConfig, аудиофайлы
├── Input/                   # Input Action Assets (New Input System)
├── Materials/               # URP-материалы
├── Models/                  # 3D-модели
├── Prefabs/                 # Prefab-окружение, персонажи
├── Scenes/
│   ├── MainMenu/            # MainMenu.unity, Prologue.unity
│   ├── Levels/
│   │   └── Chapter01/       # Chapter01.unity
│   └── Testing/             # Test.unity — изолированное тестирование систем
├── ScriptableObjects/       # AudioData, MovementSettings и другие SO
├── Scripts/
│   ├── Core/                # MessageBus, PersistentSingleton, Localization, Services, Bootstrap
│   ├── Data/                # GameProgressData, WorldStateData, SaveMetadata, ScriptableObjects
│   ├── Gameplay/            # Player (Input, Movement, Camera), Narrative (Cutscene)
│   ├── Systems/             # AudioSystem, SaveSystem, LocalizationManager
│   ├── Tools/               # GuidGenerator
│   ├── UI/                  # UIPanel, PauseController, MainMenuController, SettingsUIController, Effects
│   └── Utils/               # Yielders
├── Shaders/
└── UI/                      # UI-спрайты и атласы
```

---

## Сцены

| Сцена | Назначение |
|---|---|
| `MainMenu.unity` | Главное меню. Стартовая сцена — все `PersistentSingleton` (Audio, Save, Localization) создаются здесь |
| `Prologue.unity` | Нарративный пролог: кадры `CutsceneData`, постепенный вывод текста, переход в Chapter01 |
| `Chapter01.unity` | Первая игровая зона |
| `Test.unity` | Техническая сцена для изолированного тестирования отдельных систем |

---

## Запуск

1. Открыть Unity Hub → **Open** → выбрать корень репозитория
2. Дождаться импорта пакетов (TextMeshPro, Input System)
3. При запросе Import TMP Essentials — подтвердить
4. Открыть `Assets/_Project/Scenes/MainMenu/MainMenu.unity`
5. Нажать **Play**

Подробная настройка AudioMixer и Input System — в [`Docs/README.md`](Docs/README.md).