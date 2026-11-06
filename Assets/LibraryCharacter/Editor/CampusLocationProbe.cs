using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using CampusCommons.StudyImmersive;
public static class CampusLocationProbe {
 [InitializeOnLoadMethod] static void Init(){if(File.Exists("Library/campus-location.request")) EditorApplication.delayCall+=Run;}
 static void Run(){try{File.Delete("Library/campus-location.request");var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();var game=all.Select(x=>x.GetComponent<EscapeGame>()).First(x=>x);var s=new StringBuilder();s.AppendLine("scene="+scene.path+" arrival="+game.schoolArrival.ToString("F2")+" yaw="+game.schoolYaw);
 Physics.SyncTransforms();var origin=game.schoolArrival;origin.y=2.15f; for(int i=0;i<16;i++){var dir=Quaternion.Euler(0,i*22.5f,0)*Vector3.forward; foreach(var h in Physics.RaycastAll(origin,dir,15).OrderBy(h=>h.distance).Take(2)) s.AppendLine("RAY "+(i*22.5f)+" "+h.collider.name+" p="+h.point.ToString("F2")+" normal="+h.normal.ToString("F2")+" dist="+h.distance.ToString("F2"));}
 foreach(var r in all.Select(x=>x.GetComponent<Renderer>()).Where(x=>x&&x.sharedMaterials.Any(m=>m&&m.name=="KIT Campus Notice")))s.AppendLine("POSTER "+r.name+" pos="+r.bounds.center.ToString("F2")+" size="+r.bounds.size.ToString("F2")); File.WriteAllText("Tools/campus-location-probe.txt",s.ToString());}catch(Exception e){File.WriteAllText("Tools/campus-location-probe.txt","FAIL "+e);Debug.LogException(e);}}
}
