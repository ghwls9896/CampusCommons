using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.GrandLibrary.Editor
{
    public static class SchoolNavigationAudit
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(GrandLibrarySceneBuilder.ScenePath);
            Physics.SyncTransforms();
            var gate=GameObject.Find("ESCAPE ROOM").GetComponent<LibraryPortal>();
            var spawn=gate.arrivalPosition;
            var start=new Vector2Int(Mathf.RoundToInt(spawn.x*2),Mathf.RoundToInt(spawn.z*2));
            var queue=new Queue<Vector2Int>(); var seen=new HashSet<Vector2Int>();
            var blocked=new HashSet<Vector2Int>(); queue.Enqueue(start); seen.Add(start);
            var directions=new[] {Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
            int minX=start.x,maxX=start.x,minZ=start.y,maxZ=start.y;
            while(queue.Count>0 && seen.Count<18000)
            {
                var current=queue.Dequeue();
                minX=Mathf.Min(minX,current.x); maxX=Mathf.Max(maxX,current.x);
                minZ=Mathf.Min(minZ,current.y); maxZ=Mathf.Max(maxZ,current.y);
                foreach(var direction in directions)
                {
                    var p=current+direction;
                    if (p.x<920 || p.x>1080 || p.y< -110 || p.y>110 || seen.Contains(p) || blocked.Contains(p)) continue;
                    float x=p.x*.5f,z=p.y*.5f;
                    bool floor=Physics.Raycast(new Vector3(x,1.8f,z),Vector3.down,out RaycastHit hit,2.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y)<.3f;
                    bool clear=floor && !Physics.CheckCapsule(new Vector3(x,hit.point.y+.36f,z),new Vector3(x,hit.point.y+1.48f,z),.29f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                    if (clear) { seen.Add(p); queue.Enqueue(p); } else blocked.Add(p);
                }
            }
            var report="Reachable cells: "+seen.Count+"\nBounds x: "+minX*.5f+" to "+maxX*.5f+"\nBounds z: "+minZ*.5f+" to "+maxZ*.5f+"\nGrid: 0.5m; player radius: 0.29m\n";
            Directory.CreateDirectory("Design/GrandLibrary");
            File.WriteAllText("Design/GrandLibrary/school-navigation-validation.txt",report);
            Debug.Log("SCHOOL_NAVIGATION "+report);
            if (seen.Count<80 || (maxX-minX)<10 || (maxZ-minZ)<10) throw new Exception("The school arrival is isolated or too cramped: "+report);
            Debug.Log("SCHOOL_NAVIGATION_PASS");
        }
    }
}
