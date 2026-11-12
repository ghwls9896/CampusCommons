using System;using System.IO;using System.Linq;using System.Threading.Tasks;using CampusCommons.Account;using UnityEngine;
namespace CampusCommons.Webcam {
 [Serializable]public class StudySyncRequest {public string action="sync",session_id;public long started_ms,ended_ms;public StudySegment[] segments;}
 [Serializable]public class StudySummaryRequest {public string action="summary";}
 [Serializable]public class StudyHistory {public string session_id;public long started_ms,ended_ms,study_ms,phone_ms,other_ms,away_ms,drowsy_ms;}
 [Serializable]public class StudyReply {public string user_id,session_id;public long day_ms,today_ms;public int ack_sequence;public StudyHistory[] sessions;}
 public sealed class StudyRecordService:MonoBehaviour {
  public static StudyRecordService Instance {get;private set;}
  public StudyTracker Tracker {get;private set;}
  public string SyncStatus {get;private set;}="기록 준비 중";
  public StudyHistory[] History {get;private set;}=Array.Empty<StudyHistory>();
  public bool Available=>store!=null&&!storageFailed;
  StudyStore store;StudyRecord active;double anchor,harvested,nextSave,nextSync;long utcAnchor;bool syncing,storageFailed;string owner;long summaryDay=-1;
  public long SessionMilliseconds {get;private set;}
  public long TodayMilliseconds=>owner==null||store==null?0:store.Today(owner,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Instance=null;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Install(){if(!Instance)new GameObject("Study records").AddComponent<StudyRecordService>();}
  void Awake(){Instance=this;DontDestroyOnLoad(gameObject);try{store=StudyStore.Load(Path.Combine(Application.persistentDataPath,"study-records.xml"));
    // A crash cannot infer the missing tail. Close recovered sessions at their final saved evidence.
    foreach(var r in store.Data.sessions)if(r.ended_ms==0)r.ended_ms=r.segments.Count==0?r.started_ms:r.segments[r.segments.Count-1].end_ms;
    Persist();SyncStatus="로그인 후 기록을 불러옵니다.";
   }catch{storageFailed=true;SyncStatus="기록 파일을 읽을 수 없습니다. 측정을 시작하지 않았습니다.";}}
  public bool Begin(){var gate=AccountGate.Instance;if(!Available||!gate||!gate.Ready||!gate.Session.IsAuthenticated)return false;
   Finish();owner=gate.Session.UserId;utcAnchor=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();anchor=Time.realtimeSinceStartupAsDouble;harvested=anchor;
   active=store.Begin(owner,utcAnchor);Tracker=new StudyTracker();SessionMilliseconds=0;Persist();return Available;}
  public void Observe(double captured,double[] scores){if(active==null)return;var gate=AccountGate.Instance;
   if(!gate||!gate.Ready||gate.Session.UserId!=active.user_id){Finish();return;}
   Tracker.Observe(captured,scores);Harvest();SessionMilliseconds=(long)Math.Round(Tracker.StudySeconds*1000);}
  void Harvest(){if(active==null||Tracker==null)return;foreach(var x in Tracker.Intervals){if(x.End<=harvested)continue;double start=Math.Max(x.Start,harvested);
    store.Append(active,utcAnchor+(long)Math.Round((start-anchor)*1000),utcAnchor+(long)Math.Round((x.End-anchor)*1000),x.State);harvested=x.End;}
   // Only the final interval is needed for further coalescing.
   if(Tracker.Intervals.Count>1)Tracker.Intervals.RemoveRange(0,Tracker.Intervals.Count-1);
  }
  public void Finish(){if(active==null)return;Harvest();Tracker.Break();active.ended_ms=active.segments.Count==0?active.started_ms:active.segments[active.segments.Count-1].end_ms;active=null;Persist();nextSync=0;}
  void Persist(){if(store==null||storageFailed)return;try{store.Save();}catch{storageFailed=true;SyncStatus="기록 저장에 실패했습니다. 저장 공간을 확인해 주세요.";}}
  void Update(){var gate=AccountGate.Instance;string current=gate&&gate.Ready&&gate.Session.IsAuthenticated?gate.Session.UserId:null;
   if(current!=owner){Finish();owner=current;summaryDay=-1;History=Array.Empty<StudyHistory>();SessionMilliseconds=0;nextSync=0;}
   if(storageFailed){var webcam=FindAnyObjectByType<LobbyStudyWebcam>();if(webcam&&webcam.IsRunning)webcam.StopStudy();return;}
   double now=Time.realtimeSinceStartupAsDouble;if(active!=null&&now>=nextSave){Persist();nextSave=now+5;}
   if(owner!=null&&!syncing&&now>=nextSync){nextSync=now+30;_ = Sync();}
  }
  public void RetrySync(){nextSync=0;}
  async Task Sync(){syncing=true;string user=owner;var account=AccountGate.Instance.Session;
   try{
    SyncStatus="기록 동기화 중";long day=StudyTime.DayStart(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    // Send all pending local records first: a summary before a lost-ACK retry would double count them.
    foreach(var r in store.Data.sessions.Where(x=>x.user_id==user).ToArray()){
     if(r.acked==r.segments.Count-1&&(r.ended_ms==0||r.end_acked))continue;
     var batch=store.Batch(r);Persist();if(!Available)return;
     bool finalBatch=batch.Length==0||batch[batch.Length-1].sequence==r.segments.Count-1;
     long end=finalBatch?r.ended_ms:0;
     var result=await account.StudyCallAsync<StudyReply>(user,new StudySyncRequest{session_id=r.session_id,started_ms=r.started_ms,ended_ms=end,segments=batch});
     if(owner!=user)return;
     if(result==null||result.user_id!=user||result.session_id!=r.session_id||result.ack_sequence!=(batch.Length==0?r.acked:batch[batch.Length-1].sequence))throw new InvalidDataException();
     store.Acknowledge(r,result.ack_sequence,result.day_ms,result.today_ms);if(end>0)r.end_acked=true;summaryDay=result.day_ms;History=result.sessions??Array.Empty<StudyHistory>();Persist();
     if(r.acked<r.segments.Count-1){nextSync=0;SyncStatus="남은 기록 동기화 중";return;}
    }
    if(summaryDay!=day){var result=await account.StudyCallAsync<StudyReply>(user,new StudySummaryRequest());if(owner!=user)return;
     if(result==null||result.user_id!=user)throw new InvalidDataException();store.SetTotal(user,result.day_ms,result.today_ms);summaryDay=result.day_ms;History=result.sessions??Array.Empty<StudyHistory>();Persist();}
    if(owner==user)SyncStatus="기록 저장됨";
   }catch(OperationCanceledException){if(owner==user)SyncStatus="기기에 보관 중 · 로그인 후 동기화";}
   catch(AccountException e){if(owner==user)SyncStatus=e.Status==409?"다른 기기 기록과 겹칩니다 · 기기에 보관 중":"기기에 보관 중 · 연결되면 재전송";}
   catch{if(owner==user)SyncStatus="기기에 보관 중 · 동기화 재시도 예정";}
   finally{syncing=false;}
  }
  void OnApplicationQuit(){Finish();Persist();}
  bool showHistory;Vector2 scroll;GUIStyle ui;Font font;
  void LateUpdate(){if(owner!=null&&Input.GetKeyDown(KeyCode.F9))showHistory=!showHistory;if(showHistory&&owner!=null&&Application.isFocused){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
  void OnGUI(){if(owner==null)return;
   if(ui==null){font=Font.CreateDynamicFontFromOSFont(new[]{"Apple SD Gothic Neo","Malgun Gothic","Arial"},16);ui=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};}
   var old=GUI.matrix;GUI.matrix=Matrix4x4.identity;
   var head=new Rect(Mathf.Max(8,Screen.width-320),12,304,64);GUI.Box(head,GUIContent.none);
   GUI.Label(new Rect(head.x+10,head.y+4,284,26),"오늘 공부  "+StudyTime.Clock(TodayMilliseconds),ui);
   if(GUI.Button(new Rect(head.x+10,head.y+31,284,26),"공부 기록 (F9)"))showHistory=!showHistory;
   if(showHistory){float w=Mathf.Min(600,Screen.width-24),h=Mathf.Min(500,Screen.height-100);
    var rect=new Rect((Screen.width-w)/2,80,w,h);GUI.Box(rect,GUIContent.none);GUILayout.BeginArea(new Rect(rect.x+14,rect.y+12,w-28,h-24));
    GUILayout.Label("내 공부 기록 · 한국시간 기준",ui);GUILayout.Label(SyncStatus,ui);
    GUILayout.BeginHorizontal();if(GUILayout.Button("동기화"))RetrySync();if(GUILayout.Button("닫기 (F9)"))showHistory=false;GUILayout.EndHorizontal();
    scroll=GUILayout.BeginScrollView(scroll);
    if(History.Length==0)GUILayout.Label("저장된 기록이 없거나 아직 동기화되지 않았습니다.",ui);
    foreach(var row in History){var date=DateTimeOffset.FromUnixTimeMilliseconds(row.started_ms).ToOffset(TimeSpan.FromHours(9));
     GUILayout.Label(date.ToString("MM/dd HH:mm")+"  공부 "+StudyTime.Clock(row.study_ms),ui);
     GUILayout.Label("휴대폰 "+StudyTime.Clock(row.phone_ms)+" · 자리비움 "+StudyTime.Clock(row.away_ms)+"\n자는중 "+StudyTime.Clock(row.drowsy_ms)+" · 다른활동 "+StudyTime.Clock(row.other_ms),ui);GUILayout.Space(12);}
    GUILayout.EndScrollView();GUILayout.EndArea();}
   GUI.matrix=old;
  }
  void OnDestroy(){Finish();store?.Dispose();if(font)Destroy(font);if(Instance==this)Instance=null;}
 }
}
