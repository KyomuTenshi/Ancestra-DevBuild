// Модальный диалог подтверждения с двумя кнопками (Apply / Discard).
// Используется в SettingsUIController когда пользователь уходит из подменю
// с несохранёнными изменениями. Показывает UIPanel мгновенно (ShowImmediate),
// чтобы диалог появился без анимации поверх текущего подменю.
//
// Листенеры кнопок подключены только через Inspector (persistent onClick).
// Добавлять их кодом в Awake нельзя — это создаёт дубликат, и каждый клик
// вызывал бы обработчик дважды. Флаг _hasResponded защищает от этого
// как запасной механизм на случай неожиданного дублирования.

using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Модальный диалог подтверждения. Принимает два колбэка через Show() и вызывает
/// один из них при нажатии кнопки. Каждый вызов Show() открывает новую сессию:
/// _hasResponded сбрасывается, гарантируя ровно одно срабатывание на сессию.
/// </summary>
[RequireComponent(typeof(UIPanel))]
public class ConfirmDialog : MonoBehaviour
{
    // ─── Инспектор ────────────────────────────────────────────────────────────

    [Header("UI")]
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _discardButton;

    // ─── Приватное состояние ──────────────────────────────────────────────────

    private Action  _onConfirm;
    private Action  _onDiscard;
    private UIPanel _panel;

    // Сбрасывается в false при каждом Show(). Если обработчик сработает повторно
    // в той же сессии (из-за дублирующего листенера или иной причины) — возврат без действий.
    private bool _hasResponded;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _panel = GetComponent<UIPanel>();
        // Листенеры подключены в Inspector — добавлять кодом нельзя.
        // Inspector уже создаёт persistent-листенер для каждой кнопки; дублирование
        // через AddListener вызвало бы двойное срабатывание при каждом клике.
    }

    private void OnDestroy()
    {
        // RemoveAllListeners снимает только runtime-листенеры (добавленные кодом).
        // Persistent-листенеры из Inspector остаются нетронутыми — это штатное поведение Unity.
        _confirmButton?.onClick.RemoveAllListeners();
        _discardButton?.onClick.RemoveAllListeners();
    }

    // ─── Публичный API ────────────────────────────────────────────────────────

    /// <summary>
    /// Открывает диалог и регистрирует колбэки для текущей сессии.
    /// Сбрасывает _hasResponded, чтобы кнопки реагировали на нажатие снова.
    /// </summary>
    /// <param name="onConfirm">Вызывается при нажатии кнопки "Apply".</param>
    /// <param name="onDiscard">Вызывается при нажатии кнопки "Discard".</param>
    public void Show(Action onConfirm, Action onDiscard)
    {
        _onConfirm    = onConfirm;
        _onDiscard    = onDiscard;
        _hasResponded = false;

        if (_panel == null)
        {
            Debug.LogError("[ConfirmDialog] UIPanel не найден. Добавь компонент UIPanel на тот же GameObject.", this);
            return;
        }

        _panel.ShowImmediate();
    }

    /// <summary>Скрывает диалог без вызова колбэков.</summary>
    public void Hide() => _panel.HideImmediate();

    // ─── Обработчики (public — подключены в Inspector через persistent onClick) ────

    /// <summary>
    /// Обработчик кнопки "Apply". Вызывает onConfirm-колбэк, переданный в Show().
    /// </summary>
    public void OnConfirmClicked()
    {
        if (_hasResponded) return;
        _hasResponded = true;
        Hide();
        _onConfirm?.Invoke();
    }

    /// <summary>
    /// Обработчик кнопки "Discard". Вызывает onDiscard-колбэк, переданный в Show().
    /// </summary>
    public void OnDiscardClicked()
    {
        if (_hasResponded) return;
        _hasResponded = true;
        Hide();
        _onDiscard?.Invoke();
    }
}
