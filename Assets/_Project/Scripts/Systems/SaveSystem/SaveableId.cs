// GUID-компонент для идентификации объектов в системе сохранений.
// ID генерируется в OnValidate (Editor) при добавлении компонента — не при запуске.
// Важно: не меняй и не удаляй _id у существующих GameObjects в сцене —
// загруженные save files не найдут объект и потеряют его состояние.

using UnityEngine;

/// <summary>
/// GUID-компонент для идентификации объектов в SaveableRegistry.
/// ID генерируется автоматически в Editor при добавлении компонента.
/// Никогда не меняй и не удаляй _id у объектов с существующими save-файлами.
/// </summary>
public class SaveableId : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Уникальный идентификатор объекта. Генерируется автоматически. НЕ изменяй вручную.")]
    private string _id;

    [Tooltip("True для объектов, созданных в runtime (из пула/Instantiate). " +
             "ID будет переопределён при спавне.")]
    public bool IsRuntimeGenerated;

    public string Id => _id;
    public bool HasValidId => GuidGenerator.IsValid(_id);

    private void Awake()
    {
        // Runtime-объекты получают ID через AssignRuntimeId() сразу после спавна.
        if (!IsRuntimeGenerated && !HasValidId)
        {
            Debug.LogWarning(
                $"[SaveableId] '{name}': ID не задан. " +
                "Добавь компонент в Editor — ID будет сгенерирован автоматически. " +
                "Генерирую runtime ID как fallback (не сохранится между сессиями).",
                this
            );
            _id = GuidGenerator.New();
        }
    }

    /// <summary>
    /// Устанавливает GUID для runtime-созданных объектов.
    /// Вызывай сразу после спавна, до Awake дочерних компонентов.
    /// </summary>
    public void AssignRuntimeId(string id)
    {
        if (!GuidGenerator.IsValid(id))
        {
            Debug.LogError($"[SaveableId] AssignRuntimeId: '{id}' — невалидный GUID.", this);
            return;
        }
        _id = id;
    }

    public void GenerateNewId() => _id = GuidGenerator.New();

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Генерируем ID при добавлении в Editor, но не в Prefab Stage —
        // там ID должен остаться пустым, чтобы каждый инстанс получил свой.
        if (string.IsNullOrEmpty(_id) && !IsRuntimeGenerated)
        {
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                _id = GuidGenerator.New();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
    }

    [ContextMenu("Regenerate ID (danger: breaks existing saves)")]
    private void RegenerateIdMenu()
    {
        bool confirmed = UnityEditor.EditorUtility.DisplayDialog(
            "Пересоздать ID?",
            $"Объект '{name}' получит новый GUID.\n" +
            "Существующие save files не смогут найти этот объект.\n\n" +
            "Продолжить?",
            "Да, пересоздать",
            "Отмена"
        );

        if (confirmed)
        {
            _id = GuidGenerator.New();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
