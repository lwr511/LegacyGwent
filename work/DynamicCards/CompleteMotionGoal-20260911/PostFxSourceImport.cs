using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;using Assets.Script.DynamicCards;using Assets.Script.DynamicCards.SourceParticles;
public static class PostFxSourceImport
{
    [Serializable] public class Doc{public Record[] records;}
    [Serializable] public class Record{public string source,scene,prefab;public Setting[] components;public Curve[] curves;}
    [Serializable] public class Setting{public string path,kind,dataJson,rotationObject,noise,shader;public bool enabled;}
    [Serializable] public class Curve{public string animator,clip,path,kind,property;public float duration;public float[] samples;public int[] removeWrongTypes;}
    [Serializable] public class Evidence{public string scene,clip,path,kind,property;public float maxSampleError,maxAppliedError;public int removedWrongBindings;}
    [Serializable] public class Report{public bool complete;public int effects;public List<Evidence> curves=new List<Evidence>();}
    static string Arg(string name){var args=Environment.GetCommandLineArgs();return args[Array.IndexOf(args,name)+1];}
    static string Safe(string name){return new string(name.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_').ToArray());}
    static Type Kind(string kind){return kind=="Glitch"?typeof(DynamicCardGlitch):kind=="Bloom"?typeof(DynamicCardBloom):typeof(Hayate);}
    public static void Run()
    {
        var noiseImporter=(TextureImporter)AssetImporter.GetAtPath("Assets/DynamicCards/Content/SourcePostFx/VHS_Static.png"); noiseImporter.sRGBTexture=true; noiseImporter.alphaIsTransparency=false; noiseImporter.filterMode=FilterMode.Bilinear; noiseImporter.wrapMode=TextureWrapMode.Repeat; noiseImporter.textureCompression=TextureImporterCompression.Uncompressed; noiseImporter.npotScale=TextureImporterNPOTScale.None; noiseImporter.mipmapEnabled=false; noiseImporter.SaveAndReimport();
        var doc=JsonUtility.FromJson<Doc>(File.ReadAllText(Arg("-postData")));var report=new Report();
        foreach(var record in doc.records)
        {
            var root=PrefabUtility.LoadPrefabContents(record.prefab);
            try
            {
                foreach(var setting in record.components)
                {
                    var node=DynamicCardPaths.Find(root.transform,setting.path);if(node==null)throw new Exception("Missing post effect target "+setting.path);
                    var type=Kind(setting.kind);var component=(DynamicCardPostEffect)node.GetComponent(type);if(component==null)component=(DynamicCardPostEffect)node.gameObject.AddComponent(type);
                    JsonUtility.FromJsonOverwrite(setting.dataJson,component);component.enabled=setting.enabled;component.SourceShader=AssetDatabase.LoadAssetAtPath<Shader>(setting.shader);
                    if(component.SourceShader==null)throw new Exception("Missing source shader "+setting.shader);
                    if(component is DynamicCardGlitch)
                    {
                        var glitch=(DynamicCardGlitch)component;glitch.StaticNoise=AssetDatabase.LoadAssetAtPath<Texture2D>(setting.noise);if(glitch.StaticNoise==null)throw new Exception("Missing original VHS noise");
                    }
                    if(component is DynamicCardBloom && !string.IsNullOrEmpty(setting.rotationObject))
                    {
                        var bloom=(DynamicCardBloom)component;bloom.ObjCtrl=DynamicCardPaths.Find(root.transform,setting.rotationObject);if(bloom.ObjCtrl==null)throw new Exception("Missing original rotation controller");
                    }
                    report.effects++;
                }
                PrefabUtility.SaveAsPrefabAsset(root,record.prefab);
                foreach(var source in record.curves)
                {
                    var anchor=DynamicCardPaths.Find(root.transform,source.animator);if(anchor==null)throw new Exception("Missing original Animator");
                    string prefix="Source_"+unchecked((uint)Animator.StringToHash(source.animator)).ToString("x8");
                    var animator=anchor.GetComponent<Animator>();var clip=animator.runtimeAnimatorController.animationClips.Single(c=>c.name==prefix+"_"+Safe(source.clip));
                    var node=string.IsNullOrEmpty(source.path)?anchor:anchor.Find(source.path);var type=Kind(source.kind);var component=node==null?null:node.GetComponent(type);
                    if(component==null)throw new Exception("Missing exact source script component "+source.kind+" "+source.path);
                    if(new SerializedObject(component).FindProperty(source.property)==null)throw new Exception("Missing original script property "+source.property);
                    if(Mathf.Abs(clip.length-source.duration)>.003f)throw new Exception("Source duration changed");
                    var e=new Evidence{scene=record.scene,clip=source.clip,path=source.path,kind=source.kind,property=source.property};
                    foreach(int wrong in source.removeWrongTypes)
                    {
                        var wrongType=wrong==95?typeof(Animator):typeof(ParticleSystemRenderer);var bad=EditorCurveBinding.FloatCurve(source.path,wrongType,source.property);
                        if(AnimationUtility.GetEditorCurve(clip,bad)!=null){AnimationUtility.SetEditorCurve(clip,bad,null);e.removedWrongBindings++;}
                    }
                    float step=source.samples.Length>1?source.duration/(source.samples.Length-1):0;var keys=new Keyframe[source.samples.Length];
                    for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(i*step,source.samples[i],i==0||step==0?0:(source.samples[i]-source.samples[i-1])/step,i==keys.Length-1||step==0?0:(source.samples[i+1]-source.samples[i])/step);
                    var curve=new AnimationCurve(keys);
                    if(source.property=="m_Enabled")for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Constant);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Constant);}
                    var binding=EditorCurveBinding.FloatCurve(source.path,type,source.property);AnimationUtility.SetEditorCurve(clip,binding,curve);EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets();
                    var saved=AnimationUtility.GetEditorCurve(clip,binding);
                    for(int i=0;i<keys.Length;i++)e.maxSampleError=Mathf.Max(e.maxSampleError,Mathf.Abs(saved.Evaluate(i*step)-source.samples[i]));
                    foreach(int i in new[]{0,keys.Length/4,keys.Length/2,3*keys.Length/4,keys.Length-1}.Distinct())
                    {
                        clip.SampleAnimation(anchor.gameObject,i*step);var actual=new SerializedObject(component).FindProperty(source.property);float value=actual.propertyType==SerializedPropertyType.Boolean?(actual.boolValue?1:0):actual.floatValue;
                        e.maxAppliedError=Mathf.Max(e.maxAppliedError,Mathf.Abs(value-source.samples[i]));
                    }
                    if(e.maxSampleError>.0001f || e.maxAppliedError>.0001f)throw new Exception("Source script curve verification failed "+JsonUtility.ToJson(e));
                    report.curves.Add(e);
                }
                Debug.Log("SOURCE_POSTFX_IMPORTED "+record.scene);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();report.complete=true;File.WriteAllText(Arg("-postReport"),JsonUtility.ToJson(report,true));Debug.Log("SOURCE_POSTFX_COMPLETE effects="+report.effects+" curves="+report.curves.Count);
    }
}
