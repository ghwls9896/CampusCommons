using System;
using System.IO;
using System.Linq;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.StudyImmersive.Editor
{
    public static class ImmersiveValidation
    {
        static EscapeModeFlow flow;
        static LibraryPlayer player;
        static CharacterController controller;
        static int state,modeIndex;
        static float started,lastFixed;
        static bool error,searchError;
        static readonly EscapeModeFlow.Mode[] Modes={EscapeModeFlow.Mode.Easy,EscapeModeFlow.Mode.Medium,EscapeModeFlow.Mode.Hard,EscapeModeFlow.Mode.TimeAttack};
        static readonly int[] Expected={3,5,7,5};
        public static void Run()
        {
            SessionState.SetBool("LivingValidation",false);
            EditorSceneManager.OpenScene(ImmersiveSceneBuilder.ScenePath);
            SessionState.SetBool("ImmersiveValidationPending",true);
            EditorApplication.EnterPlaymode();
        }
        public static void RunLiving()
        {
            SessionState.SetBool("LivingValidation",true);
            EditorSceneManager.OpenScene(WorldPolish.OutScene);
            SessionState.SetBool("ImmersiveValidationPending",true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.delayCall+=()=>
            {
                if (!SessionState.GetBool("ImmersiveValidationPending",false)) return;
                state=0;modeIndex=0;error=false;searchError=false;
                Application.logMessageReceived+=OnLog; EditorApplication.update+=Tick;
            };
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) {searchError=true;return;}
            if (type==LogType.Error || type==LogType.Exception) error=true;
        }
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        static void Next(int value) {state=value;started=Time.time;lastFixed=-1;}
        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.timeSinceLevelLoad<2) return;
            try
            {
                if(state==0)
                {
                    Check(!error,"Runtime error before checks");
                    player=UnityEngine.Object.FindFirstObjectByType<LibraryPlayer>();
                    flow=UnityEngine.Object.FindFirstObjectByType<EscapeModeFlow>();
                    Check(player && flow && Camera.main,"Player, camera or escape flow missing");
                    controller=player.GetComponent<CharacterController>();
                    Check(controller && controller.isGrounded,"Player did not spawn grounded");
                    Check(RenderSettings.skybox && AssetDatabase.GetAssetPath(RenderSettings.skybox).Contains("StudyImmersive"),"Owned skybox was not applied");
                    Check(!flow.GetComponent<LibraryPortal>(),"The original direct escape portal should be removed");
                    Check(!flow.schoolReturnTrigger.activeSelf && !flow.schoolReturnVisual.activeSelf,"School return portal must be hidden");
                    Check(flow.notebookPositions!=null && flow.notebookPositions.Length==7,"Seven reachable notebook locations required");
                    var names=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
                    bool living=SessionState.GetBool("LivingValidation",false);
                    int tableCount=living?6:4,seatCount=living?24:16;
                    Check(names.Count(t=>t.name=="Study table")==tableCount,"Wrong number of study tables");
                    Check(names.Count(t=>t.name=="Study chair")==seatCount,"Wrong number of study chairs");
                    var study=UnityEngine.Object.FindObjectsByType<LibrarySeat>(FindObjectsSortMode.None).Where(s=>s.name=="Study chair seat").ToArray();
                    Check(study.Length==seatCount && study.All(s=>Mathf.Abs(s.transform.position.y-.5f)<.01f),"Study seat height must be 0.5m");
                    foreach(var table in names.Where(t=>t.name=="Study table" || t.name=="Quiz desk"))
                    {
                        var pieces=table.GetComponentsInChildren<Renderer>();
                        Check(pieces.Length>0,"Table has no visible geometry");
                        var visible=pieces[0].bounds;foreach(var piece in pieces) visible.Encapsulate(piece.bounds);
                        var solid=table.GetComponent<BoxCollider>().bounds;
                        Check(Mathf.Abs(visible.max.y-solid.max.y)<.04f,"Visible table and collider heights differ at "+table.name+": "+visible.max.y+" vs "+solid.max.y);
                        Check(visible.max.y-.5f>=.2f && visible.max.y-.5f<=.32f,"Table to seat height is unsuitable");
                    }
                    Check(names.Count(t=>t.name=="Indoor plant")>=(living?4:8),"Detailed indoor plants missing");
                    var plant=names.First(t=>t.name=="Indoor plant");
                    Check(plant.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>m && m.shader.name=="StudyImmersive/RealFoliage")),"Plant leaves are not using the foliage material");
                    Check(names.Count(t=>t.name=="Detailed book set")>=4,"Detailed books missing");
                    if(living)
                    {
                        Check(names.Count(t=>t.name=="Detailed indoor tree")==4,"Detailed indoor trees missing");
                        Check(names.Count(t=>t.name.StartsWith("Owned Grass_"))>=30,"Owned grass meshes missing");
                        Check(names.Count(t=>t.name.StartsWith("Owned Fern_"))>=15,"Owned fern meshes missing");
                        var top=GameObject.Find("LIB_Mafia__wood__SOLID");
                        Check(top && Mathf.Abs(top.GetComponent<Renderer>().bounds.max.y-.75f)<.02f,"Mafia table was not corrected to 0.75m");
                        Check(names.Count(t=>t.name=="Room detailed bookcase")==4,"Room bookcases missing");
                    }
                    controller.enabled=false; player.transform.position=new Vector3(11,.08f,5.5f); controller.enabled=true; Physics.SyncTransforms();
                    Next(1);
                }
                if(state==1 && Time.time-started>.6f) Next(2);
                if(state==2)
                {
                    if(Time.fixedTime>lastFixed) {lastFixed=Time.fixedTime;controller.Move(new Vector3(.09f,-.02f,0));}
                    if(flow.IsSelecting)
                    {
                        Check(!player.enabled && player.currentSpace==LibraryPortal.Space.Library,"Selection must pause the lounge controller before travel");
                        Check(flow.ChooseMode(Modes[0]),"Could not choose easy mode"); Next(3);
                    }
                    Check(Time.time-started<7,"Walking through the escape portal did not open mode selection: "+player.transform.position);
                }
                if(state==3 && Time.time-started>.6f)
                {
                    Check(flow.IsRunning && player.currentSpace==LibraryPortal.Space.Escape,"Easy mode did not enter school");
                    Check(flow.Target==Expected[0] && flow.ActiveNotebookCount==Expected[0],"Easy notebook count mismatch");
                    Check(!player.enabled && !player.transform.Find("Student character").gameObject.activeSelf,"Avatar must be hidden in first person");
                    Check(Vector3.Distance(Camera.main.transform.position,player.transform.position+Vector3.up*1.62f)<.06f,"Camera is not at first-person eye position");
                    Check(!flow.schoolReturnTrigger.activeSelf && !flow.schoolReturnVisual.activeSelf,"School portal became visible");
                    Vector3 note=flow.notebookPositions[0];
                    controller.enabled=false;player.transform.position=note+new Vector3(.7f,.08f,0);controller.enabled=true;Physics.SyncTransforms();
                    Check(flow.CollectNearest() && flow.Found==1 && flow.ActiveNotebookCount==2,"Notebook collection failed");
                    for (int i=1;i<Expected[0];i++)
                    {
                        controller.enabled=false;player.transform.position=flow.notebookPositions[i]+new Vector3(.7f,.08f,0);controller.enabled=true;Physics.SyncTransforms();
                        Check(flow.CollectNearest(),"Could not collect easy notebook "+i);
                    }
                    Check(flow.Completed && flow.Found==Expected[0] && flow.ActiveNotebookCount==0,"Completing the easy mode failed");
                    flow.ExitToLobby();
                    Check(!flow.IsRunning && player.enabled && player.currentSpace==LibraryPortal.Space.Library && player.transform.Find("Student character").gameObject.activeSelf,"ESC exit did not restore lobby state");
                    modeIndex=1; Next(4);
                }
                if(state==4 && Time.time-started>1.4f)
                {
                    Check(flow.BeginSelection(player),"Could not reopen selection for "+Modes[modeIndex]);
                    if(modeIndex==3) flow.timeLimitSeconds=.7f;
                    Check(flow.ChooseMode(Modes[modeIndex]),"Mode selection failed: "+Modes[modeIndex]);
                    Check(flow.IsRunning && flow.Target==Expected[modeIndex] && flow.ActiveNotebookCount==Expected[modeIndex],"Notebook target mismatch for "+Modes[modeIndex]);
                    Check(!flow.schoolReturnTrigger.activeSelf && !flow.schoolReturnVisual.activeSelf,"Return portal must stay hidden");
                    Next(5);
                }
                if(state==5 && Time.time-started>(modeIndex==3?1.1f:.2f))
                {
                    if(modeIndex==3) Check(flow.TimedOut && flow.Remaining<=0 && !flow.CollectNearest(),"Time attack countdown/failure is broken");
                    flow.ExitToLobby(); Check(player.enabled && !flow.IsRunning,"Lobby controller did not resume");
                    modeIndex++; if(modeIndex==Modes.Length) Next(6);else Next(4);
                }
                if(state==6)
                {
                    Check(!error,"Runtime error during mode checks");
                    Directory.CreateDirectory("Design/Immersive");
                    string report=SessionState.GetBool("LivingValidation",false)?"Design/Immersive/living-validation.txt":"Design/Immersive/validation.txt";
                    File.WriteAllText(report,"PASS\nUnity "+Application.unityVersion+"\n"+
                        (SessionState.GetBool("LivingValidation",false)?"Owned skybox, trees, grass and ferns; 6 tables, 24 chairs and corrected mafia table; ":"Owned skybox; 4 study tables and 16 raised chairs; detailed plant and book props; ")+
                        "Walk into escape portal; mode menu; 3/5/7/5 notebooks; first-person camera and hidden avatar; hidden return portal; complete easy mode; time attack expiry; return and restore third person.\n"+
                        "Escape key input maps to the tested ExitToLobby method; real multiplayer and Baldi chase AI are not implemented.\nEditor Search exception: "+searchError+"\n");
                    Debug.Log("IMMERSIVE_PLAYMODE_PASS");Finish(0);
                }
            }
            catch(Exception ex) {Debug.LogException(ex);Finish(1);}
        }
        static void Finish(int code)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            SessionState.SetBool("ImmersiveValidationPending",false);EditorApplication.Exit(code);
        }
        public static void RenderPreviews()
        {
            EditorSceneManager.OpenScene(ImmersiveSceneBuilder.ScenePath);
            Directory.CreateDirectory("Design/Immersive");
            foreach(string name in new[] {"Study notebook","Detailed book set","Desk succulent","Indoor plant","Study table","Study chair"})
            {
                var obj=GameObject.Find(name);
                if (!obj) continue;
                var rs=obj.GetComponentsInChildren<Renderer>();
                if (rs.Length==0) continue;
                var bounds=rs[0].bounds;foreach(var renderer in rs) bounds.Encapsulate(renderer.bounds);
                Debug.Log("IMMERSIVE_BOUNDS "+name+" host="+obj.transform.position+" "+bounds);
            }
            Render("lounge.png",new Vector3(-3,2.7f,-10),new Vector3(4,3.5f,1),72);
            Render("study-close.png",new Vector3(-6,1.65f,-3.8f),new Vector3(-3.4f,.85f,-.3f),58);
            Render("portals.png",new Vector3(4.5f,2.5f,-7),new Vector3(14,3.2f,0),72);
            Render("mafia.png",new Vector3(100,2.5f,-6.3f),new Vector3(100,1.6f,1),68);
            Render("quiz.png",new Vector3(200,2.3f,-6.2f),new Vector3(200,2,3),64);
            var flow=UnityEngine.Object.FindFirstObjectByType<EscapeModeFlow>();
            if (!flow) throw new Exception("No escape portal");
            var p=flow.schoolArrival;
            Render("school-first-person.png",p+new Vector3(0,1.62f,0),p+new Vector3(0,1.5f,4),68);
            Debug.Log("IMMERSIVE_RENDERS_OK");
        }
        public static void BuildAndRender()
        {
            ImmersiveSceneBuilder.Build();
            RenderPreviews();
        }
        static void Render(string file,Vector3 position,Vector3 target,float fov)
        {
            var go=new GameObject("Preview camera");var camera=go.AddComponent<Camera>();
            camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=fov;
            camera.nearClipPlane=.08f;camera.farClipPlane=120;camera.clearFlags=CameraClearFlags.Skybox;
            var rt=new RenderTexture(1440,900,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
            File.WriteAllBytes("Design/Immersive/"+file,image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
