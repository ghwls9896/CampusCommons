using UnityEngine;

namespace CampusCommons.GrandLibrary
{
    [RequireComponent(typeof(CharacterController))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "CampusCommons.GrandLibrary", null, "LibraryPlayer")]
    public class Player : MonoBehaviour
    {
        public float walkSpeed = 4.0f, runSpeed = 6.0f;
        public Portal.Space currentSpace = Portal.Space.Library;
        CharacterController body;
        Transform model, leftLeg, rightLeg, leftArm, rightArm;


        LibraryAvatarMotion avatarMotion;
        LibraryAvatarCustomizerUI avatarUI;
        LibraryAvatarSitPose avatarSitPose;


        Camera cameraView;
        Seat[] seats;
        Seat nearest, sitting;
        LobbyDoor nearestDoor;
        Vector3 standPosition;
        float yaw, pitch = 16, verticalSpeed, gait, portalReadyAt, arrivalTime;
        bool waving,exitConfirm,firstPerson;
        float waveUntil;
        Vector3 escapeRecovery;
        GUIStyle title, detail, hint,exitText,exitChoice;
        bool koreanExit;
        public bool IsExitConfirmOpen=>exitConfirm;
        public bool IsFirstPerson=>firstPerson;
        public bool CanUseInventory => isActiveAndEnabled && currentSpace == Portal.Space.Library &&
            !exitConfirm && (avatarUI == null || !avatarUI.IsOpen);

        public static Player Create(Transform parent = null)
        {
            var go=new GameObject("Student - local player");
            if(parent)go.transform.SetParent(parent,false);
            go.layer=2;
            go.transform.position=new Vector3(0,.08f,-9.3f);
            var controller=go.AddComponent<CharacterController>();
            controller.height=1.8f; controller.center=new Vector3(0,.9f,0);
            controller.radius=.29f; controller.stepOffset=.3f; controller.skinWidth=.025f;
            return go.AddComponent<Player>();
        }
        void Start()
        {
            body=GetComponent<CharacterController>();
            seats=FindObjectsByType<Seat>();
            BuildAvatar();
            foreach (var c in FindObjectsByType<Camera>()) c.enabled=false;
            var cam=new GameObject("Player camera"); cam.tag="MainCamera";
            if(transform.parent)cam.transform.SetParent(transform.parent,true);
            cameraView=cam.AddComponent<Camera>(); cameraView.fieldOfView=62;
            cameraView.nearClipPlane=.08f; cameraView.farClipPlane=100;
            cameraView.clearFlags=CameraClearFlags.Skybox;
            cameraView.backgroundColor=new Color(.45f,.52f,.56f);
            if (!FindAnyObjectByType<AudioListener>()) cam.AddComponent<AudioListener>();
            UpdateCamera(true);
        }
        Material Material(string name, Color color)
        {
            var material=new Material(Shader.Find("Standard")) { name=name, color=color };
            material.SetFloat("_Glossiness",.14f); return material;
        }
        Transform Part(string name, PrimitiveType shape, Transform parent, Vector3 p, Vector3 scale, Material material)
        {
            var part=GameObject.CreatePrimitive(shape); part.name=name; part.layer=2;
            part.transform.SetParent(parent,false); part.transform.localPosition=p; part.transform.localScale=scale;
            Destroy(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial=material;
            return part.transform;
        }
        void BuildAvatar()
        {
            model=new GameObject("Student character").transform; model.SetParent(transform,false);

            GameObject avatarPrefab =
                Resources.Load<GameObject>("TeamAvatar/LibraryAvatar");

            if (avatarPrefab != null)
            {
                // 기존 플레이어 아래에 Sunbox 아바타 생성
                GameObject avatar = Instantiate(
                    avatarPrefab, model, false);

                avatar.name = "AvatarVisual";

                // 걷기·달리기·손 흔들기
                avatarMotion =
                    gameObject.AddComponent<LibraryAvatarMotion>();
                avatarMotion.Initialize(avatar);

                // C키 커스터마이징
                avatarUI =
                    gameObject.AddComponent<LibraryAvatarCustomizerUI>();
                avatarUI.Initialize(avatar);

                // E키 앉기 자세
                avatarSitPose =
                    gameObject.AddComponent<LibraryAvatarSitPose>();
                avatarSitPose.Initialize(avatar);

                // 실시간 캐릭터 미리보기
                LibraryAvatarPreview avatarPreview =
                    gameObject.AddComponent<LibraryAvatarPreview>();
                avatarPreview.Initialize(avatar, avatarUI);

                Debug.Log("Sunbox 아바타 시스템 연결 완료");
                return;
            }

            Debug.LogWarning(
                "LibraryAvatar를 찾지 못해 기본 캐릭터를 생성합니다.");

            var skin =Material("Skin",new Color(.78f,.56f,.40f));
            var shirt=Material("Sweater",new Color(.26f,.40f,.35f));
            var pants=Material("Trousers",new Color(.17f,.19f,.20f));
            var shoes=Material("Shoes",new Color(.78f,.75f,.68f));
            var hair=Material("Hair",new Color(.10f,.08f,.07f));
            Part("Body",PrimitiveType.Capsule,model,new Vector3(0,1.03f,0),new Vector3(.59f,.4f,.4f),shirt);
            Part("Head",PrimitiveType.Sphere,model,new Vector3(0,1.56f,0),Vector3.one*.43f,skin);
            Part("Hair",PrimitiveType.Sphere,model,new Vector3(0,1.7f,-.025f),new Vector3(.45f,.23f,.43f),hair);
            for (int i=-1;i<=1;i+=2)
            {
                Part("Eye",PrimitiveType.Sphere,model,new Vector3(i*.075f,1.59f,.195f),Vector3.one*.034f,hair);
                var leg=new GameObject(i<0?"Left leg":"Right leg").transform; leg.SetParent(model,false);
                leg.localPosition=new Vector3(i*.15f,.72f,0);
                Part("Trouser leg",PrimitiveType.Capsule,leg,new Vector3(0,-.3f,0),new Vector3(.23f,.30f,.23f),pants);
                Part("Shoe",PrimitiveType.Cube,leg,new Vector3(0,-.62f,.07f),new Vector3(.25f,.13f,.38f),shoes);
                var arm=new GameObject(i<0?"Left arm":"Right arm").transform; arm.SetParent(model,false);
                arm.localPosition=new Vector3(i*.38f,1.23f,0);
                Part("Sleeve",PrimitiveType.Capsule,arm,new Vector3(0,-.2f,0),new Vector3(.20f,.24f,.2f),shirt);
                Part("Hand",PrimitiveType.Sphere,arm,new Vector3(0,-.44f,0),Vector3.one*.17f,skin);
                if (i<0) { leftLeg=leg; leftArm=arm; } else { rightLeg=leg; rightArm=arm; }
            }
        }
        void Update()
        {
            // Quiz text input owns C/R/V and movement; Escape still controls room exit.
            var quiz = GetComponent<CampusCommons.Quiz.QuizRoom>();
            if (quiz && quiz.BlocksMovement)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (exitConfirm) CancelRoomExit(); else RequestRoomExit();
                }
                if (avatarUI != null && avatarUI.IsOpen) avatarUI.Close();
                Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
                AnimateAvatar(Vector3.zero, false);
                return;
            }


            if (Input.GetKeyDown(KeyCode.C) && !exitConfirm)
            {
                avatarUI?.Toggle();
            }

            if (avatarUI != null && avatarUI.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    avatarUI.Close();
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                avatarMotion?.UpdateMovement(
                    false, false, sitting != null);

                return;
            }

            var inventory = GetComponent<LibraryInventoryUI>();
            if (inventory != null && inventory.OwnsInput)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                AnimateAvatar(Vector3.zero, false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if(exitConfirm)CancelRoomExit();else RequestRoomExit();
                return;
            }
            if(exitConfirm)return;
            if(Input.GetKeyDown(KeyCode.V))ToggleView();
            bool looking=firstPerson || Input.GetMouseButton(1);
            if (looking)
            {
                yaw+=Input.GetAxisRaw("Mouse X")*3;
                // A negative orbit pitch lets students look up through the atrium
                // and see the skybox instead of being limited to a downward view.
                pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2,-55,60);
            }
            Cursor.visible=!looking;
            Cursor.lockState=looking?CursorLockMode.Locked:CursorLockMode.None;
            nearest=null; float distance=1.65f;
            foreach(var seat in seats)
            {
                if (seat.occupant) continue;
                float d=Vector3.Distance(transform.position+Vector3.up*.6f,seat.transform.position);
                if (d<distance) { nearest=seat; distance=d; }
            }
            nearestDoor = sitting ? null : LobbyDoor.Nearest(this);
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (sitting) Stand();
                else if (nearestDoor) nearestDoor.TryEnter(this);
                else if (nearest) TrySit(nearest);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                waving = true;
                waveUntil = Time.time + 2f;

                avatarMotion?.Wave();
            }

            if (Time.time>waveUntil) waving=false;
            float x=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
            float z=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
            Vector3 direction=Quaternion.Euler(0,yaw,0)*Vector3.ClampMagnitude(new Vector3(x,0,z),1);
            if (sitting) direction=Vector3.zero;
            if (!sitting)
            {
                if (body.isGrounded && verticalSpeed<0) verticalSpeed=-2;
                verticalSpeed+=Physics.gravity.y*Time.deltaTime;
                body.Move((direction*(Input.GetKey(KeyCode.LeftShift)?runSpeed:walkSpeed)+Vector3.up*verticalSpeed)*Time.deltaTime);
                if (transform.position.y < -5) MoveTo(RecoveryPoint(),currentSpace,0);
            }
            AnimateAvatar(direction,Input.GetKey(KeyCode.LeftShift));
        }

        void AnimateAvatar(Vector3 direction, bool running)
        {
            if (!model) return;

            // Sunbox 아바타 이동 애니메이션
            if (avatarMotion != null)
            {
                avatarMotion.UpdateMovement(
                    sitting == null && direction.sqrMagnitude > 0.01f,
                    running,
                    sitting != null
                );
            }

            // Sunbox 아바타 앉기 자세
            if (avatarSitPose != null)
            {
                avatarSitPose.SetSitting(sitting != null);
            }

            // 기존 이동 방향 회전 유지
            if (!sitting && direction.sqrMagnitude > .01f)
            {
                model.rotation = Quaternion.Slerp(
                    model.rotation,
                    Quaternion.LookRotation(direction),
                    12f * Time.deltaTime
                );
            }

            // 기본 도형 캐릭터가 없으면 아래 동작 생략
            if (!leftLeg || !rightLeg || !leftArm || !rightArm)
                return;

            // 기존 기본 캐릭터 애니메이션 유지
            gait += Time.deltaTime * (running ? 12 : 8);

            float swing = direction.sqrMagnitude > .01f
                ? Mathf.Sin(gait) * 25f : 0f;

            leftLeg.localRotation =
                Quaternion.Euler(sitting ? -75 : swing, 0, 0);

            rightLeg.localRotation =
                Quaternion.Euler(sitting ? -75 : -swing, 0, 0);

            leftArm.localRotation =
                Quaternion.Euler(-swing * .7f, 0, 0);

            rightArm.localRotation = waving
                ? Quaternion.Euler(
                    0, 0,
                    -140 + Mathf.Sin(Time.time * 14) * 20)
                : Quaternion.Euler(swing * .7f, 0, 0);
        }

        public void ToggleView(){SetFirstPerson(!firstPerson);}
        public void SetFirstPerson(bool value){firstPerson=value;RefreshView();}
        public void RefreshView()
        {
            if(model)model.gameObject.SetActive(!firstPerson);
            if(enabled)UpdateCamera(true);
        }
        Vector3 RecoveryPoint()
        {
            if (currentSpace==Portal.Space.Mafia) return new Vector3(100,.08f,-5.1f);
            if (currentSpace==Portal.Space.Quiz) return new Vector3(200,.08f,-5.1f);
            if (currentSpace==Portal.Space.Escape) return escapeRecovery;
            return new Vector3(0,.08f,-9.3f);
        }
        public bool TrySit(Seat seat)
        {
            if (!seat || seat.occupant || sitting || Vector3.Distance(transform.position+Vector3.up*.6f,seat.transform.position)>1.65f) return false;
            standPosition=transform.position; sitting=seat; seat.occupant=this;
            body.enabled=false; transform.position=seat.transform.position-Vector3.up*.72f;
            model.rotation=seat.transform.rotation;
            return true;
        }
        public void Stand()
        {
            if (!sitting) return;
            sitting.occupant=null; sitting=null; transform.position=standPosition;
            body.enabled=true; verticalSpeed=-2;
        }
        public bool TryUsePortal(Portal portal)
        {
            if (!portal || Time.time<portalReadyAt || sitting || exitConfirm || currentSpace!=Portal.Space.Library || portal.destination==Portal.Space.Library) return false;
            portalReadyAt=Time.time+1.2f;
            MoveTo(portal.arrivalPosition,portal.destination,portal.arrivalYaw);
            return true;
        }
        public bool RequestRoomExit()
        {
            if(exitConfirm || (currentSpace!=Portal.Space.Mafia && currentSpace!=Portal.Space.Quiz))return false;
            exitConfirm=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
        }
        public bool CancelRoomExit(){if(!exitConfirm)return false;exitConfirm=false;return true;}
        public bool ConfirmRoomExit()
        {
            if(!exitConfirm)return false;
            var room=currentSpace;exitConfirm=false;
            MoveTo(new Vector3(11.5f,.08f,room==Portal.Space.Mafia?-5.5f:0),Portal.Space.Library,270);
            portalReadyAt=Time.time+1.3f;return true;
        }
        void MoveTo(Vector3 position,Portal.Space space,float newYaw)
        {
            Stand(); body.enabled=false; transform.position=position; body.enabled=true;
            if (space==Portal.Space.Escape) escapeRecovery=position;
            verticalSpeed=-2; currentSpace=space; yaw=newYaw; arrivalTime=Time.time;
            model.rotation=Quaternion.Euler(0,newYaw,0);
            Physics.SyncTransforms();
            UpdateCamera(true);
        }
        void LateUpdate() { UpdateCamera(false); }
        void UpdateCamera(bool immediate)
        {
            if (!cameraView) return;
            if(firstPerson)
            {
                if(model && model.gameObject.activeSelf)model.gameObject.SetActive(false);
                cameraView.transform.SetPositionAndRotation(transform.position+Vector3.up*1.62f,Quaternion.Euler(pitch,yaw,0));
                return;
            }
            Vector3 target=transform.position+Vector3.up*1.35f;
            // Keep the camera above the floor when looking upward. Tilting the
            // camera instead of orbiting underneath the avatar leaves the sky visible.
            float orbitPitch=Mathf.Max(pitch,8f);
            Vector3 delta=Quaternion.Euler(orbitPitch,yaw,0)*new Vector3(0,0,-5.4f);
            float distance=delta.magnitude;
            if (Physics.SphereCast(target,.18f,delta.normalized,out RaycastHit hit,distance,~(1<<2),QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(.4f,hit.distance-.14f);
            if(model && model.gameObject.activeSelf!=(distance>.8f))model.gameObject.SetActive(distance>.8f);
            Vector3 p=target+delta.normalized*distance;
            cameraView.transform.position=immediate?p:Vector3.Lerp(cameraView.transform.position,p,1-Mathf.Exp(-14*Time.deltaTime));
            float upwardAngle=Mathf.Max(0,8f-pitch)*Mathf.Deg2Rad;
            cameraView.transform.LookAt(target+Vector3.up*(Mathf.Tan(upwardAngle)*distance));
        }
        void OnDestroy()
        {
            if (sitting) sitting.occupant=null;
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if (cameraView) Destroy(cameraView.gameObject);
        }
        void MakeStyles()
        {
            if (title!=null) return;
            title=new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold};
            title.normal.textColor=new Color(.94f,.87f,.74f);
            detail=new GUIStyle(GUI.skin.label){fontSize=13}; detail.normal.textColor=new Color(.86f,.82f,.73f);
            hint=new GUIStyle(detail){fontSize=17,fontStyle=FontStyle.Bold};
            exitText=new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold,wordWrap=true};exitText.normal.textColor=Color.white;
            exitChoice=new GUIStyle(GUI.skin.button){fontSize=19,fontStyle=FontStyle.Bold};
            var uiFont=Resources.Load<Font>("Inter-SemiBold");
            if(uiFont){title.font=uiFont;detail.font=uiFont;hint.font=uiFont;exitText.font=uiFont;exitChoice.font=uiFont;}
            try
            {
                if(System.Array.Exists(Font.GetOSInstalledFontNames(),n=>n.Equals("Malgun Gothic",System.StringComparison.OrdinalIgnoreCase) || n=="맑은 고딕"))
                {var font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",28);if(font){exitText.font=font;exitChoice.font=font;koreanExit=true;}}
            }
            catch(System.Exception){koreanExit=false;}
        }
        static void Panel(Rect r,float alpha=.8f)
        {
            var old=GUI.color; GUI.color=new Color(.10f,.08f,.06f,alpha); GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old;
        }
        void OnGUI()
        {
            if (currentSpace==Portal.Space.Quiz && !exitConfirm) return;
            MakeStyles(); var previous=GUI.matrix;
            float s=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*s)*.5f,(Screen.height-720*s)*.5f,0),Quaternion.identity,Vector3.one*s);
            Panel(new Rect(20,20,335,77));
            GUI.Label(new Rect(36,27,310,28),"THE GRAND LIBRARY",title);
            string room=currentSpace==Portal.Space.Library?"LOUNGE":currentSpace==Portal.Space.Mafia?"MAFIA ROOM":currentSpace==Portal.Space.Quiz?"QUIZ ROOM  /  SOLO":"ESCAPE ROOM";
            GUI.Label(new Rect(36,60,310,24),room,detail);
            if (nearestDoor || nearest || sitting) { Panel(new Rect(480,565,320,45)); GUI.Label(new Rect(498,575,300,28),sitting?"E   Stand up":nearestDoor?"E   Enter " + nearestDoor.destinationLabel:"E   Sit down",hint); }
            if (arrivalTime>0 && Time.time-arrivalTime<2.5f)
            { Panel(new Rect(500,110,280,45)); GUI.Label(new Rect(518,120,250,25),"ARRIVED  /  "+room,hint); }
            if(exitConfirm)
            {
                Panel(new Rect(360,230,560,225),.97f);
                string question=koreanExit?(currentSpace==Portal.Space.Mafia?"마피아룸을 나가시겠습니까?":"퀴즈룸을 나가시겠습니까?"):("Leave the "+room.ToLowerInvariant()+"?");
                GUI.Label(new Rect(400,270,490,60),question,exitText);
                if(GUI.Button(new Rect(400,365,220,60),koreanExit?"예":"YES",exitChoice))ConfirmRoomExit();
                if(GUI.Button(new Rect(660,365,220,60),koreanExit?"아니오":"NO",exitChoice))CancelRoomExit();
            }
            GUI.matrix=previous;
        }
    }
}
