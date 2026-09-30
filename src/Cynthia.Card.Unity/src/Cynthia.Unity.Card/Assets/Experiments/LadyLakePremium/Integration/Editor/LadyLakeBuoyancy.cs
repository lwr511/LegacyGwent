using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
namespace LegacyGwent.LadyLakePremium.Editor {
public static class LadyLakeBuoyancy {
const string Folder="Assets/Experiments/LadyLakePremium/Integration/Generated";
public static Transform Create(Transform parent){
 Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var clip=new AnimationClip{name="BuoyancyLoop",frameRate=30};
 var y=new AnimationCurve();var z=new AnimationCurve();var roll=new AnimationCurve();
 for(int i=0;i<=360;i++){float t=i/30f,p=t*Mathf.PI/6;y.AddKey(t,.035f*Mathf.Sin(p));z.AddKey(t,.014f*Mathf.Sin(p*2));roll.AddKey(t,.32f*Mathf.Sin(p));}
 AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition.y"),y);
 AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition.z"),z);
 AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),"localEulerAnglesRaw.z"),roll);
 var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
 var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/BuoyancyLoop.anim");if(existing){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);clip=existing;}else AssetDatabase.CreateAsset(clip,Folder+"/BuoyancyLoop.anim");
 var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Buoyancy.controller")??AnimatorController.CreateAnimatorControllerAtPath(Folder+"/Buoyancy.controller");
 var sm=controller.layers[0].stateMachine;foreach(var state in sm.states)sm.RemoveState(state.state);var loop=sm.AddState("Loop");loop.motion=clip;sm.defaultState=loop;EditorUtility.SetDirty(controller);
 var go=new GameObject("Buoyancy");go.transform.SetParent(parent,false);var a=go.AddComponent<Animator>();a.runtimeAnimatorController=controller;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;AssetDatabase.SaveAssets();return go.transform;
}
}}
