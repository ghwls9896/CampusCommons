using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampusCommons.LibraryCharacter.Editor
{
 public static class NarrowSchoolGates
 {
  const string ScenePath="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity";
  const string GridPath="Assets/LibraryCharacter/Generated/NarrowGateSchoolGrid.asset";
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  static GameObject Block(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,bool collide)
  {
   var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=pos;obj.transform.localScale=size;
   obj.GetComponent<Renderer>().sharedMaterial=mat;if(!collide)UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
  }
  static List<Collider> Resize(Door door,Material wall,Material frame)
  {
   Check(door.hinge&&door.secondHinge,"Missing gate hinges: "+door.name);
   Check(!door.transform.Find("Narrow Gate Surround"),"Gate already resized: "+door.name);
   float left=door.hinge.localPosition.x,right=door.secondHinge.localPosition.x,width=right-left;
   Check(width>2.2f&&width<2.8f,"Unexpected original gate width: "+door.name+" "+width);
   var panel=door.hinge.GetComponentInChildren<BoxCollider>();Check(panel,"Gate has no panel: "+door.name);
   float height=panel.bounds.size.y+.04f;float target=1.8f;float side=(width-target)*.5f;float factor=target/width;
   foreach(var hinge in new[]{door.hinge,door.secondHinge})foreach(Transform piece in hinge)
   {
    Undo.RecordObject(piece,"Resize school gate");var p=piece.localPosition;p.x*=factor;piece.localPosition=p;var s=piece.localScale;s.x*=factor;piece.localScale=s;PrefabUtility.RecordPrefabInstancePropertyModifications(piece);
   }
   Undo.RecordObject(door.hinge,"Resize school gate");Undo.RecordObject(door.secondHinge,"Resize school gate");
   var lp=door.hinge.localPosition;lp.x+=side;door.hinge.localPosition=lp;var rp=door.secondHinge.localPosition;rp.x-=side;door.secondHinge.localPosition=rp;
   PrefabUtility.RecordPrefabInstancePropertyModifications(door.hinge);PrefabUtility.RecordPrefabInstancePropertyModifications(door.secondHinge);
   var root=new GameObject("Narrow Gate Surround").transform;root.SetParent(door.transform,false);Undo.RegisterCreatedObjectUndo(root.gameObject,"Add gate surround");
   var l=Block(root,"Left Wall Return",new Vector3(left+side*.5f,height*.5f,0),new Vector3(side,height,.16f),wall,true);
   var r=Block(root,"Right Wall Return",new Vector3(right-side*.5f,height*.5f,0),new Vector3(side,height,.16f),wall,true);
   Block(root,"Left Inner Frame",new Vector3(left+side,height*.5f,0),new Vector3(.045f,height,.20f),frame,false);
   Block(root,"Right Inner Frame",new Vector3(right-side,height*.5f,0),new Vector3(.045f,height,.20f),frame,false);
   return new List<Collider>{l.GetComponent<Collider>(),r.GetComponent<Collider>()};
  }
  static bool Near(Vector3 p,Vector3[] centers){return centers.Any(c=>Mathf.Abs(c.x-p.x)<2.4f&&Mathf.Abs(c.z-p.z)<2.4f);}
  static bool Blocked(Vector3 p,HashSet<Collider> sides)
  {
   return Physics.OverlapCapsule(p+Vector3.up*.36f,p+Vector3.up*1.48f,.29f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(sides.Contains);
  }
  static bool EdgeBlocked(Vector3 p,Vector3 q,HashSet<Collider> sides)
  {
   var delta=q-p;
   return Physics.CapsuleCastAll(p+Vector3.up*.36f,p+Vector3.up*1.48f,.29f,delta.normalized,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(h=>sides.Contains(h.collider));
  }
  static SchoolGrid UpdateGrid(SchoolGrid original,Vector3[] centers,List<Collider> infill,Vector3 spawn,StringBuilder report)
  {
   Check(original&&original.links!=null&&original.links.Length==original.width*original.height,"School navigation grid missing");
   var grid=UnityEngine.Object.Instantiate(original);grid.name="Narrow Gate School Grid";grid.links=(byte[])original.links.Clone();var sides=new HashSet<Collider>(infill);int total=grid.links.Length;
   var blocked=new bool[total];for(int i=0;i<total;i++){var p=grid.Position(i);if(Near(p,centers))blocked[i]=Blocked(p,sides);}
   int removed=0;int[] dx={0,1,0,-1},dz={1,0,-1,0};
   for(int i=0;i<total;i++){
    if(grid.links[i]==0)continue;var p=grid.Position(i);if(!Near(p,centers))continue;int x=i%grid.width,z=i/grid.width;
    for(int d=0;d<4;d++){
     if((grid.links[i]&(1<<d))==0)continue;int nx=x+dx[d],nz=z+dz[d];if(nx<0||nx>=grid.width||nz<0||nz>=grid.height)continue;int j=nz*grid.width+nx;var q=grid.Position(j);
     if(!blocked[i]&&!blocked[j]&&!EdgeBlocked(p,q,sides))continue;
     grid.links[i]=(byte)(grid.links[i]&~(1<<d));grid.links[j]=(byte)(grid.links[j]&~(1<<((d+2)%4)));removed++;
    }
   }
   var route=new List<Vector3>();int reachable=0;foreach(var sheet in original.sheetLocations)if(grid.FindPath(spawn,sheet,route))reachable++;
   Check(reachable==original.sheetLocations.Length,"A note became unreachable after resizing gates: "+reachable+"/"+original.sheetLocations.Length);
   Check(grid.FindPath(spawn,original.exitLocation,route),"The original exit path became unreachable");
   Check(!AssetDatabase.LoadAssetAtPath<SchoolGrid>(GridPath),"Destination navigation asset already exists");AssetDatabase.CreateAsset(grid,GridPath);
   report.AppendLine("Grid blocked nodes="+blocked.Count(x=>x)+" removed links="+removed+" reachable notes="+reachable);return grid;
  }
  [MenuItem("Campus/Fix wide school doors")]
  public static void ApplyFromMenu()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!=ScenePath){Debug.LogWarning("Open GrandLibrary_Living and stop Play before fixing school gates.");return;}
   var gates=UnityEngine.Object.FindObjectsByType<Door>().Where(d=>d.secondHinge).ToArray();
   if(gates.Length==8&&gates.All(d=>d.transform.Find("Narrow Gate Surround"))){Debug.Log("The school gates are already resized.");return;}
   try{Apply();}catch(Exception e){File.WriteAllText("Tools/narrow-school-gates-result.txt","FAIL: "+e);Debug.LogException(e);}
  }
  public static void Apply()
  {
   var scene=SceneManager.GetActiveScene();Check(!EditorApplication.isPlaying&&scene.path==ScenePath,"Open GrandLibrary_Living and stop Play before applying the gate fix");
   var gates=UnityEngine.Object.FindObjectsByType<Door>().Where(d=>d.secondHinge).OrderBy(d=>d.name).ToArray();Check(gates.Length==8,"Expected eight wide gates, found "+gates.Length);
   var game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var monster=UnityEngine.Object.FindAnyObjectByType<Monster>();Check(game&&monster&&game.schoolGrid&&monster.map==game.schoolGrid,"School navigation references are not as expected");
   var wall=AssetDatabase.LoadAssetAtPath<Material>("Assets/LibraryCharacter/School/CampusTheme/Campus Wall.mat");var frame=AssetDatabase.LoadAssetAtPath<Material>("Assets/LibraryCharacter/Generated/Gate Dark Metal.mat");Check(wall&&frame,"School wall or gate frame material missing");
   Check(!AssetDatabase.LoadAssetAtPath<SchoolGrid>(GridPath),"Narrow gate navigation asset already exists");
   foreach(var d in gates){Check(d.hinge&&d.secondHinge&&!d.transform.Find("Narrow Gate Surround"),"A gate has already been modified: "+d.name);float width=d.secondHinge.localPosition.x-d.hinge.localPosition.x;Check(width>2.2f&&width<2.8f&&d.hinge.GetComponentInChildren<BoxCollider>()&&d.secondHinge.GetComponentInChildren<BoxCollider>(),"Unexpected gate geometry: "+d.name);}
   string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/LibraryCharacter/Backups/GrandLibrary_before_gate_resize.unity");Check(EditorSceneManager.SaveScene(scene,backup,true),"Could not back up the current scene");
   Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Resize school gates");
   try{
    Undo.RecordObject(game,"Update gate navigation");Undo.RecordObject(monster,"Update gate navigation");
    var rotations=gates.Select(d=>(d.hinge.localRotation,d.secondHinge.localRotation)).ToArray();
    foreach(var d in gates){Undo.RecordObject(d.hinge,"Resize school gate");Undo.RecordObject(d.secondHinge,"Resize school gate");d.hinge.localRotation=Quaternion.identity;d.secondHinge.localRotation=Quaternion.identity;}
    Physics.SyncTransforms();var centers=new List<Vector3>();var infill=new List<Collider>();
    foreach(var door in gates){var p=door.hinge.GetComponentInChildren<Collider>().bounds;var b=door.secondHinge.GetComponentInChildren<Collider>();p.Encapsulate(b.bounds);centers.Add(p.center);infill.AddRange(Resize(door,wall,frame));}
    Physics.SyncTransforms();var report=new StringBuilder();var updated=UpdateGrid(game.schoolGrid,centers.ToArray(),infill,game.schoolArrival,report);game.schoolGrid=updated;monster.map=updated;
    for(int i=0;i<gates.Length;i++){gates[i].hinge.localRotation=rotations[i].Item1;gates[i].secondHinge.localRotation=rotations[i].Item2;PrefabUtility.RecordPrefabInstancePropertyModifications(gates[i].hinge);PrefabUtility.RecordPrefabInstancePropertyModifications(gates[i].secondHinge);}
    PrefabUtility.RecordPrefabInstancePropertyModifications(game);PrefabUtility.RecordPrefabInstancePropertyModifications(monster);
    EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();Check(EditorSceneManager.SaveScene(scene),"Could not save scene");Undo.CollapseUndoOperations(undo);
    File.WriteAllText("Tools/narrow-school-gates-result.txt","PASS: "+gates.Length+" gates narrowed to 1.8 m, backed up to "+backup+"\n"+report);Debug.Log("NARROW_SCHOOL_GATES_OK "+report);
   }catch{Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(undo);if(AssetDatabase.LoadAssetAtPath<SchoolGrid>(GridPath))AssetDatabase.DeleteAsset(GridPath);throw;}
  }
  public static void RunBatch(){EditorSceneManager.OpenScene(ScenePath);Apply();}
 }
}
