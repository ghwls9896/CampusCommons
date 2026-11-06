using System;
using System.IO;
using System.Linq;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEngine;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class LibraryInteriorValidation
    {
        const string Request = "Tools/library-interior-test.request";
        const string Pending = "LibraryInteriorValidationPending";
        static int stage; static float at, angle, lastFixed; static Player player; static CharacterController body;
        static LocalTimeLighting clock; static EscapeGame escape; static bool loggedError;
        [InitializeOnLoadMethod] static void Init()
        {
            EditorApplication.delayCall += () => {
                if (File.Exists(Request))
                {
                    File.Delete(Request);
                    if (EditorApplication.isPlaying) { File.WriteAllText("Tools/library-interior-validation.txt", "FAIL: editor is already playing"); return; }
                    SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
                }
                if (SessionState.GetBool(Pending,false)) { EditorApplication.update -= Tick; EditorApplication.update += Tick; Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog; }
            };
        }
        static void OnLog(string message,string stack,LogType type) { if (type == LogType.Error || type == LogType.Exception) loggedError = true; }
        static void Check(bool condition,string message) { if (!condition) throw new Exception(message); }
        static void Next(int value) { stage=value; at=Time.time; }
        static Vector3 At(float radius,float degrees,float y) { float a=degrees*Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a)*radius,y,Mathf.Cos(a)*radius); }
        static void Tick()
        {
            if (!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || Time.timeSinceLevelLoad < 1f) return;
            try
            {
                if (stage == 0)
                {
                    player=UnityEngine.Object.FindAnyObjectByType<Player>(); clock=UnityEngine.Object.FindAnyObjectByType<LocalTimeLighting>(); escape=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();
                    Check(player && clock && escape,"Player, clock, or escape flow missing"); body=player.GetComponent<CharacterController>(); Check(body && body.enabled,"Player controller missing");
                    Check(GameObject.Find("Grand double-ring chandelier") && GameObject.Find("Stair handrail collisions"),"New interior objects missing");
                    Check(GameObject.Find("Stair handrail collisions").GetComponentsInChildren<BoxCollider>().Length==54,"Expected 54 stair rail collision segments");
                    Check(clock.sun && clock.interiorLights.Length >= 9,"Lighting references incomplete");
                    float baseline=clock.interiorLights[0].intensity;
                    clock.ApplyHour(12); float daySun=clock.sun.intensity, dayLamp=clock.interiorLights[0].intensity, daySky=RenderSettings.skybox.GetFloat("_Exposure");
                    Render("day-lounge.png");
                    clock.ApplyHour(0); float nightSun=clock.sun.intensity, nightLamp=clock.interiorLights[0].intensity, nightSky=RenderSettings.skybox.GetFloat("_Exposure");
                    Render("night-lounge.png");
                    Check(daySun>.1f && nightSun<.01f && nightLamp>dayLamp*1.8f && nightSky<daySky*.2f,"Day/night lighting comparison failed");
                    clock.RefreshNow(); Check(baseline>=0,"Invalid initial lighting");
                    // Verify colliders actually stop a character at several heights and on both flights.
                    foreach(float testAngle in new[] {-125f,-45f,25f,85f,165f,245f})
                    {
                        float rise = testAngle < 60f ? (testAngle+150f)/210f*4.8f : 4.8f+(testAngle-60f)/210f*4.8f;
                        Vector3 start=At(12.65f,testAngle,rise+.08f);
                        body.enabled=false; player.transform.position=start; body.enabled=true; Physics.SyncTransforms();
                        Vector3 inward=At(1f,testAngle,0).normalized;
                        var flags=body.Move(-inward*2f);
                        Check((flags & CollisionFlags.Sides)!=0 && new Vector2(player.transform.position.x,player.transform.position.z).magnitude>12.0f,"Character passed through stair handrail at "+testAngle);
                    }
                    body.enabled=false;player.transform.position=new Vector3(0,.08f,-9.3f);body.enabled=true;Physics.SyncTransforms();
                    var seat=UnityEngine.Object.FindObjectsByType<Seat>().First(s=>s.name=="Study Chair Seat 01");
                    body.enabled=false;player.transform.position=seat.transform.position+new Vector3(0,-.5f,-.9f);body.enabled=true;Physics.SyncTransforms();
                    Check(player.TrySit(seat) && seat.occupant==player,"Study chair interaction failed");player.Stand();Check(body.enabled && !seat.occupant,"Standing failed");
                    var portal=UnityEngine.Object.FindObjectsByType<Portal>().First(p=>p.destination==Portal.Space.Mafia);
                    Check(player.TryUsePortal(portal) && player.currentSpace==Portal.Space.Mafia,"Mafia portal failed");
                    Check(player.RequestRoomExit() && player.ConfirmRoomExit() && player.currentSpace==Portal.Space.Library,"Mafia return failed");
                    Next(1);
                }
                else if (stage == 1 && Time.time-at>1.5f)
                {
                    var portal=UnityEngine.Object.FindObjectsByType<Portal>().First(p=>p.destination==Portal.Space.Quiz);
                    Check(player.TryUsePortal(portal) && player.currentSpace==Portal.Space.Quiz,"Quiz portal failed");
                    Check(player.RequestRoomExit() && player.ConfirmRoomExit() && player.currentSpace==Portal.Space.Library,"Quiz return failed"); Next(2);
                }
                else if (stage == 2 && Time.time-at>1.5f)
                {
                    Check(escape.BeginSelection(player),"Escape mode selection failed");
                    clock.RefreshNow(); Check(RenderSettings.skybox && !RenderSettings.skybox.name.Contains("runtime"),"School did not restore its original sky");
                    Check(escape.ChooseMode(EscapeGame.Mode.Easy) && escape.IsRunning,"Escape mode start failed");
                    escape.ExitToLobby(); Check(player.enabled && player.currentSpace==Portal.Space.Library,"Escape return failed");clock.RefreshNow();
                    player.enabled=false;angle=-152f;lastFixed=-1;body.enabled=false;player.transform.position=At(13.2f,angle,.08f);body.enabled=true;Physics.SyncTransforms(); Next(3);
                }
                else if (stage == 3 && Time.time-at>.5f)
                {
                    if (Time.fixedTime>lastFixed)
                    {
                        lastFixed=Time.fixedTime; angle=Mathf.Min(270,angle+.9f);var desired=At(13.2f,angle,player.transform.position.y);
                        var delta=desired-player.transform.position;delta.y=-.035f;body.Move(delta);
                        Check(new Vector2(player.transform.position.x-desired.x,player.transform.position.z-desired.z).magnitude<.8f,"Stair route blocked at "+angle+" position="+player.transform.position);
                    }
                    if(angle>=59 && angle<61) Check(player.transform.position.y>4.4f,"First staircase level not reached");
                    if(angle>=270)
                    {
                        Check(player.transform.position.y>9.2f,"Top floor not reached");Check(!loggedError,"Unity reported a runtime error");
                        File.WriteAllText("Tools/library-interior-validation.txt","PASS: day/night lighting; six physical railing impacts; two-floor staircase; study chair; mafia and quiz portal returns; escape mode and original school lighting.\n");Finish();
                    }
                    Check(Time.time-at<25,"Stair traversal timed out");
                }
            }
            catch(Exception e) { File.WriteAllText("Tools/library-interior-validation.txt","FAIL: "+e);Debug.LogException(e);Finish(); }
        }
        static void Finish() { EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Pending,false);EditorApplication.ExitPlaymode(); }
        static void Render(string file)
        {
            Directory.CreateDirectory("Design/InteriorUpdate");var go=new GameObject("Test capture camera");var cam=go.AddComponent<Camera>();
            cam.transform.position=new Vector3(-2,5,-13);cam.transform.LookAt(new Vector3(0,9,2));cam.fieldOfView=70;cam.farClipPlane=150;cam.clearFlags=CameraClearFlags.Skybox;
            var rt=new RenderTexture(1280,800,24){antiAliasing=2};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var img=new Texture2D(1280,800,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,800),0,0);img.Apply();File.WriteAllBytes("Design/InteriorUpdate/"+file,img.EncodeToPNG());
            cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(img);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
