using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>Small, self-contained Python questions; no downloaded code is executed.</summary>
    public static class CodeQuestions
    {
        public sealed class Question
        {
            public readonly string Prompt;
            public readonly string[] Options;
            public readonly int Correct;
            public Question(string prompt,string[] options,int correct){Prompt=prompt;Options=options;Correct=correct;}
        }
        static readonly Question[] Bank={
            new Question("다음 Python 코드의 출력은?\n\nprint(2 + 3 * 2)",new[]{"7","10","12","8"},0),
            new Question("다음 Python 코드의 출력은?\n\nprint(7 // 2)",new[]{"3.5","3","4","1"},1),
            new Question("다음 Python 코드의 출력은?\n\nitems = [4, 7, 9]\nprint(items[1])",new[]{"4","7","9","1"},1),
            new Question("다음 Python 코드의 출력은?\n\nprint(len('code'))",new[]{"3","4","5","0"},1),
            new Question("다음 Python 코드의 출력은?\n\nprint(10 % 3)",new[]{"0","1","3","3.33"},1),
            new Question("다음 Python 코드의 출력은?\n\nx = 5\nx += 2\nprint(x)",new[]{"5","7","10","52"},1),
            new Question("다음 Python 코드의 출력은?\n\nprint(2 ** 3)",new[]{"6","8","9","23"},1),
            new Question("다음 Python 코드의 출력 순서는?\n\nfor i in range(3):\n    print(i, end=' ')",new[]{"1 2 3","0 1 2","0 1 2 3","3 2 1"},1),
            new Question("다음 Python 코드의 출력은?\n\nname = 'Python'\nprint(name[0])",new[]{"P","y","Python","0"},0),
            new Question("다음 Python 코드의 출력은?\n\nprint(3 > 2 and 2 > 4)",new[]{"True","False","0","오류"},1),
            new Question("다음 Python 코드의 출력은?\n\nitems = [1, 2]\nitems.append(3)\nprint(len(items))",new[]{"2","3","4","6"},1),
            new Question("다음 Python 코드의 출력은?\n\nprint('Hi' * 2)",new[]{"HiHi","Hi2","2Hi","오류"},0),
            new Question("다음 Python 코드의 출력은?\n\nx = 4\nif x > 3:\n    print('A')\nelse:\n    print('B')",new[]{"A","B","AB","출력 없음"},0),
            new Question("다음 Python 코드의 출력은?\n\nprint([2, 4, 6][-1])",new[]{"2","4","6","-1"},2),
            new Question("다음 Python 코드의 출력은?\n\nprint('PyThOn'.lower())",new[]{"PYTHON","python","PyThOn","오류"},1),
            new Question("다음 Python 코드의 출력은?\n\nprint(sum([1, 2, 3]))",new[]{"3","5","6","123"},2),
            new Question("다음 Python 코드의 출력은?\n\nprint(5 == '5')",new[]{"True","False","5","오류"},1),
            new Question("다음 Python 코드의 출력은?\n\nx = 3\nprint(x > 1 or x < 0)",new[]{"True","False","3","오류"},0),
            new Question("다음 Python 코드의 출력은?\n\nprint('hello'[1:4])",new[]{"hel","ell","ello","llo"},1),
            new Question("다음 Python 코드의 출력은?\n\nvalues = [3, 1, 2]\nprint(max(values))",new[]{"1","2","3","6"},2)
        };
        public static Question[] RandomThree()
        {
            int[] indices=new int[Bank.Length];for(int i=0;i<indices.Length;i++)indices[i]=i;
            for(int i=0;i<3;i++){int j=Random.Range(i,indices.Length);int tmp=indices[i];indices[i]=indices[j];indices[j]=tmp;}
            var questions=new Question[3];
            for(int i=0;i<questions.Length;i++)
            {
                var original=Bank[indices[i]];int[] order={0,1,2,3};
                for(int j=0;j<order.Length;j++){int k=Random.Range(j,order.Length);int tmp=order[j];order[j]=order[k];order[k]=tmp;}
                var options=new string[4];int correct=0;
                for(int j=0;j<order.Length;j++){options[j]=original.Options[order[j]];if(order[j]==original.Correct)correct=j;}
                questions[i]=new Question(original.Prompt,options,correct);
            }
            return questions;
        }
    }
}
