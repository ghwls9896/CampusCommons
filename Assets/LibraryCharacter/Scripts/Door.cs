using CampusCommons.StudyImmersive;
using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>An actual hinged, collidable school door.</summary>
    public sealed class Door : MonoBehaviour
    {
        public Transform hinge;
        public Transform secondHinge;
        public EscapeGame game;
        public bool exitDoor;
        public float openAngle=88;
        public float degreesPerSecond=260;
        bool open;
        float lockUntil;
        GameObject lockBarrier;
        readonly System.Collections.Generic.List<GameObject> lockMarks=new System.Collections.Generic.List<GameObject>();
        AudioSource openingAudio;
        AudioClip openingClip,closingClip;
        Collider panel,secondPanel;
        Vector3 doorwayCenter;
        float doorwayWidth;
        public bool IsOpen=>open;
        public Vector3 DoorwayCenter=>doorwayCenter;
        public float DoorwayWidth=>doorwayWidth;
        public bool IsFullyOpen=>open && hinge && Mathf.Abs(Mathf.DeltaAngle(hinge.localEulerAngles.y,openAngle))<3f &&
            (!secondHinge || Mathf.Abs(Mathf.DeltaAngle(secondHinge.localEulerAngles.y,-openAngle))<3f);
        public bool IsLocked=>lockBarrier && Time.time<lockUntil;
        public string Hint=>IsLocked?"잠긴 문":open?"E / 클릭  문 닫기":"E / 클릭  문 열기";
        void Awake()
        {
            if(!hinge)hinge=transform;if(!game)game=FindAnyObjectByType<EscapeGame>();
            panel=hinge.GetComponentInChildren<Collider>();if(secondHinge)secondPanel=secondHinge.GetComponentInChildren<Collider>();
            if(panel)
            {
                var bounds=panel.bounds;if(secondPanel)bounds.Encapsulate(secondPanel.bounds);
                doorwayCenter=bounds.center;
                doorwayWidth=Mathf.Abs(transform.right.x)*bounds.size.x+Mathf.Abs(transform.right.z)*bounds.size.z;
            }
            else{doorwayCenter=transform.position;doorwayWidth=1f;}
        }
        void Update()
        {
            if(lockBarrier && Time.time>=lockUntil)Unlock();
            if(!hinge)return;float angle=Mathf.MoveTowardsAngle(hinge.localEulerAngles.y,open?openAngle:0,degreesPerSecond*Time.deltaTime);
            hinge.localRotation=Quaternion.Euler(0,angle,0);
            if(secondHinge)
            {
                float other=Mathf.MoveTowardsAngle(secondHinge.localEulerAngles.y,open?-openAngle:0,degreesPerSecond*Time.deltaTime);
                secondHinge.localRotation=Quaternion.Euler(0,other,0);
            }
        }
        public void Interact()
        {
            if(IsLocked)return;
            open=!open;
            PlayDoorSound(open,true);
        }
        void PlayDoorSound(bool opening,bool notifyMonster)
        {
            if(!openingAudio)
            {
                var emitter=new GameObject("Door opening audio");emitter.transform.SetParent(transform,true);
                emitter.transform.position=doorwayCenter;
                openingAudio=emitter.AddComponent<AudioSource>();openingAudio.playOnAwake=false;
                openingAudio.spatialBlend=1f;openingAudio.rolloffMode=AudioRolloffMode.Linear;
                openingAudio.minDistance=1.5f;openingAudio.maxDistance=18f;openingAudio.dopplerLevel=0f;
                openingClip=Resources.Load<AudioClip>("EscapeSounds/KenneyDoorOpen");
                closingClip=Resources.Load<AudioClip>("EscapeSounds/KenneyDoorClose");
            }
            var clip=opening?openingClip:closingClip;
            if(clip){openingAudio.Stop();openingAudio.PlayOneShot(clip,.55f);}
            if(notifyMonster && game && game.monster)game.monster.HearDoor(doorwayCenter);
        }
        public void RetireExitDesignation()
        {
            exitDoor=false;
            foreach(var sign in GetComponentsInChildren<TextMesh>(true))
                if(sign.text=="EXIT")sign.gameObject.SetActive(false);
        }
        public void OpenForMonster(){if(!IsLocked && !open){open=true;PlayDoorSound(true,false);}}
        public bool TryLock(float seconds,Sprite icon)
        {
            if(exitDoor || IsLocked || seconds<=0f || !hinge)return false;
            if(open){open=false;PlayDoorSound(false,true);}
            lockUntil=Time.time+seconds;
            lockBarrier=new GameObject("Temporary locked doorway");lockBarrier.transform.SetParent(transform,true);
            lockBarrier.transform.SetPositionAndRotation(doorwayCenter,transform.rotation);
            var wall=lockBarrier.AddComponent<BoxCollider>();wall.size=new Vector3(Mathf.Max(.7f,doorwayWidth),2.2f,.18f);
            if(icon)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    var mark=new GameObject("Padlock mark");mark.transform.SetParent(lockBarrier.transform,false);
                    mark.transform.localPosition=new Vector3(0,0,.13f*side);
                    mark.transform.localRotation=Quaternion.Euler(0,side<0?0f:180f,0);
                    float scale=.38f/Mathf.Max(.01f,icon.rect.height/icon.pixelsPerUnit);mark.transform.localScale=Vector3.one*scale;
                    mark.AddComponent<SpriteRenderer>().sprite=icon;lockMarks.Add(mark);
                }
            }
            return true;
        }
        public void Unlock()
        {
            if(lockBarrier){lockBarrier.SetActive(false);Destroy(lockBarrier);}lockBarrier=null;lockUntil=0f;
            lockMarks.Clear();
        }
        public void ResetForNewRound()
        {
            Unlock();open=false;if(openingAudio)openingAudio.Stop();
            if(hinge)hinge.localRotation=Quaternion.identity;
            if(secondHinge)secondHinge.localRotation=Quaternion.identity;
        }
        public void AddOpenPanels(System.Collections.Generic.List<Collider> result)
        {
            if(!IsFullyOpen)return;
            if(panel && panel.enabled)result.Add(panel);
            if(secondPanel && secondPanel.enabled)result.Add(secondPanel);
        }
        public Vector3 NearestPoint(Vector3 position)
        {
            if(!panel && hinge)panel=hinge.GetComponentInChildren<Collider>();
            if(!secondPanel && secondHinge)secondPanel=secondHinge.GetComponentInChildren<Collider>();
            var point=position+Vector3.up*.8f;
            Vector3 nearest=panel?panel.ClosestPoint(point):transform.position;
            if(secondPanel)
            {
                var other=secondPanel.ClosestPoint(point);
                if((other-point).sqrMagnitude<(nearest-point).sqrMagnitude)nearest=other;
            }
            return nearest;
        }
        public float DistanceFrom(Vector3 position)
        {
            if(!panel && hinge)panel=hinge.GetComponentInChildren<Collider>();
            if(!secondPanel && secondHinge)secondPanel=secondHinge.GetComponentInChildren<Collider>();
            var point=position+Vector3.up*.8f;
            float distance=panel?Vector3.Distance(point,panel.ClosestPoint(point)):Vector3.Distance(position,transform.position);
            return secondPanel?Mathf.Min(distance,Vector3.Distance(point,secondPanel.ClosestPoint(point))):distance;
        }
    }
}
