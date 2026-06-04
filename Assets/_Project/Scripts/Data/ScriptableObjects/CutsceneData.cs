using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Narrative
{
    /// <summary>
    /// ScriptableObject с данными пролога.
    /// Создаётся через: Assets → Ancestra → Narrative → Cutscene Data
    /// </summary>
    [CreateAssetMenu(fileName = "NewCutsceneData", menuName = "Ancestra/Narrative/Cutscene Data")]
    public class CutsceneData : ScriptableObject
    {
        [Tooltip("Имя сцены, которая загрузится после пролога.")]
        public string nextSceneName = "00_Level";

        [SerializeField]
        private List<CutsceneFrame> frames = new();

        public IReadOnlyList<CutsceneFrame> Frames => frames;
        public bool HasFrames => frames != null && frames.Count > 0;
    }

    /// <summary>
    /// Один кадр пролога: текст + опциональная озвучка.
    /// Фон чёрный — задаётся на Canvas, здесь не нужен.
    /// </summary>
    [Serializable]
    public class CutsceneFrame
    {
        [Tooltip("Текст кадра.")]
        [TextArea(2, 5)]
        public string text;

        [Tooltip("Озвучка реплики (опционально).")]
        public AudioClip voice;

        [Tooltip("Скорость печати (символов/сек). 0 = глобальное значение контроллера.")]
        [Range(0f, 200f)]
        public float charactersPerSecondOverride;
    }
}
