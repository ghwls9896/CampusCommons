using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.GrandLibrary.Editor
{
    public static class GrandLibraryValidation
    {
        static int state,index;
        static float stateStart,lastFixed;
        static float stairAngle;
        static bool runtimeError, searchError;
        static Player player;
        static CharacterController body;
        static readonly float[] HubZ={-5.5f,0,5.5f};
        static readonly float[] RoomX={100,200,500};
        static readonly Portal.Space[] Spaces={Portal.Space.Mafia,Portal.Space.Quiz,Portal.Space.Escape};

        public static void Run()
        {
            EditorSceneManager.OpenScene(GrandLibrarySceneBuilder.ScenePath);
            SessionState.SetBool("GrandLibraryValidation",true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.delayCall+=() =>
            {
                if (!SessionState.GetBool("GrandLibraryValidation",false)) return;
                state=0; index=0; runtimeError=false; searchError=false;
                Application.logMessageReceived+=OnLog;
                EditorApplication.update+=Tick;
            };
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) { searchError=true; return; }
            if (type==LogType.Exception || type==LogType.Error) runtimeError=true;
        }
        static void Check(bool condition,string explanation)
        {
            if (!condition) throw new Exception(explanation);
        }
        static void Next(int newState) { state=newState; stateStart=Time.time; lastFixed=-1; }
        static bool Step(Vector3 delta)
        {
            if (Time.fixedTime<=lastFixed) return false;
            lastFixed=Time.fixedTime; body.Move(delta); return true;
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.timeSinceLevelLoad<2) return;
            try
            {
                if (state==0)
                {
                    Check(!runtimeError,"Unexpected runtime error before tests");
                    player=UnityEngine.Object.FindAnyObjectByType<Player>();
                    Check(player && Camera.main,"Character and camera spawned");
                    body=player.GetComponent<CharacterController>();
                    Check(body.isGrounded,"Player is not on the library floor at "+player.transform.position);
                    var portals=UnityEngine.Object.FindObjectsByType<Portal>();
                    Check(portals.Length==6,"Expected six portal triggers, got "+portals.Length);
                    var quizSeats=Array.FindAll(UnityEngine.Object.FindObjectsByType<Seat>(),s=>s.name=="Quiz player seat");
                    Check(quizSeats.Length==2,"Quiz room must have exactly two seats");
                    Check(Physics.Raycast(new Vector3(0,3,-9),Vector3.down,out _,5),"No collision under library spawn");
                    foreach(float room in new[] {100f,200f})
                    {
                        bool found=Physics.Raycast(new Vector3(room,3,-5),Vector3.down,out RaycastHit floorHit,5);
                        Check(found && Mathf.Abs(floorHit.point.y)<.1f,"No upward-facing floor under room spawn x="+room+"; hit y="+floorHit.point.y);
                    }
                    var schoolPortal=GameObject.Find("ESCAPE ROOM TO LOUNGE");
                    var entrance=GameObject.Find("ESCAPE ROOM").GetComponent<Portal>();
                    Check(schoolPortal && entrance && Mathf.Abs(entrance.arrivalPosition.x-500)<20,"Escape portal must lead to the imported school");
                    var schoolStart=entrance.arrivalPosition;
                    Check(Physics.Raycast(schoolStart+Vector3.up*1.3f,Vector3.down,out RaycastHit schoolFloor,2.0f) && Mathf.Abs(schoolFloor.point.y)<.3f,
                          "School arrival must have a floor");
                    var school=GameObject.Find("User supplied Baldi school");
                    Check(school && school.GetComponentsInChildren<MeshRenderer>().Length>=40,"Imported Baldi school model missing");
                    var desk=GameObject.Find("Study table"); var studyChair=GameObject.Find("Study chair");
                    Check(desk && studyChair,"Study furniture missing");
                    var tableBounds=desk.GetComponent<BoxCollider>().bounds; var chairBounds=studyChair.GetComponent<BoxCollider>().bounds;
                    float gap=Mathf.Abs(tableBounds.center.z-chairBounds.center.z)-tableBounds.extents.z-chairBounds.extents.z;
                    Check(gap>=-.02f && gap<.22f,"Study chair should be close to the table; gap="+gap);
                    Check(desk.GetComponentInChildren<Renderer>().sharedMaterial.mainTexture,"Realistic furniture texture missing");
                    var model=GameObject.Find("LIB_Library__floor__SOLID");
                    Check(model && model.GetComponent<MeshRenderer>(),"Library floor mesh missing");
                    var bounds=model.GetComponent<MeshRenderer>().bounds;
                    Check(bounds.size.x>40 && bounds.size.z>40 && Mathf.Abs(bounds.center.y+.19f)<.3f,"Imported FBX scale/orientation incorrect: "+bounds);
                    Next(1);
                }
                if (state==1)
                {
                    Check(player.currentSpace==Portal.Space.Library,"Expected the player in the lounge");
                    body.enabled=false; player.transform.position=new Vector3(11.0f,.08f,HubZ[index]); body.enabled=true;
                    Physics.SyncTransforms(); Next(2);
                }
                if (state==2 && Time.time-stateStart>1.35f) Next(3);
                if (state==3)
                {
                    Step(new Vector3(.09f,-.02f,0));
                    if (player.currentSpace==Spaces[index])
                    {
                        Check(Mathf.Abs(player.transform.position.x-RoomX[index])<(index==2?20:.3f),"Wrong destination for "+Spaces[index]);
                        Debug.Log("PORTAL_OUTBOUND_OK "+Spaces[index]); Next(4);
                    }
                    Check(Time.time-stateStart<6,"Walking into portal did not reach "+Spaces[index]+"; position "+player.transform.position);
                }
                if (state==4 && Time.time-stateStart>(index==1?.05f:1.4f))
                {
                    Check(player.currentSpace==Spaces[index],"Immediate accidental return from "+Spaces[index]); Next(5);
                }
                if (state==5)
                {
                    Step(new Vector3(0,-.02f,-.09f));
                    if (player.currentSpace==Portal.Space.Library)
                    {
                        Check(Mathf.Abs(player.transform.position.z-HubZ[index])<.3f && Mathf.Abs(player.transform.position.x-11.5f)<.3f,"Return portal reached the wrong location");
                        Debug.Log("PORTAL_RETURN_OK "+Spaces[index]); index++;
                        if (index==3) Next(6); else Next(1);
                    }
                    Check(Time.time-stateStart<6,"Walking back through portal failed; position "+player.transform.position);
                }
                if (state==6)
                {
                    var quizSeat=Array.Find(UnityEngine.Object.FindObjectsByType<Seat>(),s=>s.name=="Quiz player seat");
                    Check(quizSeat,"Quiz seat missing");
                    body.enabled=false;
                    Vector3 standing=quizSeat.transform.position-Vector3.up*quizSeat.transform.position.y-Vector3.forward*1.1f+Vector3.up*.08f;
                    player.transform.position=standing; body.enabled=true; Physics.SyncTransforms();
                    Check(player.TrySit(quizSeat) && quizSeat.occupant==player && !body.enabled,"Sitting in the realistic quiz chair failed");
                    player.Stand();
                    Check(!quizSeat.occupant && body.enabled && Vector3.Distance(player.transform.position,standing)<.01f,"Standing from the quiz chair failed");
                    // Walk the full curved staircase, crossing both upper-floor heights.
                    stairAngle=-152f;
                    float angle=stairAngle*Mathf.Deg2Rad;
                    body.enabled=false;
                    player.transform.position=new Vector3(Mathf.Sin(angle)*13.2f,.08f,Mathf.Cos(angle)*13.2f);
                    body.enabled=true; Physics.SyncTransforms(); Next(7);
                }
                if (state==7 && Time.time-stateStart>.5f) Next(8);
                if (state==8)
                {
                    if (Time.fixedTime>lastFixed)
                    {
                        lastFixed=Time.fixedTime;
                        stairAngle=Mathf.Min(270,stairAngle+.9f);
                        float a=stairAngle*Mathf.Deg2Rad;
                        var desired=new Vector3(Mathf.Sin(a)*13.2f,player.transform.position.y,Mathf.Cos(a)*13.2f);
                        var delta=desired-player.transform.position; delta.y=-.035f; body.Move(delta);
                        Check(new Vector2(player.transform.position.x-desired.x,player.transform.position.z-desired.z).magnitude<.8f,
                              "Stair blocked at "+stairAngle+" degrees, position "+player.transform.position);
                    }
                    if (stairAngle>=59 && stairAngle<61)
                        Check(player.transform.position.y>4.4f,"First staircase did not reach upper floor: "+player.transform.position);
                    if (stairAngle>=270)
                    {
                        Check(player.transform.position.y>9.2f,"Second staircase did not reach top floor: "+player.transform.position);
                        Debug.Log("STAIRCASE_TWO_LEVELS_OK"); Next(9);
                    }
                    Check(Time.time-stateStart<20,"Staircase validation timed out");
                }
                if (state==9)
                {
                    Check(!runtimeError,"Unexpected runtime error during validation");
                    Directory.CreateDirectory("Design/GrandLibrary");
                    File.WriteAllText("Design/GrandLibrary/unity-validation.txt",
                        "PASS\nUnity "+Application.unityVersion+"\n"+
                        "Play Mode: imported textured user school and safe arrival; realistic furniture; study chair distance; sitting and standing; two quiz seats; all three portal trips and returns; two-floor curved staircase.\n"+
                        "Editor Search batch-mode exception separately observed: "+searchError+"\n"+
                        "Multiplayer, quiz rules, mafia rules and classroom puzzle are not implemented or tested.\n");
                    Debug.Log("GRAND_LIBRARY_PLAYMODE_PASS"); Finish(0);
                }
            }
            catch(Exception e) { Debug.LogException(e); Finish(1); }
        }
        static void Finish(int code)
        {
            EditorApplication.update-=Tick; Application.logMessageReceived-=OnLog;
            SessionState.SetBool("GrandLibraryValidation",false); EditorApplication.Exit(code);
        }
        public static void RenderPreviews()
        {
            EditorSceneManager.OpenScene(GrandLibrarySceneBuilder.ScenePath);
            Directory.CreateDirectory("Design/GrandLibrary");
            var report="";
            foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
                if (r.name.Contains("__portal_") || r.name.Contains("__floor__SOLID") || r.name.StartsWith("LIB_Mafia__wood_dark__SOLID") || r.name.StartsWith("LIB_Quiz__ivory__SOLID") || r.name.StartsWith("LIB_Escape__school_wall__SOLID"))
                    report+=r.name+"   "+r.bounds+"\n";
            File.WriteAllText("Design/GrandLibrary/model-bounds.txt",report);
            Render("unity-library.png",new Vector3(-3,2.7f,-10),new Vector3(4,4.1f,1),72);
            Render("unity-portal-side.png",new Vector3(4.5f,2.5f,-7),new Vector3(14,3.2f,0),72);
            Render("unity-mafia.png",new Vector3(100,2.5f,-6.3f),new Vector3(100,1.6f,1),68);
            Render("unity-quiz.png",new Vector3(200,2.3f,-6.2f),new Vector3(200,2,3),64);
            var schoolPortal=GameObject.Find("ESCAPE ROOM TO LOUNGE");
            if (!schoolPortal) throw new Exception("Escape room return portal is missing.");
            var p=schoolPortal.transform.position;
            Render("unity-school.png",p+new Vector3(0,1.5f,1.9f),p+new Vector3(0,1.3f,5.0f),68);
            Render("unity-school-return.png",p+new Vector3(0,1.5f,3.0f),p+new Vector3(0,1.1f,0),70);
            Debug.Log("GRAND_LIBRARY_RENDERS_OK");
        }
        public static void GenerateAndRender()
        {
            GrandLibrarySceneBuilder.Generate();
            RenderPreviews();
        }
        static void Render(string file,Vector3 position,Vector3 target,float fov)
        {
            var go=new GameObject("Preview camera"); var camera=go.AddComponent<Camera>();
            camera.transform.position=position; camera.transform.LookAt(target); camera.fieldOfView=fov;
            camera.nearClipPlane=.08f; camera.farClipPlane=90; camera.clearFlags=CameraClearFlags.Skybox;
            var texture=new RenderTexture(1440,900,24) { antiAliasing=4 }; camera.targetTexture=texture; camera.Render();
            RenderTexture.active=texture; var result=new Texture2D(1440,900,TextureFormat.RGB24,false);
            result.ReadPixels(new Rect(0,0,1440,900),0,0); result.Apply();
            File.WriteAllBytes("Design/GrandLibrary/"+file,result.EncodeToPNG());
            camera.targetTexture=null; RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(result); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
