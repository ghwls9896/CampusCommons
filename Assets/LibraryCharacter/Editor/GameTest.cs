using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class GameTest
    {
        static int state,index;static float started,lastFixed;static bool error;
        static Camera main,probe;static FaceCamera picture;static Monster monster;static EscapeGame game;static Player player;static CharacterController body;static Door door;
        static Vector3 monsterStart,doorNormal,doorCenter,doorStart;static readonly Vector3[] Directions={Vector3.forward,Vector3.right,Vector3.back,Vector3.left};
        public static void Run()
        {
            string path=AssetDatabase.LoadAssetAtPath<SceneAsset>(LibraryCharacterBuilder.PreviewScene)?LibraryCharacterBuilder.PreviewScene:"Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity";
            EditorSceneManager.OpenScene(path);SessionState.SetBool("GameTestPending",true);EditorApplication.EnterPlaymode();
        }
        public static void RunPolished()
        {
            EditorSceneManager.OpenScene("Assets/LibraryCharacter/Preview/LiveScenePolishInput.unity");
            SessionState.SetBool("GameTestPending",true);EditorApplication.EnterPlaymode();
        }
        public static void RunCampus()
        {
            const string preview="Assets/LibraryCharacter/Preview/CampusRefreshInput.unity";
            EditorSceneManager.OpenScene(AssetDatabase.LoadAssetAtPath<SceneAsset>(preview)?preview:"Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
            SessionState.SetBool("GameTestPending",true);EditorApplication.EnterPlaymode();
        }
        public static void RunEntrance()
        {
            const string preview="Assets/LibraryCharacter/Preview/EntranceRefreshInput.unity";
            EditorSceneManager.OpenScene(AssetDatabase.LoadAssetAtPath<SceneAsset>(preview)?preview:"Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
            SessionState.SetBool("GameTestPending",true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.delayCall+=()=>{if(!SessionState.GetBool("GameTestPending",false))return;state=0;index=0;error=false;Application.logMessageReceived+=OnLog;EditorApplication.update+=Tick;};
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;
            if(type==LogType.Error || type==LogType.Exception)error=true;
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static void Next(int value){state=value;started=Time.time;lastFixed=-1;}
        static void AnswerCurrent(bool correct)
        {
            int answer=game.CurrentCorrectAnswer;Check(answer>=0,"No coding question is active");
            Check(game.SubmitAnswer(correct?answer:(answer+1)%4),"Answer could not be submitted");
            Check(game.LastAnswerCorrect==correct && game.AwaitingNextQuestion,"Answer result incorrect");
            Check(game.ContinueQuestion(),"Could not continue coding question");
        }
        static bool PlaceAcrossDoor()
        {
            var map=game.schoolGrid;var path=new List<Vector3>();
            foreach(var candidate in UnityEngine.Object.FindObjectsByType<Door>().OrderByDescending(d=>d.secondHinge!=null))
            {
                if(candidate.exitDoor)continue;
                if(candidate.IsOpen){candidate.Interact();candidate.hinge.localRotation=Quaternion.identity;if(candidate.secondHinge)candidate.secondHinge.localRotation=Quaternion.identity;}
                var panel=candidate.hinge.GetComponentInChildren<BoxCollider>();if(!panel)continue;
                var center=panel.bounds.center;var normal=candidate.transform.forward;normal.y=0;normal.Normalize();
                var a=center+normal*1.15f;var b=center-normal*2.1f;a.y=b.y=0;
                int ai=map.NearestCell(a),bi=map.NearestCell(b);if(ai<0 || bi<0)continue;
                var ap=map.Position(ai);var bp=map.Position(bi);
                if(Vector3.Distance(ap,a)>.6f || Vector3.Distance(bp,b)>.6f || !map.FindPath(ap,bp,path) || path.Count<4 || path.Count>13)continue;
                if(Vector3.Dot(ap-center,normal)*Vector3.Dot(bp-center,normal)>=0)continue;
                door=candidate;doorNormal=normal;doorCenter=center;doorStart=ap;
                var creature=monster.GetComponent<CharacterController>();creature.enabled=false;monster.transform.position=ap+Vector3.up*.08f;creature.enabled=true;
                body.enabled=false;player.transform.position=bp+Vector3.up*.08f;body.enabled=true;Physics.SyncTransforms();return true;
            }
            return false;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<2)return;
            try
            {
                if(state==0)
                {
                    Check(!error,"Runtime error before game checks");player=UnityEngine.Object.FindAnyObjectByType<Player>();game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();
                    monster=UnityEngine.Object.FindAnyObjectByType<Monster>();main=Camera.main;
                    Check(player && game && monster && main && game.schoolGrid,"Player, game, monster, camera, or school navigation missing");
                    body=player.GetComponent<CharacterController>();picture=monster.GetComponentInChildren<FaceCamera>(true);
                    Check(body && picture,"Player body or bird image missing");
                    var books=GameObject.Find("Library Books");Check(books && books.GetComponentsInChildren<MeshRenderer>().Length==960,"Detailed 3D library books missing");
                    var bookRenderer=books.GetComponentInChildren<MeshRenderer>();
                    Check(bookRenderer.sharedMaterials.Length==7 && bookRenderer.sharedMaterials.All(m=>m && m.enableInstancing && m.mainTexture && m.mainTexture.width==4096),"Book textures or instancing invalid");
                    Check(books.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Distinct().Count()==8,"Mixed book row variants missing");
                    Check(UnityEngine.Object.FindObjectsByType<Door>().Length==23,"School must contain 23 working doors");
                    Check(UnityEngine.Object.FindObjectsByType<Door>().Count(d=>d.secondHinge)==8,"Eight double gates are required");
                    Check(UnityEngine.Object.FindObjectsByType<ElevatorExit>().Length==3,"School elevators are missing");
                    Check(game.schoolGrid.sheetLocations.Length==20,"The school needs 20 distributed sheet locations");
                    var route=new List<Vector3>();foreach(var point in game.schoolGrid.sheetLocations)Check(game.schoolGrid.FindPath(game.schoolArrival,point,route),"Sheet location unreachable: "+point);
                    foreach(var elevator in UnityEngine.Object.FindObjectsByType<ElevatorExit>())
                        Check(game.schoolGrid.FindPath(game.schoolArrival,elevator.transform.position-elevator.transform.forward,route),"Elevator unreachable");
                    for(int i=0;i<30;i++)
                    {
                        var questions=CodeQuestions.RandomThree();Check(questions.Length==3 && questions.Select(q=>q.Prompt).Distinct().Count()==3,"Questions are not three distinct random prompts");
                        foreach(var q in questions)Check(q.Options.Length==4 && q.Options.Distinct().Count()==4 && q.Correct>=0 && q.Correct<4,"Question answers are invalid");
                    }
                    body.enabled=false;player.transform.position=new Vector3(11,.08f,5.5f);body.enabled=true;Physics.SyncTransforms();Next(1);
                }
                if(state==1 && Time.time-started>.6f)Next(2);
                if(state==2)
                {
                    if(Time.fixedTime>lastFixed){lastFixed=Time.fixedTime;body.Move(new Vector3(.09f,-.02f,0));}
                    if(game.IsSelecting)
                    {
                        Check(player.currentSpace==Portal.Space.Escape && Vector3.Distance(player.transform.position,game.schoolArrival)<.25f,"Portal did not enter the school before mode selection");
                        monster.graceSeconds=100;Check(game.ChooseMode(EscapeGame.Mode.Easy),"Could not select easy escape mode");Next(3);
                    }
                    Check(Time.time-started<7,"Walking into the escape portal failed");
                }
                if(state==3 && Time.time-started>.5f)
                {
                    Check(game.IsRunning && game.Target==5 && game.ActiveNotebookCount==5,"Easy mode did not spawn five sheets");
                    Check(!player.enabled && player.currentSpace==Portal.Space.Escape,"First-person school transition failed");
                    Check(!game.schoolReturnTrigger.activeSelf && !game.schoolReturnVisual.activeSelf,"Return portal should be hidden");
                    var sprite=picture.GetComponent<SpriteRenderer>();Check(sprite && sprite.sprite && sprite.sprite.texture.alphaIsTransparency,"Transparent character sprite missing");
                    Check(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(main),sprite.bounds),"Character not visible from school arrival");
                    var cameraObject=new GameObject("Billboard test camera");probe=cameraObject.AddComponent<Camera>();main.tag="Untagged";probe.tag="MainCamera";index=0;Next(4);
                }
                if(state==4)
                {
                    probe.transform.position=picture.transform.position+Directions[index]*3+Vector3.up*1.4f;
                    probe.transform.LookAt(picture.transform.position+Vector3.up*1f);Next(5);
                }
                if(state==5 && Time.time-started>.12f)
                {
                    var toCamera=probe.transform.position-picture.transform.position;toCamera.y=0;toCamera.Normalize();
                    Check(Vector3.Dot(-picture.transform.forward,toCamera)>.999f && Vector3.Dot(picture.transform.up,Vector3.up)>.999f,"Sprite did not stay upright facing camera in direction "+index);
                    index++;if(index<Directions.Length)Next(4);else Next(6);
                }
                if(state==6)
                {
                    probe.tag="Untagged";main.tag="MainCamera";UnityEngine.Object.Destroy(probe.gameObject);
                    door=UnityEngine.Object.FindObjectsByType<Door>().First(d=>!d.exitDoor && d.secondHinge);
                    var rightPanel=door.secondHinge.GetComponentInChildren<BoxCollider>();var rightEdge=rightPanel.bounds.center+door.transform.right*.5f+door.transform.forward*.2f;rightEdge.y=0;
                    Check(door.DistanceFrom(rightEdge)<.35f,"Monster cannot detect the right-hand gate leaf");
                    Check(!door.IsOpen,"Door should start closed");door.Interact();Check(door.IsOpen,"Door did not open");Next(7);
                }
                if(state==7 && Time.time-started>.5f)
                {
                    Check(Mathf.Abs(Mathf.DeltaAngle(door.hinge.localEulerAngles.y,door.openAngle))<2,"Door did not rotate open");
                    Check(Mathf.Abs(Mathf.DeltaAngle(door.secondHinge.localEulerAngles.y,-door.openAngle))<2,"Right gate leaf did not rotate open");
                    door.Interact();Check(!door.IsOpen,"Door did not close");Next(8);
                }
                if(state==8 && Time.time-started>.5f)
                {
                    Check(Mathf.Abs(Mathf.DeltaAngle(door.hinge.localEulerAngles.y,0))<2,"Door did not rotate closed");
                    Check(Mathf.Abs(Mathf.DeltaAngle(door.secondHinge.localEulerAngles.y,0))<2,"Right gate leaf did not close");
                    var normal=UnityEngine.Object.FindObjectsByType<Door>().First(d=>!d.exitDoor && !d.secondHinge);
                    normal.Interact();Check(normal.IsOpen,"Normal classroom door did not open");normal.Interact();Check(!normal.IsOpen,"Normal classroom door did not close");
                    var sheet=UnityEngine.Object.FindObjectsByType<Sheet>().First();var path=new List<Vector3>();
                    Check(game.schoolGrid.FindPath(sheet.transform.position,game.schoolArrival,path) && path.Count>2,"Cannot find a nearby position for clicking a sheet");
                    body.enabled=false;player.transform.position=path[Mathf.Min(2,path.Count-1)]+Vector3.up*.08f;body.enabled=true;Physics.SyncTransforms();
                    main.transform.position=player.transform.position+Vector3.up*1.62f;main.transform.LookAt(sheet.GetComponent<Collider>().bounds.center);
                    Check(game.Interact(true) && game.IsAnswering && game.QuestionNumber==1,"Clicking a sheet did not open coding questions");
                    Check(game.RequestExit() && game.IsExitConfirmOpen,"Escape exit confirmation did not open");
                    Check(game.CancelExit() && game.IsAnswering && game.QuestionNumber==1,"Cancelling exit lost quiz progress");
                    monster.graceSeconds=0;monster.Begin(game,player.transform,EscapeGame.Mode.Easy);
                    Check(!monster.IsChasing,"Monster must pause while answering");float before=monster.Speed;
                    Check(game.SubmitAnswer((game.CurrentCorrectAnswer+1)%4),"Could not submit wrong answer");
                    Check(!game.LastAnswerCorrect && monster.Speed>before,"Wrong answer did not increase monster speed");
                    float increased=monster.Speed;Check(!game.SubmitAnswer(0) && Mathf.Approximately(increased,monster.Speed),"Repeated click increased speed twice");
                    Check(game.ContinueQuestion() && game.QuestionNumber==2,"Question two missing");AnswerCurrent(true);Check(game.QuestionNumber==3,"Question three missing");AnswerCurrent(true);
                    Check(!game.IsAnswering && game.Found==1 && game.ActiveNotebookCount==4,"Three answers did not collect one sheet");
                    monster.graceSeconds=100;monster.Begin(game,player.transform,EscapeGame.Mode.Easy);
                    foreach(var remaining in UnityEngine.Object.FindObjectsByType<Sheet>())
                    {
                        Check(game.OpenSheet(remaining.gameObject),"Could not open another sheet");
                        for(int i=0;i<3;i++)AnswerCurrent(true);
                    }
                    Check(game.Found==5 && game.ReadyToExit && !game.Completed,"Collecting all sheets must unlock the exit");
                    UnityEngine.Object.FindAnyObjectByType<ElevatorExit>().Interact();Check(game.Completed,"Elevator did not complete the escape");
                    Check(game.RequestExit() && game.ConfirmExit(),"Confirming exit did not return to the lounge");
                    Check(player.enabled && player.currentSpace==Portal.Space.Library && !monster.picture.gameObject.activeSelf,"Lobby return did not restore the player");Next(9);
                }
                if(state==9 && Time.time-started>1.4f)
                {
                    monster.graceSeconds=.05f;Check(game.BeginSelection(player) && game.ChooseMode(EscapeGame.Mode.Medium),"Could not begin chase test");
                    monsterStart=monster.transform.position;Next(10);
                }
                if(state==10 && Time.time-started>1.1f)
                {
                    Check(Vector3.Distance(monster.transform.position,monsterStart)>.25f,"Monster did not follow the school path");
                    Check(!game.Caught,"Monster caught the player unexpectedly early");
                    Check(PlaceAcrossDoor(),"Could not find a reachable door for chase testing");Next(14);
                    Check(door.secondHinge,"Chase test did not find a reachable double gate");
                }
                if(state==14 && Time.time-started>2.1f)
                {
                    Check(door.IsOpen,"Monster did not open the door on its path");
                    float startSide=Vector3.Dot(doorStart-doorCenter,doorNormal),currentSide=Vector3.Dot(monster.transform.position-doorCenter,doorNormal);
                    Debug.Log("MONSTER_DOOR_CROSS start="+startSide+" current="+currentSide+" moved="+Vector3.Distance(monster.transform.position,doorStart));
                    Check(startSide*currentSide<0,"Monster could not cross the open doorway");
                    body.enabled=false;player.transform.position=monster.transform.position+new Vector3(.65f,.08f,0);body.enabled=true;Physics.SyncTransforms();Next(11);
                }
                if(state==11 && Time.time-started>.35f)
                {
                    Check(game.Caught,"Monster proximity did not trigger capture");game.ExitToLobby();Next(12);
                }
                if(state==12 && Time.time-started>1.4f)
                {
                    monster.graceSeconds=100;game.timeLimitSeconds=.6f;
                    Check(game.BeginSelection(player) && game.ChooseMode(EscapeGame.Mode.TimeAttack),"Could not start time attack");
                    Check(game.RequestExit(),"Could not open exit confirmation in time attack");Next(15);
                }
                if(state==15 && Time.time-started>.9f)
                {
                    Check(!game.TimedOut && game.Remaining>.5f && !monster.IsChasing,"Exit confirmation must pause the timer and monster");
                    Check(game.CancelExit(),"Could not cancel time attack exit");Next(13);
                }
                if(state==13 && Time.time-started>.9f)
                {
                    Check(!game.TimedOut && game.IsRunning && game.Remaining<=0 && !game.ReadyToExit,"Unlimited time attack ended at the legacy time limit");game.ExitToLobby();Check(!error,"Runtime error during game checks");
                    Directory.CreateDirectory("Design/CharacterAndBooks");File.WriteAllText("Design/CharacterAndBooks/game-validation.txt",
                        "PASS\n960 detailed 3D book rows with 4K textures; 23 operable doors; 20 reachable distributed sheet locations.\n"+
                        "Real portal entry, 1st-person mode, sprite facing in four directions, clickable sheet, 3 distinct coding questions, wrong-answer speed increase, unlocked exit and lobby return.\n"+
                        "Monster path movement and capture, unlimited time-attack continuation.\n");
                    Debug.Log("SCHOOL_GAME_PLAYMODE_PASS");Finish(0);
                }
            }
            catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        static void Finish(int code){EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool("GameTestPending",false);EditorApplication.Exit(code);}
    }
}
