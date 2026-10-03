using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Сценарий телефонного разговора-собеседования. Порядок звонка:
    ///   1. Intro - вступление ("рады вас принять, но есть вопросы"), своё для каждой вакансии;
    ///   2. случайные вопросы из QuestionBank (сколько - QuestionsToAsk) - ОДНА общая база
    ///      вопросов на все вакансии (см. CallQuestionBank), чтобы не дублировать записи;
    ///      на каждый вопрос игрок отвечает цифрой, после чего играет запись-реакция на
    ///      именно этот ответ (CallChoice.ResponseClip); если ответ помечен как отказ -
    ///      остальные вопросы пропускаются;
    ///   3. финал: AcceptedEnding (взяли) или RejectedEnding (отказ) - тоже своё для вакансии.
    /// Создаётся как ассет: правый клик в Project -> Create -> Game Phone -> Call Script.
    /// Назначается в поле Call Script у PhoneNumberEntry (номера телефона).
    /// </summary>
    [CreateAssetMenu(menuName = "Game Phone/Call Script", fileName = "NewCallScript")]
    public class CallScript : ScriptableObject
    {
        [Tooltip("Варианты вступления - при каждом звонке случайно берётся ОДИН целиком, " +
                 "чтобы разговор не звучал одинаково при повторных звонках. Внутри варианта " +
                 "реплики (если их несколько) играют по порядку")]
        public List<CallStepVariant> IntroVariants = new();

        [Tooltip("Общая база вопросов (одна на всю игру, см. CallQuestionBank) - назначь сюда ту же " +
                 "базу, что и у других вакансий, чтобы вопросы не повторялось записывать заново")]
        public CallQuestionBank QuestionBank;

        [Tooltip("Сколько случайных вопросов из базы задать за один звонок")]
        [Min(0)] public int QuestionsToAsk = 1;

        [Tooltip("Варианты концовки, если человека берут на работу - случайно один целиком, как у Intro")]
        public List<CallStepVariant> AcceptedEndingVariants = new();

        [Tooltip("Варианты концовки при отказе - случайно один целиком, как у Intro")]
        public List<CallStepVariant> RejectedEndingVariants = new();
    }

    // Один вариант вступления/концовки - обычно одна реплика, но можно и несколько подряд
    [Serializable]
    public class CallStepVariant
    {
        [Tooltip("Просто для себя - что это за вариант (чтобы не путаться в списке)")]
        public string Name;

        public List<CallStep> Steps = new();
    }

    // Обычная реплика без вопроса
    [Serializable]
    public class CallStep
    {
        [Tooltip("Просто для себя - что тут говорится (чтобы не путаться в списке)")]
        public string Name;

        [Tooltip("Озвучка этой реплики")]
        public AudioClip Clip;
    }

    // Вопрос из общей базы (CallQuestionBank) - три части озвучки: сам вопрос
    // (Clip) и у каждого варианта ответа (Choices) своя реплика-реакция (ResponseClip)
    [Serializable]
    public class CallQuestion
    {
        [Tooltip("Просто для себя - что это за вопрос (чтобы не путаться в списке)")]
        public string Name;

        [Tooltip("Озвучка самого вопроса с вариантами (\"1 - розовый, 2 - синий...\")")]
        public AudioClip Clip;

        [Tooltip("Варианты ответа, которые игрок выбирает цифрой. Можно нажимать, не дожидаясь конца Clip")]
        public List<CallChoice> Choices = new();

        [Tooltip("Под этим именем (например \"любимый цвет\") запоминается выбранный ответ (Label). " +
                 "Достать потом: PlayerAnswers.Get(\"любимый цвет\")")]
        public string AnswerKey;

        [Tooltip("Сколько секунд ждать ответ после конца реплики, прежде чем повторить вопрос. 0 - ждать вечно")]
        public float ChoiceTimeout = 10f;
    }

    [Serializable]
    public class CallChoice
    {
        [Tooltip("Цифра на клавиатуре звонилки: \"1\", \"2\" ... \"0\"")]
        public string Digit = "1";

        [Tooltip("Что это за ответ - запоминается как ответ игрока (если у вопроса задан Answer Key)")]
        public string Label;

        [Tooltip("Реплика-реакция, которая играет СРАЗУ ПОСЛЕ того, как игрок выбрал этот ответ " +
                 "(например \"Ага, понятно\"), перед следующим вопросом или концовкой")]
        public AudioClip ResponseClip;

        [Tooltip("Этот ответ - отказ: на работу не берут, остальные вопросы пропускаются, играет RejectedEnding")]
        public bool Reject;
    }
}
