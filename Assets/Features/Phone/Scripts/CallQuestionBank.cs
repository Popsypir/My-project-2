using System.Collections.Generic;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Общая база вопросов для собеседований - одна на всю игру, используется
    /// сразу несколькими CallScript (Продавец, Охранник, Уборщик и т.д.), чтобы
    /// не дублировать одни и те же вопросы в каждой вакансии.
    /// Создаётся как ассет: правый клик в Project -> Create -> Game Phone -> Call Question Bank.
    /// </summary>
    [CreateAssetMenu(menuName = "Game Phone/Call Question Bank", fileName = "CallQuestionBank")]
    public class CallQuestionBank : ScriptableObject
    {
        public List<CallQuestion> Questions = new();
    }
}
