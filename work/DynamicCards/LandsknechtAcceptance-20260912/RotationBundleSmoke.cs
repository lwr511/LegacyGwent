using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class RotationBundleSmoke
{
    [Serializable] public class Track {public string path;public int attribute,offset,dimension;}
    [Serializable] public class Case {public string name,before,after,data,beforeController,afterController;public int frames,columns;public float duration;public Track[] tracks;public string[] nodePaths,eulerPaths;}
    [Serializable] public class Request {public Case[] cases;}
    [Serializable] public class Result {public string name;public int frames,bones;public double beforeMaxDegrees,afterMaxDegrees,unchangedEulerMaxDegrees;public float unchangedLocalPositionMax,unchangedLocalScaleMax;}
    [Serializable] public class Report {public bool complete;public string bundle;public List<Result> cases=new List<Result>();}
    const string Flag="RotationBundleSmoke.Pending";
    static RotationBundleSmoke(){EditorApplication.update+=Poll;}
    public static void Run()
    {
        var work=Path.GetFullPath("..");var request=JsonUtility.FromJson<Request>(File.ReadAllText(work+"/bundle-smoke-cases.json"));var assets=new List<string>();
        foreach(var item in request.cases)
        {
            foreach(bool after in new[]{false,true})
            {
                string folder="Assets/Smoke/"+item.name+"/"+(after?"after":"before");Directory.CreateDirectory(folder);
                string asset=folder+"/"+Path.GetFileName(item.before);File.Copy(after?item.after:item.before,asset,true);
                AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);if(clip==null)throw new Exception("Smoke clip import failed");
                string controllerPath=folder+"/Pose.controller";
                var controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);var machine=controller.layers[0].stateMachine;
                var state=machine.AddState("Pose");state.motion=clip;machine.defaultState=state;
                if(after)item.afterController=controllerPath;else item.beforeController=controllerPath;
                item.nodePaths=AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Transform)).Select(b=>b.path).Distinct().ToArray();
                item.eulerPaths=AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName.StartsWith("localEulerAnglesRaw.",StringComparison.Ordinal)).Select(b=>b.path).Distinct().ToArray();
                assets.Add(asset);assets.Add(controllerPath);
            }
        }
        AssetDatabase.SaveAssets();Directory.CreateDirectory(work+"/BundleSmokePayload");
        var build=new AssetBundleBuild{assetBundleName="rotation-smoke",assetNames=assets.ToArray()};
        if(BuildPipeline.BuildAssetBundles(work+"/BundleSmokePayload",new[]{build},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64)==null)throw new Exception("Smoke asset bundle build failed");
        File.WriteAllText(work+"/bundle-smoke-runtime-request.json",JsonUtility.ToJson(request));
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    static void Poll()
    {
        if(!EditorApplication.isPlaying || !SessionState.GetBool(Flag,false))return;
        SessionState.SetBool(Flag,false);string work=Path.GetFullPath("..");
        try
        {
            var request=JsonUtility.FromJson<Request>(File.ReadAllText(work+"/bundle-smoke-runtime-request.json"));
            var bundle=AssetBundle.LoadFromFile(work+"/BundleSmokePayload/rotation-smoke");if(bundle==null)throw new Exception("Cannot load compiled smoke bundle");
            var report=new Report{bundle=work+"/BundleSmokePayload/rotation-smoke"};
            foreach(var item in request.cases)
            {
                var before=Rig(item,bundle.LoadAsset<RuntimeAnimatorController>(item.beforeController));var after=Rig(item,bundle.LoadAsset<RuntimeAnimatorController>(item.afterController));
                var ba=before[""].GetComponent<Animator>();var aa=after[""].GetComponent<Animator>();
                var bytes=File.ReadAllBytes(item.data);var values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,values,0,bytes.Length);
                var result=new Result{name=item.name,frames=item.frames,bones=item.tracks.Count(t=>t.attribute==2)};
                for(int frame=0;frame<item.frames;frame++)
                {
                    float time=frame/(float)(item.frames-1);ba.Play("Pose",0,time);aa.Play("Pose",0,time);ba.Update(0);aa.Update(0);
                    if(ba.GetCurrentAnimatorClipInfo(0).Length!=1 || aa.GetCurrentAnimatorClipInfo(0).Length!=1)throw new Exception("Animator did not bind the source clip");
                    foreach(var t in item.tracks.Where(t=>t.attribute==2))
                    {
                        int offset=frame*item.columns+t.offset;var expected=new Quaternion(values[offset],values[offset+1],values[offset+2],values[offset+3]);
                        result.beforeMaxDegrees=Math.Max(result.beforeMaxDegrees,Angle(before[t.path].localRotation,expected));
                        result.afterMaxDegrees=Math.Max(result.afterMaxDegrees,Angle(after[t.path].localRotation,expected));
                    }
                    foreach(var path in item.nodePaths)
                    {
                        result.unchangedLocalPositionMax=Mathf.Max(result.unchangedLocalPositionMax,Vector3.Distance(before[path].localPosition,after[path].localPosition));
                        result.unchangedLocalScaleMax=Mathf.Max(result.unchangedLocalScaleMax,Vector3.Distance(before[path].localScale,after[path].localScale));
                    }
                    foreach(var path in item.eulerPaths)result.unchangedEulerMaxDegrees=Math.Max(result.unchangedEulerMaxDegrees,Angle(before[path].localRotation,after[path].localRotation));
                }
                report.cases.Add(result);File.WriteAllText(work+"/bundle-smoke-results.json",JsonUtility.ToJson(report,true));
                if(result.afterMaxDegrees>.051 || result.unchangedEulerMaxDegrees>.01 || result.unchangedLocalPositionMax>.00001 || result.unchangedLocalScaleMax>.00001)throw new Exception("Compiled Animator comparison failed: "+JsonUtility.ToJson(result));
                UnityEngine.Object.Destroy(before[""].gameObject);UnityEngine.Object.Destroy(after[""].gameObject);
            }
            report.complete=true;File.WriteAllText(work+"/bundle-smoke-results.json",JsonUtility.ToJson(report,true));bundle.Unload(true);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(work+"/bundle-smoke-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Dictionary<string,Transform> Rig(Case item,RuntimeAnimatorController controller)
    {
        if(controller==null)throw new Exception("Compiled controller missing");var root=new GameObject(item.name);var nodes=new Dictionary<string,Transform>{{"",root.transform}};
        foreach(string full in item.nodePaths)
        {
            var parent=root.transform;string path="";
            foreach(string part in full.Split('/'))
            {
                if(part.Length==0)continue;path=path.Length==0?part:path+"/"+part;Transform node;
                if(!nodes.TryGetValue(path,out node)){node=new GameObject(part).transform;node.SetParent(parent,false);nodes[path]=node;}parent=node;
            }
        }
        var a=root.AddComponent<Animator>();a.runtimeAnimatorController=controller;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.updateMode=AnimatorUpdateMode.UnscaledTime;a.Rebind();a.Update(0);return nodes;
    }
    static double Angle(Quaternion a,Quaternion b)
    {
        double dot=a.x*(double)b.x+a.y*(double)b.y+a.z*(double)b.z+a.w*(double)b.w;
        double na=a.x*(double)a.x+a.y*(double)a.y+a.z*(double)a.z+a.w*(double)a.w;double nb=b.x*(double)b.x+b.y*(double)b.y+b.z*(double)b.z+b.w*(double)b.w;
        return Math.Acos(Math.Min(1,Math.Abs(dot/Math.Sqrt(na*nb))))*360/Math.PI;
    }
}
