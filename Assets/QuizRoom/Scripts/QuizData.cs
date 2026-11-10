using System;
using System.Collections.Generic;

namespace CampusCommons.Quiz
{
    [Serializable] public class QuizQuestion
    {
        public int level;
        public string answer_type;
        public string prompt;
        public string[] options;
        public int correct_index;
        public string model_answer;
        public string[] grading_criteria;
        public string explanation;
        public int source_page;
        public string source_quote;
    }
    [Serializable] public class QuizPageRange {public int start_page,end_page,total_pages;}
    [Serializable] public class QuizSet
    {
        public QuizQuestion[] questions;
        public QuizPageRange page_range;
        public bool IsValid(out string error)
        {
            error="서버에서 올바른 3문항을 받지 못했습니다. 다시 생성해 주세요.";
            if(questions==null || questions.Length!=3) return false;
            if(page_range!=null && (page_range.start_page<1 || page_range.end_page<page_range.start_page || page_range.end_page>page_range.total_pages)) return false;
            var prompts=new HashSet<string>();
            string[] kinds={"multiple_choice","short_answer","written"};
            for(int i=0;i<3;i++) {
                var q=questions[i];
                if(q==null || q.level!=i+1 || q.answer_type!=kinds[i] || string.IsNullOrWhiteSpace(q.prompt) ||
                    !prompts.Add(q.prompt.Trim()) || q.options==null || string.IsNullOrWhiteSpace(q.model_answer) ||
                    q.grading_criteria==null || string.IsNullOrWhiteSpace(q.explanation) || string.IsNullOrWhiteSpace(q.source_quote) || q.source_page<1) return false;
                if(page_range!=null && (q.source_page<page_range.start_page || q.source_page>page_range.end_page)) return false;
                if(i==0) {
                    if(q.options.Length!=4 || q.correct_index<0 || q.correct_index>3) return false;
                    var choices=new HashSet<string>();
                    foreach(var option in q.options) if(string.IsNullOrWhiteSpace(option) || !choices.Add(option.Trim())) return false;
                } else if(q.options.Length!=0 || q.correct_index!=-1 || q.grading_criteria.Length==0) return false;
            }
            error=null;return true;
        }
    }
    [Serializable] public class QuizGrade {public int level;public bool correct;public string feedback;}
    [Serializable] public class QuizGradeSet {public QuizGrade[] grades;}
    [Serializable] public class QuizAnswerSubmission {
        public int level;public string prompt,model_answer,answer;public string[] grading_criteria;
    }
    [Serializable] public class QuizGradingRequest {public QuizAnswerSubmission[] answers;}

    public sealed class QuizSession
    {
        double startedAt;
        public QuizSet Data {get;private set;}
        public int Index {get;private set;}
        public bool Finished {get;private set;}
        public string[] Responses {get;private set;}=new[]{"","",""};
        public bool[] TimedOut {get;private set;}=new bool[3];
        public bool?[] Correct {get;private set;}=new bool?[3];
        public string[] GradingFeedback {get;private set;}=new[]{"","",""};
        public QuizQuestion Current=>Data==null || Finished?null:Data.questions[Index];
        public int TimeLimit=>new[]{10,15,20}[Index];
        public int Score {get {int count=0;foreach(var answer in Correct) if(answer==true) count++;return count;}}
        public bool NeedsGrading=>Finished && (!Correct[1].HasValue || !Correct[2].HasValue);
        public double Remaining(double now)=>Current==null?0:Math.Max(0,TimeLimit-(now-startedAt));
        public void Begin(QuizSet data,double now)
        {
            if(data==null || !data.IsValid(out _)) throw new ArgumentException("Invalid quiz");
            Reset();Data=data;startedAt=now;
        }
        public bool Tick(double now,int choice=-1,string draft="")
        {
            if(Current==null || Remaining(now)>0) return false;
            Record(choice,draft,true,now);return true;
        }
        public bool SubmitChoice(int choice,double now)
        {
            if(Current==null || Index!=0 || choice<0 || choice>3) return false;
            if(Remaining(now)<=0) return Tick(now,choice);
            Record(choice,"",false,now);return true;
        }
        public bool SubmitText(string answer,int expectedLevel,double now)
        {
            if(Current==null || Index==0 || Current.level!=expectedLevel || string.IsNullOrWhiteSpace(answer)) return false;
            if(Remaining(now)<=0) return Tick(now,-1,answer);
            Record(-1,answer,false,now);return true;
        }
        void Record(int choice,string draft,bool timedOut,double now)
        {
            var q=Current;TimedOut[Index]=timedOut;
            if(Index==0) {
                Responses[0]=choice>=0 && choice<4?q.options[choice]:"";
                Correct[0]=choice>=0 && choice==q.correct_index;
            } else {
                string text=QuizText.Display(draft).Trim();int limit=Index==1?300:1000;
                Responses[Index]=text.Length>limit?text.Substring(0,limit):text;
                Correct[Index]=Responses[Index].Length==0?(bool?)false:null;
            }
            if(Index==2) Finished=true;else {Index++;startedAt=now;}
        }
        public QuizGradingRequest GradingRequest()
        {
            if(!Finished) throw new InvalidOperationException("Quiz is not finished");
            var answers=new QuizAnswerSubmission[2];
            for(int i=1;i<3;i++) {
                var q=Data.questions[i];answers[i-1]=new QuizAnswerSubmission {level=q.level,prompt=q.prompt,model_answer=q.model_answer,grading_criteria=q.grading_criteria,answer=Responses[i]};
            }
            return new QuizGradingRequest {answers=answers};
        }
        public bool ApplyGrades(QuizGradeSet result)
        {
            if(!Finished || result?.grades==null || result.grades.Length!=2) return false;
            var second=Array.Find(result.grades,g=>g!=null && g.level==2);
            var third=Array.Find(result.grades,g=>g!=null && g.level==3);
            if(second==null || third==null || string.IsNullOrWhiteSpace(second.feedback) || string.IsNullOrWhiteSpace(third.feedback)) return false;
            foreach(var grade in new[]{second,third}) {
                int i=grade.level-1;Correct[i]=Responses[i].Length>0 && grade.correct;GradingFeedback[i]=grade.feedback;
            }
            return true;
        }
        public void Reset()
        {
            Data=null;Index=0;Finished=false;startedAt=0;Responses=new[]{"","",""};TimedOut=new bool[3];Correct=new bool?[3];GradingFeedback=new[]{"","",""};
        }
    }
}
