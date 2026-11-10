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
            public bool pursuing,hasDestination;
            public Vector3 destination;
            public AudioSource audio;
            public AudioClip ambient;
            public float ambientWait;
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
        readonly List<GameObject> vendingMachines=new List<GameObject>();
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
                foreach(var machine in vendingMachines)if(machine && NearAndVisible(machine.transform.position+Vector3.up*.8f,false,1.8f))return "E / 클릭  캔음료 구매 (동전 1개)";
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
            if(game.monster)usedPositions.Add(game.monster.transform.position);
            foreach(var p in game.PendingSheetPositions)usedPositions.Add(p);
            // Put the professor in a nearby visible part of the school so players can find them.
            professor=SpawnPatrol("Professor",professorSprite,2.0f,true);
            worker=SpawnPatrol("Maintenance Worker",workerSprite,2.0f,false);
            var machines=EscapeSpawnPoint.FindAll(EscapeSpawnPoint.Kind.Vending);
            if(machines.Count>0)foreach(var marker in machines)SpawnVending(marker.transform.position,marker.transform.rotation);
            else
            {
                SpawnVending(new Vector3(504f,0,41f));
                SpawnVending(new Vector3(482f,0,33.5f));
                SpawnVending(new Vector3(527.5f,0,29f));
            }
            for(int i=0;i<3;i++)SpawnPickup(EscapeGame.InventoryItem.Empty,true);
            SpawnPickup(EscapeGame.InventoryItem.Padlock,false);
            SpawnPickup(EscapeGame.InventoryItem.ExcuseNote,false);
            barrierCountdown=Random.Range(18f,28f);
        }
        Sprite ItemSprite(EscapeGame.InventoryItem item)
        {
            return item==EscapeGame.InventoryItem.Soda?soda:item==EscapeGame.InventoryItem.Padlock?padlock:item==EscapeGame.InventoryItem.ExcuseNote?note:null;
        }
        public Texture2D ItemTexture(EscapeGame.InventoryItem item){var sprite=ItemSprite(item);return sprite?sprite.texture:null;}
        Vector3 Flat(Vector3 p){p.y=0;return p;}
        bool TryPoint(float minFromPlayer,out Vector3 result,float separation=1.8f)
        {
            result=Vector3.zero;if(!map || map.links==null || map.links.Length==0)return false;
            var route=new List<Vector3>();
            for(int i=0;i<500;i++)
            {
                int index=Random.Range(0,map.links.Length);if(map.links[index]==0)continue;
                var candidate=map.Position(index);float dist=Vector3.Distance(Flat(candidate),Flat(player.position));
                if(dist<minFromPlayer || dist>45f)continue;
                bool tooClose=false;foreach(var used in usedPositions)if(Vector3.Distance(Flat(candidate),Flat(used))<separation){tooClose=true;break;}
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
        bool TryVendingPoint(Vector3 anchor,out Vector3 position,out Vector3 towardWall)
        {
            position=Vector3.zero;towardWall=Vector3.zero;var route=new List<Vector3>();var candidates=new List<int>();
            for(int i=0;i<map.links.Length;i++)if(map.links[i]!=0 && Horizontal(map.Position(i),anchor)<=2.5f)candidates.Add(i);
            candidates.Sort((a,b)=>{int cmp=Horizontal(map.Position(a),anchor).CompareTo(Horizontal(map.Position(b),anchor));return cmp!=0?cmp:a.CompareTo(b);});
            foreach(int index in candidates)
            {
                Vector3 point=map.Position(index);
                if(!map.FindPath(player.position,point,route))continue;
                foreach(var direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                {
                    bool backfaces=Physics.queriesHitBackfaces;RaycastHit[] hits;
                    try{Physics.queriesHitBackfaces=true;hits=Physics.RaycastAll(point+Vector3.up*1.1f,direction,2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);}
                    finally{Physics.queriesHitBackfaces=backfaces;}
                    Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                    if(hits.Length==0)continue;var hit=hits[0];
                    if(!hit.collider || !hit.collider.name.StartsWith("School Structure") || hit.distance<.65f || Mathf.Abs(Vector3.Dot(hit.normal,direction))<.9f)continue;
                    // A corridor has a nearby opposing wall and enough space left to walk past.
                    if(!WallDistance(point,-direction,out float opposite) || hit.distance+opposite<2.5f || hit.distance+opposite>5f)continue;
                    Vector3 place=point+direction*(hit.distance-.24f);
                    bool nearDoor=false;foreach(var door in doors)if(door && door.DistanceFrom(place)<1.4f){nearDoor=true;break;}
                    if(nearDoor)continue;
                    position=place;towardWall=direction;usedPositions.Add(position);return true;
                }
            }
            return false;
        }
        void SpawnVending(Vector3 anchor)
        {
            if(!TryVendingPoint(anchor,out Vector3 p,out Vector3 wallDirection)){Debug.LogWarning("No safe vending position near "+anchor);return;}
            SpawnVending(p,Quaternion.LookRotation(wallDirection,Vector3.up));
        }
        void SpawnVending(Vector3 p,Quaternion rotation)
        {
            usedPositions.Add(p);
            vending=new GameObject("Soda vending machine");vendingMachines.Add(vending);vending.layer=2;vending.transform.SetParent(transform,false);
            vending.transform.position=p;vending.transform.rotation=rotation;
            var blue=Material(new Color(.08f,.22f,.48f));
            Cube(vending.transform,"Machine",new Vector3(0,.9f,0),new Vector3(.85f,1.8f,.42f),blue);
            var image=Resources.Load<Texture2D>("EscapePixels/VendingMachinePhoto");
            if(image)
            {
                var shader=Shader.Find("Unlit/Texture");if(!shader)shader=Shader.Find("Standard");
                var material=new Material(shader);material.mainTexture=image;materials.Add(material);
                var face=GameObject.CreatePrimitive(PrimitiveType.Quad);face.name="Photographic vending front";face.layer=2;
                face.transform.SetParent(vending.transform,false);face.transform.localPosition=new Vector3(0,.9f,-.216f);
                face.transform.localRotation=Quaternion.identity;face.transform.localScale=new Vector3(.85f,1.8f,1f);
                face.GetComponent<Renderer>().sharedMaterial=material;Destroy(face.GetComponent<Collider>());
                // Use the supplied university artwork itself, preserving its Korean name exactly.
                var logo=Resources.Load<Texture2D>("EscapePixels/KIT_WordmarkVending");
                if(logo)
                {
                    var logoMaterial=new Material(shader);logoMaterial.mainTexture=logo;materials.Add(logoMaterial);
                    var decal=GameObject.CreatePrimitive(PrimitiveType.Quad);decal.name="KIT university logo";decal.layer=2;
                    decal.transform.SetParent(vending.transform,false);decal.transform.localPosition=new Vector3(-.12f,.65f,-.219f);
                    decal.transform.localRotation=Quaternion.identity;decal.transform.localScale=new Vector3(.47f,.11f,1f);
                    decal.GetComponent<Renderer>().sharedMaterial=logoMaterial;Destroy(decal.GetComponent<Collider>());
                }
            }
        }
        void SpawnPickup(EscapeGame.InventoryItem item,bool coin)
        {
            if(!TryPoint(5f,out Vector3 p,5f))return;
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
        bool TryVisiblePatrolPoint(out Vector3 result)
        {
            result=Vector3.zero;var route=new List<Vector3>();var candidates=new List<Vector3>();
            for(int index=0;index<map.links.Length;index++)
            {
                if(map.links[index]==0)continue;var point=map.Position(index);float distance=Horizontal(point,player.position);
                if(distance>=6f && distance<=12f)candidates.Add(point);
            }
            for(int i=candidates.Count-1;i>0;i--){int j=Random.Range(0,i+1);var temp=candidates[i];candidates[i]=candidates[j];candidates[j]=temp;}
            foreach(var point in candidates)
            {
                Vector3 eye=player.position+Vector3.up*1.2f,target=point+Vector3.up*1.2f;
                if(Physics.Linecast(eye,target,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                if(!map.FindPath(player.position,point,route))continue;
                result=point;usedPositions.Add(point);return true;
            }
            foreach(var point in candidates)if(map.FindPath(player.position,point,route))
            {result=point;usedPositions.Add(point);return true;}
            return TryPoint(6f,out result);
        }
        Patrol SpawnPatrol(string name,Sprite sprite,float height,bool visible)
        {
            if(!sprite){Debug.LogWarning("Escape patrol sprite missing: "+name);return null;}
            var marker=EscapeSpawnPoint.Find(visible?EscapeSpawnPoint.Kind.Professor:EscapeSpawnPoint.Kind.Worker);
            Vector3 p;
            if(marker){p=marker.transform.position;usedPositions.Add(p);}
            else {if(!(visible?TryVisiblePatrolPoint(out p):TryPoint(8f,out p)))return null;p+=Vector3.up*.08f;}
            var root=new GameObject(name);root.layer=2;root.transform.SetParent(transform,false);root.transform.position=p;
            var body=root.AddComponent<CharacterController>();body.height=1.8f;body.radius=.27f;body.center=Vector3.up*.9f;
            if(name=="Professor") { var playerBody=player.GetComponent<CharacterController>();if(playerBody)Physics.IgnoreCollision(body,playerBody,true); }
            var picture=new GameObject("Character sprite");picture.layer=2;picture.transform.SetParent(root.transform,false);picture.transform.localPosition=Vector3.up*.95f;
            CreateBillboard(picture.transform,sprite,height,"Artwork");
            var sound=root.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=1f;
            sound.rolloffMode=AudioRolloffMode.Linear;sound.minDistance=1.5f;sound.maxDistance=11f;sound.dopplerLevel=0f;sound.volume=.5f;
            bool isProfessor=name=="Professor";
            return new Patrol{root=root,body=body,audio=sound,
                ambient=Resources.Load<AudioClip>(isProfessor?"EscapeSounds/ProfessorMurmur":"EscapeSounds/WorkerTool"),
                ambientWait=Random.Range(isProfessor?3f:6f,isProfessor?5f:8f)};
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
            UpdatePatrolAmbient(professor,dt);
            UpdatePatrolAmbient(worker,dt);
            if(!game.IsAnswering)
            {
                if(!BlocksMovement)
                {
                    bool pursuing=professorCooldown<=0f && CanSeePlayer(5f);
                    if(professor!=null && pursuing && (!professor.pursuing || professor.replan<=dt))
                    {
                        professor.route.Clear();professor.next=0;professor.replan=.5f;
                        professor.destination=player.position;professor.hasDestination=true;
                        if(!map.FindPath(professor.root.transform.position,professor.destination,professor.route,CanTraverse))ReplanPatrol(professor,false);
                    }
                    if(professor!=null)professor.pursuing=pursuing;
                    MovePatrol(professor,4f,dt);
                }
                MovePatrol(worker,3.6f,dt);
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
        void UpdatePatrolAmbient(Patrol npc,float dt)
        {
            if(npc==null || !npc.audio || !npc.ambient)return;
            npc.ambientWait-=dt;
            if(npc.ambientWait<=0f)
            {
                var other=npc==professor?worker:professor;
                if(npc.audio.isPlaying || (other!=null && other.audio && other.audio.isPlaying && Horizontal(npc.root.transform.position,other.root.transform.position)<11f))
                {npc.ambientWait=1f;return;}
                npc.audio.pitch=Random.Range(.97f,1.03f);npc.audio.PlayOneShot(npc.ambient,.55f);npc.ambientWait=Random.Range(5.5f,9f);
            }
        }
        static float Horizontal(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        bool CanMeetProfessor()=>CanSeePlayer(1.25f);
        bool CanSeePlayer(float range)
        {
            if(professor==null || !professor.root || !player)return false;
            Vector3 origin=professor.root.transform.position+Vector3.up*1.1f,target=player.position+Vector3.up*1.1f;
            if(Horizontal(origin,target)>=range || Mathf.Abs(origin.y-target.y)>1.2f)return false;
            Vector3 delta=target-origin;if(delta.sqrMagnitude<.0001f)return true;
            bool previous=Physics.queriesHitBackfaces;RaycastHit[] hits;
            try{Physics.queriesHitBackfaces=true;hits=Physics.RaycastAll(origin,delta.normalized,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);}
            finally{Physics.queriesHitBackfaces=previous;}
            foreach(var hit in hits)
                if(hit.collider && hit.collider.transform!=player && !hit.collider.transform.IsChildOf(player))return false;
            return true;
        }
        bool ReplanPatrol(Patrol npc,bool keepDestination)
        {
            npc.route.Clear();npc.next=0;
            if(keepDestination && npc.hasDestination && map.FindPath(npc.root.transform.position,npc.destination,npc.route,CanTraverse) && npc.route.Count>0)
            {npc.replan=4f;return true;}
            npc.route.Clear();npc.hasDestination=false;
            var candidates=new List<int>();
            for(int i=0;i<map.links.Length;i++)
            {
                if(map.links[i]==0)continue;float distance=Horizontal(map.Position(i),npc.root.transform.position);
                if(distance>=2f && distance<=12f)candidates.Add(i);
            }
            for(int i=candidates.Count-1;i>0;i--){int j=Random.Range(0,i+1);int swap=candidates[i];candidates[i]=candidates[j];candidates[j]=swap;}
            int attempts=0;
            foreach(int candidate in candidates)
            {
                if(attempts++>=96)break;
                Vector3 point=map.Position(candidate);
                if(map.FindPath(npc.root.transform.position,point,npc.route,CanTraverse) && npc.route.Count>0)
                {npc.destination=point;npc.hasDestination=true;npc.replan=4f;return true;}
            }
            npc.route.Clear();npc.replan=.5f;return false;
        }
        bool RouteBlocked(Patrol npc)
        {
            Vector3 from=npc.root.transform.position;
            for(int i=npc.next;i<npc.route.Count;i++)
            {
                Vector3 to=npc.route[i];if(IsEdgeBlocked(from,to))return true;from=to;
            }
            return false;
        }
        void MovePatrol(Patrol npc,float speed,float dt)
        {
            if(npc==null || !npc.root || !npc.body || !map)return;
            npc.replan-=dt;
            while(npc.next<npc.route.Count && Horizontal(npc.root.transform.position,npc.route[npc.next])<.18f)npc.next++;
            if(npc.next<npc.route.Count && RouteBlocked(npc))
            {
                if(!ReplanPatrol(npc,true)){npc.body.Move(Vector3.down*2f*dt);return;}
            }
            else if(npc.next>=npc.route.Count || npc.replan<=0f)
            {
                bool keep=npc.next<npc.route.Count && npc.hasDestination;
                if(!ReplanPatrol(npc,keep)){npc.body.Move(Vector3.down*2f*dt);return;}
            }
            while(npc.next<npc.route.Count && Horizontal(npc.root.transform.position,npc.route[npc.next])<.18f)npc.next++;
            if(npc.next>=npc.route.Count){npc.body.Move(Vector3.down*2f*dt);return;}
            Vector3 delta=npc.route[npc.next]-npc.root.transform.position;delta.y=0;
            foreach(var door in doors)
            {
                if(!door || door.IsFullyOpen || door.DistanceFrom(npc.root.transform.position)>1.1f)continue;
                Vector3 toward=door.NearestPoint(npc.root.transform.position)-npc.root.transform.position;toward.y=0;
                if(toward.sqrMagnitude>.01f && Vector3.Dot(delta.normalized,toward.normalized)<.1f)continue;
                if(door.IsLocked)
                {
                    if(!IsEdgeBlocked(npc.root.transform.position,npc.route[npc.next]))continue;
                    ReplanPatrol(npc,true);return;
                }
                door.OpenForMonster();if(!door.IsFullyOpen)return;
            }
            var before=npc.root.transform.position;npc.body.Move(delta.normalized*Mathf.Min(speed*dt,delta.magnitude)+Vector3.down*2f*dt);
            float walked=Horizontal(before,npc.root.transform.position);
            if(walked<.001f && dt>0)npc.replan-=dt*3f;
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
            foreach(var machine in vendingMachines)if(machine && NearAndVisible(machine.transform.position+Vector3.up*.8f,requireAim,1.8f))
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
            professorQuestion=false;reviewRemaining=0;professorCooldown=20f;question=null;
            if(professor!=null && professor.root && player && map)
            {
                Vector3 direction=player.position-professor.root.transform.position;direction.y=0;
                if(direction.sqrMagnitude<.01f)direction=player.forward;
                professor.route.Clear();professor.next=0;professor.replan=4f;professor.pursuing=false;
                professor.destination=player.position+direction.normalized*5f;professor.hasDestination=true;
                if(!map.FindPath(professor.root.transform.position,professor.destination,professor.route,CanTraverse))ReplanPatrol(professor,false);
            }
            if(game && game.IsRunning && !game.IsAnswering && !game.IsExitConfirmOpen){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        }
        public void DrawStatus(GUIStyle text,bool korean)
        {
            if(speedRemaining>0f)GUI.Label(new Rect(1040,104,205,25),(korean?"속도 증가 ":"SPEED ")+Mathf.CeilToInt(speedRemaining),text);
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
