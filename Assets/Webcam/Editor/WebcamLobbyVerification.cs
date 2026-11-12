using System;
using System.Linq;
using CampusCommons.GrandLibrary;
using CampusCommons.Webcam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WebcamLobbyVerification
{
    const string Pending="WebcamLobbyVerification.Pending";
    static int stage;static Player player;static LobbyStudyWebcam study;
    public static void Run()
    {
        StudySessionChecks.Run();
        EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]static void Resume()
    {
        EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Pending,false))EditorApplication.update+=Tick;};
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        try{
            if(stage==0){
                if(Time.timeSinceLevelLoad<2)return;
                player=UnityEngine.Object.FindAnyObjectByType<Player>();study=UnityEngine.Object.FindAnyObjectByType<LobbyStudyWebcam>();
                Check(player && study,"Player/bootstrap missing");Check(!study.IsRunning,"Camera must never auto-start");
                var seat=UnityEngine.Object.FindObjectsByType<Seat>().First(x=>!x.occupant && Mathf.Abs(x.transform.position.x)<30);
                var body=player.GetComponent<CharacterController>();body.enabled=false;player.transform.position=seat.transform.position-Vector3.up*.6f;body.enabled=true;
                Check(player.TrySit(seat),"Actual lobby seat failed");stage=1;
            }else if(stage==1){
                Check(study.IsAvailable,"Seated player should see Start");
                Check(!study.IsRunning,"Seating must not open camera");
                player.Stand();stage=2;
            }else{
                Check(!study.IsAvailable && !study.IsRunning,"Standing must hide/stop session");
                Check(LobbyStudyWebcam.DecisionState("{\"ok\":true,\"mode\":\"fast\",\"state\":\"drowsy\",\"probabilities\":{\"studying\":0.1,\"phone\":0.1,\"other\":0.1,\"away\":0.1,\"drowsy\":0.6}}") == "drowsy","Fast response decoding");
                Debug.Log("WEBCAM_LOBBY_PLAY_PASS: actual seat, manual-start guard, standing; no camera accessed");Finish(0);
            }
        }catch(Exception e){Debug.LogException(e);Finish(1);}
    }
    static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.Exit(code);}
}
