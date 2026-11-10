using System;
using System.Linq;
using System.Reflection;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.Quiz.Editor
{
    // Real scene wiring/lifecycle; fixture questions never represent a real Gemini call.
    public static class QuizRoomTest
    {
        static int step;
        static bool runtimeError;
        static float changed;
        static double deadline;
        static Player player;
        static QuizRoom room;
        static QuizSession session;
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
            SessionState.SetBool("PdfQuizTest",true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.delayCall += () => {
                if (!SessionState.GetBool("PdfQuizTest",false)) return;
                step=0; runtimeError=false; Application.logMessageReceived += OnLog; deadline=EditorApplication.timeSinceStartup+60;
                EditorApplication.update += Tick;
            };
        }
        static void OnLog(string message,string stack,LogType type)
        {
            // Known editor-only indexing error in this installed Unity version.
            if(stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if(type==LogType.Exception || type==LogType.Error) runtimeError=true;
        }
        static void Check(bool ok,string reason) { if(!ok) throw new Exception(reason); }
        static QuizSet Fixture()
        {
            var data=new QuizSet { questions=new QuizQuestion[3] };
            for(int i=0;i<3;i++) data.questions[i]=new QuizQuestion {
                level=i+1,answer_type=new[]{"multiple_choice","short_answer","written"}[i],model_answer="핵심 개념",grading_criteria=i==0?new QuizGradingCriterion[0]:new[]{new QuizGradingCriterion {description="핵심 개념",max_points=10}},prompt="테스트 자료의 핵심 개념은 무엇인가요? "+i,
                options=i==0?new[]{"정답 보기","다른 개념","관련 없는 개념","반대 개념"}:new string[0],
                correct_index=i==0?0:-1,explanation="테스트 해설입니다. 원문의 핵심 개념을 확인합니다.",source_page=1,source_quote="핵심 개념"
            };
            return data;
        }
        static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Quiz integration test timed out");
                if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<2) return;
                if(step==0)
                {
                    player=UnityEngine.Object.FindAnyObjectByType<Player>();
                    Check(player,"Scene did not create player"); room=player.GetComponent<QuizRoom>();
                    Check(room && !room.BlocksMovement,"Quiz component missing or blocks lobby");
                    // No external network/PlayerPrefs dependency in this fixture.
                    typeof(QuizRoom).GetField("server",Private).SetValue(room,"");
                    var portal=UnityEngine.Object.FindObjectsByType<Portal>().First(p=>p.destination==Portal.Space.Quiz);
                    Check(player.TryUsePortal(portal),"Cannot enter actual quiz portal");
                    step=1; changed=Time.time; return;
                }
                if(Time.time-changed<.4f) return;
                if(step==1)
                {
                    Check(room.BlocksMovement && Cursor.visible && Cursor.lockState==CursorLockMode.None,"Quiz must release cursor and block movement");
                    var document=(QuizDocument)typeof(QuizRoom).GetField("document",Private).GetValue(room);
                    document.Select("/tmp/분할정복.pdf",2048);
                    Check(document.Name=="분할정복.pdf","Decomposed Korean filename was not composed");
                    document.ApplyInspection(new QuizPdfInspection {size_bytes=2048,page_count=120,max_pages=1000,max_selected_pages=1000,supports_page_ranges=true,max_size_bytes=10485760,within_limits=true});
                    document.SetRange("81","100");Check(document.CanGenerate,"Large PDF range must be selectable");
                    document.SetRange("100","81");
                    typeof(QuizRoom).GetMethod("Generate",Private).Invoke(room,null);
                    Check(!document.CanGenerate && !(bool)typeof(QuizRoom).GetField("working",Private).GetValue(room),"Invalid range must prevent generating");
                    document.Reset();
                    session=(QuizSession)typeof(QuizRoom).GetField("session",Private).GetValue(room);
                    double now=Time.realtimeSinceStartupAsDouble;
                    session.Begin(Fixture(),now);Check(session.SubmitChoice(0,now+1),"Choice answer failed");
                    Check(player.RequestRoomExit() && player.CancelRoomExit(),"Exit cancellation failed");
                    Check(session.Current.level==2 && session.Responses[0]=="정답 보기","Cancel exit lost answer");
                    Check(session.SubmitText("입력한 단답",2,now+2),"Short answer did not advance");
                    Check(session.Tick(now+22,-1,"입력한 서술형 답"),"Written timeout did not advance");
                    Check(session.Finished && session.ApplyGrades(new QuizGradeSet {grades=new[]{new QuizGrade {level=2,score=4,criterion_scores=new[]{4},feedback="핵심 일부 충족"},new QuizGrade {level=3,score=8,criterion_scores=new[]{8},feedback="핵심 대부분 충족"}}}),"Final grading failed");
                    Check(session.Score==22 && !session.NeedsGrading,"Final result wrong");
                    step=2;changed=Time.time;return;
                }
                if(step==2)
                {
                    Check(player.RequestRoomExit() && player.ConfirmRoomExit(),"Exit did not return to lobby");
                    step=3;changed=Time.time;return;
                }
                if(step==3)
                {
                    Check(player.currentSpace==Portal.Space.Library && !room.BlocksMovement && session.Data==null,"Leaving did not clear quiz");
                    // Verify a pending request is aborted on leave, without any network call.
                    player.currentSpace=Portal.Space.Quiz;
                    step=4;changed=Time.time;return;
                }
                if(step==4)
                {
                    var pending=UnityEngine.Networking.UnityWebRequest.Get("https://example.invalid");
                    typeof(QuizRoom).GetField("request",Private).SetValue(room,pending);
                    typeof(QuizRoom).GetField("working",Private).SetValue(room,true);
                    Check(player.RequestRoomExit() && player.ConfirmRoomExit(),"Second exit failed");
                    step=5;changed=Time.time;return;
                }
                if(step==5)
                {
                    Check(typeof(QuizRoom).GetField("request",Private).GetValue(room)==null &&
                        !(bool)typeof(QuizRoom).GetField("working",Private).GetValue(room),"Leaving retained pending request");
                    Check(!runtimeError,"Runtime errors were logged during quiz flow");
                    Debug.Log("PDF_QUIZ_ROOM_PASS"); Finish(0);
                }
            }
            catch(Exception ex) { Debug.LogException(ex); Finish(1); }
        }
        static void Finish(int code)
        {
            Application.logMessageReceived-=OnLog;
            SessionState.SetBool("PdfQuizTest",false);EditorApplication.update-=Tick;EditorApplication.Exit(code);
        }
    }
}
