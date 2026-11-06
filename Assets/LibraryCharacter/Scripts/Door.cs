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
        Collider panel,secondPanel;
        Vector3 doorwayCenter;
        float doorwayWidth;
        public bool IsOpen=>open;
        public Vector3 DoorwayCenter=>doorwayCenter;
        public float DoorwayWidth=>doorwayWidth;
        public bool IsFullyOpen=>open && hinge && Mathf.Abs(Mathf.DeltaAngle(hinge.localEulerAngles.y,openAngle))<3f &&
            (!secondHinge || Mathf.Abs(Mathf.DeltaAngle(secondHinge.localEulerAngles.y,-openAngle))<3f);
        public string Hint=>open?"E / 클릭  문 닫기":"E / 클릭  문 열기";
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
            open=!open;
        }
        public void RetireExitDesignation()
        {
            exitDoor=false;
            foreach(var sign in GetComponentsInChildren<TextMesh>(true))
                if(sign.text=="EXIT")sign.gameObject.SetActive(false);
        }
        public void OpenForMonster(){open=true;}
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
