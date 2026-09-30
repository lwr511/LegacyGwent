using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Script.DynamicCards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace LegacyGwent.LadyLakePremium.Acceptance {
public sealed class PremiumPlaybackProbe:MonoBehaviour {
public bool Finished {get;private set;}
const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
string folder;DynamicCardView view;GameObject model;int errors;
List<string> checks=new List<string>(),failures=new List<string>(); List<Sample> samples=new List<Sample>();
[Serializable] class Sample {public string name;public float age,animatorTime,gripDistance,swordY; public string animatorState;public Vector3 grip,sword; public Vector2 angle;}
[Serializable] class Result {public int errors;public float observedSeconds;public bool technicalPassed;public string[] checks,failures;public Sample[] samples;}
object Field(string name){return typeof(DynamicCardView).GetField(name,Flags).GetValue(view);}
void Check(bool ok,string what){(ok?checks:failures).Add(what);}
void OnEnable(){Application.logMessageReceived+=OnLog;}
void OnDisable(){Application.logMessageReceived-=OnLog;}
void OnLog(string s,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert){errors++;failures.Add(s);}}
IEnumerator Start(){folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../../work/LadyLakePremium/review"));Directory.CreateDirectory(folder);
float limit=Time.realtimeSinceStartup+60;while(Time.realtimeSinceStartup<limit){view=FindObjectOfType<DynamicCardView>();if(view!=null){model=Field("model") as GameObject;if(model!=null && Field("surface")!=null)break;}yield return null;}
if(model==null){failures.Add("Production DynamicCardView did not load a model");Finish();yield break;}
var card=view.GetComponentInParent<ArtCard>();Check(card!=null,"Uses collection ArtCard component");if(card!=null){Check(card.CardBorder.sprite==card.GoldBorder,"Collection gold border reference");Check(card.FactionIcon.sprite==card.NeutralGoldIcon,"Collection neutral gold emblem reference");}
var surface=Field("surface") as RawImage;Check(surface!=null&&surface.texture!=null,"Production render texture bound to visible surface");
Check(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length>0,"Loaded prefab has actual skinned renderers");
var anims=model.GetComponentsInChildren<Animator>(true);Check(anims.Any(a=>a.runtimeAnimatorController!=null),"Asset Animator controllers loaded");var sound=Field("sound") as AudioSource;Check(sound!=null&&sound.clip!=null&&sound.isPlaying,"Production preview audio loaded and playing");
checks.Add("Audio clip="+(sound==null||sound.clip==null?"none":sound.clip.name+" length="+sound.clip.length));
float[] times={.8f,2.2f,3.25f,4.3f,5.8f,7.4f,10f,12.15f,15.25f,17.8f,23.9f};
foreach(float target in times){while((float)Field("age")<target)yield return null;yield return Shot("phase-"+target.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));}
var initial=(Vector2)Field("current");yield return new WaitForSecondsRealtime(.6f);Check(Vector2.Distance(initial,(Vector2)Field("current"))>.001f,"Original idle turn changes angle");
if(card!=null&&EventSystem.current!=null){var border=card.CardBorder.rectTransform;var canvas=border.GetComponentInParent<Canvas>();var center=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,border.TransformPoint(border.rect.center));var data=new PointerEventData(EventSystem.current){position=center,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Check(hits.Count>0,"UI raycast hits real card");if(hits.Count>0){var hit=hits[0].gameObject;data.pointerPressRaycast=hits[0];ExecuteEvents.ExecuteHierarchy(hit,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(hit,data,ExecuteEvents.beginDragHandler);Check(view.IsDragging,"Original DynamicCardDragHandle starts drag");data.position=center+new Vector2(300,100)*Screen.height/900f;ExecuteEvents.ExecuteHierarchy(hit,data,ExecuteEvents.dragHandler);yield return new WaitForSecondsRealtime(.65f);Check(((Vector2)Field("current")).magnitude>.1f,"Drag changes production angle");yield return Shot("drag");ExecuteEvents.ExecuteHierarchy(hit,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.ExecuteHierarchy(hit,data,ExecuteEvents.endDragHandler);yield return new WaitForSecondsRealtime(.8f);Check(!view.IsDragging&&((Vector2)Field("current")).magnitude<.03f,"Release recenters");yield return Shot("release");}}
Finish();}
IEnumerator Shot(string name){yield return new WaitForEndOfFrame();var all=model.GetComponentsInChildren<Transform>(true);var grip=all.FirstOrDefault(t=>t.name=="GripSocket");var sword=all.FirstOrDefault(t=>t.name=="Sword");var swordGrip=sword==null?null:sword.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Grip");var animator=model.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.runtimeAnimatorController!=null&&a.transform.name=="FigureRig")??model.GetComponentInChildren<Animator>();var info=animator!=null?animator.GetCurrentAnimatorStateInfo(0):new AnimatorStateInfo();samples.Add(new Sample{name=name,age=(float)Field("age"),animatorTime=info.normalizedTime,animatorState=info.IsName("Intro")?"Intro":info.IsName("Loop")?"Loop":"other",grip=grip==null?Vector3.zero:model.transform.InverseTransformPoint(grip.position),sword=sword==null?Vector3.zero:model.transform.InverseTransformPoint(sword.position),gripDistance=grip==null||swordGrip==null?-1:Vector3.Distance(grip.position,swordGrip.position),swordY=sword==null?0:model.transform.InverseTransformPoint(sword.position).y,angle=(Vector2)Field("current")});ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield return null;}
void Finish(){
if(samples.Count>=11){
 var start=samples.First(x=>x.name=="phase-0.80");var caught=samples.First(x=>x.name=="phase-3.25");var held=samples.First(x=>x.name=="phase-5.80");
 Check(start.sword.y>caught.sword.y+.8f,"Sword actually falls from above before catch");
 Check(held.sword.y>caught.sword.y+.2f,"Caught sword actually rises to painting pose");
 foreach(var sample in samples.Where(x=>new[]{"phase-3.25","phase-4.30","phase-5.80","phase-7.40","phase-15.25","phase-17.80"}.Contains(x.name)))Check(sample.gripDistance>=0&&sample.gripDistance<.06f,"Hilt stays in grip: "+sample.name+" distance="+sample.gripDistance);
 Check(samples.Any(x=>x.age>12&&x.animatorState=="Loop"),"Animator transitions to Loop after Intro");
}
File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(new Result{errors=errors,technicalPassed=failures.Count==0,checks=checks.ToArray(),failures=failures.ToArray(),observedSeconds=Time.timeSinceLevelLoad,samples=samples.ToArray()},true));Finished=true;}
}}
