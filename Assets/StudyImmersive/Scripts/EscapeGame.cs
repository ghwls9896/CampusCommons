using System;
using System.Collections.Generic;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    /// <summary>Mode selection, school interactions, sheet collection, and return to the lobby.</summary>
    [RequireComponent(typeof(BoxCollider))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "CampusCommons.StudyImmersive", null, "EscapeModeFlow")]
    public class EscapeGame : MonoBehaviour
    {
        public enum Mode { Easy, Medium, Hard, TimeAttack }
        public enum InventoryItem { Empty, Soda, Padlock, ExcuseNote }
        const int InventorySlots=3;
        const float MaxStamina=100f;
        const float SprintDrainPerSecond=12.5f;
        const float StaminaRecoveryPerSecond=15f;
        public Vector3 schoolArrival;
        public float schoolYaw;
        public GameObject schoolReturnVisual;
        public GameObject schoolReturnTrigger;
        public GameObject notebookPrefab;
        public Vector3[] notebookPositions;
        public Vector3 lobbyReturn=new Vector3(11.5f,.08f,5.5f);
        public float timeLimitSeconds=180;
        public SchoolGrid schoolGrid;
        public Transform schoolRoot;
        public Monster monster;
        public Door exitDoor;
        [Header("Capture effect")]
        public float jumpScareSeconds=1.35f;
        public Texture2D jumpScareImage;
        public AudioClip jumpScareSound;
        [Header("Final chase atmosphere")]
        [Range(0,1)] public float panicTintOpacity=.55f;
        [Range(0,2)] public float panicShakeStrength=1f;

        Player player;
        CharacterController body;
        Camera view;
        Transform avatar;
        Door[] doors=Array.Empty<Door>();
        ElevatorExit[] elevators=Array.Empty<ElevatorExit>();
        readonly List<GameObject> notebooks=new List<GameObject>();
        // Teacher desks in the five furnished classrooms, relative to Escape School.
        // Each has two separated positions on the tabletop for modes requiring more than five sheets.
        static readonly Vector3[] classroomDesks={
            new Vector3(-12.25f,.788f,35f),new Vector3(10.5f,.788f,35f),
            new Vector3(-10.5f,.788f,16.25f),new Vector3(-10f,.788f,1.25f),
            new Vector3(14.75f,.788f,-12.5f)
        };
        // Additional enclosed rooms without classroom desks, in Escape School coordinates.
        static readonly Vector3[] otherRoomLocations={new Vector3(32f,0,-26f),new Vector3(22.5f,0,0f)};
        bool selecting,running,completed,timedOut,caught,quizOpen,quizFeedback,lastCorrect,exitConfirm;
        GameObject activeSheet;
        CodeQuestions.Question[] quizQuestions;
        int quizIndex;
        Mode currentMode;
        float yaw,pitch,falling,elapsed,rearmAt,noticeUntil;
        float jumpElapsed;
        float panicElapsed;
        Vector3 jumpCameraStart,jumpCameraEnd,jumpLookAt;
        Quaternion jumpCameraRotation;
        AudioSource jumpAudio;
        AudioClip crowAnger;
        int found,target;
        readonly InventoryItem[] inventory=new InventoryItem[InventorySlots];
        float stamina=MaxStamina;
        bool sprintExhausted;
        string notice="";
        string noticeEnglish="";
        GUIStyle title,text,choice,crosshair;
        Font koreanFont;
        bool korean;

        public bool IsSelecting=>selecting;
        public bool IsRunning=>running;
        public bool IsFirstPerson=>true;
        public bool IsExitConfirmOpen=>exitConfirm;
        public bool Completed=>completed;
        public bool TimedOut=>timedOut;
        public bool Caught=>caught;
        public bool IsJumpScarePlaying=>caught && jumpElapsed<jumpScareSeconds;
        public bool IsEnded=>completed || timedOut || caught;
        public bool IsAnswering=>quizOpen && running && !IsEnded;
        public bool AwaitingNextQuestion=>quizFeedback;
        public int QuestionNumber=>quizOpen?quizIndex+1:0;
        public int CurrentCorrectAnswer=>quizOpen?quizQuestions[quizIndex].Correct:-1;
        public bool LastAnswerCorrect=>lastCorrect;
        public bool ReadyToExit=>running && !IsEnded && found>=target;
        public bool IsPanicActive=>ReadyToExit;
        public int ElevatorCount=>elevators.Length;
        public int Found=>found;
        public int Target=>target;
        public float Remaining=>Mathf.Max(0,timeLimitSeconds-elapsed);
        public Mode CurrentMode=>currentMode;
        public int ActiveNotebookCount=>notebooks.FindAll(n=>n && n.activeSelf).Count;
        public EscapeWorld World{get;private set;}
        public IEnumerable<Vector3> PendingSheetPositions
        {
            get { foreach(var sheet in notebooks)if(sheet && sheet.activeSelf)yield return sheet.transform.position; }
        }
        public IEnumerable<Vector3> ExitPositions
        {
            get { foreach(var elevator in elevators)if(elevator)yield return elevator.transform.position; }
        }
        public float Stamina=>stamina;
        public InventoryItem ItemAt(int slot)=>slot>=0 && slot<inventory.Length?inventory[slot]:InventoryItem.Empty;
        public bool AddItem(InventoryItem item)
        {
            if(item==InventoryItem.Empty)return false;
            for(int i=0;i<inventory.Length;i++)if(inventory[i]==InventoryItem.Empty){inventory[i]=item;return true;}
            return false;
        }
        public bool RemoveItem(int slot)
        {
            if(slot<0 || slot>=inventory.Length || inventory[slot]==InventoryItem.Empty)return false;
            inventory[slot]=InventoryItem.Empty;return true;
        }

        void Awake()
        {
            if(schoolReturnVisual)schoolReturnVisual.SetActive(false);
            if(schoolReturnTrigger)schoolReturnTrigger.SetActive(false);
            if(!jumpScareImage)jumpScareImage=Resources.Load<Texture2D>("CrowJumpscare");
            if(!jumpScareSound)jumpScareSound=Resources.Load<AudioClip>("CrowJumpscare");
            crowAnger=Resources.Load<AudioClip>("CrowAlert");
            jumpAudio=gameObject.AddComponent<AudioSource>();jumpAudio.playOnAwake=false;jumpAudio.spatialBlend=0;
            if(schoolGrid)
            {
                SchoolUpperTrim.Create(schoolRoot);SchoolHallDecorations.Create(schoolRoot);elevators=SchoolElevators.Create(this,schoolRoot);
            }
            if(exitDoor)exitDoor.RetireExitDesignation();
        }
        void OnTriggerEnter(Collider other){var local=other.GetComponent<Player>();if(local)BeginSelection(local);}
        void OnTriggerStay(Collider other)
        {
            if(selecting || running)return;var local=other.GetComponent<Player>();if(local)BeginSelection(local);
        }
        public bool BeginSelection(Player local)
        {
            if(!local || !local.enabled || local.currentSpace!=Portal.Space.Library || selecting || running || Time.time<rearmAt)return false;
            var controller=local.GetComponent<CharacterController>();if(!controller || !controller.enabled)return false;
            player=local;body=controller;view=Camera.main;
            if(!view || notebookPositions==null || notebookPositions.Length<7 || !notebookPrefab)return false;
            avatar=player.transform.Find("Student character");
            selecting=true;exitConfirm=false;player.enabled=false;
            body.enabled=false;player.transform.position=schoolArrival;body.enabled=true;player.currentSpace=Portal.Space.Escape;
            if(avatar)avatar.gameObject.SetActive(false);
            yaw=schoolYaw;pitch=0;falling=-2;Physics.SyncTransforms();UpdateCamera();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
        }
        public bool ChooseMode(Mode mode)
        {
            if(!selecting || exitConfirm || !player || !body || !view)return false;
            ResetRoundState();
            currentMode=mode;target=mode==Mode.Easy?3:mode==Mode.Medium?5:mode==Mode.Hard?7:5;
            ClearNotebooks();
            doors=FindObjectsByType<Door>();ResetSchoolDoors();
            if(jumpAudio)jumpAudio.Stop();
            var points=schoolGrid && schoolGrid.sheetLocations!=null && schoolGrid.sheetLocations.Length>=target?schoolGrid.sheetLocations:notebookPositions;
            if(points==null || points.Length<target)return false;
            var school=schoolRoot?schoolRoot.Find("Escape School"):null;
            if(!school){var foundSchool=GameObject.Find("Escape School");if(foundSchool)school=foundSchool.transform;}
            if(!school){Debug.LogWarning("Escape School missing; cannot place question sheets in rooms.");return false;}
            var deskOrder=new int[classroomDesks.Length+otherRoomLocations.Length];var firstSide=new int[deskOrder.Length];
            for(int i=0;i<deskOrder.Length;i++){deskOrder[i]=i;firstSide[i]=UnityEngine.Random.value<.5f?-1:1;}
            for(int i=deskOrder.Length-1;i>0;i--){int j=UnityEngine.Random.Range(0,i+1);int temp=deskOrder[i];deskOrder[i]=deskOrder[j];deskOrder[j]=temp;}
            for(int i=0;i<target;i++)
            {
                Vector3 position=points[0];
                Quaternion rotation=Quaternion.identity;
                bool onDesk=school;
                if(onDesk)
                {
                    int deskIndex=deskOrder[i%deskOrder.Length];
                    if(deskIndex<classroomDesks.Length)
                    {
                        int side=i<deskOrder.Length?firstSide[deskIndex]:-firstSide[deskIndex];
                        position=school.TransformPoint(classroomDesks[deskIndex]+Vector3.forward*(.45f*side));
                        rotation=school.rotation*Quaternion.Euler(0,90,0);
                    }
                    else
                    {
                        onDesk=false;
                        position=school.TransformPoint(otherRoomLocations[deskIndex-classroomDesks.Length]);
                        rotation=school.rotation;
                    }
                }
                var item=Instantiate(notebookPrefab,position,rotation);item.name="Question Sheet "+(i+1);
                if(schoolRoot)item.transform.SetParent(schoolRoot,true);
                if(onDesk)item.transform.localScale*=.5f;
                var sheet=item.GetComponent<Sheet>();if(sheet)sheet.game=this;notebooks.Add(item);
            }
            body.enabled=false;player.transform.position=schoolArrival;body.enabled=true;player.currentSpace=Portal.Space.Escape;
            if(avatar)avatar.gameObject.SetActive(false);
            yaw=schoolYaw;pitch=0;falling=-2;selecting=false;running=true;Physics.SyncTransforms();UpdateCamera();
            if(monster)monster.Begin(this,player.transform,mode);
            World=EscapeWorld.Create(this,player.transform,schoolGrid,doors);
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;return true;
        }
        void Update()
        {
            if(!selecting && !running)return;
            if(Input.GetKeyDown(KeyCode.Escape)){if(exitConfirm)CancelExit();else RequestExit();return;}
            if(exitConfirm)return;
            if(IsJumpScarePlaying){UpdateJumpScare();return;}
            if(!running || !player || !body || !view || IsEnded)return;
            if(IsPanicActive)panicElapsed+=Time.deltaTime;
            if(currentMode==Mode.TimeAttack)
            {
                elapsed+=Time.deltaTime;
                if(elapsed>=timeLimitSeconds){elapsed=timeLimitSeconds;timedOut=true;return;}
            }
            if(IsAnswering)return;
            if(World)World.HandleInventoryKeys();
            if(World && World.BlocksMovement)return;
            yaw+=Input.GetAxisRaw("Mouse X")*2.5f;pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2.0f,-80,80);
            float x=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            float z=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
            Vector3 dir=Quaternion.Euler(0,yaw,0)*Vector3.ClampMagnitude(new Vector3(x,0,z),1);
            if(body.isGrounded && falling<0)falling=-2;falling+=Physics.gravity.y*Time.deltaTime;
            bool sprinting=UpdateStamina(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),dir.sqrMagnitude>.001f,Time.deltaTime);
            float boost=World && World.HasSpeedBoost?1.35f:1f;
            body.Move((dir*((sprinting?5.2f:3.6f)*boost)+Vector3.up*falling)*Time.deltaTime);
            if(player.transform.position.y< -5){body.enabled=false;player.transform.position=schoolArrival;body.enabled=true;falling=-2;}
            UpdateCamera();
            if(Input.GetMouseButtonDown(0))Interact(true);
            if(Input.GetKeyDown(KeyCode.E))Interact(false);
        }
        bool UpdateStamina(bool sprintHeld,bool moving,float deltaTime)
        {
            if(!sprintHeld)sprintExhausted=false;
            bool sprinting=sprintHeld && !sprintExhausted && moving && stamina>0f;
            if(sprinting)
            {
                stamina=Mathf.Max(0f,stamina-SprintDrainPerSecond*deltaTime);
                if(stamina<=0f)sprintExhausted=true;
            }
            else stamina=Mathf.Min(MaxStamina,stamina+StaminaRecoveryPerSecond*deltaTime);
            return sprinting;
        }
        void UpdateCamera()
        {
            if(!view || !player)return;
            Vector3 cameraPosition=player.transform.position+Vector3.up*1.62f;
            Quaternion cameraRotation=Quaternion.Euler(pitch,yaw,0);
            if(avatar && avatar.gameObject.activeSelf)avatar.gameObject.SetActive(false);
            if(IsPanicActive)
            {
                float strength=Mathf.Clamp01(panicElapsed/.45f)*Mathf.Max(0f,panicShakeStrength);
                float horizontal=(Mathf.PerlinNoise(panicElapsed*13f,.2f)-.5f)*.13f*strength;
                float vertical=(Mathf.PerlinNoise(.4f,panicElapsed*15f)-.5f)*.09f*strength;
                cameraPosition+=cameraRotation*Vector3.right*horizontal+Vector3.up*vertical;
                cameraRotation*=Quaternion.Euler(vertical*18f,horizontal*18f,Mathf.Sin(panicElapsed*15f)*1.1f*strength);
            }
            view.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
        }
        public void AimAt(Vector3 targetPoint)
        {
            if(!view || !player)return;Vector3 delta=targetPoint-(player.transform.position+Vector3.up*1.62f);
            if(delta.sqrMagnitude<.0001f)return;
            yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            pitch=Mathf.Clamp(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,-80,80);UpdateCamera();
        }
        public bool Interact(bool requireAim)
        {
            if(!running || IsEnded || IsAnswering || exitConfirm || !view || !player || (World && World.BlocksMovement))return false;
            if(World && World.TryInteract(requireAim))return true;
            var ray=view.ViewportPointToRay(new Vector3(.5f,.5f,0));
            var hits=Physics.RaycastAll(ray,2.25f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                var sheet=hit.collider.GetComponentInParent<Sheet>();if(sheet && sheet.game==this){sheet.Collect();return true;}
                var elevator=hit.collider.GetComponentInParent<ElevatorExit>();if(elevator){elevator.Interact();return true;}
                var door=hit.collider.GetComponentInParent<Door>();if(door){door.Interact();return true;}
                if(!hit.collider.isTrigger)break;
            }
            if(requireAim)return false;
            GameObject nearestSheet=null;Door nearestDoor=null;ElevatorExit nearestElevator=null;float best=1.7f;var origin=player.transform.position+Vector3.up*.8f;
            foreach(var item in notebooks)
            {
                if(!item || !item.activeSelf)continue;float d=Vector3.Distance(origin,item.transform.position+Vector3.up*.8f);
                if(d<best){best=d;nearestSheet=item;nearestDoor=null;nearestElevator=null;}
            }
            foreach(var elevator in elevators)
            {
                if(!elevator || !elevator.gameObject.activeInHierarchy)continue;
                float d=elevator.DistanceFrom(origin);
                if(d<best){best=d;nearestElevator=elevator;nearestSheet=null;nearestDoor=null;}
            }
            foreach(var door in doors)
            {
                if(!door || !door.gameObject.activeInHierarchy)continue;
                foreach(var c in door.GetComponentsInChildren<Collider>())
                {
                    float d=Vector3.Distance(origin,c.ClosestPoint(origin));
                    if(d<best){best=d;nearestDoor=door;nearestSheet=null;nearestElevator=null;}
                }
            }
            if(nearestSheet)return OpenSheet(nearestSheet);
            if(nearestElevator){nearestElevator.Interact();return true;}
            if(nearestDoor){nearestDoor.Interact();return true;}
            return false;
        }
        public bool CollectNearest()
        {
            if(!running || IsEnded || IsAnswering || !player)return false;
            GameObject best=null;float distance=1.7f;var center=player.transform.position+Vector3.up*.8f;
            foreach(var item in notebooks)
            {
                if(!item || !item.activeSelf)continue;float d=Vector3.Distance(center,item.transform.position+Vector3.up*.8f);
                if(d<distance){best=item;distance=d;}
            }
            return best && OpenSheet(best);
        }
        public bool OpenSheet(GameObject item)
        {
            if(!running || IsEnded || IsAnswering || exitConfirm || !item || !item.activeSelf || !notebooks.Contains(item))return false;
            activeSheet=item;quizQuestions=CodeQuestions.RandomThree();quizIndex=0;quizFeedback=false;quizOpen=true;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
        }
        public bool SubmitAnswer(int answer)
        {
            if(!IsAnswering || quizFeedback || answer<0 || answer>=quizQuestions[quizIndex].Options.Length)return false;
            lastCorrect=answer==quizQuestions[quizIndex].Correct;quizFeedback=true;
            if(!lastCorrect && monster)
            {
                bool first=!monster.IsAwakened;monster.SpeedUp();
                if(first && jumpAudio && crowAnger)jumpAudio.PlayOneShot(crowAnger,1f);
            }
            return true;
        }
        public bool ContinueQuestion()
        {
            if(!IsAnswering || !quizFeedback)return false;
            quizFeedback=false;quizIndex++;
            if(quizIndex<quizQuestions.Length)return true;
            quizOpen=false;var item=activeSheet;activeSheet=null;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            return CollectSheet(item);
        }
        public bool CollectSheet(GameObject item)
        {
            if(!running || IsEnded || IsAnswering || !item || !item.activeSelf || !notebooks.Contains(item))return false;
            item.SetActive(false);found++;
            if(found>=target)
            {
                panicElapsed=0;
                Notice("문제지를 모두 수집했습니다.","All sheets collected.");
            }
            else Notice("문제지를 수집했습니다.","Sheet collected.");
            return true;
        }
        public void Notice(string message,string english="Keep moving!"){notice=message;noticeEnglish=english;noticeUntil=Time.time+3f;}
        public void CaughtByMonster()
        {
            if(!running || IsEnded)return;
            caught=true;jumpElapsed=0;
            if(view && monster)
            {
                jumpCameraStart=view.transform.position;jumpCameraRotation=view.transform.rotation;
                jumpLookAt=monster.transform.position+Vector3.up*1.2f;
                var delta=jumpLookAt-jumpCameraStart;
                float move=Mathf.Clamp(delta.magnitude-.3f,0,.55f);
                jumpCameraEnd=jumpCameraStart+(delta.sqrMagnitude>.0001f?delta.normalized*move:Vector3.zero);
                if(move>0 && Physics.SphereCast(jumpCameraStart,.1f,delta.normalized,out RaycastHit hit,move,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    jumpCameraEnd=jumpCameraStart+delta.normalized*Mathf.Max(0,hit.distance-.12f);
            }
            if(jumpAudio && jumpScareSound)jumpAudio.PlayOneShot(jumpScareSound,.85f);
        }
        void UpdateJumpScare()
        {
            jumpElapsed+=Time.deltaTime;if(!view || !monster)return;
            float amount=Mathf.SmoothStep(0,1,Mathf.Clamp01(jumpElapsed/.42f));
            view.transform.position=Vector3.Lerp(jumpCameraStart,jumpCameraEnd,amount);
            Vector3 direction=jumpLookAt-view.transform.position;
            if(direction.sqrMagnitude>.0001f)view.transform.rotation=Quaternion.Slerp(jumpCameraRotation,Quaternion.LookRotation(direction),amount);
        }
        public void CompleteEscape(){if(ReadyToExit)completed=true;}
        public bool RequestExit()
        {
            if((!selecting && !running) || exitConfirm)return false;
            exitConfirm=true;if(jumpAudio)jumpAudio.Pause();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
        }
        public bool CancelExit()
        {
            if(!exitConfirm)return false;exitConfirm=false;if(jumpAudio)jumpAudio.UnPause();
            bool useMouse=selecting || IsAnswering || IsEnded || (World && World.BlocksMovement);
            Cursor.lockState=useMouse?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=useMouse;return true;
        }
        public bool ConfirmExit(){if(!exitConfirm)return false;ExitToLobby();return true;}
        public void ExitToLobby()
        {
            if(!selecting && !running)return;selecting=false;running=false;exitConfirm=false;quizOpen=false;activeSheet=null;ClearNotebooks();if(monster)monster.End();if(jumpAudio)jumpAudio.Stop();
            if(World){World.Shutdown();World.gameObject.SetActive(false);Destroy(World.gameObject);World=null;}
            ResetSchoolDoors();
            ResetRoundState();
            if(player && body)
            {
                body.enabled=false;player.transform.position=lobbyReturn;body.enabled=true;player.currentSpace=Portal.Space.Library;
                player.enabled=true;Physics.SyncTransforms();player.RefreshView();
            }
            rearmAt=Time.time+1.3f;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void ResetRoundState()
        {
            stamina=MaxStamina;sprintExhausted=false;Array.Clear(inventory,0,inventory.Length);
            found=0;target=0;elapsed=0;panicElapsed=0;completed=false;timedOut=false;caught=false;
            jumpElapsed=0;quizOpen=false;quizFeedback=false;lastCorrect=false;quizIndex=0;quizQuestions=null;activeSheet=null;
            noticeUntil=0;notice="";noticeEnglish="";
        }
        void ResetSchoolDoors()
        {
            foreach(var door in doors)if(door)door.ResetForNewRound();
            Physics.SyncTransforms();
        }
        void ClearNotebooks(){foreach(var item in notebooks)if(item){item.SetActive(false);Destroy(item);}notebooks.Clear();}
        void OnDisable(){if(selecting || running)ExitToLobby();}
        void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle(GUI.skin.label){fontSize=27,fontStyle=FontStyle.Bold};title.normal.textColor=new Color(.97f,.91f,.80f);
            text=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true};text.normal.textColor=new Color(.93f,.9f,.83f);
            choice=new GUIStyle(GUI.skin.button){fontSize=19,fontStyle=FontStyle.Bold,wordWrap=true};
            crosshair=new GUIStyle(GUI.skin.label){fontSize=25,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};crosshair.normal.textColor=Color.white;
            koreanFont=Resources.Load<Font>("KIT/NotoSansKR");
            if(koreanFont){title.font=koreanFont;text.font=koreanFont;choice.font=koreanFont;crosshair.font=koreanFont;korean=true;}
        }
        string L(string ko,string en)=>korean?ko:en;
        static void Panel(Rect area,float alpha=.93f){Color old=GUI.color;GUI.color=new Color(.10f,.08f,.065f,alpha);GUI.DrawTexture(area,Texture2D.whiteTexture);GUI.color=old;}
        void OnGUI()
        {
            if(!selecting && !running)return;Styles();var old=GUI.matrix;float s=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*s)*.5f,(Screen.height-720*s)*.5f,0),Quaternion.identity,Vector3.one*s);
            if(IsPanicActive)DrawPanicTint();
            if(exitConfirm)
            {
                Panel(new Rect(360,230,560,250),.97f);
                GUI.Label(new Rect(400,260,500,70),L("익스케이프룸을 나가시겠습니까?","Leave the escape room?"),title);
                GUI.Label(new Rect(400,320,500,35),L("현재 진행 상황은 저장되지 않습니다.","Current progress will be lost."),text);
                if(GUI.Button(new Rect(400,375,220,65),L("예","YES"),choice))ConfirmExit();
                if(GUI.Button(new Rect(660,375,220,65),L("아니오","NO"),choice))CancelExit();
                GUI.matrix=old;return;
            }
            if(IsJumpScarePlaying)
            {
                DrawJumpScare();GUI.matrix=old;return;
            }
            if(selecting)
            {
                Panel(new Rect(355,112,570,490));GUI.Label(new Rect(389,136,500,45),L("탈출방 모드 선택","ESCAPE ROOM"),title);
                GUI.Label(new Rect(389,183,500,48),L("문제지를 찾아 코딩 문제를 풀고 탈출하세요.","Find the sheets, answer coding questions, and escape."),text);
                if(GUI.Button(new Rect(390,248,235,82),L("쉬움\n문제지 3개","EASY\n3 sheets"),choice))ChooseMode(Mode.Easy);
                if(GUI.Button(new Rect(655,248,235,82),L("보통\n문제지 5개","MEDIUM\n5 sheets"),choice))ChooseMode(Mode.Medium);
                if(GUI.Button(new Rect(390,350,235,82),L("어려움\n문제지 7개","HARD\n7 sheets"),choice))ChooseMode(Mode.Hard);
                if(GUI.Button(new Rect(655,350,235,82),L("타임어택\n문제지 5개 / 3분","TIME ATTACK\n5 sheets / 3 min"),choice))ChooseMode(Mode.TimeAttack);
                GUI.Label(new Rect(390,480,500,65),L("WASD: 이동  |  마우스: 시점  |  클릭/E: 상호작용\nESC: 라운지로 돌아가기","WASD: move  |  Mouse: look  |  Click/E: interact\nESC: return to the lounge"),text);
            }
            else
            {
                PixelBox(new Rect(20,20,240,48),new Color(.13f,.17f,.20f,.94f));
                GUI.Label(new Rect(32,30,220,28),L("남은 문제지  ","SHEETS LEFT  ")+Mathf.Max(0,target-found),text);
                if(currentMode==Mode.TimeAttack)
                {
                    PixelBox(new Rect(530,20,220,48),new Color(.13f,.17f,.20f,.94f));
                    GUI.Label(new Rect(544,30,200,28),L("남은 시간  ","TIME  ")+TimeSpan.FromSeconds(Mathf.CeilToInt(Remaining)).ToString(@"mm\:ss"),text);
                }
                if(!IsEnded)DrawEscapeHud();
                if(World && World.BlocksMovement && !IsEnded)
                {
                    World.DrawProfessor(title,text,choice,korean);GUI.matrix=old;return;
                }
                if(IsEnded)
                {
                    Panel(new Rect(385,265,510,145));string status=completed?L("탈출 성공!","ESCAPED!"):timedOut?L("시간 종료","TIME IS UP"):L("몬스터에게 잡혔습니다","CAUGHT BY THE MONSTER");
                    GUI.Label(new Rect(420,287,460,40),status,title);GUI.Label(new Rect(420,341,460,42),L("ESC를 눌러 라운지로 돌아가세요.","Press ESC to return to the lounge."),text);
                }
                else if(IsAnswering)
                {
                    var q=quizQuestions[quizIndex];Panel(new Rect(270,112,740,500),.97f);
                    GUI.Label(new Rect(305,132,670,40),L("코딩 문제  ","CODING QUESTION  ")+(quizIndex+1)+" / 3",title);
                    GUI.Label(new Rect(308,185,660,150),q.Prompt,text);
                    if(!quizFeedback)
                    {
                        for(int i=0;i<q.Options.Length;i++)
                        {
                            float x=308+(i%2)*335,y=345+(i/2)*77;
                            if(GUI.Button(new Rect(x,y,310,61),q.Options[i],choice))SubmitAnswer(i);
                        }
                    }
                    else
                    {
                        string result=lastCorrect?L("정답입니다!","Correct!"):L("오답입니다. 몬스터가 빨라졌습니다.","Incorrect. The monster is faster.");
                        GUI.Label(new Rect(310,350,645,32),result,text);
                        if(!lastCorrect)GUI.Label(new Rect(310,389,645,30),L("정답:  ","Correct answer:  ")+q.Options[q.Correct],text);
                        if(GUI.Button(new Rect(725,485,235,65),quizIndex==2?L("문제지 수집","COLLECT SHEET"):L("다음 문제","NEXT QUESTION"),choice))ContinueQuestion();
                    }
                }
                else
                {
                    GUI.Label(new Rect(622,342,36,36),"+",crosshair);
                    if(Time.time<noticeUntil){Panel(new Rect(365,555,550,48),.88f);GUI.Label(new Rect(380,565,520,28),korean?notice:noticeEnglish,text);}
                    else if(World && World.NearbyHint!=null){Panel(new Rect(420,555,440,48),.87f);GUI.Label(new Rect(435,565,410,28),World.NearbyHint,text);}
                    else if(NearestDistance()<1.7f){Panel(new Rect(475,555,330,48),.87f);GUI.Label(new Rect(490,565,300,28),L("클릭 / E   문제지 열기","CLICK / E   OPEN SHEET"),text);}
                    else
                    {
                        var elevator=NearbyElevator();
                        if(elevator){Panel(new Rect(475,555,330,48),.87f);GUI.Label(new Rect(490,565,300,28),korean?elevator.Hint:"CLICK / E   ELEVATOR",text);}
                        else{var door=NearbyDoor();if(door){Panel(new Rect(475,555,330,48),.87f);GUI.Label(new Rect(490,565,300,28),korean?door.Hint:"CLICK / E   USE DOOR",text);}}
                    }
                }
            }
            GUI.matrix=old;
        }
        static void PixelFill(Rect rect,Color color)
        {
            Color previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;
        }
        static void PixelBox(Rect rect,Color inside)
        {
            PixelFill(rect,new Color(.04f,.05f,.06f,.96f));
            PixelFill(new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6),new Color(.65f,.68f,.61f,.95f));
            PixelFill(new Rect(rect.x+6,rect.y+6,rect.width-12,rect.height-12),inside);
        }
        void DrawEscapeHud()
        {
            // Square borders and segmented bars match the retro classroom art without new scene objects.
            if(World)
            {
                PixelBox(new Rect(20,76,160,42),new Color(.13f,.17f,.20f,.96f));
                GUI.Label(new Rect(32,85,140,25),L("동전  ","COINS  ")+World.Coins,text);
                if(World.HasSpeedBoost)PixelBox(new Rect(1028,95,230,42),new Color(.13f,.17f,.20f,.96f));
            }
            PixelBox(new Rect(20,645,276,55),new Color(.13f,.17f,.20f,.94f));
            GUI.Label(new Rect(31,651,85,30),L("스태미나","STAMINA"),text);
            for(int i=0;i<10;i++)
            {
                bool lit=stamina/MaxStamina>(i/10f);
                PixelFill(new Rect(118+i*16,662,13,17),lit?new Color(.93f,.78f,.22f):new Color(.25f,.25f,.22f));
            }
            for(int i=0;i<inventory.Length;i++)
            {
                float x=1032+i*77;
                PixelBox(new Rect(x,20,70,70),new Color(.15f,.18f,.20f,.96f));
                string item=inventory[i]==InventoryItem.Soda?L("음료","CAN"):
                    inventory[i]==InventoryItem.Padlock?L("자물쇠","LOCK"):
                    inventory[i]==InventoryItem.ExcuseNote?L("공결증","NOTE"):"";
                var icon=World?World.ItemTexture(inventory[i]):null;
                if(icon)GUI.DrawTexture(new Rect(x+12,29,46,46),icon,ScaleMode.ScaleToFit,true);
                else GUI.Label(new Rect(x+8,34,58,28),item,text);
                GUI.Label(new Rect(x+7,66,20,20),(i+1).ToString(),text);
            }
            if(World)World.DrawStatus(text,korean);
        }
        void DrawPanicTint()
        {
            Color old=GUI.color;
            GUI.color=new Color(.86f,.01f,.02f,Mathf.Clamp01(panicTintOpacity+.025f*Mathf.Sin(panicElapsed*4f)));
            GUI.DrawTexture(new Rect(0,0,1280,720),Texture2D.whiteTexture);GUI.color=old;
        }
        void DrawJumpScare()
        {
            float t=Mathf.Clamp01(jumpElapsed/Mathf.Max(.1f,jumpScareSeconds));
            Color previous=GUI.color;
            GUI.color=new Color(.08f,0,.01f,Mathf.Clamp01(.3f+t*2));GUI.DrawTexture(new Rect(0,0,1280,720),Texture2D.whiteTexture);
            if(jumpScareImage)
            {
                float size=Mathf.Lerp(470,930,Mathf.SmoothStep(0,1,Mathf.Clamp01(jumpElapsed/.38f)));
                float shake=Mathf.Sin(jumpElapsed*47f)*Mathf.Lerp(12,3,t);
                GUI.color=Color.white;GUI.DrawTexture(new Rect(640-size*.5f+shake,360-size*.5f,size,size),jumpScareImage,ScaleMode.ScaleToFit,true);
            }
            GUI.color=previous;
        }
        float NearestDistance()
        {
            float result=float.PositiveInfinity;if(!player)return result;Vector3 p=player.transform.position+Vector3.up*.8f;
            foreach(var n in notebooks)if(n && n.activeSelf)result=Mathf.Min(result,Vector3.Distance(p,n.transform.position+Vector3.up*.8f));return result;
        }
        ElevatorExit NearbyElevator()
        {
            if(!player)return null;var point=player.transform.position+Vector3.up*.8f;ElevatorExit best=null;float distance=1.55f;
            foreach(var elevator in elevators)if(elevator)
            {float d=elevator.DistanceFrom(point);if(d<distance){distance=d;best=elevator;}}
            return best;
        }
        Door NearbyDoor()
        {
            if(!player)return null;var point=player.transform.position+Vector3.up*.8f;Door result=null;float best=1.55f;
            foreach(var door in doors)if(door)foreach(var c in door.GetComponentsInChildren<Collider>())
            {float distance=Vector3.Distance(point,c.ClosestPoint(point));if(distance<best){best=distance;result=door;}}
            return result;
        }
    }
}
