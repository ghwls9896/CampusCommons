using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace CampusCommons.StudyImmersive
{
    // Scene markers are editable in the hierarchy and have no runtime collider.
    public sealed class EscapeSpawnPoint : MonoBehaviour
    {
        public enum Kind { Monster, Professor, Worker, Vending }
        public Kind kind;
        public static EscapeSpawnPoint Find(Kind kind)
        {
            foreach(var point in FindObjectsByType<EscapeSpawnPoint>())if(point.kind==kind)return point;
            return null;
        }
        public static List<EscapeSpawnPoint> FindAll(Kind kind)
        {
            var result=new List<EscapeSpawnPoint>();
            foreach(var point in FindObjectsByType<EscapeSpawnPoint>())if(point.kind==kind)result.Add(point);
            return result;
        }
        void OnDrawGizmos()
        {
            Color color=kind==Kind.Monster?Color.red:kind==Kind.Professor?Color.cyan:kind==Kind.Worker?Color.yellow:new Color(.2f,.5f,1f);
            Gizmos.color=color;
            Gizmos.matrix=Matrix4x4.TRS(transform.position,transform.rotation,Vector3.one);
            Vector3 size=kind==Kind.Vending?new Vector3(.85f,1.8f,.42f):new Vector3(.55f,1.8f,.55f);
            Gizmos.DrawWireCube(Vector3.up*.9f,size);Gizmos.DrawLine(Vector3.up*.9f,Vector3.up*.9f+Vector3.forward*.7f);
#if UNITY_EDITOR
            Handles.Label(transform.position+Vector3.up*2.05f,gameObject.name);
#endif
        }
    }
}
