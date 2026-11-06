using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class SchoolElevatorTest
    {
        static int stage,index;
        static float since,maxShake;
        static bool error;
        static Player player;
        static CharacterController body;
        static Monster monster;
        static EscapeGame game;
        static ElevatorExit[] elevators;
        static readonly List<Vector3> route=new List<Vector3>();
        static readonly FieldInfo notice=typeof(EscapeGame).GetField("notice",BindingFlags.Instance|BindingFlags.NonPublic);
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
            SessionState.SetBool("SchoolElevatorTestPending",true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.delayCall+=()=>{
                if(!SessionState.GetBool("SchoolElevatorTestPending",false))return;
                stage=0;index=0;error=false;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
            };
        }
        static void Log(string msg,string stack,LogType type)
        {if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;if(type==LogType.Error || type==LogType.Exception)error=true;}
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Next(int value){stage=value;since=Time.time;}
        static void Begin()
        {
            Check(game.BeginSelection(player),"Could not enter school");monster.enabled=true;monster.graceSeconds=1000;
            Check(game.ChooseMode(EscapeGame.Mode.Easy),"Could not start easy mode");
        }
        static void PlaceAndAim(ElevatorExit elevator,bool aim)
        {
            Vector3 p=elevator.transform.position-elevator.transform.forward*1.1f;p.y=.08f;
            body.enabled=false;player.transform.position=p;body.enabled=true;Physics.SyncTransforms();
            Check(game.schoolGrid.FindPath(game.schoolArrival,p,route),"Elevator approach is unreachable: "+elevator.name);
            int cell=game.schoolGrid.NearestCell(p);
            Check(cell>=0 && Vector3.Distance(game.schoolGrid.Position(cell),new Vector3(p.x,0,p.z))<.8f,"Elevator is not beside a walkable position");
            game.AimAt(aim?elevator.transform.position:elevator.transform.position+Vector3.up*10);
        }
        static void SolveAll()
        {
            foreach(var sheet in UnityEngine.Object.FindObjectsByType<Sheet>())
            {
                Check(game.OpenSheet(sheet.gameObject),"Could not open a sheet");
                for(int q=0;q<3;q++)
                {Check(game.SubmitAnswer(game.CurrentCorrectAnswer),"Could not answer");Check(game.ContinueQuestion(),"Could not collect sheet");}
            }
            Check(game.ReadyToExit && game.IsPanicActive && monster.IsFinalHunt,"The final sheet did not start the endgame");
            monster.enabled=false;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<2)return;
            try
            {
                if(stage==0)
                {
                    player=UnityEngine.Object.FindAnyObjectByType<Player>();game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();monster=UnityEngine.Object.FindAnyObjectByType<Monster>();
                    Check(player && game && monster,"School game objects missing");body=player.GetComponent<CharacterController>();
                    Check(game.panicTintOpacity>=.5f && game.panicShakeStrength>0,"Endgame tint or shake has no visible default");
                    elevators=UnityEngine.Object.FindObjectsByType<ElevatorExit>().OrderBy(e=>e.name).ToArray();
                    Check(elevators.Length==3 && game.ElevatorCount==3,"Expected three elevator exits");
                    var props=UnityEngine.Object.FindObjectsByType<Transform>().Where(t=>t.name.StartsWith("Study items ")).ToArray();
                    Check(props.Length==36 && props.All(p=>p.GetComponentsInChildren<Collider>().Length==0),"Hall tabletop items missing or blocking movement");
                    Check(props.Sum(p=>p.GetComponentsInChildren<Renderer>().Length)>100,"Hall tables do not have enough study items");
                    Check(game.exitDoor && !game.exitDoor.exitDoor,"Old classroom exit was not retired");
                    Begin();
                    foreach(var elevator in elevators)
                    {
                        Check(elevator.GetComponent<BoxCollider>().isTrigger && elevator.GetComponentInChildren<MeshRenderer>().sharedMaterial.mainTexture,"Elevator appearance or trigger missing");
                        PlaceAndAim(elevator,true);
                        Check(game.Interact(true),"Click could not reach "+elevator.name);
                        Check(!game.Completed && !game.IsPanicActive && ((string)notice.GetValue(game)).Contains("모두"),"Early elevator interaction did not show collection notice");
                    }
                    SolveAll();game.exitDoor.Interact();Check(!game.Completed,"Old classroom door still allows escape");
                    maxShake=0;Next(1);
                }
                if(stage==1)
                {
                    var delta=Camera.main.transform.position-(player.transform.position+Vector3.up*1.62f);
                    maxShake=Mathf.Max(maxShake,delta.magnitude);
                    if(Time.time-since>.8f)
                    {Check(game.IsPanicActive && maxShake>.006f,"Endgame camera is not shaking");Next(2);}
                }
                if(stage==2)
                {
                    PlaceAndAim(elevators[index],index!=1);
                    Check(game.Interact(index!=1),"Could not interact with unlocked elevator "+index);
                    Check(game.Completed && !game.IsPanicActive,"Elevator did not finish the escape or reset red phase");
                    game.ExitToLobby();Check(player.enabled && player.currentSpace==Portal.Space.Library && !game.IsPanicActive,"Lobby state did not reset");
                    index++;if(index==3){Check(!error,"Unexpected runtime error");Debug.Log("SCHOOL_ELEVATOR_ENDGAME_PASS three reachable elevators, early notice, click and E exit, red phase, camera shake, lobby reset, hall props");Finish(0);}
                    else Next(3);
                }
                if(stage==3 && Time.time-since>1.5f){Begin();SolveAll();Next(2);}
            }
            catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        static void Finish(int code)
        {EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool("SchoolElevatorTestPending",false);EditorApplication.Exit(code);}
    }
}
