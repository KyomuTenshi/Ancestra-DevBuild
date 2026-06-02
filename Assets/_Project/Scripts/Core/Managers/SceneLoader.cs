// Фасад для запуска переходов между сценами.
// При наличии SceneTransitionManager — использует полный pipeline (fade + lifecycle messages).
// При его отсутствии — падает назад на bare LoadSceneAsync без lifecycle-событий.
// Вызывающий код не должен знать какой из путей выполняется.

using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    /// <summary>
    /// Загружает сцену через полный transition pipeline (fade + уведомления AudioSystem).
    /// Если SceneTransitionManager не существует — загружает напрямую без lifecycle-событий.
    /// </summary>
    public static void Load(string sceneName, float fadeDuration = 1.5f)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoader] Load: имя сцены пустое или null.");
            return;
        }

        // Lifecycle-сообщения диспатчатся только когда доступен полный pipeline.
        // Безусловный диспатч приводил бы к тому что AudioSystem начинал fade музыки,
        // но никогда не получал TransitionFinishedMessage — аудио зависало в приглушённом состоянии.
        if (SceneTransitionManager.Instance != null)
        {
            MessageBus<SceneTransitionRequestedMessage>.Dispatch(
                new SceneTransitionRequestedMessage(sceneName)
            );
            SceneTransitionManager.Instance.LoadScene(sceneName, fadeDuration);
        }
        else
        {
            Debug.LogWarning("[SceneLoader] SceneTransitionManager не найден. " +
                             "Загрузка без transition pipeline.");
            SceneManager.LoadSceneAsync(sceneName);
        }
    }

    /// <summary>True если в данный момент активен переход между сценами.</summary>
    public static bool IsTransitioning =>
        SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning;
}
