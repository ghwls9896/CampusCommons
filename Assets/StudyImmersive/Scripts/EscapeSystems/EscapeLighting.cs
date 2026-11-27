using System.Collections.Generic;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.Rendering;
namespace CampusCommons.StudyImmersive {
 public sealed class EscapeLighting : MonoBehaviour {
  struct SavedLight { public Light light; public bool enabled; }
  struct SavedRenderer { public Renderer renderer; public Material[] materials; public int lightmapIndex; public LightProbeUsage lightProbes; public ReflectionProbeUsage reflectionProbes; }
  readonly List<SavedRenderer> renderers=new List<SavedRenderer>(); readonly Dictionary<Material,Material> replacements=new Dictionary<Material,Material>();
  readonly List<SavedRenderer> brightBoards=new List<SavedRenderer>();
  readonly List<Material> brightBoardMaterials=new List<Material>();
  readonly Dictionary<Material, Material> textAtlases = new Dictionary<Material, Material>();
  readonly List<SavedLight> saved=new List<SavedLight>(); LocalTimeLighting clock; bool clockEnabled,active;
  AmbientMode ambientMode; Color ambientLight; float ambientIntensity, reflectionIntensity; Material sky; Camera cam; CameraClearFlags flags; Color background; Light flashlight;
  public bool IsDark=>active;
  public static EscapeLighting Create(Camera view){var go=new GameObject("Escape lighting runtime");var c=go.AddComponent<EscapeLighting>();c.cam=view;return c;}
  public void StartBright(Transform root)
  {
   if (!root || brightBoards.Count > 0) return;
   var shader = Shader.Find("Standard");
   if (!shader) return;
   foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
   {
    if (!renderer.name.StartsWith("Classroom Doodle Board") && !renderer.name.StartsWith("Celebration Notice")) continue;
    var originals = renderer.sharedMaterials;
    var replacementsForBoard = (Material[])originals.Clone();
    for (int i = 0; i < originals.Length; i++)
    {
     var source = originals[i];
     if (!source || !source.shader.name.StartsWith("Unlit/")) continue;
     var boardShader = source.name == "KIT Campus Notice" ? Resources.Load<Shader>("KIT/EscapeNoticeLit") : shader;
     if (!boardShader) continue;
     var material = new Material(boardShader) { name = source.name + " (lit)", mainTexture = source.mainTexture };
     material.mainTextureScale = source.mainTextureScale;
     material.mainTextureOffset = source.mainTextureOffset;
     material.color = Color.white;
     material.SetFloat("_Glossiness", .08f);
     brightBoardMaterials.Add(material);
     replacementsForBoard[i] = material;
    }
    brightBoards.Add(new SavedRenderer { renderer = renderer, materials = originals, lightmapIndex = renderer.lightmapIndex });
    renderer.sharedMaterials = replacementsForBoard;
   }
  }
  void RestoreBright()
  {
   foreach (var item in brightBoards) if (item.renderer) item.renderer.sharedMaterials = item.materials;
   brightBoards.Clear();
   foreach (var material in brightBoardMaterials) if (material) Destroy(material);
   brightBoardMaterials.Clear();
  }
  public void StartDark(bool severe = false){if(active || !cam)return;active=true;
   clock=FindAnyObjectByType<LocalTimeLighting>();clockEnabled=clock && clock.enabled;if(clockEnabled)clock.enabled=false;
   ambientMode=RenderSettings.ambientMode;ambientLight=RenderSettings.ambientLight;ambientIntensity=RenderSettings.ambientIntensity;reflectionIntensity=RenderSettings.reflectionIntensity;sky=RenderSettings.skybox;flags=cam.clearFlags;background=cam.backgroundColor;
   foreach(var light in FindObjectsByType<Light>()){saved.Add(new SavedLight{light=light,enabled=light.enabled});light.enabled=false;}
   RenderSettings.ambientMode=AmbientMode.Flat;
   RenderSettings.ambientLight=severe?new Color(.0015f,.0018f,.0025f):new Color(.012f,.014f,.020f);
   RenderSettings.ambientIntensity=.025f;
   if(severe)RenderSettings.reflectionIntensity=0f;
   RenderSettings.skybox=null;cam.clearFlags=CameraClearFlags.SolidColor;
   cam.backgroundColor=severe?new Color(.001f,.0012f,.0018f):new Color(.012f,.016f,.025f);
   var go=new GameObject("Handheld flashlight");go.transform.SetParent(cam.transform,false);go.transform.localPosition=new Vector3(.12f,-.12f,.1f);flashlight=go.AddComponent<Light>();flashlight.type=LightType.Spot;flashlight.range=18f;flashlight.spotAngle=severe?48f:60f;flashlight.intensity=1.65f;flashlight.color=new Color(1f,.94f,.80f);flashlight.shadows=LightShadows.Soft;
   flashlight.renderMode=LightRenderMode.ForcePixel;
   flashlight.shadowResolution=LightShadowResolution.High;
   flashlight.shadowNearPlane=.08f;
   flashlight.shadowBias=.025f;
   flashlight.shadowNormalBias=.18f;
   PrepareDynamicMaterials(severe);
  }
  void PrepareDynamicMaterials(bool severe)
  {
   var standard = Shader.Find("Standard");
   if (!standard) return;
   foreach (var renderer in FindObjectsByType<Renderer>())
   {
    if (!(renderer is MeshRenderer) && !(renderer is SpriteRenderer)) continue;
    if (renderer.bounds.SqrDistance(cam.transform.position) > 120f*120f) continue;
    bool isText = renderer.GetComponent<TextMesh>();
    var originals = renderer.sharedMaterials;
    var changed = (Material[])originals.Clone();
    bool any = false;
    for (int i = 0; i < originals.Length; i++)
    {
     var source = originals[i];
     if (!source || (!isText && !(renderer is SpriteRenderer) && source.shader.name.Contains("Transparent"))) continue;
     if (!replacements.TryGetValue(source, out var replacement))
     {
      if (isText)
      {
       var textShader = Resources.Load<Shader>("KIT/EscapeWorldTextLit");
       if (!textShader) continue;
       replacement = new Material(textShader) { mainTexture = source.mainTexture };
       if (source.HasProperty("_Color")) replacement.color = source.color;
       if (severe) replacement.SetFloat("_DimEmission", .015f);
       textAtlases[replacement] = source;
      }
      else if (source.shader == standard)
      {
       replacement = new Material(source);
       replacement.DisableKeyword("_EMISSION");
       if (source.name == "Campus Window")
       {
        replacement.SetFloat("_Glossiness", .2f);
        replacement.SetFloat("_Metallic", 0f);
       }
      }
      else
      {
       var shader = source.name.StartsWith("KIT Campus Notice") ? Resources.Load<Shader>("KIT/EscapeNoticeLit") :
        renderer is SpriteRenderer ? Shader.Find("Sprites/Diffuse") : standard;
       if (!shader) continue;
       replacement = new Material(shader);
       if (source.HasProperty("_MainTex"))
       {
        replacement.mainTexture = source.mainTexture;
        replacement.mainTextureScale = source.mainTextureScale;
        replacement.mainTextureOffset = source.mainTextureOffset;
       }
       if (source.HasProperty("_Color")) replacement.color = source.color;
       if (replacement.HasProperty("_Glossiness")) replacement.SetFloat("_Glossiness", 0f);
      }
      if(severe && replacement.shader==standard)
      {
       // A very faint textured floor of light keeps nearby shapes perceptible.
       replacement.EnableKeyword("_EMISSION");
       replacement.SetTexture("_EmissionMap", replacement.mainTexture ? replacement.mainTexture : Texture2D.whiteTexture);
       var tint = replacement.HasProperty("_Color") ? replacement.color : Color.white;
       replacement.SetColor("_EmissionColor", new Color(.015f*tint.r,.016f*tint.g,.020f*tint.b));
      }
      replacements.Add(source,replacement);
     }
     changed[i] = replacement;
     any = true;
    }
    if (!any) continue;
    renderers.Add(new SavedRenderer{renderer=renderer,materials=originals,lightmapIndex=renderer.lightmapIndex,
      lightProbes=renderer.lightProbeUsage,reflectionProbes=renderer.reflectionProbeUsage});
    if(severe)renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
    renderer.sharedMaterials=changed;
    renderer.lightmapIndex=-1;
   }
  }
  void LateUpdate(){foreach(var pair in textAtlases)if(pair.Key && pair.Value && pair.Key.mainTexture != pair.Value.mainTexture)pair.Key.mainTexture=pair.Value.mainTexture;}
  void RestoreDynamicMaterials(){textAtlases.Clear();foreach(var item in renderers)if(item.renderer){item.renderer.sharedMaterials=item.materials;item.renderer.lightmapIndex=item.lightmapIndex;item.renderer.lightProbeUsage=item.lightProbes;item.renderer.reflectionProbeUsage=item.reflectionProbes;}renderers.Clear();foreach(var m in replacements.Values)if(m)Destroy(m);replacements.Clear();}
  public void Toggle(){if(flashlight)flashlight.enabled=!flashlight.enabled;}
  public void Restore(){if(!active)return;active=false;RestoreDynamicMaterials();if(flashlight){Destroy(flashlight.gameObject);flashlight=null;}foreach(var entry in saved)if(entry.light)entry.light.enabled=entry.enabled;saved.Clear();RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambientLight;RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.reflectionIntensity=reflectionIntensity;RenderSettings.skybox=sky;if(cam){cam.clearFlags=flags;cam.backgroundColor=background;}if(clock && clockEnabled)clock.enabled=true;}
  void OnDestroy(){Restore();RestoreBright();}
 }
}
