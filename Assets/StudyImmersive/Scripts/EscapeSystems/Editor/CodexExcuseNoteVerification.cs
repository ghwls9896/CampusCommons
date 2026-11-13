using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexExcuseNoteVerification {
 const string Key="CodexExcuseNoteVerification"; const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetString("LocalIntegrationAudit","world");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod] static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2||!CampusCommons.Account.AccountGate.Instance||!CampusCommons.Account.AccountGate.Instance.Ready)return;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;try{
 var g=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var p=UnityEngine.Object.FindAnyObjectByType<Player>();Check(g.BeginSelection(p)&&g.ChooseMode(EscapeGame.Mode.Easy),"start");var w=g.World;w.enabled=false;
 var begin=typeof(EscapeWorld).GetMethod("BeginProfessorQuestion",F);var tryKey=typeof(EscapeWorld).GetMethod("TryUseProfessorKey",F);var find=typeof(EscapeWorld).GetMethod("ExcuseNoteSlot",F);var review=typeof(EscapeWorld).GetField("reviewRemaining",F);
 Func<KeyCode,bool> key=k=>(bool)tryKey.Invoke(w,new object[]{k});Action clear=()=>{for(int i=0;i<3;i++)g.RemoveItem(i);};
 var row=new[]{KeyCode.Alpha1,KeyCode.Alpha2,KeyCode.Alpha3};var pad=new[]{KeyCode.Keypad1,KeyCode.Keypad2,KeyCode.Keypad3};
 for(int slot=0;slot<3;slot++)for(int keypad=0;keypad<2;keypad++){
  clear();for(int i=0;i<slot;i++)Check(g.AddItem(EscapeGame.InventoryItem.Soda),"filler");Check(g.AddItem(EscapeGame.InventoryItem.ExcuseNote),"note");begin.Invoke(w,null);
  Check(w.ProfessorQuestionOpen&&w.BlocksMovement,"question missing");Check((int)find.Invoke(w,null)==slot,"displayed wrong slot");
  int wrong=(slot+1)%3;Check(!key(row[wrong]),"wrong slot triggered");Check(g.ItemAt(slot)==EscapeGame.InventoryItem.ExcuseNote,"note consumed by wrong slot");
  Check(key(keypad==0?row[slot]:pad[slot]),"slot key failed");Check(!w.BlocksMovement&&g.ItemAt(slot)==EscapeGame.InventoryItem.Empty,"question not released or note not consumed");
 }
 clear();Check(g.AddItem(EscapeGame.InventoryItem.ExcuseNote),"review note");begin.Invoke(w,null);var q=(CodeQuestions.Question)typeof(EscapeWorld).GetField("question",F).GetValue(w);Check(w.AnswerProfessor((q.Correct+1)%q.Options.Length)&&!w.ProfessorQuestionOpen&&(float)review.GetValue(w)>0,"wrong-answer review missing");Check(key(KeyCode.Alpha1)&&!w.BlocksMovement,"review skip failed");
 clear();Check(g.AddItem(EscapeGame.InventoryItem.ExcuseNote),"button note");begin.Invoke(w,null);Check(w.UseItem((int)find.Invoke(w,null))&&!w.BlocksMovement,"button use failed");
 clear();Check(g.AddItem(EscapeGame.InventoryItem.ExcuseNote),"outside note");Check(!key(KeyCode.Alpha1)&&g.ItemAt(0)==EscapeGame.InventoryItem.ExcuseNote,"used while not blocked");
 clear();begin.Invoke(w,null);Check(!key(KeyCode.Alpha1)&&w.BlocksMovement,"empty slot should not skip");
 Debug.Log("EXCUSE_NOTE_PASS questions/review slots 1-3 row/keypad, wrong slots, button action, no note");g.ExitToLobby();SessionState.EraseString("LocalIntegrationAudit");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);SessionState.EraseString("LocalIntegrationAudit");EditorApplication.Exit(1);}}
}
