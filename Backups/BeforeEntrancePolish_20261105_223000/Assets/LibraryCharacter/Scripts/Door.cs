using CampusCommons.StudyImmersive;
using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>An actual hinged, collidable school door.</summary>
    public sealed class Door : MonoBehaviour
    {
        public Transform hinge;
        public EscapeGame game;
        public bool exitDoor;
        public float openAngle=88;
        public float degreesPerSecond=260;
        bool open;
        Collider panel;
        public bool IsOpen=>open;
        public string Hint=>exitDoor && game && game.ReadyToExit?"E / 클릭  탈출":(open?"E / 클릭  문 닫기":"E / 클릭  문 열기");
        void Awake(){if(!hinge)hinge=transform;if(!game)game=FindAnyObjectByType<EscapeGame>();panel=hinge.GetComponentInChildren<Collider>();}
        void Update()
        {
            if(!hinge)return;float angle=Mathf.MoveTowardsAngle(hinge.localEulerAngles.y,open?openAngle:0,degreesPerSecond*Time.deltaTime);
            hinge.localRotation=Quaternion.Euler(0,angle,0);
        }
        public void Interact()
        {
            if(exitDoor)
            {
                if(game && game.ReadyToExit){open=true;game.CompleteEscape();return;}
                if(game)game.Notice("문제지를 모두 모은 뒤 이 문에서 탈출하세요.");
            }
            open=!open;
        }
        public void OpenForMonster(){open=true;}
        public float DistanceFrom(Vector3 position)
        {
            if(!panel && hinge)panel=hinge.GetComponentInChildren<Collider>();
            var point=position+Vector3.up*.8f;
            return panel?Vector3.Distance(point,panel.ClosestPoint(point)):Vector3.Distance(position,transform.position);
        }
    }
}
