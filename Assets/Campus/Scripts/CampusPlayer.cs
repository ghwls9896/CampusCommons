using UnityEngine;

namespace CampusCommons
{
    [RequireComponent(typeof(CharacterController))]
    public class CampusPlayer : MonoBehaviour
    {
        public float walkSpeed = 4.2f, runSpeed = 6.5f;
        CharacterController body;
        Transform visual, leftLeg, rightLeg, leftArm, rightArm;
        Camera view;
        CampusSeat[] seats;
        CampusSeat sitting, nearest;
        Vector3 standPosition;
        float yaw, pitch = 23, fallSpeed, gait;
        bool map, waving;
        float waveUntil;
        GUIStyle title, small, badge;

        public static CampusPlayer Create()
        {
            var go = new GameObject("Student - local player");
            go.layer = 2;
            go.transform.position = new Vector3(0, .1f, -6);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.center = new Vector3(0, .9f, 0); cc.radius = .29f;
            cc.stepOffset = .3f; cc.skinWidth = .025f;
            return go.AddComponent<CampusPlayer>();
        }
        void Start()
        {
            body = GetComponent<CharacterController>();
            seats = FindObjectsByType<CampusSeat>(FindObjectsSortMode.None);
            BuildAvatar();
            // Scene cameras can be kept as design cameras without competing with the player camera.
            foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            var cameraGo = new GameObject("Third person camera");
            view = cameraGo.AddComponent<Camera>(); view.tag = "MainCamera";
            view.fieldOfView = 58; view.nearClipPlane = .1f; view.farClipPlane = 140;
            view.backgroundColor = new Color(.78f, .85f, .88f);
            cameraGo.AddComponent<AudioListener>();
            UpdateCamera(true);
        }
        Material Mat(string name, Color color)
        {
            var m = new Material(Shader.Find("Standard")) { name = name, color = color };
            m.SetFloat("_Glossiness", .1f); return m;
        }
        Transform Piece(string name, PrimitiveType type, Transform parent, Vector3 p, Vector3 s, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = s;
            Destroy(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = mat;
            // Ignore avatar in camera obstruction casts.
            go.layer = 2; return go.transform;
        }
        void BuildAvatar()
        {
            visual = new GameObject("Default student avatar").transform; visual.SetParent(transform, false);
            var skin = Mat("Skin", new Color(.83f,.61f,.43f));
            var shirt = Mat("Sage hoodie", new Color(.35f,.58f,.48f));
            var pants = Mat("Trousers", new Color(.2f,.28f,.32f));
            var shoes = Mat("Sneakers", new Color(.93f,.91f,.85f));
            var hair = Mat("Hair", new Color(.16f,.12f,.1f));
            Piece("Hoodie", PrimitiveType.Capsule, visual, new Vector3(0,1.02f,0), new Vector3(.6f,.42f,.38f), shirt);
            Piece("Head", PrimitiveType.Sphere, visual, new Vector3(0,1.57f,0), Vector3.one*.42f, skin);
            Piece("Hair", PrimitiveType.Sphere, visual, new Vector3(0,1.7f,-.025f), new Vector3(.44f,.23f,.42f), hair);
            for (int i=-1;i<=1;i+=2)
            {
                Piece("Eye",PrimitiveType.Sphere,visual,new Vector3(i*.075f,1.59f,.194f),Vector3.one*.038f,hair);
                Transform leg = new GameObject(i<0 ? "Left leg pivot":"Right leg pivot").transform;
                leg.SetParent(visual,false); leg.localPosition = new Vector3(i*.16f,.72f,0);
                Piece("Trouser leg",PrimitiveType.Capsule,leg,new Vector3(0,-.29f,0),new Vector3(.23f,.31f,.23f),pants);
                Piece("Shoe",PrimitiveType.Cube,leg,new Vector3(0,-.62f,.07f),new Vector3(.25f,.13f,.38f),shoes);
                Transform arm = new GameObject(i<0 ? "Left arm pivot":"Right arm pivot").transform;
                arm.SetParent(visual,false); arm.localPosition = new Vector3(i*.39f,1.22f,0);
                Piece("Sleeve",PrimitiveType.Capsule,arm,new Vector3(0,-.19f,0),new Vector3(.2f,.24f,.2f),shirt);
                Piece("Hand",PrimitiveType.Sphere,arm,new Vector3(0,-.43f,0),Vector3.one*.17f,skin);
                if (i<0) { leftLeg=leg; leftArm=arm; } else { rightLeg=leg; rightArm=arm; }
            }
        }
        bool Key(KeyCode key) => Input.GetKey(key);
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) map = !map;
            if (Input.GetKeyDown(KeyCode.Escape)) map = false;
            if (Input.GetKeyDown(KeyCode.R)) { waving=true; waveUntil=Time.time+2; }
            if (waving && Time.time > waveUntil) waving=false;
            if (Input.GetMouseButton(1) && !map)
            {
                yaw += Input.GetAxisRaw("Mouse X") * 3;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 2, 12, 65);
            }
            Cursor.visible = !Input.GetMouseButton(1) || map;
            Cursor.lockState = Input.GetMouseButton(1) && !map ? CursorLockMode.Locked : CursorLockMode.None;
            nearest = null; float best = 1.65f;
            foreach (var seat in seats)
            {
                if (seat.occupant) continue;
                float distance=Vector3.Distance(transform.position+Vector3.up*.6f,seat.transform.position);
                if (distance<best) { best=distance; nearest=seat; }
            }
            if (!map && Input.GetKeyDown(KeyCode.E))
            {
                if (sitting) Stand();
                else if (nearest) TrySit(nearest);
            }
            float x=(Key(KeyCode.D)||Key(KeyCode.RightArrow)?1:0)-(Key(KeyCode.A)||Key(KeyCode.LeftArrow)?1:0);
            float z=(Key(KeyCode.W)||Key(KeyCode.UpArrow)?1:0)-(Key(KeyCode.S)||Key(KeyCode.DownArrow)?1:0);
            Vector3 move = Quaternion.Euler(0,yaw,0) * Vector3.ClampMagnitude(new Vector3(x,0,z),1);
            if (map || sitting) move=Vector3.zero;
            if (!sitting)
            {
                if (body.isGrounded && fallSpeed<0) fallSpeed=-2;
                fallSpeed += Physics.gravity.y * Time.deltaTime;
                body.Move((move*(Key(KeyCode.LeftShift)?runSpeed:walkSpeed)+Vector3.up*fallSpeed)*Time.deltaTime);
                if (move.sqrMagnitude>.01f) visual.rotation=Quaternion.Slerp(visual.rotation,Quaternion.LookRotation(move),Time.deltaTime*12);
                if (transform.position.y < -5) Teleport(new Vector3(0,.1f,-6));
            }
            gait += Time.deltaTime * (Key(KeyCode.LeftShift)?12:8);
            float swing = move.sqrMagnitude>.01f ? Mathf.Sin(gait)*26 : 0;
            leftLeg.localRotation=Quaternion.Euler(sitting?-75:swing,0,0);
            rightLeg.localRotation=Quaternion.Euler(sitting?-75:-swing,0,0);
            leftArm.localRotation=Quaternion.Euler(-swing*.7f,0,0);
            rightArm.localRotation=waving?Quaternion.Euler(0,0,-140+Mathf.Sin(Time.time*14)*20):Quaternion.Euler(swing*.7f,0,0);
        }
        public bool TrySit(CampusSeat seat)
        {
            if (!seat || sitting || seat.occupant || Vector3.Distance(transform.position+Vector3.up*.6f,seat.transform.position)>1.65f) return false;
            standPosition=transform.position; sitting=seat; seat.occupant=this;
            body.enabled=false;
            transform.position=seat.transform.position-Vector3.up*.72f;
            visual.rotation=seat.transform.rotation;
            return true;
        }
        public void Stand()
        {
            if (!sitting) return;
            sitting.occupant=null; sitting=null;
            transform.position=standPosition; body.enabled=true; fallSpeed=-2;
        }
        void Teleport(Vector3 point)
        {
            Stand(); body.enabled=false; transform.position=point; body.enabled=true; fallSpeed=-2; map=false;
        }
        void LateUpdate() { UpdateCamera(false); }
        void UpdateCamera(bool immediate)
        {
            if (!view) return;
            Vector3 target=transform.position+Vector3.up*1.35f;
            Vector3 offset=Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-5.3f);
            float distance=offset.magnitude;
            if (Physics.SphereCast(target,.2f,offset.normalized,out RaycastHit hit,distance,~(1<<2),QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(.35f,hit.distance-.15f);
            Vector3 desired=target+offset.normalized*distance;
            view.transform.position=immediate?desired:Vector3.Lerp(view.transform.position,desired,1-Mathf.Exp(-14*Time.deltaTime));
            view.transform.LookAt(target);
        }
        void OnDestroy()
        {
            if (sitting) sitting.occupant=null;
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if (view) Destroy(view.gameObject);
        }
        void Styles()
        {
            if (title!=null) return;
            title=new GUIStyle(GUI.skin.label) { fontSize=25, fontStyle=FontStyle.Bold };
            title.normal.textColor=new Color(.17f,.24f,.23f);
            small=new GUIStyle(GUI.skin.label) {fontSize=14,wordWrap=true};
            small.normal.textColor=new Color(.24f,.31f,.29f);
            badge=new GUIStyle(small) {fontSize=18,fontStyle=FontStyle.Bold};
        }
        void Panel(Rect r, Color color)
        {
            Color previous=GUI.color; GUI.color=color; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=previous;
        }
        void OnGUI()
        {
            Styles();
            // Logical 1280x720 layout also fits smaller game windows.
            Matrix4x4 previous=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            Panel(new Rect(24,24,390,108),new Color(.97f,.95f,.9f,.96f));
            GUI.Label(new Rect(42,35,355,36),"CAMPUS COMMONS",title);
            string room=transform.position.z>14?(transform.position.x<0?"02 / QUIZ ROOM":"03 / MAFIA CLUB"):"01 / THE LOUNGE";
            GUI.Label(new Rect(42,80,355,30),room,badge);
            Panel(new Rect(24,595,730,101),new Color(.97f,.95f,.9f,.95f));
            GUI.Label(new Rect(42,607,690,28),"WASD / Arrows  Move     Shift  Run     Hold right mouse  Look",small);
            GUI.Label(new Rect(42,637,690,28),"E  Sit / Stand     R  Wave     M  Room map     Esc  Close map",small);
            Panel(new Rect(900,24,356,65),new Color(.97f,.95f,.9f,.96f));
            GUI.Label(new Rect(916,35,323,45),"LOCAL SPACE PROTOTYPE\nMultiplayer connection is not enabled.",small);
            if (sitting || nearest)
            {
                Panel(new Rect(450,520,380,45),new Color(.97f,.95f,.9f,.96f));
                GUI.Label(new Rect(465,529,350,32),sitting?"E  Stand up":"E  Sit here",badge);
            }
            if (map)
            {
                Panel(new Rect(350,160,580,340),new Color(.97f,.95f,.9f,.99f));
                GUI.Label(new Rect(380,183,520,40),"CHOOSE A SPACE",title);
                if (GUI.Button(new Rect(380,242,245,95),"QUIZ ROOM\n12 seats")) Teleport(new Vector3(-9,.1f,16));
                if (GUI.Button(new Rect(645,242,255,95),"MAFIA CLUB\n10 seats")) Teleport(new Vector3(9,.1f,16));
                if (GUI.Button(new Rect(380,352,520,95),"THE LOUNGE\nSofas / study tables / social space")) Teleport(new Vector3(0,.1f,-6));
                GUI.Label(new Rect(380,461,520,25),"Walk through the open doorways to move between rooms.",small);
            }
            GUI.matrix=previous;
        }
    }
}
