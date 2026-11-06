using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.LibraryCharacter.Editor
{
    // Runs against the actual living scene in a separate Unity test copy.
    public static class MonsterBehaviourTest
    {
        static int stage,doorCase;
        static float since;
        static bool error,opened;
        static Player player;
        static CharacterController playerBody,monsterBody;
        static EscapeGame game;
        static Monster monster;
        static Door door;
        static Vector3 a,b,center,normal,startPosition;
        static GameObject wall;
        static readonly List<Vector3> route=new List<Vector3>();
        static FieldInfo heading=typeof(Monster).GetField("heading",BindingFlags.NonPublic|BindingFlags.Instance);
        static MethodInfo canSee=typeof(Monster).GetMethod("CanSeeTarget",BindingFlags.NonPublic|BindingFlags.Instance);
        static FieldInfo lastDoor=typeof(Monster).GetField("lastInvestigatedDoor",BindingFlags.NonPublic|BindingFlags.Instance);
        static FieldInfo roomDelay=typeof(Monster).GetField("roomSearchRemaining",BindingFlags.NonPublic|BindingFlags.Instance);
        static Vector3 cameraStart;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
            SessionState.SetBool("MonsterBehaviourTestPending",true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.delayCall+=()=>{
                if(!SessionState.GetBool("MonsterBehaviourTestPending",false))return;
                stage=0;doorCase=0;error=false;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
            };
        }
        static void Log(string msg,string stack,LogType type)
        {
            if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;
            if(type==LogType.Error || type==LogType.Exception)error=true;
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Next(int value){stage=value;since=Time.time;}
        static void Place(Transform t,CharacterController body,Vector3 p)
        {body.enabled=false;t.position=p+Vector3.up*.08f;body.enabled=true;Physics.SyncTransforms();}
        static void Face(Vector3 direction)
        {direction.y=0;heading.SetValue(monster,direction.normalized);}
        static void Verify360()
        {
            Check(game.RequestExit(),"Could not pause for vision test");
            Vector3 origin=new Vector3(1000,0,1000);
            Place(monster.transform,monsterBody,origin);
            foreach(var direction in new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left})
            {
                Place(player.transform,playerBody,origin+direction*5);Face(-direction);
                Check((bool)canSee.Invoke(monster,null),"360-degree sight missed "+direction);
            }
            Place(player.transform,playerBody,origin+Vector3.forward*100);
            Check((bool)canSee.Invoke(monster,null),"Monster could not see a distant unobstructed player");
            Place(player.transform,playerBody,origin+Vector3.right*5);
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position=origin+Vector3.right*2.5f+Vector3.up*1.2f;
            blocker.transform.localScale=new Vector3(.3f,3,4);Physics.SyncTransforms();
            Check(!(bool)canSee.Invoke(monster,null),"Vision saw through a wall");
            blocker.GetComponent<Collider>().enabled=false;UnityEngine.Object.Destroy(blocker);
            var oneSided=new GameObject("One-sided school wall test");
            var mesh=new Mesh{vertices=new[]{new Vector3(0,-2,-2),new Vector3(0,2,-2),new Vector3(0,2,2),new Vector3(0,-2,2)},triangles=new[]{0,1,2,0,2,3}};
            oneSided.transform.position=origin+Vector3.right*2.5f+Vector3.up*1.2f;
            oneSided.AddComponent<MeshCollider>().sharedMesh=mesh;Physics.SyncTransforms();
            Check(!(bool)canSee.Invoke(monster,null),"Vision leaked through the back face of a wall");
            Place(monster.transform,monsterBody,origin+Vector3.right*5);Place(player.transform,playerBody,origin);
            Check(!(bool)canSee.Invoke(monster,null),"Vision leaked through the front face of a wall");
            UnityEngine.Object.Destroy(oneSided);UnityEngine.Object.Destroy(mesh);
            Place(player.transform,playerBody,game.schoolArrival);
            monster.sightDistance=13f;
            monster.Begin(game,player.transform,EscapeGame.Mode.Medium);Check(game.CancelExit(),"Could not resume after vision test");
        }
        static bool FindDoorCase(bool wide,int side)
        {
            foreach(var d in UnityEngine.Object.FindObjectsByType<Door>().Where(x=>!x.exitDoor && (x.secondHinge!=null)==wide))
            {
                if(d.IsOpen)d.Interact();
                d.hinge.localRotation=Quaternion.identity;if(d.secondHinge)d.secondHinge.localRotation=Quaternion.identity;
                Physics.SyncTransforms();
                var left=d.hinge.GetComponentInChildren<BoxCollider>();if(!left)continue;
                var bounds=left.bounds;
                if(d.secondHinge){var right=d.secondHinge.GetComponentInChildren<BoxCollider>();if(!right)continue;bounds.Encapsulate(right.bounds);}
                Vector3 n=d.transform.forward;n.y=0;n.Normalize();
                var p=bounds.center+n*(2.1f*side);var q=bounds.center-n*(3.0f*side);p.y=q.y=0;
                int pi=game.schoolGrid.NearestCell(p),qi=game.schoolGrid.NearestCell(q);if(pi<0 || qi<0)continue;
                var pp=game.schoolGrid.Position(pi);var qq=game.schoolGrid.Position(qi);
                if(Vector3.Distance(pp,p)>.55f || Vector3.Distance(qq,q)>.55f)continue;
                if(Vector3.Dot(pp-bounds.center,n)*Vector3.Dot(qq-bounds.center,n)>=0)continue;
                if(!game.schoolGrid.FindPath(pp,qq,route) || route.Count<6 || route.Count>18)continue;
                if(Physics.CheckCapsule(pp+Vector3.up*.36f,pp+Vector3.up*1.48f,.29f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ||
                   Physics.CheckCapsule(qq+Vector3.up*.36f,qq+Vector3.up*1.48f,.29f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                door=d;center=bounds.center;normal=n;a=pp;b=qq;return true;
            }
            return false;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<2)return;
            try
            {
                if(stage==0)
                {
                    player=UnityEngine.Object.FindAnyObjectByType<Player>();game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();
                    monster=UnityEngine.Object.FindAnyObjectByType<Monster>();
                    Check(player && game && monster && game.schoolGrid,"Missing scene objects");
                    playerBody=player.GetComponent<CharacterController>();monsterBody=monster.GetComponent<CharacterController>();
                    Check(monster.sightDistance>=100f,"Default vision distance is not long enough");
                    Check(game.BeginSelection(player),"Could not enter school");monster.graceSeconds=0;
                    Check(game.ChooseMode(EscapeGame.Mode.Medium),"Could not start game");
                    Check(monster.footstepClip && monster.alertClip,"Crow sounds not loaded");
                    var sounds=monster.GetComponents<AudioSource>();
                    Check(sounds.Length>=2 && sounds.All(s=>s.spatialBlend==1 && s.maxDistance>=15),"Sounds are not spatial");
                    var trim=GameObject.Find("School Upper Blue Trim");
                    Check(trim && trim.GetComponent<MeshFilter>().sharedMesh.vertexCount>100 && !trim.GetComponent<Collider>(),"School upper trim missing or blocks movement");
                    Check(game.jumpScareImage && game.jumpScareSound,"Capture image or sound missing");
                    Verify360();
                    startPosition=monster.transform.position;Next(1);
                }
                if(stage==1 && Time.time-since>.8f)
                {
                    Check(monster.IsChasing,"Visible player did not trigger chase");
                    Check(Vector3.Distance(startPosition,monster.transform.position)>.3f,"Chasing monster did not move");
                    Check(game.RequestExit(),"Could not pause game");startPosition=monster.transform.position;Next(2);
                }
                if(stage==2 && Time.time-since>.5f)
                {
                    Check(!monster.IsChasing && Vector3.Distance(startPosition,monster.transform.position)<.03f,"Monster did not pause");
                    Check(game.CancelExit(),"Could not resume game");
                    Vector3 delta=player.transform.position-monster.transform.position;delta.y=0;
                    Check(delta.magnitude>1,"Not enough space for visibility check");Face(delta);
                    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Test visibility blocker";
                    wall.transform.position=(player.transform.position+monster.transform.position)*.5f+Vector3.up*1.2f;
                    wall.transform.rotation=Quaternion.LookRotation(delta.normalized);wall.transform.localScale=new Vector3(5,3,.3f);Physics.SyncTransforms();
                    monster.searchSeconds=1.2f;Next(3);
                }
                if(stage==3 && Time.time-since>.75f)
                {
                    Check(monster.State==Monster.Behaviour.Search,"Wall did not break vision and trigger search");
                    Place(player.transform,playerBody,game.schoolGrid.sheetLocations.Last());Next(4);
                }
                if(stage==4 && Time.time-since>1.6f)
                {
                    Check(monster.State==Monster.Behaviour.Patrol,"Monster did not resume patrol after search");
                    UnityEngine.Object.Destroy(wall);monster.searchSeconds=9;startPosition=monster.transform.position;Next(40);
                }
                if(stage==40 && Time.time-since>2.5f)
                {
                    Check(monster.State==Monster.Behaviour.Patrol && Vector3.Distance(startPosition,monster.transform.position)>.25f,"Monster did not move while patrolling");
                    Check(FindDoorCase(false,1),"No doorway available for room investigation");
                    Check(game.RequestExit(),"Could not pause room investigation setup");
                    Place(player.transform,playerBody,game.schoolGrid.sheetLocations.OrderByDescending(p=>Vector3.Distance(p,a)).First());
                    monster.Begin(game,player.transform,EscapeGame.Mode.Medium);Place(monster.transform,monsterBody,a);
                    startPosition=monster.transform.position;roomDelay.SetValue(monster,0f);
                    Check(game.CancelExit(),"Could not resume room investigation");Next(42);
                }
                if(stage==42)
                {
                    door=(Door)lastDoor.GetValue(monster);
                    if(door){center=door.DoorwayCenter;normal=door.transform.forward;Next(41);}
                    else if(Time.time-since>2f)throw new Exception("Monster did not select a nearby room on its own");
                }
                if(stage==41)
                {
                    float startSide=Vector3.Dot(startPosition-center,normal),nowSide=Vector3.Dot(monster.transform.position-center,normal);
                    if(startSide*nowSide<0 && Mathf.Abs(nowSide)>.35f)
                    {Check(door.IsOpen,"Monster entered room without opening its door");Debug.Log("ROOM_INVESTIGATION_PASS");Next(5);}
                    else if(Time.time-since>10f)throw new Exception("Monster did not investigate room: "+door.name+" pos="+monster.transform.position);
                }
                if(stage==5)
                {
                    bool wide=doorCase>=2;int side=doorCase%2==0?1:-1;
                    Check(FindDoorCase(wide,side),"Could not find a usable test doorway for case "+doorCase);
                    Check(game.RequestExit(),"Could not pause for door setup");door.OpenForMonster();Next(6);
                }
                if(stage==6 && Time.time-since>.55f)
                {
                    Check(door.IsFullyOpen,"Test door did not open");
                    monster.Begin(game,player.transform,EscapeGame.Mode.Medium);
                    Place(monster.transform,monsterBody,a);Place(player.transform,playerBody,b);Face(b-a);
                    Check(!Physics.Linecast(monster.transform.position+Vector3.up*1.2f,player.transform.position+Vector3.up*1.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),"Test doorway blocks open sight");
                    Check(game.CancelExit(),"Could not resume door test");Next(7);
                }
                if(stage==7 && Time.time-since>.18f)
                {
                    Check(monster.IsChasing,"Monster did not see player through open doorway");
                    door.Interact();opened=false;startPosition=monster.transform.position;
                    Debug.Log("DOOR_CASE_START case="+doorCase+" door="+door.name+" start="+a+" target="+b);Next(8);
                }
                if(stage==8)
                {
                    if(door.IsOpen)opened=true;
                    float startSide=Vector3.Dot(a-center,normal),nowSide=Vector3.Dot(monster.transform.position-center,normal);
                    if(startSide*nowSide<0 && Mathf.Abs(nowSide)>.65f && monster.IsChasing)
                    {
                        Check(opened,"Monster crossed without reopening the door");
                        Debug.Log("DOOR_CASE_PASS case="+doorCase+" moved="+Vector3.Distance(startPosition,monster.transform.position));
                        doorCase++;if(doorCase<4)Next(5);else Next(9);
                    }
                    else if(Time.time-since>9f)
                        throw new Exception("Door crossing timed out case="+doorCase+" open="+door.IsOpen+" state="+monster.State+" pos="+monster.transform.position+" start="+a+" goal="+b);
                }
                if(stage==9)
                {
                    monster.graceSeconds=100;monster.Begin(game,player.transform,EscapeGame.Mode.Medium);
                    foreach(var sheet in UnityEngine.Object.FindObjectsByType<Sheet>().ToArray())
                    {
                        Check(game.OpenSheet(sheet.gameObject),"Could not open sheet for final-hunt test");
                        for(int i=0;i<3;i++)
                        {Check(game.SubmitAnswer(game.CurrentCorrectAnswer),"Could not answer sheet");Check(game.ContinueQuestion(),"Could not continue sheet");}
                    }
                    Check(game.ReadyToExit && monster.IsFinalHunt && monster.CurrentMoveSpeed>5.2f,"Final sheet did not activate fast pursuit");
                    Check(FindDoorCase(false,1),"No doorway available for final hunt");
                    Place(monster.transform,monsterBody,a);Place(player.transform,playerBody,b);Face(a-b);
                    Check(!(bool)canSee.Invoke(monster,null),"Final-hunt test door did not block normal vision");
                    startPosition=monster.transform.position;Next(10);
                }
                if(stage==10)
                {
                    float startSide=Vector3.Dot(startPosition-center,normal),nowSide=Vector3.Dot(monster.transform.position-center,normal);
                    if(startSide*nowSide<0 && Mathf.Abs(nowSide)>.35f)
                    {
                        Check(monster.IsChasing && door.IsOpen,"Final hunt did not find and pursue player through door");
                        Vector3 direction=(b-monster.transform.position);direction.y=0;direction.Normalize();
                        Place(player.transform,playerBody,monster.transform.position+direction*.65f);
                        Camera.main.transform.rotation=Quaternion.LookRotation(-direction);
                        cameraStart=Camera.main.transform.position;Next(11);
                    }
                    else if(Time.time-since>9f)throw new Exception("Final hunt failed to reach player behind the door");
                }
                if(stage==11 && Time.time-since>.25f)
                {
                    Check(game.Caught && game.IsJumpScarePlaying,"Capture did not start jumpscare");
                    cameraStart=Camera.main.transform.position;Next(110);
                }
                if(stage==110 && Time.time-since>.15f)
                {
                    Check(Vector3.Distance(cameraStart,Camera.main.transform.position)>.02f,"Capture did not pull camera toward monster");Next(12);
                }
                if(stage==12 && Time.time-since>1.5f)
                {
                    Check(game.Caught && !game.IsJumpScarePlaying,"Capture did not reach the result screen");
                    Check(!error,"Runtime error during monster checks");
                    Debug.Log("MONSTER_BEHAVIOUR_PLAYMODE_PASS 360 sight, occlusion, room search, final hunt, camera jumpscare, audio, 4 doorway crossings");Finish(0);
                }
            }
            catch(Exception ex){Debug.LogException(ex);Finish(1);}
        }
        static void Finish(int code)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            SessionState.SetBool("MonsterBehaviourTestPending",false);EditorApplication.Exit(code);
        }
    }
}
