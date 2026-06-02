// Интерфейс сохраняемого компонента с типизированными данными.
// Реализуй через абстрактный SaveableComponent<TData> — не напрямую.
// SaveableRegistry вызывает Capture()/Restore() через внутренний ISaveableComponent.

/// <summary>
/// Типизированный контракт сохранения. TData должен быть сериализуемым через JsonUtility.
/// Capture() — чистая функция, не меняет состояние. Restore() — единственная точка восстановления.
/// </summary>
public interface ISaveable<TData>
{
    /// <summary>
    /// Снимает текущее состояние в сериализуемый объект.
    /// Должен быть чистой функцией — не изменяет состояние компонента.
    /// </summary>
    TData Capture();

    /// <summary>Восстанавливает состояние из десериализованных данных.</summary>
    void Restore(TData data);
}
