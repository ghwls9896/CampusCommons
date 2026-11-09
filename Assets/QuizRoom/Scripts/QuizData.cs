using System;
using System.Collections.Generic;

namespace CampusCommons.Quiz
{
    [Serializable]
    public class QuizQuestion
    {
        public int level;
        public string prompt;
        public string[] options;
        public int correct_index;
        public string explanation;
        public int source_page;
        public string source_quote;
    }

    [Serializable]
    public class QuizSet
    {
        public QuizQuestion[] questions;
        public bool IsValid(out string error)
        {
            error = "서버에서 올바른 3문항을 받지 못했습니다. 다시 생성해 주세요.";
            if (questions == null || questions.Length != 3) return false;
            var prompts = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                var q = questions[i];
                if (q == null || q.level != i + 1 || string.IsNullOrWhiteSpace(q.prompt) ||
                    !prompts.Add(q.prompt.Trim()) || q.options == null || q.options.Length != 4 ||
                    q.correct_index < 0 || q.correct_index > 3 || q.source_page < 1 || q.source_page > 80 ||
                    string.IsNullOrWhiteSpace(q.explanation) || string.IsNullOrWhiteSpace(q.source_quote)) return false;
                var choices = new HashSet<string>();
                foreach (var option in q.options)
                    if (string.IsNullOrWhiteSpace(option) || !choices.Add(option.Trim())) return false;
            }
            error = null;
            return true;
        }
    }

    public class QuizSession
    {
        public QuizSet Data { get; private set; }
        public int Index { get; private set; }
        public int Score { get; private set; }
        public bool Finished { get; private set; }
        public int[] Answers { get; private set; } = new[] { -1, -1, -1 };
        public QuizQuestion Current => Data == null ? null : Data.questions[Index];
        public bool Submitted => Data != null && Answers[Index] >= 0;
        public void Begin(QuizSet data)
        {
            if (data == null || !data.IsValid(out _)) throw new ArgumentException("Invalid quiz");
            Reset(); Data = data;
        }
        public bool Submit(int answer)
        {
            if (Data == null || Finished || Submitted || answer < 0 || answer > 3) return false;
            Answers[Index] = answer;
            if (answer == Current.correct_index) Score++;
            return true;
        }
        public bool Next()
        {
            if (!Submitted || Finished) return false;
            if (Index == 2) Finished = true; else Index++;
            return true;
        }
        public void Reset()
        {
            Data = null; Index = Score = 0; Finished = false;
            Answers = new[] { -1, -1, -1 };
        }
    }
}
