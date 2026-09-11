using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SourceAnimationImporter
{
    private static readonly System.Reflection.MethodInfo BatchCurve = typeof(AnimationUtility).GetMethod("Internal_SetEditorCurve", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[]{typeof(AnimationClip),typeof(EditorCurveBinding),typeof(AnimationCurve),typeof(bool)},null);
    [Serializable] class Document { public Record[] animators; }
    [Serializable] class MaterialDocument {public MaterialProperty[] properties;}
    [Serializable] class MaterialProperty {public string id,path,Name;public string[] targets;public int Type,Slot;public Color m_Color;public float m_Float;public Vector4 m_Vector;public Vector2 m_Offset,m_Scale;}

    [Serializable] class Record { public string id,path; public Clip[] clips; public Layer[] layers; }
    [Serializable] class Layer { public string name; public float weight; public bool additive; public int defaultState; public State[] states; }
    [Serializable] class State { public string name; public string[] clips; public float speed=1; public bool loop,writeDefaults;public Transition[] transitions; }
    [Serializable] class Transition {public int destination;public float exitTime,duration,offset;public bool fixedDuration,hasExitTime,unconditional;}
    [Serializable] class Clip { public string name,file; public float duration; public int frames,columns; public Track[] tracks; public PointerTrack[] pointerTracks; }
    [Serializable] class PointerTrack {public string path;public int slot;public PointerKey[] keys;}
    [Serializable] class PointerKey {public float time;public string asset;}
    [Serializable] class Track { public string path; public uint attribute; public int offset,dimension,typeId; public bool optional; }
    [Serializable] class Conversion { public string id;public int particles;public MaterialInfo[] materials;public AnimationInfo[] animations; }
    [Serializable] class MaterialInfo { public string asset,originalName,shader; }
    [Serializable] class AnimationInfo {public string path,intro,loop; public float introDuration;}
    public static void Run()
    {
        string data=Path.GetFullPath(Environment.GetEnvironmentVariable("DYNAMIC_ANIMATION_DATA") ?? "../NativeAnimationData");
        string content=Environment.GetEnvironmentVariable("DYNAMIC_ANIMATION_CONTENT") ?? "Assets/DynamicCards/Content";
        var doc=JsonUtility.FromJson<Document>(File.ReadAllText(data+"/animations.json"));
        var materialDoc=File.Exists(data+"/materials.json")?JsonUtility.FromJson<MaterialDocument>(File.ReadAllText(data+"/materials.json")):new MaterialDocument{properties=new MaterialProperty[0]};
        int count=0,missing=0,processed=0;
        AssetDatabase.StartAssetEditing();
        try
        {
        foreach(var group in doc.animators.GroupBy(r=>r.id))
        {
            var args=Environment.GetCommandLineArgs();int filter=Array.IndexOf(args,"-repairCards");
            if(filter>=0 && !args[filter+1].Split(',').Contains(group.Key))continue;
            string selected=Environment.GetEnvironmentVariable("DYNAMIC_ANIMATION_IDS");
            if(!string.IsNullOrEmpty(selected) && !selected.Split(',').Contains(group.Key))continue;
            string folder=content+"/"+group.Key,prefab=folder+"/Card.prefab";
            if(!File.Exists(prefab))continue;
            var root=PrefabUtility.LoadPrefabContents(prefab);
            var descriptions=new List<AnimationInfo>();
            var expected=JsonUtility.FromJson<Conversion>(File.ReadAllText(folder+"/conversion.json"));
            var expectedPaths=new HashSet<string>(expected.animations.Select(a=>a.path));
            try
            {
                foreach(var item in materialDoc.properties.Where(p=>p.id==group.Key))
                {
                    var node=FindExpanded(root.transform,item.path);if(node==null){missing++;continue;}
                    var property=node.GetComponent<Assets.Script.DynamicCards.DynamicCardAnimatedMaterialProperty>();if(property==null)property=node.gameObject.AddComponent<Assets.Script.DynamicCards.DynamicCardAnimatedMaterialProperty>();
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(item),property);
                    property.Targets=item.targets.Select(path=>FindExpanded(root.transform,path)).Where(t=>t!=null).Select(t=>t.GetComponent<Renderer>()).Where(t=>t!=null).ToArray();
                }
                foreach(var record in group)
                {

                    if(record.clips==null || record.clips.Length==0){Debug.Log("SOURCE_EMPTY_CONTROLLER "+record.id+" "+record.path);continue;}
                    if(record.clips.GroupBy(c=>c.name).Any(g=>g.Count()>1))throw new InvalidOperationException(record.id+": duplicate source clip names; rebuild contracts using clip file IDs first.");
                    var anchor=FindExpanded(root.transform,record.path);
                    if(anchor==null){Debug.LogError("LEGACY_ANIM_MISSING_ROOT "+record.id+" "+record.path);missing++;continue;}
                    string controllerName="Source_"+unchecked((uint)Animator.StringToHash(record.path)).ToString("x8");
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
                    if(controller.layers.Length==0)controller.AddLayer("Base Layer");
                    var sm=controller.layers[0].stateMachine;
                    foreach(var state in sm.states)sm.RemoveState(state.state);
                    AnimatorState intro=null,loop=null;
                    var description=new AnimationInfo{path=record.path,intro="",loop=""};
                    var imported=new Dictionary<string,AnimationClip>();
                    foreach(var clip in record.clips)
                    {
                        if(clip.duration<=0)continue;
                        if(Environment.GetCommandLineArgs().Contains("-controllersOnly"))
                        {
                            var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"/"+controllerName+"_"+Safe(clip.name)+".anim");
                            if(saved!=null){imported[clip.name]=saved;continue;}
                        }
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
                                if(string.IsNullOrEmpty(binding.propertyName))binding=SerializedBinding(target,track.typeId,track.attribute);
                                if(string.IsNullOrEmpty(binding.propertyName))binding=MaterialBinding(target,track.attribute);
                                if(string.IsNullOrEmpty(binding.propertyName)){Debug.LogWarning("SOURCE_UNRESOLVED_PROPERTY "+record.id+" "+track.path+" "+track.attribute);continue;}
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
                                if(property=="m_IsActive" || property=="m_Enabled") {lastCurve=new AnimationCurve(keys.ToArray());for(int k=0;k<lastCurve.length;k++){AnimationUtility.SetKeyLeftTangentMode(lastCurve,k,AnimationUtility.TangentMode.Constant);AnimationUtility.SetKeyRightTangentMode(lastCurve,k,AnimationUtility.TangentMode.Constant);}}
                                if(BatchCurve!=null)BatchCurve.Invoke(null,new object[]{animation,lastBinding,lastCurve,false});
                                else AnimationUtility.SetEditorCurve(animation,lastBinding,lastCurve);
                            }
                        }
                        if(lastCurve==null)
                        {
                            var clock=anchor.Find("__SourceClipClock");if(clock==null){clock=new GameObject("__SourceClipClock").transform;clock.SetParent(anchor,false);}
                            lastBinding=EditorCurveBinding.FloatCurve("__SourceClipClock",typeof(Transform),"m_LocalPosition.x");
                            lastCurve=AnimationCurve.Constant(0,clip.duration,0);
                        }
                        AnimationUtility.SetEditorCurve(animation,lastBinding,lastCurve);
                        foreach(var pointer in clip.pointerTracks ?? new PointerTrack[0])
                        {
                            var node=string.IsNullOrEmpty(pointer.path)?anchor:anchor.Find(pointer.path);
                            var renderer=node==null?null:node.GetComponent<Renderer>();
                            if(renderer==null)throw new InvalidOperationException(record.id+": missing material animation renderer "+pointer.path);
                            var keys=pointer.keys.Select(k=>new ObjectReferenceKeyframe{time=k.time,value=AssetDatabase.LoadAssetAtPath<Material>(k.asset)}).ToArray();
                            if(keys.Any(k=>k.value==null))throw new InvalidOperationException(record.id+": missing animated material");
                            AnimationUtility.SetObjectReferenceCurve(animation,EditorCurveBinding.PPtrCurve(pointer.path,renderer.GetType(),"m_Materials.Array.data["+pointer.slot+"]"),keys);
                        }
                        animation.EnsureQuaternionContinuity();
                        // Quaternion continuity can smooth the vector tangents across a held pose.
                        // Restore per-component linear interpolation after continuity so sparse
                        // constant tails cannot overshoot following a fast motion.
                        foreach(var binding in AnimationUtility.GetCurveBindings(animation))
                        {
                            if(!binding.propertyName.StartsWith("m_LocalRotation.",StringComparison.Ordinal))continue;
                            var curve=AnimationUtility.GetEditorCurve(animation,binding);
                            for(int k=0;k<curve.length;k++)
                            {
                                AnimationUtility.SetKeyBroken(curve,k,true);
                                AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);
                                AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);
                            }
                            AnimationUtility.SetEditorCurve(animation,binding,curve);
                        }
                        var settings=AnimationUtility.GetAnimationClipSettings(animation);settings.loopTime=clip.name.IndexOf("loop",StringComparison.OrdinalIgnoreCase)>=0;AnimationUtility.SetAnimationClipSettings(animation,settings);
                        string clipPath=folder+"/"+controllerName+"_"+Safe(clip.name)+".anim";
                        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if(existing!=null){EditorUtility.CopySerialized(animation,existing);UnityEngine.Object.DestroyImmediate(animation);animation=existing;}else AssetDatabase.CreateAsset(animation,clipPath);
                        imported[clip.name]=animation;
                        var state=sm.AddState(clip.name);state.motion=animation;
                        if(clip.name.IndexOf("intro",StringComparison.OrdinalIgnoreCase)>=0){intro=state;description.intro=clip.name;description.introDuration=clip.duration;}
                        if(clip.name.IndexOf("loop",StringComparison.OrdinalIgnoreCase)>=0){loop=state;description.loop=clip.name;}
                        count++;
                    }
                    if(loop==null && sm.states.Length>0){loop=sm.states.Last().state;description.loop=loop.name;}
                    if(intro!=null && loop!=null && intro!=loop){var transition=intro.AddTransition(loop);transition.hasExitTime=true;transition.exitTime=1;transition.hasFixedDuration=true;transition.duration=0;}
                    sm.defaultState=intro ?? loop;
                    RestoreLayers(controller,record,imported);
                    animator.runtimeAnimatorController=controller;animator.avatar=null;animator.applyRootMotion=false;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    animator.updateMode=AnimatorUpdateMode.UnscaledTime;animator.keepAnimatorControllerStateOnDisable=true;
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
            Debug.Log("SOURCE_ANIMATION_CARD "+group.Key);
            if(++processed%20==0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.StopAssetEditing();
                EditorUtility.UnloadUnusedAssetsImmediate();
                GC.Collect();
                AssetDatabase.StartAssetEditing();
                Debug.Log("SOURCE_ANIMATION_CHECKPOINT "+processed);
            }
        }
        AssetDatabase.SaveAssets();
        }
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();Debug.Log("SOURCE_ANIMATION_IMPORT clips="+count+" missing="+missing);
        if(missing>0)throw new InvalidOperationException("Legacy animation binding paths require repair");
    }
    private static void RestoreLayers(AnimatorController controller, Record record, Dictionary<string,AnimationClip> clips)
    {
        if(record.layers==null || record.layers.Length==0)return;
        var layers=new List<AnimatorControllerLayer>();
        for(int index=0;index<record.layers.Length;index++)
        {
            var data=record.layers[index];var machine=new AnimatorStateMachine { name=data.name };
            AssetDatabase.AddObjectToAsset(machine,controller);
            AnimatorState intro=null,loop=null;var states=new List<AnimatorState>();
            foreach(var item in data.states)
            {
                if(item.clips!=null && item.clips.Length>1)throw new InvalidOperationException(record.id+": source blend tree requires explicit reconstruction");
                AnimationClip clip=null;if(item.clips!=null && item.clips.Length==1)clips.TryGetValue(item.clips[0],out clip);
                var state=machine.AddState(item.name);state.motion=clip;state.speed=item.speed;state.writeDefaultValues=item.writeDefaults;states.Add(state);
                string phase=(item.name+" "+(item.clips!=null && item.clips.Length>0?item.clips[0]:"")).ToLowerInvariant();
                if(phase.Contains("intro") || phase.Contains("entry"))intro=state;
                if(phase.Contains("loop") || phase.Contains("idle"))loop=state;
            }
            if(states.Count==0)
            {
                layers.Add(new AnimatorControllerLayer { name=data.name,stateMachine=machine,defaultWeight=index==0?1:data.weight,blendingMode=data.additive?AnimatorLayerBlendingMode.Additive:AnimatorLayerBlendingMode.Override });
                Debug.Log("SOURCE_EMPTY_LAYER "+record.id+" "+data.name);continue;
            }
            if(loop==null && states.Count>1)loop=states[states.Count-1];
            if(intro==null && states.Count>1 && states[0]!=loop)intro=states[0];
            bool restored=false;
            if(states.Count==data.states.Length)
            for(int stateIndex=0;stateIndex<states.Count;stateIndex++)
                foreach(var original in data.states[stateIndex].transitions ?? new Transition[0])
                {
                    if(!original.unconditional || original.destination<0 || original.destination>=states.Count)continue;
                    var transition=states[stateIndex].AddTransition(states[original.destination]);transition.hasExitTime=original.hasExitTime;transition.exitTime=original.exitTime;transition.hasFixedDuration=original.fixedDuration;transition.duration=original.duration;transition.offset=original.offset;restored=true;
                }
            // Source layers are authoritative, including the absence of transitions.
            for(int stateIndex=0;stateIndex<states.Count;stateIndex++){var clip=states[stateIndex].motion as AnimationClip;if(clip==null)continue;var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=data.states[stateIndex].loop;AnimationUtility.SetAnimationClipSettings(clip,settings);}
            machine.defaultState=states[Mathf.Clamp(data.defaultState,0,states.Count-1)];
            layers.Add(new AnimatorControllerLayer { name=data.name,stateMachine=machine,defaultWeight=index==0?1:data.weight,blendingMode=data.additive?AnimatorLayerBlendingMode.Additive:AnimatorLayerBlendingMode.Override });
        }
        controller.layers=layers.ToArray();
    }
    private static EditorCurveBinding SerializedBinding(Transform target,int type,uint attribute)
    {
        if(type==198 && attribute==2181258151u)attribute=2883525743u;
        foreach(var component in target.GetComponents<Component>())
        {
            if(component==null || (type==198 && !(component is ParticleSystem)))continue;
            var serialized=new SerializedObject(component);var property=serialized.GetIterator();
            while(property.Next(true))
                if(unchecked((uint)Animator.StringToHash(property.propertyPath))==attribute && (property.propertyType==SerializedPropertyType.Float || property.propertyType==SerializedPropertyType.Integer || property.propertyType==SerializedPropertyType.Boolean))
                    return EditorCurveBinding.FloatCurve("",component.GetType(),property.propertyPath);
        }
        return default(EditorCurveBinding);
    }
    private static EditorCurveBinding MaterialBinding(Transform target,uint attribute)
    {
        var renderer=target.GetComponent<Renderer>();if(renderer==null)return default(EditorCurveBinding);
        foreach(var material in renderer.sharedMaterials)
        {
            if(material==null || material.shader==null)continue;
            for(int i=0;i<ShaderUtil.GetPropertyCount(material.shader);i++)
            {
                string name=ShaderUtil.GetPropertyName(material.shader,i);var type=ShaderUtil.GetPropertyType(material.shader,i);
                bool texture=type==ShaderUtil.ShaderPropertyType.TexEnv;
                if(texture)name+="_ST";
                uint hash=unchecked((uint)Animator.StringToHash(name));
                if((hash&0x0fffffff)!=(attribute&0x0fffffff))continue;
                bool vector=texture || type==ShaderUtil.ShaderPropertyType.Color || type==ShaderUtil.ShaderPropertyType.Vector;
                int component=(int)(attribute>>28)&3;
                if(vector && component>3)continue;
                string suffix=vector?"."+(type==ShaderUtil.ShaderPropertyType.Color?"rgba":"xyzw")[component]:"";
                return EditorCurveBinding.FloatCurve("",renderer.GetType(),"material."+name+suffix);
            }
        }
        return default(EditorCurveBinding);
    }
    private static Transform FindExpanded(Transform root,string path)
    {
        var exact=root.Find(path);if(exact!=null)return exact;
        var parts=path.Split('/');Transform found=null;int best=int.MaxValue;bool ambiguous=false;
        foreach(var candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if(candidate.name!=parts[parts.Length-1])continue;
            int index=0;foreach(var part in AnimationUtility.CalculateTransformPath(candidate,root).Split('/'))if(index<parts.Length && part==parts[index])index++;
            if(index!=parts.Length)continue;int depth=AnimationUtility.CalculateTransformPath(candidate,root).Split('/').Length;
            if(depth<best){found=candidate;best=depth;ambiguous=false;}else if(depth==best)ambiguous=true;
        }
        return ambiguous?null:found;
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
