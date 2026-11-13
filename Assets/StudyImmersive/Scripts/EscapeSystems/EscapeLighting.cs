using System.Collections.Generic;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.Rendering;
namespace CampusCommons.StudyImmersive {
 public sealed class EscapeLighting : MonoBehaviour {
  struct SavedLight { public Light light; public bool enabled; }
  struct SavedRenderer { public Renderer renderer; public Material[] materials; public int lightmapIndex; }
  readonly List<SavedRenderer> renderers=new List<SavedRenderer>(); readonly Dictionary<Material,Material> replacements=new Dictionary<Material,Material>();
  readonly List<SavedLight> saved=new List<SavedLight>(); LocalTimeLighting clock; bool clockEnabled,active;
  AmbientMode ambientMode; Color ambientLight; float ambientIntensity; Material sky; Camera cam; CameraClearFlags flags; Color background; Light flashlight;
  public bool IsDark=>active;
  public static EscapeLighting Create(Camera view){var go=new GameObject("Escape lighting runtime");var c=go.AddComponent<EscapeLighting>();c.cam=view;return c;}
  public void StartDark(){if(active || !cam)return;active=true;
   clock=FindAnyObjectByType<LocalTimeLighting>();clockEnabled=clock && clock.enabled;if(clockEnabled)clock.enabled=false;
   ambientMode=RenderSettings.ambientMode;ambientLight=RenderSettings.ambientLight;ambientIntensity=RenderSettings.ambientIntensity;sky=RenderSettings.skybox;flags=cam.clearFlags;background=cam.backgroundColor;
   foreach(var light in FindObjectsByType<Light>()){saved.Add(new SavedLight{light=light,enabled=light.enabled});light.enabled=false;}
   RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.012f,.014f,.020f);RenderSettings.ambientIntensity=.025f;RenderSettings.skybox=null;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.012f,.016f,.025f);
   var go=new GameObject("Handheld flashlight");go.transform.SetParent(cam.transform,false);go.transform.localPosition=new Vector3(.12f,-.12f,.1f);flashlight=go.AddComponent<Light>();flashlight.type=LightType.Spot;flashlight.range=18f;flashlight.spotAngle=60f;flashlight.intensity=3.4f;flashlight.color=new Color(1f,.94f,.80f);flashlight.shadows=LightShadows.Soft;PrepareDynamicMaterials();
  }
  void PrepareDynamicMaterials(){var standard=Shader.Find("Standard");if(!standard)return;foreach(var renderer in FindObjectsByType<Renderer>()){if(!(renderer is MeshRenderer) && !(renderer is SpriteRenderer))continue;if(renderer.bounds.SqrDistance(cam.transform.position)>120f*120f || renderer.GetComponent<TextMesh>())continue;var originals=renderer.sharedMaterials;var changed=(Material[])originals.Clone();bool any=false;for(int i=0;i<originals.Length;i++){var source=originals[i];if(!source || (!(renderer is SpriteRenderer) && source.shader.name.Contains("Transparent")))continue;if(!replacements.TryGetValue(source,out var replacement)){var shader=renderer is SpriteRenderer?Shader.Find("Sprites/Diffuse"):standard;if(!shader)continue;replacement=new Material(shader);if(source.HasProperty("_MainTex")){replacement.mainTexture=source.mainTexture;replacement.mainTextureScale=source.mainTextureScale;replacement.mainTextureOffset=source.mainTextureOffset;}if(source.HasProperty("_Color"))replacement.color=source.color;replacement.SetFloat("_Glossiness",0f);replacements.Add(source,replacement);}changed[i]=replacement;any=true;}if(any){renderers.Add(new SavedRenderer{renderer=renderer,materials=originals,lightmapIndex=renderer.lightmapIndex});renderer.sharedMaterials=changed;renderer.lightmapIndex=-1;}}}
  void RestoreDynamicMaterials(){foreach(var item in renderers)if(item.renderer){item.renderer.sharedMaterials=item.materials;item.renderer.lightmapIndex=item.lightmapIndex;}renderers.Clear();foreach(var m in replacements.Values)if(m)Destroy(m);replacements.Clear();}
  public void Toggle(){if(flashlight)flashlight.enabled=!flashlight.enabled;}
  public void Restore(){if(!active)return;active=false;RestoreDynamicMaterials();if(flashlight){Destroy(flashlight.gameObject);flashlight=null;}foreach(var entry in saved)if(entry.light)entry.light.enabled=entry.enabled;saved.Clear();RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambientLight;RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.skybox=sky;if(cam){cam.clearFlags=flags;cam.backgroundColor=background;}if(clock && clockEnabled)clock.enabled=true;}
  void OnDestroy(){Restore();}
 }
}
