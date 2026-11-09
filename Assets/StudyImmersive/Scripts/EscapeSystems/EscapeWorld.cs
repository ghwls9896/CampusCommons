using System;
using System.Collections.Generic;
using CampusCommons.LibraryCharacter;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CampusCommons.StudyImmersive
{
    /// <summary>Round-scoped items, teacher and maintenance patrols for the escape room.</summary>
    public sealed class EscapeWorld : MonoBehaviour
    {
        sealed class Pickup
        {
            public GameObject root;
            public EscapeGame.InventoryItem item;
            public bool coin;
        }
        sealed class Patrol
        {
            public GameObject root;
            public CharacterController body;
            public readonly List<Vector3> route=new List<Vector3>();
            public int next;
            public float replan;
        }
        EscapeGame game;
        Transform player;
        SchoolGrid map;
        Door[] doors;
        Sprite soda, padlock, note, professorSprite, workerSprite;
        readonly List<Pickup> pickups=new List<Pickup>();
        readonly List<Vector3> usedPositions=new List<Vector3>();
        readonly List<Material> materials=new List<Material>();
        readonly List<ConstructionBarrier> barriers=new List<ConstructionBarrier>();
        Patrol professor,worker;
        GameObject vending;
        int coins;
        float speedRemaining,professorCooldown,reviewRemaining,barrierCountdown;
        bool professorQuestion;
        CodeQuestions.Question question;
        public bool BlocksMovement=>professorQuestion || reviewRemaining>0f;
        public bool HasSpeedBoost=>speedRemaining>0f;
        public int Coins=>coins;
        public bool ProfessorQuestionOpen=>professorQuestion;
        public float ReviewRemaining=>reviewRemaining;
        public int ActiveBarrierCount { get { barriers.RemoveAll(b=>!b);return barriers.Count; } }
        public int PickupCount=>pickups.Count;
        public string NearbyHint
        {
            get
            {
                foreach(var pickup in pickups)if(pickup.root && NearAndVisible(pickup.root.transform.position,false,1.65f))
                    return pickup.coin?"E / 클릭  동전 줍기":"E / 클릭  아이템 줍기";
                if(vending && NearAndVisible(vending.transform.position+Vector3.up*.8f,false,1.8f))return "E / 클릭  캔음료 구매 (동전 1개)";
                return null;
            }
        }

        public static EscapeWorld Create(EscapeGame owner,Transform player,SchoolGrid grid,Door[] roomDoors)
        {
            var root=new GameObject("Escape world systems");
            var world=root.AddComponent<EscapeWorld>();
            world.game=owner;world.player=player;world.map=grid;world.doors=roomDoors??Array.Empty<Door>();
            world.Initialize();return world;
        }
        void Initialize()
        {
            soda=Resources.Load<Sprite>("EscapeItemIcons/SodaCan");
            padlock=Resources.Load<Sprite>("EscapeItemIcons/Padlock");
            note=Resources.Load<Sprite>("EscapeItemIcons/ExcuseNote");
            professorSprite=Resources.Load<Sprite>("EscapePixels/CodingProfessor");
            workerSprite=Resources.Load<Sprite>("EscapePixels/MaintenanceWorker");
            if(!map || !player)return;
            usedPositions.Add(player.position);
            foreach(var p in game.PendingSheetPositions)usedPositions.Add(p);
            SpawnVending();
            for(int i=0;i<6;i++)SpawnPickup(EscapeGame.InventoryItem.Empty,true);
            for(int i=0;i<2;i++)SpawnPickup(EscapeGame.InventoryItem.Padlock,false);
            for(int i=0;i<2;i++)SpawnPickup(EscapeGame.InventoryItem.ExcuseNote,false);
            professor=SpawnPatrol("Professor",professorSprite,2.0f);
            worker=SpawnPatrol("Maintenance Worker",workerSprite,2.0f);
            barrierCountdown=Random.Range(18f,28f);
        }
        Sprite ItemSprite(EscapeGame.InventoryItem item)
        {
            return item==EscapeGame.InventoryItem.Soda?soda:item==EscapeGame.InventoryItem.Padlock?padlock:item==EscapeGame.InventoryItem.ExcuseNote?note:null;
        }
        public Texture2D ItemTexture(EscapeGame.InventoryItem item){var sprite=ItemSprite(item);return sprite?sprite.texture:null;}
        Vector3 Flat(Vector3 p){p.y=0;return p;}
        bool TryPoint(float minFromPlayer,out Vector3 result)
        {
            result=Vector3.zero;if(!map || map.links==null || map.links.Length==0)return false;
            var route=new List<Vector3>();
            for(int i=0;i<500;i++)
            {
                int index=Random.Range(0,map.links.Length);if(map.links[index]==0)continue;
                var candidate=map.Position(index);float dist=Vector3.Distance(Flat(candidate),Flat(player.position));
                if(dist<minFromPlayer || dist>45f)continue;
                bool tooClose=false;foreach(var used in usedPositions)if(Vector3.Distance(Flat(candidate),Flat(used))<1.8f){tooClose=true;break;}
                if(tooClose || !map.FindPath(player.position,candidate,route))continue;
                result=candidate;usedPositions.Add(candidate);return true;
            }
            return false;
        }
        Material Material(Color color)
        {
            var shader=Shader.Find("Unlit/Color");if(!shader)shader=Shader.Find("Standard");
            var material=new Material(shader){color=color};materials.Add(material);return material;
        }
        GameObject Cube(Transform parent,string name,Vector3 local,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.layer=2;
            obj.transform.SetParent(parent,false);obj.transform.localPosition=local;obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            var collider=obj.GetComponent<Collider>();if(collider)Destroy(collider);
            return obj;
        }
        void SpawnVending()
        {
            if(!TryPoint(5f,out Vector3 p))return;
            vending=new GameObject("Soda vending machine");vending.layer=2;vending.transform.SetParent(transform,false);
            Vector3 wallDirection=Vector3.zero;float nearest=3.1f;
            foreach(var direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                if(WallDistance(p,direction,out float distance) && distance>.6f && distance<nearest)
                {nearest=distance;wallDirection=direction;}
            if(wallDirection!=Vector3.zero)
            {
                vending.transform.position=p+wallDirection*Mathf.Max(0f,nearest-.25f);
                vending.transform.rotation=Quaternion.LookRotation(wallDirection,Vector3.up);
            }
            else vending.transform.position=p;
            usedPositions.Add(vending.transform.position);
            var blue=Material(new Color(.08f,.22f,.48f));var grey=Material(new Color(.30f,.33f,.36f));
            var pale=Material(new Color(.74f,.84f,.88f));
            Cube(vending.transform,"Machine",new Vector3(0,.9f,0),new Vector3(.85f,1.8f,.42f),blue);
            Cube(vending.transform,"Display",new Vector3(0,1.24f,-.22f),new Vector3(.66f,.62f,.03f),pale);
            Cube(vending.transform,"Dispenser",new Vector3(0,.43f,-.22f),new Vector3(.50f,.22f,.03f),grey);
            if(soda)
            {
                var preview=new GameObject("Soda preview");preview.layer=2;preview.transform.SetParent(vending.transform,false);
                preview.transform.localPosition=new Vector3(0,1.24f,-.245f);
                preview.transform.localScale=Vector3.one*(.48f/Mathf.Max(.01f,soda.rect.height/soda.pixelsPerUnit));
                preview.AddComponent<SpriteRenderer>().sprite=soda;
            }
            var label=new GameObject("Vending label");label.transform.SetParent(vending.transform,false);label.transform.localPosition=new Vector3(0,1.68f,-.245f);
            label.transform.localRotation=Quaternion.identity;label.transform.localScale=Vector3.one*.020f;
            var text=label.AddComponent<TextMesh>();text.text="음료";text.font=Resources.Load<Font>("KIT/NotoSansKR");text.fontSize=38;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
            if(text.font)text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
        }
        void SpawnPickup(EscapeGame.InventoryItem item,bool coin)
        {
            if(!TryPoint(3f,out Vector3 p))return;
            var root=new GameObject(coin?"Coin":item.ToString());root.layer=2;root.transform.SetParent(transform,false);root.transform.position=p+Vector3.up*.70f;
            if(coin)
            {
                var mesh=GameObject.CreatePrimitive(PrimitiveType.Cylinder);mesh.layer=2;mesh.name="Coin graphic";mesh.transform.SetParent(root.transform,false);
                mesh.transform.localRotation=Quaternion.Euler(90,0,0);mesh.transform.localScale=new Vector3(.26f,.025f,.26f);
                mesh.GetComponent<Renderer>().sharedMaterial=Material(new Color(1f,.68f,.08f));Destroy(mesh.GetComponent<Collider>());root.AddComponent<FaceCamera>();
            }
            else CreateBillboard(root.transform,ItemSprite(item),.55f,"Pickup sprite");
            pickups.Add(new Pickup{root=root,item=item,coin=coin});
        }
        void CreateBillboard(Transform parent,Sprite sprite,float height,string name)
        {
            if(!sprite)return;var child=new GameObject(name);child.layer=2;child.transform.SetParent(parent,false);
            var renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=sprite;child.AddComponent<FaceCamera>();
            float scale=height/Mathf.Max(.01f,sprite.rect.height/sprite.pixelsPerUnit);child.transform.localScale=Vector3.one*scale;
        }
        Patrol SpawnPatrol(string name,Sprite sprite,float height)
        {
            if(!TryPoint(8f,out Vector3 p))return null;
            var root=new GameObject(name);root.layer=2;root.transform.SetParent(transform,false);root.transform.position=p+Vector3.up*.08f;
            var body=root.AddComponent<CharacterController>();body.height=1.8f;body.radius=.27f;body.center=Vector3.up*.9f;
            var picture=new GameObject("Character sprite");picture.layer=2;picture.transform.SetParent(root.transform,false);picture.transform.localPosition=Vector3.up*.95f;
            CreateBillboard(picture.transform,sprite,height,"Artwork");
            return new Patrol{root=root,body=body};
        }
        void Update()
        {
            if(!game || !game.IsRunning || game.IsEnded || game.IsExitConfirmOpen)return;
            float dt=Time.deltaTime;
            if(speedRemaining>0f)speedRemaining=Mathf.Max(0,speedRemaining-dt);
            if(professorCooldown>0f)professorCooldown=Mathf.Max(0,professorCooldown-dt);
            if(reviewRemaining>0f)
            {
                reviewRemaining=Mathf.Max(0,reviewRemaining-dt);
                if(reviewRemaining<=0f)ReleaseProfessor();
            }
            if(!game.IsAnswering)
            {
                if(!BlocksMovement)MovePatrol(professor,3.4f,dt);
                MovePatrol(worker,3f,dt);
                if(!BlocksMovement && professorCooldown<=0 && CanMeetProfessor())BeginProfessorQuestion();
                barrierCountdown-=dt;
                if(barrierCountdown<=0f)
                {
                    barrierCountdown=Random.Range(25f,40f);
                    if(ActiveBarrierCount==0 && worker!=null && worker.root)
                    {
                        Vector3 ahead=worker.root.transform.position;
                        if(worker.next<worker.route.Count)
                        {
                            Vector3 direction=worker.route[worker.next]-ahead;direction.y=0;
                            if(direction.sqrMagnitude>.01f)ahead+=direction.normalized*1.5f;
                        }
                        int cell=map.NearestCell(ahead);
                        if(cell<0 || !TryPlaceSafeBarrier(map.Position(cell)))barrierCountdown=Random.Range(4f,7f);
                    }
                }
            }
        }
        static float Horizontal(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        bool CanMeetProfessor()
        {
            if(professor==null || !professor.root || !player)return false;
            Vector3 origin=professor.root.transform.position+Vector3.up*1.1f,target=player.position+Vector3.up*1.1f;
            if(Horizontal(origin,target)>=1.25f || Mathf.Abs(origin.y-target.y)>1.2f)return false;
            Vector3 delta=target-origin;if(delta.sqrMagnitude<.0001f)return true;
            bool previous=Physics.queriesHitBackfaces;RaycastHit[] hits;
            try{Physics.queriesHitBackfaces=true;hits=Physics.RaycastAll(origin,delta.normalized,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);}
            finally{Physics.queriesHitBackfaces=previous;}
            foreach(var hit in hits)
                if(hit.collider && hit.collider.transform!=player && !hit.collider.transform.IsChildOf(player))return false;
            return true;
        }
        void MovePatrol(Patrol npc,float speed,float dt)
        {
            if(npc==null || !npc.root || !npc.body || !map)return;
            npc.replan-=dt;
            if(npc.next>=npc.route.Count || npc.replan<=0f)
            {
                npc.route.Clear();npc.next=0;npc.replan=4f;
                for(int i=0;i<25;i++)
                {
                    int index=Random.Range(0,map.links.Length);if(map.links[index]==0)continue;
                    Vector3 point=map.Position(index);
                    if(Horizontal(point,npc.root.transform.position)>12f || Horizontal(point,npc.root.transform.position)<2f)continue;
                    if(map.FindPath(npc.root.transform.position,point,npc.route,CanTraverse) && npc.route.Count>0)break;
                }
            }
            while(npc.next<npc.route.Count && Horizontal(npc.root.transform.position,npc.route[npc.next])<.18f)npc.next++;
            if(npc.next>=npc.route.Count){npc.body.Move(Vector3.down*2f*dt);return;}
            Vector3 delta=npc.route[npc.next]-npc.root.transform.position;delta.y=0;
            foreach(var door in doors)
            {
                if(!door || door.IsFullyOpen || door.DistanceFrom(npc.root.transform.position)>1.1f)continue;
                Vector3 toward=door.NearestPoint(npc.root.transform.position)-npc.root.transform.position;toward.y=0;
                if(toward.sqrMagnitude>.01f && Vector3.Dot(delta.normalized,toward.normalized)<.1f)continue;
                if(door.IsLocked){npc.route.Clear();npc.replan=0;return;}
                door.OpenForMonster();if(!door.IsFullyOpen)return;
            }
            var before=npc.root.transform.position;npc.body.Move(delta.normalized*Mathf.Min(speed*dt,delta.magnitude)+Vector3.down*2f*dt);
            if(Horizontal(before,npc.root.transform.position)<.001f && dt>0)npc.replan-=dt*3f;
        }
        bool CanTraverse(Vector3 a,Vector3 b)=>!IsEdgeBlocked(a,b);
        static bool Crosses(Vector3 a,Vector3 b,Vector3 center,Quaternion rotation,float width)
        {
            var inverse=Quaternion.Inverse(rotation);Vector3 x=inverse*(a-center),y=inverse*(b-center);
            if((x.z>.20f && y.z>.20f)||(x.z<-.20f && y.z<-.20f))return false;
            float fraction=Mathf.Abs(x.z-y.z)<.0001f?.5f:Mathf.Clamp01(x.z/(x.z-y.z));
            return Mathf.Abs(Mathf.Lerp(x.x,y.x,fraction))<=width*.5f+.32f;
        }
        public bool IsEdgeBlocked(Vector3 a,Vector3 b)
        {
            barriers.RemoveAll(barricade=>!barricade);
            foreach(var barrier in barriers)if(barrier && Crosses(a,b,barrier.transform.position,barrier.transform.rotation,barrier.Width))return true;
            foreach(var door in doors)if(door && door.IsLocked && Crosses(a,b,door.DoorwayCenter,door.transform.rotation,door.DoorwayWidth))return true;
            return false;
        }
        bool WallDistance(Vector3 p,Vector3 direction,out float distance)
        {
            distance=0;bool previous=Physics.queriesHitBackfaces;RaycastHit[] hits;
            try{Physics.queriesHitBackfaces=true;hits=Physics.RaycastAll(p+Vector3.up*1.1f,direction,3.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);}
            finally{Physics.queriesHitBackfaces=previous;}
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.collider.GetComponentInParent<Door>() || hit.collider.GetComponentInParent<EscapeWorld>())continue;
                if(hit.distance<.45f)continue;distance=hit.distance;return true;
            }
            return false;
        }
        public bool TryPlaceSafeBarrier(Vector3 around)
        {
            if(!game || !map || !player || ActiveBarrierCount>0 || Horizontal(around,player.position)<3f)return false;
            foreach(var axis in new[]{Vector3.right,Vector3.forward})
            {
                if(!WallDistance(around,axis,out float positive) || !WallDistance(around,-axis,out float negative))continue;
                float width=positive+negative;if(width<1.2f || width>4.2f)continue;
                Vector3 center=Flat(around)+axis*((positive-negative)*.5f);
                Quaternion rotation=axis==Vector3.right?Quaternion.identity:Quaternion.Euler(0,90,0);
                bool CanUse(Vector3 a,Vector3 b)=>!IsEdgeBlocked(a,b) && !Crosses(a,b,center,rotation,width);
                var path=new List<Vector3>();bool reachable=true;
                foreach(var target in game.PendingSheetPositions)if(!map.FindPath(player.position,target,path,CanUse)){reachable=false;break;}
                if(!reachable)continue;
                bool exitReachable=false;foreach(var exit in game.ExitPositions)if(map.FindPath(player.position,exit,path,CanUse)){exitReachable=true;break;}
                if(!exitReachable)continue;
                var barrier=ConstructionBarrier.Create(center,rotation,width,Random.Range(20f,30f));barrier.transform.SetParent(transform,true);barriers.Add(barrier);return true;
            }
            return false;
        }
        public void HandleInventoryKeys()
        {
            if(Input.GetKeyDown(KeyCode.Alpha1))UseItem(0);
            if(Input.GetKeyDown(KeyCode.Alpha2))UseItem(1);
            if(Input.GetKeyDown(KeyCode.Alpha3))UseItem(2);
        }
        public bool UseItem(int slot)
        {
            var item=game.ItemAt(slot);
            if(item==EscapeGame.InventoryItem.Soda && !BlocksMovement)
            {if(!game.RemoveItem(slot))return false;speedRemaining=10f;game.Notice("이동속도가 빨라졌습니다.","Speed increased.");return true;}
            if(item==EscapeGame.InventoryItem.ExcuseNote && BlocksMovement)
            {if(!game.RemoveItem(slot))return false;ReleaseProfessor();game.Notice("공결증을 사용했습니다.","Used excuse note.");return true;}
            if(item==EscapeGame.InventoryItem.Padlock && !BlocksMovement)
            {
                Door nearest=null;float best=1.8f;
                foreach(var door in doors)if(door && !door.exitDoor){float d=door.DistanceFrom(player.position);if(d<best){nearest=door;best=d;}}
                if(!nearest || !nearest.TryLock(15f,padlock)){game.Notice("잠글 수 있는 문에 가까이 가세요.","Approach a door to lock it.");return false;}
                game.RemoveItem(slot);game.Notice("15초 동안 문이 잠겼습니다.","Door locked for 15 seconds.");return true;
            }
            return false;
        }
        bool NearAndVisible(Vector3 point,bool requireAim,float distance)
        {
            Vector3 head=player.position+Vector3.up*.8f;Vector3 delta=point-head;
            if(delta.magnitude>distance)return false;
            if(Physics.Linecast(head,point,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return false;
            if(!requireAim)return true;
            return Camera.main && Vector3.Dot(Camera.main.transform.forward,delta.normalized)>.58f;
        }
        public bool TryInteract(bool requireAim)
        {
            if(!player || BlocksMovement)return false;
            Pickup nearest=null;float best=1.65f;
            foreach(var pickup in pickups)
            {
                if(!pickup.root || !NearAndVisible(pickup.root.transform.position,requireAim,best))continue;
                best=Vector3.Distance(player.position+Vector3.up*.8f,pickup.root.transform.position);nearest=pickup;
            }
            if(nearest!=null)
            {
                if(nearest.coin){coins++;game.Notice("동전을 주웠습니다.","Coin collected.");}
                else if(game.AddItem(nearest.item))game.Notice("아이템을 주웠습니다.","Item collected.");
                else {game.Notice("인벤토리가 가득 찼습니다.","Inventory is full.");return true;}
                pickups.Remove(nearest);Destroy(nearest.root);return true;
            }
            if(vending && NearAndVisible(vending.transform.position+Vector3.up*.8f,requireAim,1.8f))
            {
                if(coins<1)game.Notice("동전이 필요합니다.","A coin is required.");
                else if(game.AddItem(EscapeGame.InventoryItem.Soda)){coins--;game.Notice("캔음료를 뽑았습니다.","Soda acquired.");}
                else game.Notice("인벤토리가 가득 찼습니다.","Inventory is full.");
                return true;
            }
            return false;
        }
        void BeginProfessorQuestion()
        {
            professorQuestion=true;question=CodeQuestions.RandomThree()[0];Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public bool AnswerProfessor(int answer)
        {
            if(!professorQuestion || question==null || answer<0 || answer>=question.Options.Length)return false;
            if(answer==question.Correct)ReleaseProfessor();
            else{professorQuestion=false;reviewRemaining=10f;}
            return true;
        }
        void ReleaseProfessor()
        {
            professorQuestion=false;reviewRemaining=0;professorCooldown=25f;question=null;
            if(game && game.IsRunning && !game.IsAnswering && !game.IsExitConfirmOpen){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        }
        public void DrawStatus(GUIStyle text,bool korean)
        {
            GUI.Label(new Rect(1034,93,225,25),(korean?"동전 ":"COINS ")+coins,text);
            if(speedRemaining>0f)GUI.Label(new Rect(1034,119,225,25),(korean?"속도 증가 ":"SPEED ")+Mathf.CeilToInt(speedRemaining),text);
        }
        public void DrawProfessor(GUIStyle title,GUIStyle text,GUIStyle button,bool korean)
        {
            var old=GUI.color;GUI.color=new Color(.08f,.08f,.07f,.96f);GUI.DrawTexture(new Rect(270,110,740,505),Texture2D.whiteTexture);GUI.color=old;
            GUI.Label(new Rect(305,130,660,45),korean?"교수님의 즉석 질문":"PROFESSOR'S QUESTION",title);
            if(reviewRemaining>0f)
            {
                GUI.Label(new Rect(310,205,650,80),(korean?"오답입니다. 복습 중: ":"Reviewing: ")+Mathf.CeilToInt(reviewRemaining)+(korean?"초":"s"),text);
                GUI.Label(new Rect(310,295,650,70),korean?"공결증이 있으면 숫자 키로 바로 사용할 수 있습니다.":"Use an excuse note with its number key.",text);return;
            }
            if(question==null)return;
            GUI.Label(new Rect(310,185,650,145),question.Prompt,text);
            for(int i=0;i<question.Options.Length;i++)
            {
                float x=308+(i%2)*335,y=345+(i/2)*77;
                if(GUI.Button(new Rect(x,y,310,61),question.Options[i],button)){AnswerProfessor(i);break;}
            }
        }
        public void Shutdown()
        {
            foreach(var door in doors)if(door && door.IsLocked)door.Unlock();
            foreach(var barrier in barriers)if(barrier)Destroy(barrier.gameObject);
            barriers.Clear();
        }
        void OnDestroy(){foreach(var material in materials)if(material)Destroy(material);}
    }
}
