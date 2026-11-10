using System.Collections.Generic;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEngine;
namespace CampusCommons.Account
{
 // sceneLoaded runs after GameStart.Awake and before Player.Start/first input.
 [DefaultExecutionOrder(-31999)]
 public sealed class AccountWorldGate : MonoBehaviour
 {
  public AccountGate Gate;
  sealed class PlayerState {public MonoBehaviour player;public bool enabled,controller;}
  sealed class BehaviourState {public MonoBehaviour component;public bool enabled;}
  readonly List<PlayerState> players=new List<PlayerState>();
  readonly List<BehaviourState> rooms=new List<BehaviourState>();
  bool blocked=true;
  public void Capture()
  {
   players.RemoveAll(x=>!x.player);rooms.RemoveAll(x=>!x.component);
   foreach(var p in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
    if((p is Player || p is CampusPlayer)&&!players.Exists(x=>x.player==p))players.Add(new PlayerState{player=p,enabled=p.enabled,controller=p.GetComponent<CharacterController>()?.enabled??false});
   foreach(var component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
    if(component.GetType().FullName=="CampusCommons.Quiz.QuizRoom"&&!rooms.Exists(x=>x.component==component))rooms.Add(new BehaviourState{component=component,enabled=component.enabled});
   if(blocked)ApplyBlock();
  }
  public void Block()
  {
   if(!blocked)
   {
    // End local activities before disabling their player. No XP is inferred here.
    foreach(var escape in FindObjectsByType<EscapeGame>(FindObjectsSortMode.None))if(escape.IsSelecting||escape.IsRunning)escape.ExitToLobby();
   }
   blocked=true;Capture();ApplyBlock();
  }
  void ApplyBlock()
  {
   foreach(var s in players)if(s.player){s.player.enabled=false;var body=s.player.GetComponent<CharacterController>();if(body)body.enabled=false;}
   foreach(var s in rooms)if(s.component)s.component.enabled=false;
   Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
  }
  public void Release()
  {
   blocked=false;
   foreach(var s in players)if(s.player){var body=s.player.GetComponent<CharacterController>();if(body)body.enabled=s.controller;s.player.enabled=s.enabled;}
   foreach(var s in rooms)if(s.component)s.component.enabled=s.enabled;
  }
  void Update(){if(Gate&&!Gate.Ready)Capture();}
  void LateUpdate(){if(Gate&&!Gate.Ready){Cursor.visible=true;Cursor.lockState=CursorLockMode.None;}}
 }
}
