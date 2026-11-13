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

public static class EscapeRoundResetVerification
{
    const string Pending="EscapeRoundResetVerificationPending";
    static EscapeGame game; static Player player; static Monster monster;
    static Door single, doubleDoor, locked; static GameObject oldWorld, oldLock;
    static Sheet[] oldSheets; static float start, originalLimit; static int stage;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Next(int value){stage=value;start=Time.time;}
    static bool Closed(Door door){return !door.IsOpen && !door.IsLocked && Quaternion.Angle(door.hinge.localRotation,Quaternion.identity)<.1f && (!door.secondHinge || Quaternion.Angle(door.secondHinge.localRotation,Quaternion.identity)<.1f);}
    public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Pending,false))EditorApplication.update+=Tick;};}
    static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.Exit(code);}
    static void Tick(){if(!EditorApplication.isPlaying)return;try
    {
        if(stage==0)
        {
            if(Time.timeSinceLevelLoad<1.5f)return;
            game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();player=UnityEngine.Object.FindAnyObjectByType<Player>();monster=UnityEngine.Object.FindAnyObjectByType<Monster>();
            Check(game && player && monster,"Missing game components");originalLimit=game.timeLimitSeconds;
            Check(game.BeginSelection(player) && game.ChooseMode(EscapeGame.Mode.Easy),"First round failed to start");
            var doors=UnityEngine.Object.FindObjectsByType<Door>();single=doors.First(d=>d.hinge && !d.secondHinge);doubleDoor=doors.First(d=>d.hinge && d.secondHinge);locked=doors.First(d=>d.hinge && d!=single && d!=doubleDoor);
            single.Interact();doubleDoor.Interact();Check(locked.TryLock(600f,Resources.Load<Sprite>("EscapeItemIcons/Padlock")),"Could not lock test door");
            oldLock=locked.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Temporary locked doorway").gameObject;
            Check(game.AddItem(EscapeGame.InventoryItem.Soda) && game.AddItem(EscapeGame.InventoryItem.ExcuseNote),"Could not fill inventory");
            var world=game.World;oldWorld=world.gameObject;
            typeof(EscapeWorld).GetField("coins",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(world,4);
            typeof(EscapeWorld).GetField("speedRemaining",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(world,50f);
            var barrier=ConstructionBarrier.Create(game.schoolArrival+Vector3.right*8,Quaternion.identity,2f,600f);barrier.transform.SetParent(oldWorld.transform,true);
            var barriers=(List<ConstructionBarrier>)typeof(EscapeWorld).GetField("barriers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world);barriers.Add(barrier);
            oldSheets=UnityEngine.Object.FindObjectsByType<Sheet>();Check(oldSheets.Length==5,"First-round sheets missing");
            Check(game.OpenSheet(oldSheets[0].gameObject),"Could not open test sheet");Check(game.SubmitAnswer((game.CurrentCorrectAnswer+1)%4),"Could not answer test sheet");Check(monster.IsAwakened,"Monster did not awaken");Next(1);
        }
        else if(stage==1 && Time.time-start>.7f)
        {
            Check(single.IsFullyOpen && doubleDoor.IsFullyOpen,"Test doors did not open");
            game.CaughtByMonster();Check(game.Caught,"Could not end first round");game.ExitToLobby();
            Check(Closed(single)&&Closed(doubleDoor)&&Closed(locked),"Doors or locks did not reset immediately");
            Check(!oldLock.activeInHierarchy && !oldWorld.activeInHierarchy,"Old lock or world still active");
            Check(oldSheets.All(s=>!s.gameObject.activeSelf),"Old sheets still active");
            Check(!monster.IsAwakened && !monster.picture.gameObject.activeSelf,"Monster still active after exit");
            Check(game.World==null && game.Found==0 && game.Target==0 && !game.Caught && !game.Completed && !game.TimedOut,"Round result not cleared");
            Check(Mathf.Approximately(game.Stamina,100f) && Enumerable.Range(0,3).All(i=>game.ItemAt(i)==EscapeGame.InventoryItem.Empty),"Stamina or inventory not cleared");Next(2);
        }
        else if(stage==2 && Time.time-start>1.5f)
        {
            Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Medium),"Second round failed to start");
            Check(game.World && game.World.gameObject!=oldWorld && game.World.Coins==0 && !game.World.HasSpeedBoost && !game.World.BlocksMovement && game.World.ActiveBarrierCount==0,"World state persisted into second round");
            Check(game.Found==0 && game.Target==7 && game.ActiveNotebookCount==7 && !monster.IsAwakened && monster.picture.gameObject.activeSelf,"Second round was not fresh");
            Check(Closed(single)&&Closed(doubleDoor)&&Closed(locked),"A door stayed open or locked");
            foreach(var sheet in UnityEngine.Object.FindObjectsByType<Sheet>())Check(game.CollectSheet(sheet.gameObject),"Could not finish second-round sheet");
            var elevator=UnityEngine.Object.FindAnyObjectByType<ElevatorExit>();Check(elevator && game.ReadyToExit,"Second round exit unavailable");elevator.Interact();Check(game.Completed,"Second round did not complete");game.ExitToLobby();Check(!game.Completed && game.World==null && Closed(single),"Completed round did not reset");Next(3);
        }
        else if(stage==3 && Time.time-start>1.5f)
        {
            game.timeLimitSeconds=.2f;Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.TimeAttack),"Timed round failed to start");Next(4);
        }
        else if(stage==4 && Time.time-start>.4f)
        {
            Check(!game.TimedOut && game.IsRunning && game.Remaining<=0 && !game.ReadyToExit,"Unlimited time attack ended at the legacy limit");var sheet=UnityEngine.Object.FindObjectsByType<Sheet>().First();Check(game.CollectSheet(sheet.gameObject)&&game.Found==1&&sheet.gameObject.activeSelf&&game.ActiveNotebookCount==5,"Time attack sheet did not respawn");game.ExitToLobby();Check(!game.TimedOut && game.World==null && game.Found==0,"Time attack round did not reset");game.timeLimitSeconds=originalLimit;Next(5);
        }
        else if(stage==5 && Time.time-start>1.5f)
        {
            Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Easy),"Fourth round failed to start");
            Check(game.Found==0 && game.Target==5 && !game.TimedOut && !game.Caught && !game.Completed && Closed(single)&&Closed(doubleDoor)&&Closed(locked),"Fourth round is not fresh");
            game.ExitToLobby();Next(6);
        }
        else if(stage==6 && Time.time-start>1.5f)
        {
            game.timeLimitSeconds=.2f;Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.TimeAttackDark),"Dark time attack failed to start");Check(game.IsDark&&game.Target==5,"Dark mode configuration missing");Next(7);
        }
        else if(stage==7 && Time.time-start>.4f)
        {
            Check(!game.TimedOut&&game.IsRunning&&!game.ReadyToExit,"Dark time attack ended at legacy limit");game.ExitToLobby();game.timeLimitSeconds=originalLimit;Check(!game.IsDark&&player.enabled&&game.World==null,"Dark mode did not restore lobby");
            Debug.Log("ESCAPE_ROUND_RESET_PASS doors, locks, world, items, sheets, monster, success, unlimited bright/dark time attack and lobby restore");Finish(0);
        }
    }
    catch(Exception ex){Debug.LogException(ex);Finish(1);}}
}
