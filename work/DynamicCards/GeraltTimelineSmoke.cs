using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Assets.Script.DynamicCards;
using UnityEngine;
using UnityEngine.UI;
public sealed class GeraltTimelineSmoke:MonoBehaviour
{
 IEnumerator Start()
 {
  DynamicCardSettings.Enabled=true;
  var canvas=new GameObject("Timeline test",typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
  var art=new GameObject("Art",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();art.transform.SetParent(canvas.transform,false);art.rectTransform.sizeDelta=new Vector2(500,700);
  DynamicCardView.Bind(art,"11210300",false,true);
  float deadline=Time.realtimeSinceStartup+120;
  while(art.GetComponentInChildren<RawImage>()==null || !art.GetComponentInChildren<RawImage>().enabled)
  {if(Time.realtimeSinceStartup>deadline){Finish(false,"load timeout");yield break;}yield return null;}
  try
  {
   var view=art.GetComponent<DynamicCardView>();var flags=BindingFlags.NonPublic|BindingFlags.Instance;
   var model=(GameObject)typeof(DynamicCardView).GetField("model",flags).GetValue(view);
   var entry=(DynamicCardEntry)typeof(DynamicCardView).GetField("entry",flags).GetValue(view);
   var age=typeof(DynamicCardView).GetField("age",flags);var cut=typeof(DynamicCardView).GetMethod("ApplyCut",flags);
   var before=DynamicCardPaths.Find(model.transform,entry.beforeCut);var after=DynamicCardPaths.Find(model.transform,entry.afterCut);
   foreach(float time in new[]{0,entry.cutTime-.001f,entry.cutTime+.001f,entry.introDuration+entry.loopDuration*2,0})
   {
    age.SetValue(view,time);cut.Invoke(view,null);bool expected=time>=entry.cutTime;
    if(before.gameObject.activeSelf==expected || after.gameObject.activeSelf!=expected)throw new Exception("rig switch at "+time);
    var visible=(expected?after:before).GetComponentsInChildren<SkinnedMeshRenderer>(false);
    if(visible.Length==0 || visible.Any(r=>!r.enabled))throw new Exception("missing pose meshes at "+time);
   }
   var animator=model.GetComponentsInChildren<Animator>(true).Single(a=>a.layerCount==2);
   if(animator.GetLayerName(1)!="VFX")throw new Exception("source VFX layer missing");
   animator.Rebind();animator.Update(0);
   if(!animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Initial"))throw new Exception("intro not default");
   animator.Update(entry.introDuration+.05f);animator.Update(.05f);
   if(!animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"))throw new Exception("main intro did not transition to loop");
   animator.Update(entry.loopDuration*2+.2f);
   if(!animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"))throw new Exception("loop did not persist");
   animator.Rebind();animator.Update(0);
   if(!animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Initial"))throw new Exception("replay did not restart intro");
   Finish(true,"source-layers intro-loop persistent-loop cut-boundary visible-meshes restart");
  }
  catch(Exception e){Finish(false,e.ToString());}
 }
 void Finish(bool pass,string detail){if(pass)Debug.Log("GERALT_TIMELINE_PASS "+detail);else Debug.LogError("GERALT_TIMELINE_FAIL "+detail);
#if UNITY_EDITOR
UnityEditor.EditorApplication.Exit(pass?0:1);
#else
Application.Quit(pass?0:1);
#endif
}
}
