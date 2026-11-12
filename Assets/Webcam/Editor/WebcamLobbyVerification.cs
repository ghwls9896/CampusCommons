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
    static float stageAt; static Seat seatForTest; static int stage;static Player player;static LobbyStudyWebcam study;
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
                Check(player && study,"Player/bootstrap missing");study.SendMessage("OnApplicationFocus",false);Check(!study.IsRunning,"Analysis must never auto-start");
                var seat=UnityEngine.Object.FindObjectsByType<Seat>().First(x=>!x.occupant && Mathf.Abs(x.transform.position.x)<30);
                seatForTest=seat;
                var body=player.GetComponent<CharacterController>();body.enabled=false;player.transform.position=seat.transform.position-Vector3.up*.6f;body.enabled=true;
                Check(player.TrySit(seat),"Actual lobby seat failed");stage=1;stageAt=Time.time;
            }else if(stage==1){
                if(Time.time-stageAt<.2f)return;
                Check(study.IsAvailable,"Seated player should see Start");
                Check(!study.IsRunning,"Seating must not start inference");
                var end=typeof(LobbyStudyWebcam).GetMethod("EndStudy");
                Check(end!=null,"Exit action must release the actual seat");
                end.Invoke(study,null);Check(!seatForTest.occupant,"Exit retained seat occupant");stage=2;stageAt=Time.time;
            }else if(stage==2){
                if(Time.time-stageAt<.2f)return;
                Check(!study.IsAvailable && !study.IsRunning,"Standing must hide/stop session");
                var body=player.GetComponent<CharacterController>();body.enabled=false;player.transform.position=seatForTest.transform.position-Vector3.up*.6f;body.enabled=true;
                Check(player.TrySit(seatForTest),"Reseating failed");stage=3;stageAt=Time.time;
            }else if(stage==3){
                if(Time.time-stageAt<.2f)return;
                Check(study.IsAvailable,"Reseating panel missing");
                player.enabled=false;player.GetComponent<CharacterController>().enabled=false;stage=4;stageAt=Time.time;
            }else{
                if(Time.time-stageAt<.2f)return;
                Check(!seatForTest.occupant && !study.IsAvailable && !study.IsRunning,"Account block retained occupied seat/session");
                Check(!player.GetComponent<CharacterController>().enabled,"Seat cleanup re-enabled blocked player");
                Check(LobbyStudyWebcam.DecisionState("{\"ok\":true,\"mode\":\"fast\",\"state\":\"drowsy\",\"probabilities\":{\"studying\":0.1,\"phone\":0.1,\"other\":0.1,\"away\":0.1,\"drowsy\":0.6}}") == "drowsy","Fast response decoding");
                Debug.Log("WEBCAM_LOBBY_PLAY_PASS: actual seat, manual-analysis guard, exit releases seat; account-block cleanup; camera disabled");Finish(0);
            }
        }catch(Exception e){Debug.LogException(e);Finish(1);}
    }
    static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.Exit(code);}
}
