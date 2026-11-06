using System;
using System.IO;
using System.Linq;
using System.Text;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
namespace CampusCommons.LibraryCharacter.Editor {
 public static class KoreanCampusBuilder {
  const string ScenePath="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity";
  const string Art="Assets/StudyImmersive/Art/KIT/";
  const string Request="Library/korean-campus.request",Result="Tools/korean-campus-result.txt";
  static Font font;static Material ink,blue,ivory,navy,frame,wordmark,seal,notice;
  static Texture2D wall;
  [InitializeOnLoadMethod]static void Init(){if(File.Exists(Request))EditorApplication.delayCall+=Requested;}
  static void Requested(){if(!File.Exists(Request))return;File.Delete(Request);try{Apply();Preview();}catch(Exception e){File.WriteAllText(Result,"FAIL "+e);Debug.LogException(e);}}
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  static Transform[] All()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
  static Transform One(string name){var a=All().Where(t=>t.name==name).ToArray();Check(a.Length==1,"Expected one "+name+", got "+a.Length);return a[0];}
  static Material Mat(string name,string shader,Color color,Texture tex=null){
   string path=Art+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
   Check(m.shader,"Missing shader for "+name);m.color=color;if(tex)m.mainTexture=tex;
   if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.22f);
   EditorUtility.SetDirty(m);return m;
  }
  static void Assets(){
   Directory.CreateDirectory(Art);AssetDatabase.Refresh();
   font=AssetDatabase.LoadAssetAtPath<Font>("Assets/StudyImmersive/Resources/KIT/NotoSansKR.ttf");
   var logo=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"KIT_Wordmark.png");var emblem=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"KIT_Seal.png");
   wall=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LibraryCharacter/School/CampusTheme/CampusWall.png");
   Check(font&&logo&&emblem&&wall,"Korean font or logo files are missing");
   Check(font.HasCharacter('금')&&font.HasCharacter('호')&&font.HasCharacter('축'),"The bundled font does not provide Hangul");
   font.RequestCharactersInTexture("국립금오공과대학교에 오신 것을 환영합니다 라운지 카페 마피아방 퀴즈방 탈출방 코스튬 상점 커피 차 출구 디지털관 강의실 인공지능 중심 대학 선정 경축 함께 만드는 미래 준비 생각 도전 0123456789:·",64,FontStyle.Normal);
   ink=Mat("KIT Korean Text","Campus/WorldTextDepth",Color.white,font.material.mainTexture);
   navy=Mat("KIT Navy","Standard",new Color(.035f,.13f,.21f));
   blue=Mat("KIT Blue","Standard",new Color(.02f,.40f,.65f));
   ivory=Mat("KIT White","Standard",new Color(.97f,.98f,.98f));
   frame=Mat("KIT Frame","Standard",new Color(.33f,.38f,.40f));
   wordmark=Mat("KIT Wordmark","Unlit/Texture",Color.white,logo);
   seal=Mat("KIT Seal","Unlit/Texture",Color.white,emblem);
   string poster=Art+"CampusNotice_Korean.png";
   if(!File.Exists(poster))RenderNotice(poster);
   AssetDatabase.ImportAsset(poster,ImportAssetOptions.ForceSynchronousImport);
   var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(poster);Check(tex,"Korean campus notice texture not imported");
   notice=Mat("KIT Campus Notice","Unlit/Texture",Color.white,tex);
  }
  static GameObject Cube(string name,Transform parent,Vector3 pos,Vector3 size,Material material,bool temporary=false){
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
   go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
   if(temporary)go.layer=31;return go;
  }
  static GameObject Quad(string name,Transform parent,Vector3 pos,Vector2 size,Material material,bool temporary=false){
   var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=new Vector3(size.x,size.y,1);
   go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
   if(temporary)go.layer=31;return go;
  }
  static TextMesh Label(string name,Transform parent,string value,Vector3 position,float size,Color color,bool temporary=false){
   var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;if(temporary)go.layer=31;
   var text=go.AddComponent<TextMesh>();text.font=font;text.text=value;text.fontSize=64;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=color;
   go.GetComponent<Renderer>().sharedMaterial=ink;return text;
  }
  static void RenderNotice(string path){
   // Compose a new campus notice from the wall texture, vector panels, text and supplied seal.
   var root=new GameObject("Temporary notice artwork"){hideFlags=HideFlags.HideAndDontSave};root.layer=31;
   var bg=new Material(Shader.Find("Unlit/Texture")){mainTexture=wall};
   var unlitBlue=new Material(Shader.Find("Unlit/Color")){color=new Color(.045f,.15f,.25f)};
   var unlitWhite=new Material(Shader.Find("Unlit/Color")){color=new Color(.98f,.99f,.99f)};
   var unlitAccent=new Material(Shader.Find("Unlit/Color")){color=new Color(.05f,.50f,.72f)};
   Quad("Wall",root.transform,Vector3.zero,Vector2.one,bg,true);
   Quad("Frame",root.transform,new Vector3(0,0,-.02f),new Vector2(.74f,.57f),unlitBlue,true);
   Quad("Paper",root.transform,new Vector3(0,0,-.03f),new Vector2(.70f,.53f),unlitWhite,true);
   Quad("Top rule",root.transform,new Vector3(0,.232f,-.04f),new Vector2(.70f,.055f),unlitBlue,true);
   Quad("Accent rule",root.transform,new Vector3(0,-.155f,-.04f),new Vector2(.57f,.006f),unlitAccent,true);
   Quad("University seal",root.transform,new Vector3(-.23f,.105f,-.05f),new Vector2(.15f,.15f),seal,true);
   Label("School name",root.transform,"국립금오공과대학교",new Vector3(.075f,.135f,-.055f),.0040f,new Color(.08f,.16f,.23f),true);
   Label("Announcement",root.transform,"인공지능 중심 대학",new Vector3(0,-.005f,-.055f),.0048f,new Color(.06f,.16f,.27f),true);
   Label("Celebration",root.transform,"선정 경축",new Vector3(0,-.095f,-.055f),.0060f,new Color(.03f,.32f,.52f),true);
   Label("Caption",root.transform,"함께 만드는 미래",new Vector3(0,-.205f,-.055f),.0037f,new Color(.25f,.31f,.35f),true);
   ink.mainTexture=font.material.mainTexture;
   var cameraObject=new GameObject("Temporary notice camera"){hideFlags=HideFlags.HideAndDontSave};cameraObject.layer=31;
   var cam=cameraObject.AddComponent<Camera>();cam.cullingMask=1<<31;cam.orthographic=true;cam.orthographicSize=.5f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.white;cam.transform.position=new Vector3(0,0,-3);cam.nearClipPlane=.1f;cam.farClipPlane=10;
   var rt=new RenderTexture(1024,1024,24){antiAliasing=2};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
   var image=new Texture2D(1024,1024,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
   cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(bg);UnityEngine.Object.DestroyImmediate(unlitBlue);UnityEngine.Object.DestroyImmediate(unlitWhite);UnityEngine.Object.DestroyImmediate(unlitAccent);
  }
  static void ExistingText(){
   foreach(var text in All().Select(t=>t.GetComponent<TextMesh>()).Where(t=>t)){
    Undo.RecordObject(text,"Korean signage");var r=text.GetComponent<Renderer>();if(r)Undo.RecordObject(r,"Korean signage");
    string v=text.text;
    switch(v){
     case "MAFIA":v="마피아방";break;case "QUIZ":case "QUIZ  1 VS 1":v="1:1 퀴즈방";break;
     case "ESCAPE":case "ESCAPE ROOM":v="탈출방";break;case "LOUNGE":v="라운지";break;
     case "ATRIUM CAFE":v="라운지 카페";break;case "STYLE SHOP":v="코스튬 상점";break;
     case "COFFEE":v="커피";break;case "CAFE":v="카페";break;case "COFFEE\nTEA":v="커피\n차";break;case "EXIT":v="출구";break;
    }
    text.text=v;text.font=font;if(r)r.sharedMaterial=ink;
   }
   var atlas=All().Select(t=>t.GetComponent<WorldTextFontAtlas>()).FirstOrDefault(x=>x);
   Check(atlas,"World text atlas is missing");Undo.RecordObject(atlas,"Korean world font");atlas.font=font;atlas.depthMaterial=ink;EditorUtility.SetDirty(atlas);
   ink.mainTexture=font.material.mainTexture;
  }
  static Transform Group(string name,Transform parent,Vector3 position,Quaternion rotation){
   var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Add Korean sign");go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(position,rotation);return go.transform;
  }
  static void WorldSigns(){
   var lobby=One("Maps").Find("Lobby");Check(lobby,"Lobby missing");
   var g=Group("University Welcome",One("Cafe Shop"),new Vector3(0,3.65f,10.82f),Quaternion.identity);
   Cube("Metal Frame",g,Vector3.zero,new Vector3(7.0f,.84f,.08f),frame);
   Cube("White Face",g,new Vector3(0,0,-.052f),new Vector3(6.94f,.78f,.025f),ivory);
   Cube("Blue Accent",g,new Vector3(0,-.34f,-.070f),new Vector3(6.9f,.055f,.012f),blue);
   Cube("Left Mount",g,new Vector3(-1.45f,-.70f,.02f),new Vector3(.045f,.60f,.08f),frame);
   Cube("Right Mount",g,new Vector3(1.45f,-.70f,.02f),new Vector3(.045f,.60f,.08f),frame);
   Quad("KIT Wordmark",g,new Vector3(-2.32f,.02f,-.075f),new Vector2(1.78f,.44f),wordmark);
   Label("Welcome",g,"금오공과대학교에 오신 것을 환영합니다",new Vector3(.96f,.015f,-.082f),.027f,new Color(.055f,.13f,.19f));
   var mafia=One("Mafia Wall Sign").GetComponent<Renderer>();Check(mafia,"Mafia sign missing");Undo.RecordObject(mafia,"Replace English wall sign");mafia.enabled=false;
   var room=One("Maps").Find("Mafia Room");var m=Group("Mafia Room Sign",room,new Vector3(100,3.36f,8.54f),Quaternion.identity);
   Label("Title",m,"마피아 토론실",Vector3.zero,.13f,new Color(.97f,.91f,.76f));
   var q1=One("SIGN_Quiz_QUIZ  1  VS  1").GetComponent<Renderer>();var q2=One("SIGN_Quiz_READY  /  THINK  /  ANSWER").GetComponent<Renderer>();
   Check(q1&&q2,"Quiz wall signs missing");Undo.RecordObject(q1,"Replace English wall sign");Undo.RecordObject(q2,"Replace English wall sign");q1.enabled=false;q2.enabled=false;
   var quiz=One("Maps").Find("Quiz Room");var q=Group("Quiz Room Sign",quiz,new Vector3(200,2.93f,8.51f),Quaternion.identity);
   Label("Title",q,"1:1 퀴즈방",Vector3.zero,.13f,new Color(.84f,.94f,1f));
   Label("Subtitle",q,"준비 · 생각 · 도전",new Vector3(0,-.79f,0),.052f,new Color(.75f,.88f,.95f));
  }
  static void DoorPlaque(Transform face,string label){
   var plate=new GameObject("Room Number");Undo.RegisterCreatedObjectUndo(plate,"Add classroom number");
   plate.transform.SetParent(face,false);plate.transform.localPosition=new Vector3(0,.409f,-.012f);
   var scale=face.lossyScale;plate.transform.localScale=new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z));
   Cube("Border",plate.transform,Vector3.zero,new Vector3(.88f,.20f,.012f),navy);
   Cube("Face",plate.transform,new Vector3(0,0,-.008f),new Vector3(.855f,.176f,.006f),ivory);
   Cube("Blue Stripe",plate.transform,new Vector3(-.407f,0,-.012f),new Vector3(.025f,.17f,.006f),blue);
   Quad("Seal",plate.transform,new Vector3(-.32f,0,-.015f),new Vector2(.13f,.13f),seal);
   Label("Room Name",plate.transform,label,new Vector3(.06f,0,-.019f),.012f,new Color(.03f,.12f,.20f));
  }
  static int ClassroomSigns(){
   var doors=All().Select(t=>t.GetComponent<Door>()).Where(d=>d&&d.name.StartsWith("Classroom Door ")&&!d.secondHinge).OrderBy(d=>d.name).ToArray();Check(doors.Length>=10,"Classroom doors are missing");
   for(int i=0;i<doors.Length;i++){
    var faces=doors[i].GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Door Texture")).ToArray();Check(faces.Length==2,"Missing classroom door faces: "+doors[i].name);
    foreach(var face in faces)DoorPlaque(face,"디지털관 "+(100+i)+"호");
   }
   var exit=All().Select(t=>t.GetComponent<Door>()).FirstOrDefault(d=>d&&d.name=="Exit Door");
   if(exit)foreach(var label in exit.GetComponentsInChildren<TextMesh>(true).Where(t=>t.text=="출구")){Undo.RecordObject(label.gameObject,"Retire former exit sign");label.gameObject.SetActive(false);}
   return doors.Length;
  }
  static int ReplaceNotices(){
   int n=0;foreach(var r in All().Select(t=>t.GetComponent<Renderer>()).Where(r=>r)){
    var mats=r.sharedMaterials;bool changed=false;
    for(int i=0;i<mats.Length;i++)if(mats[i]&&mats[i].name=="Campus Notice"){mats[i]=notice;changed=true;}
    if(changed){Undo.RecordObject(r,"Replace campus notices");r.sharedMaterials=mats;n++;}
   }
   Check(n>0,"School notice surfaces missing");return n;
  }
  public static void Apply(){
   var scene=SceneManager.GetActiveScene();Check(!EditorApplication.isPlayingOrWillChangePlaymode&&scene.path==ScenePath,"Open GrandLibrary_Living and stop Play.");
   Check(!All().Any(x=>x.name=="University Welcome"),"Korean campus signs have already been installed.");
   Assets();
   string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/LibraryCharacter/Backups/GrandLibrary_before_korean_signs_apply.unity");Check(EditorSceneManager.SaveScene(scene,backup,true),"Could not back up the current scene");
   Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install Korean campus signs");
   try{
    ExistingText();WorldSigns();int doors=ClassroomSigns();int notices=ReplaceNotices();
    Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);Check(EditorSceneManager.SaveScene(scene),"Could not save scene");AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);
    File.WriteAllText(Result,"PASS\nbackup="+backup+"\nclassrooms="+doors+"\nnoticeMeshes="+notices+"\n");Debug.Log("KOREAN_CAMPUS_OK rooms="+doors+" notices="+notices);
   }catch{Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(undo);throw;}
  }
  public static void RunBatch(){try{EditorSceneManager.OpenScene(ScenePath);Apply();Preview();}catch(Exception e){File.WriteAllText(Result,"FAIL "+e);Debug.LogException(e);}}
  static void Render(string file,Vector3 from,Vector3 target,float fov){
   Directory.CreateDirectory("Design/KoreanCampus");var o=new GameObject("Preview Camera"){hideFlags=HideFlags.HideAndDontSave};var c=o.AddComponent<Camera>();c.transform.position=from;c.transform.LookAt(target);c.fieldOfView=fov;c.nearClipPlane=.04f;c.farClipPlane=150;
   var rt=new RenderTexture(1200,750,24){antiAliasing=2};c.targetTexture=rt;c.Render();RenderTexture.active=rt;var img=new Texture2D(1200,750,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1200,750),0,0);img.Apply();File.WriteAllBytes("Design/KoreanCampus/"+file,img.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(img);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(o);
  }
  public static void Preview(){
   Render("lobby.png",new Vector3(0,2.4f,-8.5f),new Vector3(0,3.5f,15),65);
   Render("classroom-door.png",new Vector3(482.15f,1.8f,33.13f),new Vector3(483.75f,1.95f,33.13f),56);
   Render("mafia.png",new Vector3(100,1.8f,-4.7f),new Vector3(100,3.2f,8.6f),60);
   Render("quiz.png",new Vector3(200,1.8f,-4.5f),new Vector3(200,2.7f,8.6f),60);
   var original=One("Classroom Door 08");var copy=UnityEngine.Object.Instantiate(original.gameObject);copy.hideFlags=HideFlags.HideAndDontSave;copy.transform.SetPositionAndRotation(new Vector3(1000,0,1000),Quaternion.identity);
   Render("classroom-door-isolated.png",new Vector3(1000.63f,1.73f,997.8f),new Vector3(1000.63f,1.95f,1000),48);UnityEngine.Object.DestroyImmediate(copy);
  }
 }
}
