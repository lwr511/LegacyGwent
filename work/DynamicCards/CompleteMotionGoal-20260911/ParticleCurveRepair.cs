using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;using Assets.Script.DynamicCards;
public static class ParticleCurveRepair
{
    [Serializable] public class Doc{public Record[] records;}
    [Serializable] public class Record{public string source,scene,prefab,animator;public Curve[] curves;}
    [Serializable] public class Curve{public string clip,path,sourcePath,property;public float duration;public float[] samples;}
    [Serializable] public class Evidence{public string scene,clip,path;public int keys;public float maxSampleError,maxAppliedError;}
    [Serializable] public class Report{public bool complete;public List<Evidence> curves=new List<Evidence>();}
    static string Arg(string name){var args=Environment.GetCommandLineArgs();return args[Array.IndexOf(args,name)+1];}
    static string Safe(string name){return new string(name.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_').ToArray());}
    public static void Run()
    {
        var data=JsonUtility.FromJson<Doc>(File.ReadAllText(Arg("-curveData")));var report=new Report();
        foreach(var record in data.records)
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(record.prefab));
            try
            {
                var anchor=DynamicCardPaths.Find(root.transform,record.animator);if(anchor==null)throw new Exception("Missing Animator "+record.animator);
                var animator=anchor.GetComponent<Animator>();animator.enabled=false;
                string prefix="Source_"+unchecked((uint)Animator.StringToHash(record.animator)).ToString("x8");
                foreach(var source in record.curves)
                {
                    var target=anchor.Find(source.path);var particle=target==null?null:target.GetComponent<ParticleSystem>();
                    if(particle==null)throw new Exception("Missing particle "+record.scene+" "+source.path);
                    if(new SerializedObject(particle).FindProperty(source.property)==null)throw new Exception("Unsupported property "+source.property);
                    var clip=animator.runtimeAnimatorController.animationClips.Single(c=>c.name==prefix+"_"+Safe(source.clip));
                    if(Mathf.Abs(clip.length-source.duration)>.003f)throw new Exception("Changed source clip duration");
                    var binding=EditorCurveBinding.FloatCurve(source.path,typeof(ParticleSystem),source.property);
                    float step=source.samples.Length>1?source.duration/(source.samples.Length-1):0;var keys=new Keyframe[source.samples.Length];
                    for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(i*step,source.samples[i],i==0||step==0?0:(source.samples[i]-source.samples[i-1])/step,i==keys.Length-1||step==0?0:(source.samples[i+1]-source.samples[i])/step);
                    AnimationUtility.SetEditorCurve(clip,binding,new AnimationCurve(keys));EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets();
                    var saved=AnimationUtility.GetEditorCurve(clip,binding);var e=new Evidence{scene=record.scene,clip=source.clip,path=source.path,keys=saved.length};
                    for(int i=0;i<source.samples.Length;i++)e.maxSampleError=Mathf.Max(e.maxSampleError,Mathf.Abs(saved.Evaluate(i*step)-source.samples[i]));
                    foreach(int i in new[]{0,keys.Length/4,keys.Length/2,3*keys.Length/4,keys.Length-1}.Distinct())
                    {
                        clip.SampleAnimation(anchor.gameObject,i*step);e.maxAppliedError=Mathf.Max(e.maxAppliedError,Mathf.Abs(particle.emission.rateOverTime.constant-source.samples[i]));
                    }
                    if(e.maxSampleError>.0002f || e.maxAppliedError>.0002f)throw new Exception("Source emission verification failed: "+JsonUtility.ToJson(e));
                    report.curves.Add(e);
                }
                Debug.Log("SOURCE_PARTICLE_REPAIRED "+record.scene+" curves="+record.curves.Length);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        report.complete=true;File.WriteAllText(Arg("-curveReport"),JsonUtility.ToJson(report,true));Debug.Log("SOURCE_PARTICLE_REPAIR_COMPLETE "+report.curves.Count);
    }
}
