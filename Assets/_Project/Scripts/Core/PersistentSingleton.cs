// Базовый класс для синглтонов, которые должны жить всё время работы приложения.
// При наличии дублирующего экземпляра (например, при загрузке новой сцены с тем же префабом)
// уничтожает лишний, сохраняя первый через DontDestroyOnLoad.

using UnityEngine;

/// <summary>
/// Generic singleton с автоматическим DontDestroyOnLoad. Потокобезопасность не гарантируется —
/// создавай только из главного потока Unity. Instance равен null до первого Awake().
/// </summary>
public abstract class PersistentSingleton<T> : MonoBehaviour where T : Component
{
    /// <summary>Null до первого Awake. Не обращайся в конструкторах или field-инициализаторах.</summary>
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"[PersistentSingleton] Найден дубликат {typeof(T).Name}. " +
                "Уничтожаем лишний экземпляр."
            );
            Destroy(gameObject);
            return;
        }

        Instance = this as T;
        transform.SetParent(null); // DontDestroyOnLoad requires a root GameObject
        DontDestroyOnLoad(gameObject);
    }
}
