using System;
using System.Linq;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class RoomPortalTest
    {
        static int step;static float started,lastFixed;static Player player;static CharacterController body;static EscapeGame escape;
        static bool error;
        public static void Run()
        {
            const string preview="Assets/LibraryCharacter/Preview/CampusRefreshInput.unity";
            EditorSceneManager.OpenScene(AssetDatabase.LoadAssetAtPath<SceneAsset>(preview)?preview:"Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
            SessionState.SetBool("RoomPortalTest",true);EditorApplication.EnterPlaymode();
        }
        public static void RunEntrance()
        {
            const string preview="Assets/LibraryCharacter/Preview/EntranceRefreshInput.unity";
            EditorSceneManager.OpenScene(AssetDatabase.LoadAssetAtPath<SceneAsset>(preview)?preview:"Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
            SessionState.SetBool("RoomPortalTest",true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.delayCall+=()=>{if(!SessionState.GetBool("RoomPortalTest",false))return;step=0;started=0;lastFixed=-1;error=false;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;};
        }
        static void Log(string message,string stack,LogType type)
        {
            if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;
            if(type==LogType.Error || type==LogType.Exception)error=true;
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static void Next(int n){step=n;started=Time.time;lastFixed=-1;}
        static void Approach(float z)
        {
            body.enabled=false;player.transform.position=new Vector3(11.5f,.08f,z);body.enabled=true;Physics.SyncTransforms();
        }
        static void Walk()
        {
            if(Time.fixedTime>lastFixed){lastFixed=Time.fixedTime;body.Move(new Vector3(.09f,-.02f,0));}
            Check(Time.time-started<8,"Walking through the lobby portal failed at step "+step);
        }
        static void CheckRoom(Portal.Space room,float x)
        {
            Check(player.currentSpace==room && Mathf.Abs(player.transform.position.x-x)<1,"Portal arrived in the wrong room");
            var p=player.transform.position;
            Check(!Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.5f,.26f,~(1<<2),QueryTriggerInteraction.Ignore),"Room arrival is blocked");
            Check(player.RequestRoomExit() && player.IsExitConfirmOpen,"Room exit confirmation did not open");
            Check(player.CancelRoomExit() && !player.IsExitConfirmOpen && player.currentSpace==room,"Cancelling room exit did not preserve the room");
            Check(player.RequestRoomExit() && player.ConfirmRoomExit() && player.currentSpace==Portal.Space.Library,"Confirming room exit did not return to the lounge");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<1.5f)return;
            try
            {
                if(step==0)
                {
                    player=UnityEngine.Object.FindAnyObjectByType<Player>();escape=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();
                    Check(player && escape,"Player or escape portal is missing");body=player.GetComponent<CharacterController>();
                    Check(UnityEngine.Object.FindObjectsByType<Portal>().All(p=>p.destination!=Portal.Space.Library),"A return portal is active inside a room");
                    Approach(-5.5f);Next(1);
                }
                if(step==1)
                {
                    if(player.currentSpace==Portal.Space.Mafia){CheckRoom(Portal.Space.Mafia,100);Next(2);}
                    else Walk();
                }
                if(step==2 && Time.time-started>1.5f){Approach(0);Next(3);}
                if(step==3)
                {
                    if(player.currentSpace==Portal.Space.Quiz){CheckRoom(Portal.Space.Quiz,200);Next(4);}
                    else Walk();
                }
                if(step==4 && Time.time-started>1.5f){Approach(5.5f);Next(5);}
                if(step==5)
                {
                    if(escape.IsSelecting)
                    {
                        Check(!player.enabled && player.currentSpace==Portal.Space.Escape && Vector3.Distance(player.transform.position,escape.schoolArrival)<.25f,"Escape portal did not enter the school before selecting a mode");
                        Check(escape.RequestExit() && escape.IsExitConfirmOpen,"Escape confirmation did not open");
                        Check(!escape.ChooseMode(EscapeGame.Mode.Easy),"Mode could be changed behind the exit dialog");
                        Check(escape.CancelExit() && escape.IsSelecting,"No should stay in escape mode selection");
                        Check(escape.RequestExit() && escape.ConfirmExit() && player.currentSpace==Portal.Space.Library && player.enabled,"Yes did not return to the lounge");
                        Check(!error,"A runtime error occurred during portal testing");
                        Debug.Log("ROOM_PORTALS_AND_CONFIRM_PASS");Finish(0);
                    }
                    else Walk();
                }
            }
            catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        static void Finish(int code)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool("RoomPortalTest",false);EditorApplication.Exit(code);
        }
    }
}
