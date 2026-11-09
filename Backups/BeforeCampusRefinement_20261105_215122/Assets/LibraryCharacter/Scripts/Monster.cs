using System.Collections.Generic;
using CampusCommons.StudyImmersive;
using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>Follows the player through the school's precomputed walkable grid.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Monster : MonoBehaviour
    {
        public SchoolGrid map;
        public Transform picture;
        public float graceSeconds=8;
        public float speedIncrease=.22f;
        public float maxSpeed=4.2f;
        CharacterController body;
        EscapeGame game;
        Transform target;
        Vector3 spawn;
        Door[] doors;
        readonly List<Vector3> path=new List<Vector3>();
        int next;
        float wakeAt,repathAt,doorCheckAt;
        bool running;
        public float Speed{get;private set;}
        public bool IsChasing=>running && Time.time>=wakeAt && game && !game.IsEnded && !game.IsAnswering;
        void Awake()
        {
            spawn=transform.position;body=GetComponent<CharacterController>();
            if(!picture){var face=GetComponentInChildren<FaceCamera>(true);if(face)picture=face.transform;}
            body.enabled=false;if(picture)picture.gameObject.SetActive(false);
        }
        public void Begin(EscapeGame owner,Transform player,EscapeGame.Mode mode)
        {
            game=owner;target=player;running=true;path.Clear();next=0;
            Speed=mode==EscapeGame.Mode.Easy?1.45f:mode==EscapeGame.Mode.Medium?1.8f:mode==EscapeGame.Mode.Hard?2.15f:1.9f;
            body.enabled=false;transform.position=spawn;body.enabled=true;if(picture)picture.gameObject.SetActive(true);
            doors=FindObjectsByType<Door>();wakeAt=Time.time+graceSeconds;repathAt=0;doorCheckAt=0;
        }
        public void SpeedUp(){if(running)Speed=Mathf.Min(maxSpeed,Speed+speedIncrease);}
        public void End()
        {
            running=false;target=null;path.Clear();if(body)body.enabled=false;
            if(picture)picture.gameObject.SetActive(false);
        }
        void Update()
        {
            if(!running || !game || !target || !body || !body.enabled || game.IsEnded || game.IsAnswering || !game.IsRunning || Time.time<wakeAt)return;
            if(Time.time>=doorCheckAt)
            {
                doorCheckAt=Time.time+.18f;
                foreach(var door in doors)if(door && door.DistanceFrom(transform.position)<1.35f)door.OpenForMonster();
            }
            if(Time.time>=repathAt)
            {
                repathAt=Time.time+.8f;next=0;
                if(!map || !map.FindPath(transform.position,target.position,path))path.Clear();
            }
            while(next<path.Count && HorizontalDistance(transform.position,path[next])<.28f)next++;
            Vector3 destination=next<path.Count?path[next]:target.position;
            Vector3 move=destination-transform.position;move.y=0;
            if(move.sqrMagnitude>.0025f)body.Move((move.normalized*Speed+Vector3.down*2f)*Time.deltaTime);
            float distance=HorizontalDistance(transform.position,target.position);
            if(distance<.85f && Mathf.Abs(transform.position.y-target.position.y)<1.6f &&
                !Physics.Linecast(transform.position+Vector3.up*1.0f,target.position+Vector3.up*1.0f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                game.CaughtByMonster();
        }
        static float HorizontalDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}
