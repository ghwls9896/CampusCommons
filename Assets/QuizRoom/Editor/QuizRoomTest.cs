using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
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
        static Seat chair;
        static Canvas board;
        static Task signingIn;
        sealed class FixtureAccount : IAccountTransport
        {
            public Task<T> SendAsync<T>(string method, string path, object body, string token, CancellationToken ct) where T : class
            {
                object value = null;
                if (typeof(T) == typeof(AuthResponse))
                    value = new AuthResponse { access_token = "fixture", refresh_token = "fixture", expires_at = 4000000000,
                        user = new AuthUser { id = "quiz-test", email_confirmed_at = "date" } };
                if (typeof(T) == typeof(AccountProfile))
                    value = new AccountProfile { user_id = "quiz-test", nickname = "퀴즈검사", level = 2 };
                return Task.FromResult((T)value);
            }
        }

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
                if (step == 0)
                {
                    var gate = AccountGate.Instance;
                    gate.Configure(new FixtureAccount());
                    signingIn = gate.LoginAsync("quiz@example.test", "fixture");
                    step++; return;
                }
                if (!signingIn.IsCompleted) return;
                signingIn.GetAwaiter().GetResult();
                if (step == 1)
                {
                    player = UnityEngine.Object.FindAnyObjectByType<Player>();
                    room = player.GetComponent<QuizRoom>();
                    Check(room && !room.BlocksMovement, "Quiz component missing or blocks lobby");
                    var portal = UnityEngine.Object.FindObjectsByType<Portal>().First(p => p.destination == Portal.Space.Quiz);
                    Check(player.TryUsePortal(portal), "Cannot enter actual quiz portal");
                    step++; changed = Time.time; return;
                }
                if (Time.time - changed < .4f) return;
                if (step == 2)
                {
                    Check(!room.BlocksMovement && room.Match == null, "Walking into the quiz room started a match");
                    board = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                        .Single(c => c.name == "Quiz chalkboard");
                    Check(board.renderMode == RenderMode.WorldSpace && board.worldCamera == Camera.main &&
                        board.transform.parent.name == "Quiz Screen Structure", "Rules are not on the classroom board");
                    Check(board.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t =>
                        t.text.Contains("객관식 15초 · 2점") && t.text.Contains("단답형 30초 · 3점") &&
                        t.text.Contains("주관식 60초 · 5점")),
                        "Entering the classroom did not show its timing rules");
                    var seats = UnityEngine.Object.FindObjectsByType<Seat>().Where(s => s.name.StartsWith("Quiz Seat ")).ToArray();
                    Check(seats.Length == 2, "Expected two physical quiz seats");
                    chair = seats.OrderBy(s => s.transform.position.x).First();
                    var body = player.GetComponent<CharacterController>();
                    body.enabled = false; player.transform.position = chair.transform.position - Vector3.up * .6f;
                    body.enabled = true; Physics.SyncTransforms();
                    Check(player.TrySit(chair), "Cannot sit at the actual quiz chair");
                    step++; changed = Time.time; return;
                }
                if (step == 3)
                {
                    Check(!room.BlocksMovement && !board.enabled && chair.occupant == player,
                        "The first player should wait seated with the board hidden");
                    Apply(Round("waiting", false));
                    step++; changed = Time.time; return;
                }
                if (step == 4)
                {
                    Check(!room.BlocksMovement && !board.enabled && chair.occupant == player,
                        "Confirmed solo waiting should keep the seated view");
                    Apply(Round("upload", true));
                    step++; changed = Time.time; return;
                }
                if (step == 5)
                {
                    Check(room.BlocksMovement && board.enabled && chair.occupant == player,
                        "The second player should open the board");
                    Check(board.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "일어나기" && b.interactable),
                        "The seated player cannot interact with the classroom board");
                    Apply(Round("waiting", false));
                    step++; changed = Time.time; return;
                }
                if (step == 6)
                {
                    Check(!room.BlocksMovement && !board.enabled && chair.occupant == player,
                        "A departing partner should leave the remaining player seated");
                    var results = Round("results", true);
                    results.results = new[] { new QuizDuelResult {
                        nickname = "퀴즈검사", max_points = 10, earned_points = 6.86f, xp = 61,
                        stages = new[] { new QuizDuelStage { level = 2, max_points = 3,
                            earned_points = 1.24f, accuracy_percent = 41.25f, elapsed_ms = 15000, xp = 11 } }
                    } };
                    Apply(results);
                    step++; changed = Time.time; return;
                }
                if (step == 7)
                {
                    var labels = board.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text).ToArray();
                    Check(labels.Any(t => t.Contains("경기 XP 61")) &&
                        labels.Any(t => t.Contains("41.25%") && t.Contains("15.0초") && t.Contains("+11 XP")),
                        "The board should show precise per-question accuracy, time and XP");
                    Check(player.RequestRoomExit() && player.CancelRoomExit(), "Exit cancellation failed");
                    Check(chair.occupant == player, "Cancel exit lost the seat");
                    Check(player.RequestRoomExit() && player.ConfirmRoomExit(), "Exit did not return to lobby");
                    step++; changed = Time.time; return;
                }
                Check(player.currentSpace == Portal.Space.Library && !room.BlocksMovement &&
                    !chair.occupant && room.Match == null, "Leaving retained quiz state");
                CheckQuizModels();
                Check(!runtimeError, "Runtime errors were logged during quiz flow");
                Debug.Log("PDF_QUIZ_ROOM_PASS"); Finish(0);
            }
            catch(Exception ex) { Debug.LogException(ex); Finish(1); }
        }
        static QuizDuelState Round(string phase, bool paired)
        {
            var self = new QuizDuelPlayer { slot = 0, nickname = "퀴즈검사", level = 2 };
            var partner = new QuizDuelPlayer { slot = 1, nickname = "상대", level = 3 };
            return new QuizDuelState { phase = phase, round_id = "round",
                players = paired ? new[] { self, partner } : new[] { self } };
        }
        static void Apply(QuizDuelState state) =>
            typeof(QuizRoom).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(room, new object[] { state });

        static void CheckQuizModels()
        {
            var difficulty = new QuizDuelState { target_level = 7, difficulty_mode = "pending" };
            Check(!difficulty.DifficultyLabel.Contains("Lv."), "Unconfirmed target displayed as applied");
            difficulty.difficulty_mode = "standard";
            Check(difficulty.DifficultyLabel == "기본 3단계 난이도", "Legacy Render difficulty not displayed");
            difficulty.difficulty_mode = "account";
            Check(difficulty.DifficultyLabel == "공통 난이도 Lv.7", "Acknowledged account level missing");
            var doc = new QuizDocument();
            doc.Select("/tmp/분할정복.pdf", 2048);
            Check(doc.Name == "분할정복.pdf", "Decomposed Korean filename was not composed");
            doc.ApplyInspection(new QuizPdfInspection { size_bytes = 2048, page_count = 120, max_pages = 1000,
                max_selected_pages = 1000, supports_page_ranges = true, max_size_bytes = 10485760, within_limits = true });
            doc.SetRange("81", "100"); Check(doc.CanGenerate, "Large PDF range must be selectable");
            doc.SetRange("100", "81"); Check(!doc.CanGenerate, "Invalid range must be rejected");
            var session = new QuizSession();
            session.Begin(Fixture(), 100);
            Check(session.SubmitChoice(0, 101) && session.SubmitText("입력한 단답", 2, 102), "Submission failed");
            Check(session.Tick(122, -1, "입력한 서술형 답") && session.Finished, "Written timeout did not advance");
            Check(session.ApplyGrades(new QuizGradeSet { grades = new[] {
                new QuizGrade { level = 2, score = 4, criterion_scores = new[] { 4 }, feedback = "핵심 일부 충족" },
                new QuizGrade { level = 3, score = 8, criterion_scores = new[] { 8 }, feedback = "핵심 대부분 충족" }
            } }) && session.Score == 22, "Three-stage scoring failed");
        }
        static void Finish(int code)
        {
            Application.logMessageReceived-=OnLog;
            SessionState.SetBool("PdfQuizTest",false);EditorApplication.update-=Tick;EditorApplication.Exit(code);
        }
    }
}
