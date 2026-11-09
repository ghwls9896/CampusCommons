using System;
using System.Collections.Generic;
using CampusCommons.GrandLibrary;
using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    // Attached to the Escape Room portal in the new scene. Keeps the existing
    // lounge controller intact and takes over only while the player is inside.
    [RequireComponent(typeof(BoxCollider))]
    public class EscapeModeFlow : MonoBehaviour
    {
        public enum Mode { Easy, Medium, Hard, TimeAttack }
        public Vector3 schoolArrival;
        public float schoolYaw;
        public GameObject schoolReturnVisual;
        public GameObject schoolReturnTrigger;
        public GameObject notebookPrefab;
        public Vector3[] notebookPositions;
        public Vector3 lobbyReturn=new Vector3(11.5f,.08f,5.5f);
        public float timeLimitSeconds=180;

        LibraryPlayer player;
        CharacterController body;
        Camera view;
        Transform avatar;
        readonly List<GameObject> notebooks=new List<GameObject>();
        bool selecting, running, completed, timedOut;
        Mode currentMode;
        float yaw, pitch, falling, elapsed, rearmAt;
        int found, target;
        GUIStyle title, text, choice;
        Font koreanFont;
        bool korean;

        public bool IsSelecting => selecting;
        public bool IsRunning => running;
        public bool Completed => completed;
        public bool TimedOut => timedOut;
        public int Found => found;
        public int Target => target;
        public float Remaining => Mathf.Max(0,timeLimitSeconds-elapsed);
        public Mode CurrentMode => currentMode;
        public int ActiveNotebookCount => notebooks.FindAll(n=>n && n.activeSelf).Count;

        void Awake()
        {
            if (schoolReturnVisual) schoolReturnVisual.SetActive(false);
            if (schoolReturnTrigger) schoolReturnTrigger.SetActive(false);
        }
        void OnTriggerEnter(Collider other)
        {
            var local=other.GetComponent<LibraryPlayer>();
            if (local) BeginSelection(local);
        }
        void OnTriggerStay(Collider other)
        {
            if (selecting || running) return;
            var local=other.GetComponent<LibraryPlayer>();
            if (local) BeginSelection(local);
        }
        public bool BeginSelection(LibraryPlayer local)
        {
            if (!local || !local.enabled || local.currentSpace!=LibraryPortal.Space.Library || selecting || running || Time.time<rearmAt) return false;
            var controller=local.GetComponent<CharacterController>();
            if (!controller || !controller.enabled) return false;
            player=local; body=controller; view=Camera.main;
            if (!view || notebookPositions==null || notebookPositions.Length<7 || !notebookPrefab) return false;
            avatar=player.transform.Find("Student character");
            selecting=true; player.enabled=false;
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            return true;
        }
        public bool ChooseMode(Mode mode)
        {
            if (!selecting || !player || !body || !view) return false;
            currentMode=mode;
            target=mode==Mode.Easy?3:mode==Mode.Medium?5:mode==Mode.Hard?7:5;
            found=0; elapsed=0; completed=false; timedOut=false;
            ClearNotebooks();
            for(int i=0;i<target;i++)
            {
                var item=Instantiate(notebookPrefab,notebookPositions[i],Quaternion.Euler(0,i*47,0));
                item.name="Notebook "+(i+1); notebooks.Add(item);
            }
            body.enabled=false; player.transform.position=schoolArrival; body.enabled=true;
            player.currentSpace=LibraryPortal.Space.Escape;
            if (avatar) avatar.gameObject.SetActive(false);
            yaw=schoolYaw; pitch=0; falling=-2;
            selecting=false; running=true; Physics.SyncTransforms(); UpdateCamera();
            Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
            return true;
        }
        void Update()
        {
            if (!selecting && !running) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { ExitToLobby(); return; }
            if (!running || !player || !body || !view) return;
            if (currentMode==Mode.TimeAttack && !completed && !timedOut)
            {
                elapsed+=Time.deltaTime;
                if (elapsed>=timeLimitSeconds) { elapsed=timeLimitSeconds; timedOut=true; }
            }
            yaw+=Input.GetAxisRaw("Mouse X")*2.5f;
            pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2.0f,-80,80);
            float x=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            float z=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
            Vector3 dir=Quaternion.Euler(0,yaw,0)*Vector3.ClampMagnitude(new Vector3(x,0,z),1);
            if (body.isGrounded && falling<0) falling=-2;
            falling+=Physics.gravity.y*Time.deltaTime;
            body.Move((dir*(Input.GetKey(KeyCode.LeftShift)?5.2f:3.6f)+Vector3.up*falling)*Time.deltaTime);
            if (player.transform.position.y < -5)
            {
                body.enabled=false; player.transform.position=schoolArrival; body.enabled=true; falling=-2;
            }
            if (Input.GetKeyDown(KeyCode.E)) CollectNearest();
            UpdateCamera();
        }
        void UpdateCamera()
        {
            if (!view || !player) return;
            view.transform.position=player.transform.position+Vector3.up*1.62f;
            view.transform.rotation=Quaternion.Euler(pitch,yaw,0);
        }
        public bool CollectNearest()
        {
            if (!running || completed || timedOut || !player) return false;
            GameObject best=null; float bestDistance=1.7f;
            Vector3 center=player.transform.position+Vector3.up*.8f;
            foreach(var item in notebooks)
            {
                if (!item || !item.activeSelf) continue;
                float d=Vector3.Distance(center,item.transform.position+Vector3.up*.42f);
                if (d<bestDistance) { best=item; bestDistance=d; }
            }
            if (!best) return false;
            best.SetActive(false); found++; completed=found>=target; return true;
        }
        public void ExitToLobby()
        {
            if (!selecting && !running) return;
            selecting=false; running=false; ClearNotebooks();
            if (player && body)
            {
                body.enabled=false; player.transform.position=lobbyReturn; body.enabled=true;
                player.currentSpace=LibraryPortal.Space.Library;
                if (avatar) avatar.gameObject.SetActive(true);
                if (view)
                {
                    view.transform.position=lobbyReturn+new Vector3(4,2,-2);
                    view.transform.LookAt(lobbyReturn+Vector3.up*1.35f);
                }
                player.enabled=true; Physics.SyncTransforms();
            }
            rearmAt=Time.time+1.3f;
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
        void ClearNotebooks()
        {
            foreach(var item in notebooks) if (item) Destroy(item);
            notebooks.Clear();
        }
        void OnDisable()
        {
            if (selecting || running) ExitToLobby();
        }
        void Styles()
        {
            if (title!=null) return;
            title=new GUIStyle(GUI.skin.label){fontSize=27,fontStyle=FontStyle.Bold}; title.normal.textColor=new Color(.97f,.91f,.80f);
            text=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true}; text.normal.textColor=new Color(.93f,.9f,.83f);
            choice=new GUIStyle(GUI.skin.button){fontSize=19,fontStyle=FontStyle.Bold,wordWrap=true};
            try
            {
                if (Array.Exists(Font.GetOSInstalledFontNames(),n=>n.Equals("Malgun Gothic",StringComparison.OrdinalIgnoreCase) || n=="맑은 고딕"))
                {
                    koreanFont=Font.CreateDynamicFontFromOSFont("Malgun Gothic",28);
                    if (koreanFont) { title.font=koreanFont; text.font=koreanFont; choice.font=koreanFont; korean=true; }
                }
            }
            catch(Exception) { korean=false; }
        }
        string L(string ko,string en) => korean?ko:en;
        static void Panel(Rect area,float alpha=.93f)
        {
            Color old=GUI.color; GUI.color=new Color(.10f,.08f,.065f,alpha); GUI.DrawTexture(area,Texture2D.whiteTexture); GUI.color=old;
        }
        void OnGUI()
        {
            if (!selecting && !running) return;
            Styles(); var old=GUI.matrix;
            float s=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*s)*.5f,(Screen.height-720*s)*.5f,0),Quaternion.identity,Vector3.one*s);
            if (selecting)
            {
                Panel(new Rect(355,112,570,490));
                GUI.Label(new Rect(389,136,500,45),L("탈출방 모드 선택","ESCAPE ROOM"),title);
                GUI.Label(new Rect(389,183,500,48),L("모드를 고르고 학교에서 노트를 찾아보세요.","Choose a mode and find the notebooks in the school."),text);
                if (GUI.Button(new Rect(390,248,235,82),L("쉬움\n노트 3개","EASY\n3 notebooks"),choice)) ChooseMode(Mode.Easy);
                if (GUI.Button(new Rect(655,248,235,82),L("보통\n노트 5개","MEDIUM\n5 notebooks"),choice)) ChooseMode(Mode.Medium);
                if (GUI.Button(new Rect(390,350,235,82),L("어려움\n노트 7개","HARD\n7 notebooks"),choice)) ChooseMode(Mode.Hard);
                if (GUI.Button(new Rect(655,350,235,82),L("타임어택\n노트 5개 / 3분","TIME ATTACK\n5 notebooks / 3 min"),choice)) ChooseMode(Mode.TimeAttack);
                GUI.Label(new Rect(390,480,500,65),L("WASD: 이동  |  마우스: 시점  |  E: 수집\nESC: 라운지로 돌아가기","WASD: move  |  Mouse: look  |  E: collect\nESC: return to the lounge"),text);
            }
            else
            {
                Panel(new Rect(20,20,410,90),.86f);
                string mode=currentMode==Mode.Easy?L("쉬움","EASY"):currentMode==Mode.Medium?L("보통","MEDIUM"):currentMode==Mode.Hard?L("어려움","HARD"):L("타임어택","TIME ATTACK");
                GUI.Label(new Rect(35,28,380,32),L("탈출방  /  ","ESCAPE  /  ")+mode,title);
                string timer=currentMode==Mode.TimeAttack?L("      남은 시간  ","      TIME  ")+TimeSpan.FromSeconds(Mathf.CeilToInt(Remaining)).ToString(@"mm\:ss"):"";
                GUI.Label(new Rect(37,70,375,28),L("노트  ","NOTEBOOKS  ")+found+" / "+target+timer,text);
                Panel(new Rect(20,656,670,45),.86f);
                GUI.Label(new Rect(35,666,640,26),L("WASD  이동    마우스  시점    E  수집    ESC  라운지로","WASD  Move    Mouse  Look    E  Collect    ESC  Return to lounge"),text);
                if (completed || timedOut)
                {
                    Panel(new Rect(385,265,510,145));
                    GUI.Label(new Rect(420,287,460,40),completed?L("미션 완료","MISSION COMPLETE"):L("시간 종료","TIME IS UP"),title);
                    GUI.Label(new Rect(420,341,460,42),L("ESC를 눌러 라운지로 돌아가세요.","Press ESC to return to the lounge."),text);
                }
                else if (NearestDistance()<1.7f)
                {
                    Panel(new Rect(490,550,300,48),.87f); GUI.Label(new Rect(508,561,270,26),L("E   노트 수집","E   COLLECT NOTEBOOK"),text);
                }
            }
            GUI.matrix=old;
        }
        float NearestDistance()
        {
            float result=float.PositiveInfinity; if (!player) return result;
            Vector3 p=player.transform.position+Vector3.up*.8f;
            foreach(var n in notebooks) if(n && n.activeSelf) result=Mathf.Min(result,Vector3.Distance(p,n.transform.position+Vector3.up*.42f));
            return result;
        }
    }
}
