using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexLocalQuizFixture {
 const string K="CodexLocalQuizFixture";static bool begun;
 sealed class Fixture:IAccountTransport{public Task<T> SendAsync<T>(string method,string path,object body,string token,CancellationToken ct)where T:class{object value=null;if(typeof(T)==typeof(AuthResponse))value=new AuthResponse{access_token="fixture",refresh_token="fixture",expires_at=4000000000,user=new AuthUser{id="fixture-user",email_confirmed_at="date"}};if(typeof(T)==typeof(AccountProfile))value=new AccountProfile{user_id="fixture-user",nickname="검증사용자",level=1};return Task.FromResult((T)value);}}
 public static void RunChase(){SessionState.SetBool(K,true);CodexChaseCheck.Run();}
 public static void RunMusic(){SessionState.SetBool(K,true);CodexMusicCheck.Run();}
 public static void RunBreaker(){SessionState.SetBool(K,true);CodexBreakerCheck.Run();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(K,false))EditorApplication.update+=Tick;};}
 static async void Tick(){if(!EditorApplication.isPlaying||begun||!AccountGate.Instance)return;begun=true;try{var gate=AccountGate.Instance;gate.Configure(new Fixture());await gate.LoginAsync("audit@example.test","fixture-password");Debug.Log("LOCAL_FIXTURE_AUTH_PASS");SessionState.SetBool(K,false);EditorApplication.update-=Tick;}catch(Exception e){Debug.LogException(e);SessionState.SetBool(K,false);EditorApplication.Exit(1);}}
}



public static class CodexChaseCheck {
 static bool ready;static EscapeGame g;static Player p;static Monster m;static CharacterController pb,mb;static float start,last;static Vector3 previous;static int still;static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 public static void Run(){SessionState.SetBool("ChaseCheck",true);EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool("ChaseCheck",false))EditorApplication.update+=Tick;};}
 static void Place(CharacterController c,Vector3 pos){c.enabled=false;c.transform.position=pos;c.enabled=true;Physics.SyncTransforms();}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2f)return;try{
 if(!ready){p=UnityEngine.Object.FindAnyObjectByType<Player>();g=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();m=UnityEngine.Object.FindAnyObjectByType<Monster>();if(!g.BeginSelection(p)||!g.ChooseMode(EscapeGame.Mode.Easy))throw new Exception("start failed");m.SpeedUp();typeof(Monster).GetField("wakeRemaining",F).SetValue(m,0f);pb=p.GetComponent<CharacterController>();mb=m.GetComponent<CharacterController>();p.enabled=false;
 var ca=new Vector3(1000,.08f,1000);var cb=ca+Vector3.forward*.9f;Place(mb,ca);Place(pb,cb);
 var capture=typeof(Monster).GetMethod("CanCaptureBetween",F);if(!(bool)capture.Invoke(m,new object[]{ca,ca,cb,cb}))throw new Exception("capture at close distance failed");
 var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=(ca+cb)*.5f+Vector3.up;wall.transform.localScale=new Vector3(3,3,.1f);Physics.SyncTransforms();
 if((bool)capture.Invoke(m,new object[]{ca,ca,cb,cb}))throw new Exception("capture through wall");if((bool)typeof(Monster).GetMethod("CanSeeTarget",F).Invoke(m,null))throw new Exception("chase sees through wall");UnityEngine.Object.DestroyImmediate(wall);Debug.Log("CHASE_WALL_PROTECTION_PASS");
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Chase test floor";floor.transform.position=new Vector3(1000,-.1f,1020);floor.transform.localScale=new Vector3(8,.2f,50);
 var grid=ScriptableObject.CreateInstance<SchoolGrid>();grid.step=.5f;grid.minX=2000;grid.minZ=1995;grid.width=1;grid.height=90;grid.links=new byte[90];for(int i=0;i<90;i++)grid.links[i]=(byte)((i<89?1:0)|(i>0?4:0));m.map=grid;
 Place(mb,new Vector3(1000,.08f,1000));Place(pb,new Vector3(1000,.08f,1001.5f));pb.gameObject.AddComponent<ChasePlayerDriver>();previous=mb.transform.position;start=last=Time.time;ready=true;return;}
 if(g.Caught){CheckElapsed();Debug.Log("CHASE_TEST_PASS elapsed="+(Time.time-start)+" still="+still);Done(0);return;}
 float dt=Time.time-last;last=Time.time;if(dt>0f){if(Vector3.Distance(previous,mb.transform.position)<.001f)still++;previous=mb.transform.position;}
 if(Time.time-start>8f)throw new Exception("No capture distance="+Vector3.Distance(pb.transform.position,mb.transform.position)+" still="+still+" monster="+mb.transform.position+" player="+pb.transform.position);
 }catch(Exception e){Debug.LogError("CHASE_TEST_FAIL "+e);Done(1);}}
 static void CheckElapsed(){if(Time.time-start<1f)throw new Exception("Chase ended before sufficient travel elapsed="+(Time.time-start)+" m="+mb.transform.position+" p="+pb.transform.position);} static void Done(int code){SessionState.SetBool("ChaseCheck",false);EditorApplication.Exit(code);}
}

