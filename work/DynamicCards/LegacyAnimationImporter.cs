using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class LegacyAnimationImporter
{
    private static readonly System.Reflection.MethodInfo BatchCurve = typeof(AnimationUtility).GetMethod("Internal_SetEditorCurve", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[]{typeof(AnimationClip),typeof(EditorCurveBinding),typeof(AnimationCurve),typeof(bool)},null);
    [Serializable] class Document { public Record[] animators; }
    [Serializable] class Record { public string id,path; public Clip[] clips; }
    [Serializable] class Clip { public string name,file; public float duration; public int frames,columns; public Track[] tracks; }
    [Serializable] class Track { public string path; public uint attribute; public int offset,dimension,typeId; public bool optional; }
    [Serializable] class Conversion { public string id;public int particles;public MaterialInfo[] materials;public AnimationInfo[] animations; }
    [Serializable] class MaterialInfo { public string asset,originalName,shader; }
    [Serializable] class AnimationInfo {public string path,intro,loop; public float introDuration;}
    public static void Run()
    {
        string data=Path.GetFullPath("../LegacyAnimationData");
        string content=Directory.Exists("Assets/DynamicCards/Content/Legacy2017")?"Assets/DynamicCards/Content/Legacy2017":"Assets/PortableCards";
        var doc=JsonUtility.FromJson<Document>(File.ReadAllText(data+"/animations.json"));
        int count=0,missing=0;
        AssetDatabase.StartAssetEditing();
        try
        {
        foreach(var group in doc.animators.GroupBy(r=>r.id))
        {
            var args=Environment.GetCommandLineArgs();int filter=Array.IndexOf(args,"-repairCards");
            if(filter>=0 && !args[filter+1].Split(',').Contains(group.Key))continue;
            string folder=content+"/"+group.Key,prefab=folder+"/Card.prefab";
            if(!File.Exists(prefab))continue;
            var root=PrefabUtility.LoadPrefabContents(prefab);
            var descriptions=new List<AnimationInfo>();
            var expected=JsonUtility.FromJson<Conversion>(File.ReadAllText(folder+"/conversion.json"));
            var expectedPaths=new HashSet<string>(expected.animations.Select(a=>a.path));
            try
            {
                foreach(var record in group)
                {
                    if(!expectedPaths.Contains(record.path))continue;
                    var anchor=root.transform.Find(record.path);
                    if(anchor==null){Debug.LogError("LEGACY_ANIM_MISSING_ROOT "+record.id+" "+record.path);missing++;continue;}
                    string controllerName=Safe(anchor.name);
                    bool crossRoot=record.clips.SelectMany(c=>c.tracks).Any(t=>!t.optional && t.path!="" && anchor.Find(t.path)==null && Unique(root.transform,t.path)!=null && !Unique(root.transform,t.path).IsChildOf(anchor));
                    if(crossRoot)
                    {
                        var sourceAnchor=anchor;
                        foreach(var track in record.clips.SelectMany(c=>c.tracks))
                        {
                            var target=track.path==""?sourceAnchor:sourceAnchor.Find(track.path);
                            if(target==null && !track.optional)target=Unique(root.transform,track.path);
                            if(target!=null)track.path=AnimationUtility.CalculateTransformPath(target,root.transform);
                            else track.path=record.path+"/"+track.path;
                        }
                        var old=sourceAnchor.GetComponent<Animator>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
                        anchor=root.transform;controllerName="Global_"+controllerName;
                    }
                    var animator=anchor.GetComponent<Animator>();if(animator==null)animator=anchor.gameObject.AddComponent<Animator>();
                    string controllerPath=folder+"/"+controllerName+".controller";
                    var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                    var sm=controller.layers[0].stateMachine;
                    foreach(var state in sm.states)sm.RemoveState(state.state);
                    AnimatorState intro=null,loop=null;
                    var description=new AnimationInfo{path=record.path,intro="",loop=""};
                    foreach(var clip in record.clips)
                    {
                        var bytes=File.ReadAllBytes(data+"/"+clip.file);var values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,values,0,bytes.Length);
                        var animation=new AnimationClip{name=clip.name,frameRate=(clip.frames-1)/clip.duration};
                        EditorCurveBinding lastBinding=default(EditorCurveBinding);AnimationCurve lastCurve=null;
                        foreach(var track in clip.tracks)
                        {
                            if(track.path!="" && anchor.Find(track.path)==null && !track.optional)
                            {
                                string leaf=track.path.Split('/').Last();
                                var candidates=anchor.GetComponentsInChildren<Transform>(true).Where(t=>t.name==leaf).ToArray();
                                if(candidates.Length==1)track.path=AnimationUtility.CalculateTransformPath(candidates[0],anchor);
                            }
                            if(track.path!="" && anchor.Find(track.path)==null)
                            {
                                // Source editor camera/rotation settings are consumed as catalog data, not model bones.
                                if(track.optional || track.path=="InfoHolderRoot" || track.path.StartsWith("InfoHolderRoot/"))continue;
                                Debug.LogWarning("LEGACY_ANIM_MISSING_PATH "+record.id+" "+track.path);missing++;continue;
                            }
                            string property=track.attribute==1?"m_LocalPosition":track.attribute==2?"m_LocalRotation":track.attribute==3?"m_LocalScale":"localEulerAnglesRaw";
                            Type curveType=typeof(Transform);
                            if(track.typeId!=0)
                            {
                                var target=track.path==""?anchor:anchor.Find(track.path);
                                var bindings=AnimationUtility.GetAnimatableBindings(target.gameObject,anchor.gameObject);
                                var binding=bindings.FirstOrDefault(b=>unchecked((uint)Animator.StringToHash(b.propertyName))==track.attribute);
                                if(string.IsNullOrEmpty(binding.propertyName))continue;
                                property=binding.propertyName;curveType=binding.type;
                            }
                            for(int component=0;component<track.dimension;component++)
                            {
                                var keys=new List<Keyframe>();
                                for(int i=0;i<clip.frames;i++)
                                {
                                    float value=values[i*clip.columns+track.offset+component];
                                    float before=values[Math.Max(0,i-1)*clip.columns+track.offset+component],after=values[Math.Min(clip.frames-1,i+1)*clip.columns+track.offset+component];
                                    if(i>0 && i<clip.frames-1 && Mathf.Abs(value-before)<.000001f && Mathf.Abs(value-after)<.000001f)continue;
                                    float step=clip.duration/(clip.frames-1);
                                    keys.Add(new Keyframe(i*step,value,(value-before)/step,(after-value)/step));
                                }
                                lastBinding=EditorCurveBinding.FloatCurve(track.path,curveType,track.typeId==0?property+"."+"xyzw"[component]:property);lastCurve=Compact(keys,track.attribute==2?.00005f:.0001f);
                                if(BatchCurve!=null)BatchCurve.Invoke(null,new object[]{animation,lastBinding,lastCurve,false});
                                else AnimationUtility.SetEditorCurve(animation,lastBinding,lastCurve);
                            }
                        }
                        if(lastCurve==null)
                        {
                            lastBinding=EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition.x");
                            lastCurve=AnimationCurve.Constant(0,clip.duration,anchor.localPosition.x);
                        }
                        AnimationUtility.SetEditorCurve(animation,lastBinding,lastCurve);
                        animation.EnsureQuaternionContinuity();
                        var settings=AnimationUtility.GetAnimationClipSettings(animation);settings.loopTime=clip.name.IndexOf("loop",StringComparison.OrdinalIgnoreCase)>=0;AnimationUtility.SetAnimationClipSettings(animation,settings);
                        string clipPath=folder+"/Decoded_"+Safe(anchor.name+"_"+clip.name)+".anim";
                        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if(existing!=null){EditorUtility.CopySerialized(animation,existing);UnityEngine.Object.DestroyImmediate(animation);animation=existing;}else AssetDatabase.CreateAsset(animation,clipPath);
                        var state=sm.AddState(clip.name);state.motion=animation;
                        if(clip.name.IndexOf("intro",StringComparison.OrdinalIgnoreCase)>=0){intro=state;description.intro=clip.name;description.introDuration=clip.duration;}
                        if(clip.name.IndexOf("loop",StringComparison.OrdinalIgnoreCase)>=0){loop=state;description.loop=clip.name;}
                        count++;
                    }
                    if(loop==null && sm.states.Length>0){loop=sm.states.Last().state;description.loop=loop.name;}
                    if(intro!=null && loop!=null && intro!=loop){var transition=intro.AddTransition(loop);transition.hasExitTime=true;transition.exitTime=1;transition.hasFixedDuration=true;transition.duration=0;}
                    sm.defaultState=intro ?? loop;
                    animator.runtimeAnimatorController=controller;animator.avatar=null;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    descriptions.Add(description);EditorUtility.SetDirty(controller);
                }
                PrefabUtility.SaveAsPrefabAsset(root,prefab);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            // This runs in the isolated conversion project before content metadata is enriched.
            if(content=="Assets/PortableCards")
            {
                string path=folder+"/conversion.json";var conversion=JsonUtility.FromJson<Conversion>(File.ReadAllText(path));conversion.animations=descriptions.ToArray();File.WriteAllText(path,JsonUtility.ToJson(conversion,true));
            }
            Debug.Log("LEGACY_ANIMATION_CARD "+group.Key);
        }
        AssetDatabase.SaveAssets();
        }
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();Debug.Log("LEGACY_ANIMATION_IMPORT clips="+count+" missing="+missing);
        if(missing>0)throw new InvalidOperationException("Legacy animation binding paths require repair");
    }
    private static Transform Unique(Transform root,string path)
    {
        string name=path.Split('/').Last();var nodes=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name).ToArray();
        return nodes.Length==1?nodes[0]:null;
    }
    private static AnimationCurve Compact(List<Keyframe> keys,float tolerance)
    {
        if(keys.Count<3)return new AnimationCurve(keys.ToArray());
        var keep=new bool[keys.Count];keep[0]=keep[keys.Count-1]=true;
        var work=new Stack<int[]>();work.Push(new[]{0,keys.Count-1});
        while(work.Count>0)
        {
            var segment=work.Pop();int first=segment[0],last=segment[1],chosen=-1;float error=tolerance;
            for(int i=first+1;i<last;i++)
            {
                float amount=(keys[i].time-keys[first].time)/(keys[last].time-keys[first].time);
                float difference=Mathf.Abs(keys[i].value-Mathf.Lerp(keys[first].value,keys[last].value,amount));
                if(difference>error){error=difference;chosen=i;}
            }
            if(chosen<0)continue;
            keep[chosen]=true;work.Push(new[]{first,chosen});work.Push(new[]{chosen,last});
        }
        var compact=keys.Where((key,index)=>keep[index]).ToArray();
        for(int i=0;i<compact.Length;i++)
        {
            var key=compact[i];
            key.inTangent=i==0?0:(key.value-compact[i-1].value)/(key.time-compact[i-1].time);
            key.outTangent=i==compact.Length-1?0:(compact[i+1].value-key.value)/(compact[i+1].time-key.time);
            compact[i]=key;
        }
        return new AnimationCurve(compact);
    }
    private static string Safe(string name){return new string(name.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_').ToArray());}
}
