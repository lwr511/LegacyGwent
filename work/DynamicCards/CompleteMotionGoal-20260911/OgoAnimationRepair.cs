using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Assets.Script.DynamicCards;

// Restores source OGOAnimationIntermediary parameter curves through the existing
// material-property component. Existing states, transitions and bone curves stay intact.
public static class OgoAnimationRepair
{
    [Serializable] public class Document { public Record[] records; }
    [Serializable] public class Record { public string source,scene,prefab,animator; public Proxy[] proxies; public Curve[] curves; }
    [Serializable] public class Proxy { public string path,target,name; public int slot,kind; }
    [Serializable] public class Curve { public string proxy,field,sourceParameter,clip; public float duration; public float[] samples; public uint sourceAttribute; }
    [Serializable] public class Evidence { public string source,scene,clip,parameter,target,property; public int slot,keys; public float maxSampleError,maxAppliedError; }
    [Serializable] public class Report { public bool complete; public int scenes,curves; public List<Evidence> evidence=new List<Evidence>(); }

    static string Arg(string name)
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,name);
        if(index<0 || index+1>=args.Length)throw new InvalidOperationException("Missing "+name);
        return args[index+1];
    }
    static string Safe(string name) { return new string(name.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_').ToArray()); }
    static Transform Find(Transform root,string path)
    {
        var exact=root.Find(path);if(exact!=null)return exact;
        var expected=path.Split('/');var candidates=new List<Transform>();int best=int.MaxValue;
        foreach(var node in root.GetComponentsInChildren<Transform>(true))
        {
            if(node.name!=expected[expected.Length-1])continue;
            var actual=AnimationUtility.CalculateTransformPath(node,root).Split('/');int index=0;
            foreach(var part in actual)if(index<expected.Length && part==expected[index])index++;
            if(index!=expected.Length)continue;
            if(actual.Length<best){best=actual.Length;candidates.Clear();}
            if(actual.Length==best)candidates.Add(node);
        }
        if(candidates.Count!=1)throw new InvalidOperationException("Source target is missing or ambiguous: "+path);
        return candidates[0];
    }
    static AnimationCurve Build(Curve source)
    {
        if(source.samples==null || source.samples.Length==0)throw new InvalidOperationException("Missing samples");
        float step=source.samples.Length>1?source.duration/(source.samples.Length-1):0;
        var keys=new Keyframe[source.samples.Length];
        for(int i=0;i<keys.Length;i++)
            keys[i]=new Keyframe(i*step,source.samples[i],i==0||step==0?0:(source.samples[i]-source.samples[i-1])/step,
                i==keys.Length-1||step==0?0:(source.samples[i+1]-source.samples[i])/step);
        return new AnimationCurve(keys);
    }
    public static void Run()
    {
        var doc=JsonUtility.FromJson<Document>(File.ReadAllText(Arg("-ogoData")));var report=new Report();
        foreach(var record in doc.records)
        {
            string prefix="Source_"+unchecked((uint)Animator.StringToHash(record.animator)).ToString("x8");
            string folder=Path.GetDirectoryName(record.prefab).Replace('\\','/');
            var root=PrefabUtility.LoadPrefabContents(record.prefab);
            try
            {
                var anchor=Find(root.transform,record.animator);var animator=anchor.GetComponent<Animator>();
                if(animator==null || animator.runtimeAnimatorController==null || animator.runtimeAnimatorController.name!=prefix)
                    throw new InvalidOperationException(record.scene+": unexpected source Animator/controller");
                foreach(var proxy in record.proxies)
                {
                    var target=Find(root.transform,proxy.target).GetComponent<Renderer>();
                    if(target==null || proxy.slot<0 || proxy.slot>=target.sharedMaterials.Length)
                        throw new InvalidOperationException(record.scene+": missing renderer/slot "+proxy.target);
                    var material=target.sharedMaterials[proxy.slot];
                    if(material==null || !material.HasProperty(proxy.name))
                        throw new InvalidOperationException(record.scene+": missing original material property "+proxy.name+" at "+proxy.target);
                    var node=anchor.Find(proxy.path);
                    if(node==null){node=new GameObject(proxy.path).transform;node.SetParent(anchor,false);}
                    var property=node.GetComponent<DynamicCardAnimatedMaterialProperty>();
                    if(property==null)property=node.gameObject.AddComponent<DynamicCardAnimatedMaterialProperty>();
                    property.Type=proxy.kind;property.Slot=proxy.slot;property.Name=proxy.name;property.Targets=new[]{target};
                    if(proxy.kind==1)property.m_Color=material.GetColor(proxy.name);
                    else if(proxy.kind==2)property.m_Float=material.GetFloat(proxy.name);
                    else if(proxy.kind==3)property.m_Offset=material.GetTextureOffset(proxy.name);
                    else if(proxy.kind==4)property.m_Scale=material.GetTextureScale(proxy.name);
                    else throw new InvalidOperationException("Unsupported source parameter type");
                }
                foreach(var source in record.curves)
                {
                    string file=folder+"/"+prefix+"_"+Safe(source.clip)+".anim";
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(file);
                    if(clip==null || Mathf.Abs(clip.length-source.duration)>.003f)
                        throw new InvalidOperationException(record.scene+": missing clip or duration mismatch "+file);
                    var curve=Build(source);
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(source.proxy,typeof(DynamicCardAnimatedMaterialProperty),source.field),curve);
                    EditorUtility.SetDirty(clip);
                }
                PrefabUtility.SaveAsPrefabAsset(root,record.prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Validate(record,report);report.scenes++;
            Debug.Log("OGO_REPAIRED "+record.scene+" curves="+record.curves.Length);
        }
        report.complete=true;File.WriteAllText(Arg("-ogoReport"),JsonUtility.ToJson(report,true));
        Debug.Log("OGO_REPAIR_COMPLETE scenes="+report.scenes+" curves="+report.curves);
    }
    static void Validate(Record record,Report report)
    {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(record.prefab));
        try
        {
            var anchor=Find(root.transform,record.animator);var animator=anchor.GetComponent<Animator>();animator.enabled=false;
            foreach(var source in record.curves)
            {
                var clip=animator.runtimeAnimatorController.animationClips.Single(c=>c.name=="Source_"+unchecked((uint)Animator.StringToHash(record.animator)).ToString("x8")+"_"+Safe(source.clip));
                var binding=EditorCurveBinding.FloatCurve(source.proxy,typeof(DynamicCardAnimatedMaterialProperty),source.field);
                var curve=AnimationUtility.GetEditorCurve(clip,binding);
                if(curve==null)throw new InvalidOperationException("Missing saved curve "+source.proxy+" "+source.field);
                var proxy=record.proxies.Single(p=>p.path==source.proxy);
                var component=anchor.Find(source.proxy).GetComponent<DynamicCardAnimatedMaterialProperty>();
                var evidence=new Evidence{source=record.source,scene=record.scene,clip=source.clip,parameter=source.sourceParameter,target=proxy.target,property=proxy.name,slot=proxy.slot,keys=curve.length};
                float step=source.samples.Length>1?source.duration/(source.samples.Length-1):0;
                for(int i=0;i<source.samples.Length;i++)
                    evidence.maxSampleError=Mathf.Max(evidence.maxSampleError,Mathf.Abs(curve.Evaluate(i*step)-source.samples[i]));
                int[] points={0,source.samples.Length/4,source.samples.Length/2,3*source.samples.Length/4,source.samples.Length-1};
                foreach(int i in points.Distinct())
                {
                    clip.SampleAnimation(anchor.gameObject,i*step);component.ApplyNow();
                    var block=new MaterialPropertyBlock();component.Targets[0].GetPropertyBlock(block,component.Slot);
                    float actual=proxy.kind==2?block.GetFloat(proxy.name):0;
                    if(proxy.kind==1)actual=block.GetColor(proxy.name)["rgba".IndexOf(source.field[source.field.Length-1])];
                    if(proxy.kind==3 || proxy.kind==4)actual=block.GetVector(proxy.name+"_ST")[(proxy.kind==3?2:0)+"xy".IndexOf(source.field[source.field.Length-1])];
                    evidence.maxAppliedError=Mathf.Max(evidence.maxAppliedError,Mathf.Abs(actual-source.samples[i]));
                }
                if(evidence.maxSampleError>.0001f || evidence.maxAppliedError>.0001f)
                    throw new InvalidOperationException(record.scene+" "+source.sourceParameter+": saved/applied curve differs from original source: "+JsonUtility.ToJson(evidence));
                report.evidence.Add(evidence);report.curves++;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