public static class CodexMusicCheck {
 static int stage;static float start,vol;static EscapeGame g;static Player p;static AudioSource a;static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 public static void Run(){SessionState.SetBool("MusicCheck",true);EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool("MusicCheck",false))EditorApplication.update+=Tick;};}
 static void Check(bool c,string msg){if(!c)throw new Exception(msg);} static void Found(int n){typeof(EscapeGame).GetField("found",F).SetValue(g,n);}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2f)return;try{
 if(stage==0){p=UnityEngine.Object.FindAnyObjectByType<Player>();g=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();Check(g.BeginSelection(p)&&g.ChooseMode(EscapeGame.Mode.Easy),"start failed");var music=UnityEngine.Object.FindAnyObjectByType<FinalNotebookMusic>();Check(music,"component missing");a=music.GetComponent<AudioSource>();Check(a&&a.clip&&a.clip.name=="FinalChase"&&a.loop&&a.spatialBlend==0,"audio setup");Found(g.Target-2);start=Time.time;stage=1;return;}
 if(stage==1&&Time.time-start>.3f){Check(!a.isPlaying&&a.volume==0,"played too early");Found(g.Target-1);start=Time.time;stage=2;return;}
 if(stage==2&&Time.time-start>.6f){Check(!a.isPlaying&&a.volume==0,"music started with one notebook left");Found(g.Target);start=Time.time;stage=3;return;}
 if(stage==3&&Time.time-start>.5f){Check(a.isPlaying&&a.volume>0&&a.volume<.65f,"music did not fade in after last notebook");Check(g.RequestExit(),"cannot pause");vol=a.volume;start=Time.time;stage=4;return;}
 if(stage==4&&Time.time-start>.3f){Check(Mathf.Abs(a.volume-vol)<.0001f,"fade continued during pause");Check(g.CancelExit(),"cannot resume");g.CaughtByMonster();start=Time.time;stage=5;return;}
 if(stage==5&&Time.time-start>.3f){Check(!a.isPlaying&&a.volume==0,"music did not stop on capture");g.ExitToLobby();Debug.Log("FINAL_NOTEBOOK_MUSIC_PASS start fade continue pause capture reset");SessionState.SetBool("MusicCheck",false);EditorApplication.Exit(0);}
 }catch(Exception e){Debug.LogError("FINAL_NOTEBOOK_MUSIC_FAIL "+e);SessionState.SetBool("MusicCheck",false);EditorApplication.Exit(1);}}
}
public static class CodexBreakerCheck {
 static int stage;static float start;static EscapeGame g;static Player p;static Vector3 first;static GameObject fixedCabinet;static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 public static void Run(){SessionState.SetBool("BreakerCheck",true);EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool("BreakerCheck",false))EditorApplication.update+=Tick;};}
 static void Check(bool c,string msg){if(!c)throw new Exception(msg);} static void Place(CharacterController c,Vector3 pos){c.enabled=false;c.transform.position=pos;c.enabled=true;Physics.SyncTransforms();}
 static Vector3 Trigger(){typeof(EscapeGame).GetField("found",F).SetValue(g,2);var m=typeof(EscapeGame).GetMethod("TryBlackout",F);for(int i=0;i<300&&!g.PowerOut;i++)m.Invoke(g,null);Check(g.PowerOut,"no reachable fixed breaker");var b=(GameObject)typeof(EscapeGame).GetField("breaker",F).GetValue(g);Check(b,"breaker missing");var pos=b.transform.position;Check(pos.x>=499.9f&&pos.x<=500.1f&&pos.z>=-20.2f&&pos.z<=-20f,"not in hall: "+pos);var route=new List<Vector3>();Check(g.schoolGrid.FindPath(g.schoolArrival,pos,route),"hall breaker inaccessible");Check(b.GetComponentsInChildren<MeshRenderer>().Length>=10,"cabinet details missing");RaycastHit hit;Check(Physics.Raycast(pos+Vector3.back*.5f,Vector3.forward,out hit,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.z+20f)<.15f,"cabinet is not attached to wall");Place(p.GetComponent<CharacterController>(),new Vector3(pos.x,.08f,pos.z-1.2f));g.AimAt(pos);
 var can=typeof(EscapeGame).GetMethod("CanUseBreaker",F);Check((bool)can.Invoke(g,null),"cannot target cabinet from hall");
 var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.transform.position=pos+Vector3.back*.6f;obstruction.transform.localScale=new Vector3(2f,2f,.12f);Physics.SyncTransforms();Check(!(bool)can.Invoke(g,null),"cabinet usable through wall");UnityEngine.Object.DestroyImmediate(obstruction);Physics.SyncTransforms();
 g.AimAt(pos+Vector3.back*5f);Check(!(bool)can.Invoke(g,null),"cabinet usable while looking away");g.AimAt(pos);
 var cabinet=b.GetComponent<WallBreakerVisual>();Check(cabinet&&!cabinet.IsOpen,"cabinet initially open");Check(g.Interact(false)&&g.PowerOut&&cabinet.IsOpen,"first press did not open cabinet");Check(g.Interact(false)&&!g.PowerOut,"second press did not restore power");Check(b && ((GameObject)typeof(EscapeGame).GetField("breaker",F).GetValue(g))==b,"cabinet removed after repair");return pos;}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2f)return;try{
 if(stage==0){p=UnityEngine.Object.FindAnyObjectByType<Player>();g=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();fixedCabinet=(GameObject)typeof(EscapeGame).GetField("breaker",F).GetValue(g);Check(fixedCabinet,"cabinet missing before round");Check(g.BeginSelection(p)&&g.ChooseMode(EscapeGame.Mode.Easy),"start failed");first=Trigger();g.ExitToLobby();Check(fixedCabinet && ((GameObject)typeof(EscapeGame).GetField("breaker",F).GetValue(g))==fixedCabinet,"cabinet removed after exit");start=Time.time;stage=1;return;}
 if(stage==1&&Time.time-start>1.5f){Check(g.BeginSelection(p)&&g.ChooseMode(EscapeGame.Mode.Easy),"restart failed");Check(((GameObject)typeof(EscapeGame).GetField("breaker",F).GetValue(g))==fixedCabinet,"cabinet replaced on restart");var second=Trigger();Check(Vector3.Distance(first,second)<.01f,"location changed");var monster=g.monster;monster.SpeedUp();typeof(Monster).GetField("wakeRemaining",F).SetValue(monster,0f);Place(monster.GetComponent<CharacterController>(),p.transform.position+Vector3.forward*2);var fx=UnityEngine.Object.FindAnyObjectByType<MonsterProximityEffect>();Check(fx,"heartbeat missing");typeof(MonsterProximityEffect).GetField("intensity",F).SetValue(fx,1f);typeof(MonsterProximityEffect).GetMethod("Update",F).Invoke(fx,null);var audio=fx.GetComponent<AudioSource>();Check(audio&&audio.volume>.65f,"heartbeat too quiet");Debug.Log("BREAKER_FIXED_PASS pos="+first+" reachable repair restart; HEARTBEAT_PASS volume="+audio.volume);SessionState.SetBool("BreakerCheck",false);EditorApplication.Exit(0);}
 }catch(Exception e){Debug.LogError("BREAKER_FIXED_FAIL "+e);SessionState.SetBool("BreakerCheck",false);EditorApplication.Exit(1);}}
}





[DefaultExecutionOrder(-100)] public sealed class ChasePlayerDriver : MonoBehaviour { void Update(){ var c=GetComponent<CharacterController>();if(c&&c.enabled)c.Move(Vector3.forward*2.95f*Time.deltaTime); } }
