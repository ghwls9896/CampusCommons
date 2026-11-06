using CampusCommons.StudyImmersive;
using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>An interactable exit placed in front of an existing school elevator.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ElevatorExit : MonoBehaviour
    {
        EscapeGame game;
        BoxCollider zone;
        public string Hint=>"클릭 / E   엘리베이터";
        public void Initialize(EscapeGame owner){game=owner;zone=GetComponent<BoxCollider>();}
        public float DistanceFrom(Vector3 point)
        {
            if(!zone)zone=GetComponent<BoxCollider>();
            return zone?Vector3.Distance(point,zone.ClosestPoint(point)):float.PositiveInfinity;
        }
        public void Interact()
        {
            if(!game || !game.IsRunning || game.IsEnded || game.IsAnswering || game.IsExitConfirmOpen)return;
            if(game.ReadyToExit)game.CompleteEscape();
            else game.Notice("문제지를 모두 수집한 뒤 엘리베이터를 이용하세요.","Collect all the sheets before using the elevator.");
        }
    }

    public static class SchoolElevators
    {
        static Mesh doorMesh;
        static Material doorMaterial;
        static readonly Vector3[] centers={new Vector3(531.25f,1.25f,-6.25f),new Vector3(510f,1.25f,-45f),new Vector3(468.75f,1.25f,-3.75f)};
        // These point from each existing elevator surface toward its reachable hallway.
        static readonly Vector3[] fronts={Vector3.left,Vector3.forward,Vector3.right};
        public static ElevatorExit[] Create(EscapeGame game, Transform mapRoot = null)
        {
            var old=GameObject.Find("School Elevators");
            if(old)
            {
                var existing=old.GetComponentsInChildren<ElevatorExit>();foreach(var elevator in existing)elevator.Initialize(game);return existing;
            }
            var texture=Resources.Load<Texture2D>("SchoolElevatorDoor");
            if(!texture){Debug.LogError("School elevator texture is missing");return System.Array.Empty<ElevatorExit>();}
            if(!doorMesh)
            {
                doorMesh=new Mesh{name="Elevator front"};
                doorMesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                doorMesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
                doorMesh.triangles=new[]{0,2,1,0,3,2};doorMesh.RecalculateNormals();doorMesh.RecalculateBounds();
            }
            if(!doorMaterial)doorMaterial=new Material(Shader.Find("Unlit/Texture")){name="Bright elevator doors",mainTexture=texture};
            var root=new GameObject("School Elevators");
            if(mapRoot)root.transform.SetParent(mapRoot,true);
            var result=new ElevatorExit[centers.Length];
            for(int i=0;i<centers.Length;i++)
            {
                var item=new GameObject("School Elevator "+(i+1).ToString("00"));item.transform.SetParent(root.transform,false);
                item.transform.position=centers[i]+fronts[i]*.075f;item.transform.rotation=Quaternion.LookRotation(-fronts[i],Vector3.up);
                var zone=item.AddComponent<BoxCollider>();zone.size=new Vector3(2.45f,2.45f,.22f);zone.isTrigger=true;
                result[i]=item.AddComponent<ElevatorExit>();result[i].Initialize(game);
                var front=new GameObject("Bright EXIT door");front.transform.SetParent(item.transform,false);
                front.transform.localScale=new Vector3(2.5f,2.5f,1);front.AddComponent<MeshFilter>().sharedMesh=doorMesh;
                front.AddComponent<MeshRenderer>().sharedMaterial=doorMaterial;
            }
            return result;
        }
    }
}
