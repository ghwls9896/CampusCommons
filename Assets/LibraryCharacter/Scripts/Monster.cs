using System.Collections.Generic;
using CampusCommons.StudyImmersive;
using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>School-grid navigation, visual detection, pursuit and investigation.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Monster : MonoBehaviour
    {
        public enum Behaviour { Patrol, Chase, Search, Investigate }
        public SchoolGrid map;
        public Transform picture;
        public float graceSeconds=8;
        public float speedIncrease=.22f;
        public float maxSpeed=4.2f;
        [Header("Perception")]
        public float sightDistance=150f;
        public float searchSeconds=9f;
        public float roomSearchRange=8f;
        public float roomSearchInterval=7f;
        public float doorHearingRange=10f;
        [Header("After the final sheet")]
        public float finalSpeedMultiplier=2.2f;
        [Header("Sounds (optional overrides)")]
        public AudioClip footstepClip;
        public AudioClip alertClip;
        public float soundRange=20f;

        CharacterController body;
        EscapeGame game;
        Transform target;
        Vector3 spawn,heading,lastSeen,goal,heardPosition,picturePosition,pictureScale,blockedFrom,blockedTo;
        Door[] doors;
        Door waitingDoor;
        readonly List<Vector3> path=new List<Vector3>();
        readonly List<Vector3> roomRoute=new List<Vector3>();
        readonly List<Collider> openPanels=new List<Collider>();
        int next;
        float wakeRemaining,repathRemaining,searchRemaining,lostSightRemaining,pauseAtPoint,stuckTime,footDistance,gait,roomSearchRemaining,hearingRemaining,blockedUntil;
        bool running,hasGoal,awakened;
        Door lastInvestigatedDoor;
        AudioSource steps,alert;
        Behaviour state;
        public float Speed{get;private set;}
        public Behaviour State=>state;
        public bool IsAwakened=>running && awakened;
        public bool IsFinalHunt=>running && awakened && game && game.ReadyToExit;
        public float CurrentMoveSpeed=>Speed*(IsFinalHunt?Mathf.Max(1f,finalSpeedMultiplier):1f);
        public bool IsChasing=>running && awakened && state==Behaviour.Chase && wakeRemaining<=0 && game && game.IsRunning && !game.IsEnded && !game.IsAnswering && !game.IsExitConfirmOpen;

        void Awake()
        {
            spawn=transform.position;body=GetComponent<CharacterController>();
            if(!picture){var face=GetComponentInChildren<FaceCamera>(true);if(face)picture=face.transform;}
            if(picture){picturePosition=picture.localPosition;pictureScale=picture.localScale;picture.gameObject.SetActive(false);}
            body.enabled=false;
            if(!footstepClip)footstepClip=Resources.Load<AudioClip>("CrowFootstep");
            if(!alertClip)alertClip=Resources.Load<AudioClip>("CrowAlert");
            steps=CreateSoundSource();alert=CreateSoundSource();
        }
        AudioSource CreateSoundSource()
        {
            var source=gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Logarithmic;
            source.minDistance=1.4f;source.maxDistance=soundRange;source.dopplerLevel=0;
            return source;
        }
        public void Begin(EscapeGame owner,Transform player,EscapeGame.Mode mode)
        {
            game=owner;target=player;running=true;awakened=false;path.Clear();next=0;hasGoal=false;waitingDoor=null;
            Speed=mode==EscapeGame.Mode.Easy?2.65f:mode==EscapeGame.Mode.Medium?3.1f:mode==EscapeGame.Mode.Hard?3.5f:3.25f;
            var marker=EscapeSpawnPoint.Find(EscapeSpawnPoint.Kind.Monster);
            body.enabled=false;transform.position=marker?marker.transform.position:InitialPosition(player);if(picture)picture.gameObject.SetActive(true);
            heading=player?player.position-transform.position:Vector3.forward;heading.y=0;
            if(heading.sqrMagnitude<.01f)heading=Vector3.forward;heading.Normalize();
            doors=FindObjectsByType<Door>();wakeRemaining=graceSeconds;repathRemaining=0;pauseAtPoint=0;
            stuckTime=0;blockedUntil=0;footDistance=0;gait=0;lostSightRemaining=0;roomSearchRemaining=3f;lastInvestigatedDoor=null;
            state=Behaviour.Patrol;StopSounds();ResetPicture();
        }
        Vector3 InitialPosition(Transform player)
        {
            if(!player || !map)return spawn;
            Vector3 forward=Camera.main?Camera.main.transform.forward:player.forward;forward.y=0;
            if(forward.sqrMagnitude<.01f)forward=Vector3.forward;forward.Normalize();
            var route=new List<Vector3>();
            for(float distance=5f;distance>=2.5f;distance-=.5f)
            {
                Vector3 wanted=player.position+forward*distance;int cell=map.NearestCell(wanted);if(cell<0)continue;
                Vector3 point=map.Position(cell);point.y=player.position.y;
                Vector3 delta=point-player.position;delta.y=0;
                if(delta.magnitude<2.3f || Vector3.Dot(delta.normalized,forward)<.94f)continue;
                if(!map.FindPath(player.position,point,route) || !HasClearSight(player.position+Vector3.up*1.2f,point+Vector3.up*1.2f))continue;
                return point;
            }
            return spawn;
        }
        public void HearDoor(Vector3 position)
        {
            if(!running || !awakened || !enabled || !body || !body.enabled || !game || !game.IsRunning || game.IsEnded || game.IsAnswering || game.IsExitConfirmOpen || IsFinalHunt || state==Behaviour.Chase)return;
            if((transform.position-position).sqrMagnitude>doorHearingRange*doorHearingRange || !map)return;
            int cell=map.NearestCell(position);if(cell<0)return;
            SetState(Behaviour.Investigate);
            heardPosition=position;goal=map.Position(cell);hasGoal=true;hearingRemaining=20f;waitingDoor=null;
            path.Clear();next=0;repathRemaining=0;pauseAtPoint=0;
        }
        public void SpeedUp()
        {
            if(!running)return;
            Speed=Mathf.Min(maxSpeed,Speed+speedIncrease);
            if(!awakened)
            {
                awakened=true;wakeRemaining=0;
                if(body)body.enabled=true;if(picture)picture.gameObject.SetActive(true);
                if(game && game.World)game.World.IgnoreMonsterPatrolCollisions(body);
                SetState(Behaviour.Chase);
                if(target){lastSeen=target.position;goal=lastSeen;hasGoal=true;lostSightRemaining=8f;}
            }
        }
        public void End()
        {
            running=false;awakened=false;target=null;path.Clear();hasGoal=false;waitingDoor=null;StopSounds();ResetPicture();
            if(body)body.enabled=false;if(picture)picture.gameObject.SetActive(false);
        }
        void OnDisable(){StopSounds();ResetPicture();}
        void StopSounds(){if(steps)steps.Stop();if(alert)alert.Stop();}
        void ResetPicture(){if(picture){picture.localPosition=picturePosition;picture.localScale=pictureScale;}}

        bool CanSeeTarget()
        {
            if(!target)return false;
            Vector3 delta=target.position-transform.position;float height=Mathf.Abs(delta.y);delta.y=0;
            float distance=delta.magnitude;
            if(distance>sightDistance || height>2f)return false;
            return HasClearSight(transform.position+Vector3.up*1.2f,target.position+Vector3.up*1.2f);
        }
        static bool HasClearSight(Vector3 from,Vector3 to)
        {
            // Some imported school meshes only collide on their front face.
            // Check both directions so neither side of a wall leaks vision.
            return !Physics.Linecast(from,to,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) &&
                !Physics.Linecast(to,from,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        void SetState(Behaviour value)
        {
            if(state==value)return;
            state=value;hasGoal=false;path.Clear();next=0;repathRemaining=0;pauseAtPoint=0;
            if(value==Behaviour.Chase && alert && alertClip){alert.pitch=1;alert.PlayOneShot(alertClip,.8f);}
            if(value==Behaviour.Search)searchRemaining=searchSeconds;
        }
        void Update()
        {
            if(!running || !game || !target || !body || !body.enabled || !game.IsRunning || game.IsEnded || game.IsAnswering || game.IsExitConfirmOpen)
            {StopSounds();ResetPicture();return;}
            if(!awakened){StopSounds();ResetPicture();return;}
            float dt=Time.deltaTime;
            if(IsFinalHunt)wakeRemaining=0;
            if(wakeRemaining>0){wakeRemaining=Mathf.Max(0,wakeRemaining-dt);ResetPicture();return;}

            // Once the last sheet is solved, pursuit knows the live position
            // even behind walls, but movement still uses the school grid.
            bool visible=IsFinalHunt || CanSeeTarget();
            if(visible){lastSeen=target.position;lostSightRemaining=.5f;SetState(Behaviour.Chase);goal=lastSeen;hasGoal=true;}
            else if(state==Behaviour.Chase)
            {
                lostSightRemaining-=dt;
                if(lostSightRemaining<=0){SetState(Behaviour.Search);goal=lastSeen;hasGoal=true;}
            }
            if(state==Behaviour.Investigate)
            {
                hearingRemaining-=dt;
                if(hearingRemaining<=0f){waitingDoor=null;SetState(Behaviour.Patrol);}
            }
            if(state==Behaviour.Search)
            {
                searchRemaining-=dt;
                if(searchRemaining<=0)SetState(Behaviour.Patrol);
            }
            if(!waitingDoor && (state==Behaviour.Patrol || (state==Behaviour.Search && HorizontalDistance(transform.position,lastSeen)<4f)))
            {
                roomSearchRemaining-=dt;
                if(roomSearchRemaining<=0)
                {
                    roomSearchRemaining=Mathf.Max(1f,roomSearchInterval);
                    TryInvestigateRoom();
                }
            }
            if(state!=Behaviour.Chase && !hasGoal)
            {
                if(pauseAtPoint>0)
                {
                    pauseAtPoint-=dt;
                    if(state==Behaviour.Search)heading=Quaternion.Euler(0,85f*dt,0)*heading;
                }
                else ChooseNearbyGoal(state==Behaviour.Search?lastSeen:transform.position,state==Behaviour.Search?4.5f:10f);
            }
            if(hasGoal)
            {
                repathRemaining-=dt;
                if(repathRemaining<=0 && !waitingDoor)
                {
                    repathRemaining=state==Behaviour.Chase?.45f:1.2f;
                    if(!PlanPath() && state!=Behaviour.Chase)
                    {
                        if(state==Behaviour.Investigate){if(!PlanNearbySoundPoint())SetState(Behaviour.Patrol);}
                        else {hasGoal=false;pauseAtPoint=.4f;}
                    }
                }
            }
            MoveAlongPath(dt);
            if(HorizontalDistance(transform.position,target.position)<.85f && Mathf.Abs(transform.position.y-target.position.y)<1.6f &&
                HasClearSight(transform.position+Vector3.up*1.0f,target.position+Vector3.up*1.0f))
                game.CaughtByMonster();
        }
        bool TryInvestigateRoom()
        {
            if(!map || doors==null)return false;
            openPanels.Clear();Physics.SyncTransforms();
            foreach(var d in doors)if(d)d.AddOpenPanels(openPanels);
            Door best=null;float bestDistance=float.PositiveInfinity;Vector3 bestGoal=Vector3.zero;
            foreach(var door in doors)
            {
                if(!door || door.exitDoor || door==lastInvestigatedDoor || door.DoorwayWidth<.7f)continue;
                Vector3 center=door.DoorwayCenter;Vector3 normal=door.transform.forward;normal.y=0;normal.Normalize();
                float distance=HorizontalDistance(center,transform.position);
                if(distance>roomSearchRange || distance>=bestDistance)continue;
                float side=Vector3.Dot(transform.position-center,normal);
                if(Mathf.Abs(side)<.4f)continue;
                Vector3 wanted=center-normal*Mathf.Sign(side)*2.2f;wanted.y=0;
                int cell=map.NearestCell(wanted);if(cell<0)continue;
                Vector3 candidate=map.Position(cell);
                if(HorizontalDistance(candidate,wanted)>.8f || Vector3.Dot(candidate-center,normal)*side>=0)continue;
                if(!map.FindPath(transform.position,candidate,roomRoute,openPanels.Count>0?CanTraverse:null) || roomRoute.Count==0 ||
                    !CrossesDoor(roomRoute,door))continue;
                best=door;bestDistance=distance;bestGoal=candidate;
            }
            if(!best)return false;
            goal=bestGoal;hasGoal=true;lastInvestigatedDoor=best;pauseAtPoint=0;repathRemaining=1.2f;
            return PlanPath();
        }
        bool CrossesDoor(List<Vector3> route,Door door)
        {
            Vector3 normal=door.transform.forward;normal.y=0;normal.Normalize();
            Vector3 tangent=door.transform.right;tangent.y=0;tangent.Normalize();
            Vector3 previous=transform.position,center=door.DoorwayCenter;
            foreach(var point in route)
            {
                float a=Vector3.Dot(previous-center,normal),b=Vector3.Dot(point-center,normal);
                if(a*b<=0 && Mathf.Abs(a-b)>.0001f)
                {
                    Vector3 crossing=Vector3.Lerp(previous,point,a/(a-b));
                    if(Mathf.Abs(Vector3.Dot(crossing-center,tangent))<=door.DoorwayWidth*.5f+.15f)return true;
                }
                previous=point;
            }
            return false;
        }
        void ChooseNearbyGoal(Vector3 center,float radius)
        {
            if(!map){pauseAtPoint=1f;return;}
            for(int attempt=0;attempt<20;attempt++)
            {
                Vector2 offset=Random.insideUnitCircle*radius;
                if(offset.sqrMagnitude<2.25f)continue;
                int cell=map.NearestCell(center+new Vector3(offset.x,0,offset.y));
                if(cell<0)continue;
                Vector3 candidate=map.Position(cell);
                if(HorizontalDistance(candidate,center)>radius+1f || HorizontalDistance(candidate,transform.position)<1.2f)continue;
                goal=candidate;hasGoal=true;
                if(PlanPath() && path.Count>0){repathRemaining=1.2f;return;}
            }
            hasGoal=false;path.Clear();pauseAtPoint=1f;
        }
        bool PlanPath()
        {
            next=0;openPanels.Clear();Physics.SyncTransforms();
            if(doors!=null)foreach(var door in doors)if(door)door.AddOpenPanels(openPanels);
            if(!map || !map.FindPath(transform.position,goal,path,CanTraverse))
            {path.Clear();return false;}
            return true;
        }
        bool PlanNearbySoundPoint()
        {
            // A swinging leaf can occupy the doorway cell. Approach the nearest
            // reachable cell instead of repeatedly trying to walk through it.
            var candidates=new List<int>();
            for(int i=0;i<map.links.Length;i++)
                if(map.links[i]!=0 && HorizontalDistance(map.Position(i),heardPosition)<=1.8f)candidates.Add(i);
            candidates.Sort((a,b)=>HorizontalDistance(map.Position(a),heardPosition).CompareTo(HorizontalDistance(map.Position(b),heardPosition)));
            foreach(int cell in candidates)
            {
                goal=map.Position(cell);
                if(PlanPath()){hasGoal=true;repathRemaining=1.2f;return true;}
            }
            return false;
        }
        bool CanTraverse(Vector3 from,Vector3 to)
        {
            if(Time.time<blockedUntil && ((HorizontalDistance(from,blockedFrom)<.1f && HorizontalDistance(to,blockedTo)<.1f) ||
                (HorizontalDistance(from,blockedTo)<.1f && HorizontalDistance(to,blockedFrom)<.1f)))return false;
            if(game && game.World && game.World.IsEdgeBlocked(from,to))return false;
            if(doors!=null)foreach(var door in doors)if(door && door.OpenLeafBlocks(from,to,.29f))return false;
            return true;
        }
        void MoveAlongPath(float dt)
        {
            if(waitingDoor)
            {
                if(waitingDoor.IsLocked){waitingDoor=null;path.Clear();repathRemaining=0;return;}
                waitingDoor.OpenForMonster();
                if(!waitingDoor.IsFullyOpen){body.Move(Vector3.down*2f*dt);ResetPicture();return;}
                waitingDoor=null;repathRemaining=0;
                if(!PlanPath() && state!=Behaviour.Chase)
                {
                    if(state==Behaviour.Investigate){if(!PlanNearbySoundPoint())SetState(Behaviour.Patrol);}
                    else {hasGoal=false;pauseAtPoint=.4f;}
                }
            }
            while(next<path.Count && HorizontalDistance(transform.position,path[next])<.17f)next++;
            if(next>=path.Count)
            {
                body.Move(Vector3.down*2f*dt);ResetPicture();
                if(hasGoal && state!=Behaviour.Chase)
                {
                    if(HorizontalDistance(transform.position,goal)<.8f)
                    {
                        if(state==Behaviour.Investigate){lastSeen=heardPosition;SetState(Behaviour.Search);pauseAtPoint=.55f;}
                        else {hasGoal=false;pauseAtPoint=state==Behaviour.Search?.55f:Random.Range(.5f,1.5f);}
                    }
                    else if(repathRemaining<=0){hasGoal=false;pauseAtPoint=.3f;}
                }
                return;
            }
            Vector3 direction=path[next]-transform.position;direction.y=0;
            float remaining=direction.magnitude;if(remaining<.0001f)return;direction/=remaining;
            if(doors!=null)foreach(var door in doors)
            {
                if(!door || door.IsFullyOpen || door.DistanceFrom(transform.position)>1.35f)continue;
                if(door.IsLocked)
                {
                    if(!LockedDoorBlocksSegment(door,transform.position,path[next]))continue;
                    path.Clear();repathRemaining=0;return;
                }
                Vector3 toward=door.NearestPoint(transform.position)-transform.position;toward.y=0;
                if(toward.sqrMagnitude>.01f && Vector3.Dot(direction,toward.normalized)<.1f)continue;
                door.OpenForMonster();waitingDoor=door;body.Move(Vector3.down*2f*dt);ResetPicture();return;
            }
            float speed=(state==Behaviour.Chase || state==Behaviour.Investigate)?CurrentMoveSpeed:state==Behaviour.Search?Speed*.75f:Speed*.55f;
            Vector3 before=transform.position;
            body.Move(direction*Mathf.Min(speed*dt,remaining)+Vector3.down*2f*dt);
            Vector3 moved=transform.position-before;moved.y=0;float distance=moved.magnitude;
            if(distance>.001f)
            {
                heading=moved.normalized;AnimateAndSound(distance);
            }
            else ResetPicture();
            float progress=HorizontalDistance(before,path[next])-HorizontalDistance(transform.position,path[next]);
            if(dt>0 && progress<Mathf.Min(speed*dt,remaining)*.08f)stuckTime+=dt;
            else stuckTime=0f;
            if(stuckTime>.65f)
            {
                stuckTime=0;int cell=map?map.NearestCell(transform.position):-1;
                if(cell>=0 && next<path.Count){blockedFrom=map.Position(cell);blockedTo=path[next];blockedUntil=Time.time+2f;}
                path.Clear();repathRemaining=0;
            }
        }
        static bool LockedDoorBlocksSegment(Door door,Vector3 from,Vector3 to)
        {
            Quaternion inverse=Quaternion.Inverse(door.transform.rotation);
            Vector3 a=inverse*(from-door.DoorwayCenter),b=inverse*(to-door.DoorwayCenter);
            // Ignore doors that this segment is moving away from or past sideways.
            if(Mathf.Abs(b.z)>=Mathf.Abs(a.z) || (Mathf.Sign(a.z)==Mathf.Sign(b.z) && Mathf.Abs(b.z)>.32f))return false;
            float denominator=a.z-b.z;
            float t=Mathf.Abs(denominator)<.0001f?1f:Mathf.Clamp01(a.z/denominator);
            return Mathf.Abs(Mathf.Lerp(a.x,b.x,t))<=door.DoorwayWidth*.5f+.3f;
        }
        void AnimateAndSound(float distance)
        {
            // The supplied character is a single billboard image. A small
            // distance-driven bounce provides motion without inventing frames.
            gait+=distance*10f;footDistance+=distance;
            if(picture)
            {
                float bounce=Mathf.Abs(Mathf.Sin(gait));
                picture.localPosition=picturePosition+Vector3.up*bounce*(state==Behaviour.Chase?.05f:.025f);
                picture.localScale=Vector3.Scale(pictureScale,new Vector3(1f-.018f*bounce,1f+.025f*bounce,1f));
            }
            float stride=state==Behaviour.Chase?.8f:.95f;
            if(footDistance>=stride && steps && footstepClip)
            {footDistance=0;steps.pitch=Random.Range(.92f,1.08f);steps.PlayOneShot(footstepClip,state==Behaviour.Chase?.85f:.65f);}
        }
        static float HorizontalDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}
