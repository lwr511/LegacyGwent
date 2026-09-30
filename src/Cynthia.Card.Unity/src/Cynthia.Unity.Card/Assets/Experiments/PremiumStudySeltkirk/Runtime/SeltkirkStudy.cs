using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Script.DynamicCards;
using UnityEngine;
using UnityEngine.UI;
namespace PremiumReferenceStudy {
public sealed class SeltkirkStudy : MonoBehaviour {
 public Sprite staticArt; public bool capture; public bool complete;
 DynamicCardQuality previousQuality;
#if UNITY_EDITOR
 bool previousSourceLoading;
#endif
 DynamicCardView view; GameObject model; Camera cardCamera; string folder;
 readonly BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
 [Serializable] public class TextureInfo {public string property,path;public int width,height;}
 [Serializable] public class MaterialInfo {public string name,path,shader;public TextureInfo[] textures;}
 [Serializable] public class Submesh {public int slot;public int[] triangles;}
 [Serializable] public class Part {public string name,path,meshPath;public int vertices,triangles,referencedBones,usedBones,multiWeightedVertices;public Vector3[] positions;public Vector2[] uv;public Submesh[] submeshes;public MaterialInfo[] materials;public string[] bones,usedBoneNames;public BoneWeight[] weights;}
 [Serializable] public class PartSet {public Part[] parts;public string[] animations;}
 object Get(string n){return typeof(DynamicCardView).GetField(n,flags).GetValue(view);}
 void Awake(){
#if UNITY_EDITOR
  previousSourceLoading=DynamicCardLibrary.AllowEditorSourceLoading;
  DynamicCardLibrary.AllowEditorSourceLoading=true;
#endif
  folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../../work/PremiumStudySeltkirk"));Directory.CreateDirectory(folder);
  var canvasGo=new GameObject("Reference comparison",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvasGo.transform.SetParent(transform,false);var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
  var bg=UI<Image>("Background",canvasGo.transform);bg.color=new Color(.025f,.03f,.04f);bg.rectTransform.anchorMin=Vector2.zero;bg.rectTransform.anchorMax=Vector2.one;bg.rectTransform.offsetMin=bg.rectTransform.offsetMax=Vector2.zero;
  var left=UI<RawImage>("Original static painting",canvasGo.transform);left.texture=staticArt.texture;left.uvRect=new Rect(0,1-713f/1024,497f/1024,713f/1024);left.rectTransform.anchoredPosition=new Vector2(-330,0);left.rectTransform.sizeDelta=new Vector2(497,713)*.9f;
  var right=UI<Image>("Original premium",canvasGo.transform);right.sprite=staticArt;right.rectTransform.pivot=new Vector2(0,1);right.rectTransform.anchoredPosition=new Vector2(100,713*.45f);right.rectTransform.sizeDelta=new Vector2(1024,1024)*.9f;
  Label(canvasGo.transform,"STATIC / 20161800",new Vector2(-330,365));Label(canvasGo.transform,"ORIGINAL PREMIUM / 20161801",new Vector2(324,365));
  previousQuality=DynamicCardSettings.Quality;DynamicCardSettings.Enabled=true;DynamicCardView.Bind(right,"20161800",false,true,null,false,true,null,null,true);view=right.GetComponent<DynamicCardView>();gameObject.AddComponent<AudioListener>();
 }
 void OnDestroy(){DynamicCardSettings.Quality=previousQuality;
#if UNITY_EDITOR
  DynamicCardLibrary.AllowEditorSourceLoading=previousSourceLoading;
#endif
 }
 T UI<T>(string n,Transform p) where T:Graphic {var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(T));g.transform.SetParent(p,false);var rt=(RectTransform)g.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);return g.GetComponent<T>();}
 void Label(Transform p,string words,Vector2 point){var label=UI<Text>(words,p);label.text=words;label.font=Resources.GetBuiltinResource<Font>("Arial.ttf");label.fontSize=20;label.alignment=TextAnchor.MiddleCenter;label.rectTransform.anchoredPosition=point;label.rectTransform.sizeDelta=new Vector2(560,40);}
 IEnumerator Start(){while(view==null||Get("model")==null)yield return null;model=(GameObject)Get("model");cardCamera=(Camera)Get("renderCamera");if(!capture)yield break;
  foreach(float t in new[]{.5f,2f,3.2f,5f,8f,11f,13f}){while((float)Get("age")<t)yield return null;yield return new WaitForEndOfFrame();SaveRT(view.RenderTarget,"original-"+t.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png");}
  yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"static-vs-premium.png"));Dump();InspectGeometry();complete=true;
 }
 void SaveRT(RenderTexture rt,string name){var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(folder,name),tex.EncodeToPNG());DestroyImmediate(tex);RenderTexture.active=old;}
 void CameraShot(string name){var target=cardCamera.targetTexture;float aspect=cardCamera.aspect;var rt=new RenderTexture(1024,1024,24);try{cardCamera.targetTexture=rt;cardCamera.aspect=1;cardCamera.Render();SaveRT(rt,name);}finally{cardCamera.targetTexture=target;cardCamera.aspect=aspect;DestroyImmediate(rt);}}
 void Dump(){
#if UNITY_EDITOR
 var parts=new List<Part>();foreach(var s in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){var mesh=s.sharedMesh;var baked=new Mesh();s.BakeMesh(baked);var bw=mesh.boneWeights;var used=new HashSet<int>();foreach(var w in bw){if(w.weight0>.001)used.Add(w.boneIndex0);if(w.weight1>.001)used.Add(w.boneIndex1);if(w.weight2>.001)used.Add(w.boneIndex2);if(w.weight3>.001)used.Add(w.boneIndex3);}parts.Add(new Part{name=s.name,path=DynamicCardPaths.RelativePath(s.transform,model.transform),meshPath=UnityEditor.AssetDatabase.GetAssetPath(mesh),vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,referencedBones=s.bones.Length,usedBones=used.Count,multiWeightedVertices=bw.Count(w=>w.weight1>.001),positions=baked.vertices.Select(v=>model.transform.InverseTransformPoint(s.transform.TransformPoint(v))).ToArray(),uv=mesh.uv,submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(i=>new Submesh{slot=i,triangles=mesh.GetTriangles(i)}).ToArray(),weights=bw,usedBoneNames=used.Select(i=>s.bones[i].name).ToArray(),bones=s.bones.Select(b=>b==null?"null":b.name).ToArray(),materials=s.sharedMaterials.Select(m=>m==null?null:new MaterialInfo{name=m.name,path=UnityEditor.AssetDatabase.GetAssetPath(m),shader=m.shader.name,textures=m.GetTexturePropertyNames().Where(n=>m.GetTexture(n)!=null).Select(n=>new TextureInfo{property=n,path=UnityEditor.AssetDatabase.GetAssetPath(m.GetTexture(n)),width=m.GetTexture(n).width,height=m.GetTexture(n).height}).ToArray()}).ToArray()});DestroyImmediate(baked);}
 File.WriteAllText(Path.Combine(folder,"mesh-material-uv.json"),JsonUtility.ToJson(new PartSet{parts=parts.ToArray(),animations=model.GetComponentsInChildren<Animator>(true).Where(a=>a.runtimeAnimatorController!=null).SelectMany(a=>a.runtimeAnimatorController.animationClips).Select(c=>UnityEditor.AssetDatabase.GetAssetPath(c)+" | "+c.name+" | "+c.length).Distinct().ToArray()},true));
#endif
 }
 void InspectGeometry(){var renderers=model.GetComponentsInChildren<Renderer>(true);var enabled=renderers.Select(r=>r.enabled).ToArray();var materials=renderers.Select(r=>r.sharedMaterials).ToArray();var temporary=new List<Material>();var pos=cardCamera.transform.position;var rot=cardCamera.transform.rotation;var bg=cardCamera.backgroundColor;var ambient=RenderSettings.ambientLight;var post=cardCamera.GetComponent<DynamicCardPostProcessRenderer>();bool postEnabled=post!=null&&post.enabled;if(post!=null)post.enabled=false;var lightGo=new GameObject("Study light");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(30,-35,0);RenderSettings.ambientLight=new Color(.3f,.3f,.3f);
 try{foreach(var r in renderers){r.enabled=r is SkinnedMeshRenderer && r.name!="SeltkirkEmitter" && r.name!="HelmFireRefl";if(!r.enabled)continue;r.SetPropertyBlock(null);for(int slot=0;slot<r.sharedMaterials.Length;slot++)r.SetPropertyBlock(null,slot);var colors=new[]{new Color(.6f,.7f,.8f),new Color(.8f,.5f,.25f),new Color(.3f,.65f,.6f),new Color(.65f,.45f,.7f)};r.sharedMaterials=r.sharedMaterials.Select((m,i)=>{var n=new Material(Shader.Find("Standard"));n.color=colors[i%colors.Length];n.SetFloat("_Glossiness",.15f);temporary.Add(n);return n;}).ToArray();}
 cardCamera.backgroundColor=new Color(.04f,.05f,.06f);CameraShot("model-clay-front.png");GL.wireframe=true;CameraShot("model-wire-front.png");GL.wireframe=false;
 var center=new Vector3(pos.x,pos.y,0);cardCamera.transform.position=center+Quaternion.Euler(0,-35,0)*(pos-center);cardCamera.transform.LookAt(center);CameraShot("model-clay-side.png");GL.wireframe=true;CameraShot("model-wire-side.png");GL.wireframe=false;
 var head=renderers.First(r=>r.name=="_1_Head");foreach(var r in renderers)r.enabled=r==head;var headCenter=head.bounds.center;cardCamera.transform.position=headCenter+new Vector3(0,0,-6);cardCamera.transform.LookAt(headCenter);cardCamera.nearClipPlane=.1f;CameraShot("head-clay-front.png");cardCamera.transform.position=headCenter+new Vector3(4,0,-4);cardCamera.transform.LookAt(headCenter);CameraShot("head-clay-side.png");GL.wireframe=true;CameraShot("head-wire-side.png");GL.wireframe=false;
 }finally{if(post!=null)post.enabled=postEnabled;GL.wireframe=false;cardCamera.transform.SetPositionAndRotation(pos,rot);cardCamera.backgroundColor=bg;cardCamera.nearClipPlane=5;RenderSettings.ambientLight=ambient;for(int i=0;i<renderers.Length;i++){renderers[i].enabled=enabled[i];renderers[i].sharedMaterials=materials[i];}foreach(var m in temporary)DestroyImmediate(m);DestroyImmediate(lightGo);}
 }
}
}
