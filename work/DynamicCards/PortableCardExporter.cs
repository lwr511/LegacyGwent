using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Conversion tool only. The client never loads a newer-version bundle or source-game code.
[InitializeOnLoad]
public static class PortableCardExporter
{
    [Serializable] public class MaterialRecord { public string asset, originalName, shader; }
    [Serializable] public class AnimationRecord { public string path, intro, loop; public float introDuration; }
    [Serializable] public class CardRecord { public string id; public int particles; public List<MaterialRecord> materials=new List<MaterialRecord>(); public List<AnimationRecord> animations=new List<AnimationRecord>(); }
    static readonly Dictionary<Object,Object> Shared = new Dictionary<Object,Object>();
    [Serializable] public class Times { public string card, clip; public float[] times; }
    [Serializable] public class TimingFile { public Times[] clips; }
    static string currentCard; static TimingFile timing;
    static string[] paths; static int index; static AssetBundle bundle, textures;
    static PortableCardExporter() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run() { SessionState.SetBool("PortableCards",true); EditorApplication.isPlaying=true; }
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PortableCards",false))return;
        SessionState.SetBool("PortableCards",false);
        const string source=@"C:\SteamLibrary\steamapps\common\Thronebreaker The Witcher Tales\Thronebreaker_Data\StreamingAssets\bundledassets\cardassets\";
        textures=AssetBundle.LoadFromFile(source+"textures/premium/high");
        bundle=AssetBundle.LoadFromFile(source+"scenes");
        paths=bundle.GetAllScenePaths().OrderBy(p=>p).ToArray();
        var args=Environment.GetCommandLineArgs();int option=Array.IndexOf(args,"-cardId");
        if(option>=0)paths=paths.Where(p=>p.EndsWith(args[option+1]+".unity")).ToArray();
        int startOption=Array.IndexOf(args,"-startCard"); if(startOption>=0)paths=paths.Where(p=>string.CompareOrdinal(Path.GetFileNameWithoutExtension(p),args[startOption+1])>=0).ToArray();
        timing=JsonUtility.FromJson<TimingFile>(File.ReadAllText(Application.dataPath+"/../../long_clip_times.json"));
        Directory.CreateDirectory(Application.dataPath+"/PortableCards/Shared");AssetDatabase.Refresh();
        Next();
    }
    static void Next()
    {
        if(index>=paths.Length){AssetDatabase.SaveAssets();Debug.Log("PORTABLE_ALL_DONE count="+index);EditorApplication.Exit(0);return;}
        string path=paths[index++];var op=SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
        op.completed += _ => {
            try { Export(SceneManager.GetSceneByPath(path),Path.GetFileNameWithoutExtension(path)); }
            catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);return;}
            SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(path)).completed += __ => Next();
        };
    }
    static string Safe(string name) { return new string(name.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_').ToArray()); }
    static string Relative(Transform tr, Transform root) { return AnimationUtility.CalculateTransformPath(tr,root); }
    static void Export(Scene scene,string id)
    {
        currentCard=id; string dir="Assets/PortableCards/"+id;Directory.CreateDirectory(Application.dataPath+"/PortableCards/"+id);AssetDatabase.Refresh();
        var source=new GameObject(id);
        foreach(var r in scene.GetRootGameObjects()){r.transform.SetParent(source.transform,true);r.SetActive(true);}
        var animators=source.GetComponentsInChildren<Animator>(true);
        foreach(var animator in animators) { animator.enabled=true; animator.fireEvents=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; if(animator.hasTransformHierarchy==false)AnimatorUtility.DeoptimizeTransformHierarchy(animator.gameObject); }
        var record=new CardRecord{id=id};
        var baked=new Dictionary<Animator,RuntimeAnimatorController>();
        foreach(var animator in animators)
        {
            if(animator.runtimeAnimatorController==null)continue;
            var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
            var output=AnimatorController.CreateAnimatorControllerAtPath(dir+"/"+Safe(animator.name)+".controller");
            var sm=output.layers[0].stateMachine;AnimatorState intro=null,loop=null;
            var ar=new AnimationRecord{path=Relative(animator.transform,source.transform)};
            foreach(var clip in clips)
            {
                if(clip.length<=0)continue;
                var portable=Bake(animator,clip);
                string clipPath=dir+"/"+Safe(animator.name+"_"+clip.name)+".anim";
                AssetDatabase.CreateAsset(portable,clipPath);
                var state=sm.AddState(clip.name);state.motion=portable;
                if(clip.name.IndexOf("intro",StringComparison.OrdinalIgnoreCase)>=0){intro=state;ar.intro=clip.name;ar.introDuration=clip.length;}
                if(clip.name.IndexOf("loop",StringComparison.OrdinalIgnoreCase)>=0){loop=state;ar.loop=clip.name;}
            }
            if(loop==null && sm.states.Length>0){loop=sm.states.Last().state;ar.loop=loop.name;}
            if(intro!=null && loop!=null && intro!=loop){var t=intro.AddTransition(loop);t.hasExitTime=true;t.exitTime=1;t.hasFixedDuration=true;t.duration=0;}
            sm.defaultState=intro!=null?intro:loop;
            baked[animator]=output;record.animations.Add(ar);
        }
        // Start again from the source rest pose before copying. Playback is supplied by baked controllers.
        var deps=EditorUtility.CollectDependencies(new Object[]{source});
        var map=new Dictionary<Object,Object>(Shared); var materialPaths=new Dictionary<Material,string>();
        GameObject root=null;
        foreach(var tr in source.GetComponentsInChildren<Transform>(true))
        {
            var go=new GameObject(tr.name);map[tr.gameObject]=go;map[tr]=go.transform;
            if(tr==source.transform)root=go;else go.transform.SetParent((Transform)map[tr.parent],false);
            go.transform.localPosition=tr.localPosition;go.transform.localRotation=tr.localRotation;go.transform.localScale=tr.localScale;go.SetActive(tr.gameObject.activeSelf);
        }
        foreach(var component in source.GetComponentsInChildren<Component>(true))
        {
            if(component==null || component is Transform)continue;
            if(!(component is Renderer || component is MeshFilter || component is Animator || component is ParticleSystem))continue;
            var go=(GameObject)map[component.gameObject];var copy=go.GetComponent(component.GetType());if(copy==null)copy=go.AddComponent(component.GetType());
            EditorUtility.CopySerialized(component,copy);map[component]=copy;
        }
        var atlas=textures.LoadAllAssets<Texture2D>().FirstOrDefault(t=>t.name==id);
        if(atlas!=null && !map.ContainsKey(atlas))map[atlas]=SaveTexture(atlas);
        foreach(var d in deps)
        {
            if(d==null || map.ContainsKey(d))continue;
            if(d is Texture2D texture)map[d]=SaveTexture(texture);
            else if(d is Mesh || d is Material)
            {
                var copy=Object.Instantiate(d);string dest="Assets/PortableCards/Shared/"+currentCard+"_"+Math.Abs((long)d.GetInstanceID())+"_"+Safe(d.name)+(d is Material?".mat":".asset");
                if(d is Material material)
                {
                    // Keep the native shader name for the cross-version shader conversion step.
                    record.materials.Add(new MaterialRecord{asset=dest,originalName=d.name,shader=material.shader==null?"":material.shader.name});
                    var m=(Material)copy;
                }
                if(copy is Material pendingMaterial) materialPaths[pendingMaterial]=dest; else AssetDatabase.CreateAsset(copy,dest);map[d]=copy;
            }
        }
        foreach(var obj in map.Values.Where(o=>!(o is Texture2D)).Concat(root.GetComponentsInChildren<Component>(true).Cast<Object>()).Distinct())
        {
            if(obj==null)continue;var so=new SerializedObject(obj);var it=so.GetIterator();
            while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference && it.objectReferenceValue!=null && map.TryGetValue(it.objectReferenceValue,out var replacement))it.objectReferenceValue=replacement;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var originalMaterial in deps.OfType<Material>()) { if(!map.ContainsKey(originalMaterial))continue; var converted=(Material)map[originalMaterial]; foreach(var property in originalMaterial.GetTexturePropertyNames()) { var tex=originalMaterial.GetTexture(property); if(tex!=null && map.ContainsKey(tex)) converted.SetTexture(property,(Texture)map[tex]); } converted.shader=Shader.Find("Unlit/Texture"); }
        foreach(var pair in materialPaths)AssetDatabase.CreateAsset(pair.Key,pair.Value);
        foreach(var pair in baked)
        {
            var a=(Animator)map[pair.Key];a.runtimeAnimatorController=pair.Value;a.avatar=null;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.fireEvents=false;
        }
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
        record.particles=root.GetComponentsInChildren<ParticleSystem>(true).Length;
        if(PrefabUtility.SaveAsPrefabAsset(root,dir+"/Card.prefab")==null)throw new Exception("Cannot save "+id);
        File.WriteAllText(Application.dataPath+"/PortableCards/"+id+"/conversion.json",JsonUtility.ToJson(record,true));
        AssetDatabase.SaveAssets();
        foreach(var pair in map)if(pair.Key is Texture2D || pair.Key is Mesh)Shared[pair.Key]=pair.Value;
        Object.DestroyImmediate(root);Object.DestroyImmediate(source);
        Debug.Log("PORTABLE_CARD_DONE "+id+" particles="+record.particles+" controllers="+baked.Count);
    }
    static Texture2D SaveTexture(Texture2D texture)
    {
        string path="Assets/PortableCards/Shared/"+currentCard+"_"+Math.Abs((long)texture.GetInstanceID())+"_"+Safe(texture.name)+".png";
        var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        Graphics.Blit(texture,rt);var previous=RenderTexture.active;RenderTexture.active=rt;
        var png=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);png.Apply();RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(Application.dataPath+path.Substring(6),png.EncodeToPNG());Object.DestroyImmediate(png);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.mipmapEnabled=true;importer.wrapMode=texture.wrapMode;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static AnimationClip Bake(Animator animator,AnimationClip clip)
    {
        var sampler=new AnimatorController();sampler.AddLayer("Sample");var state=sampler.layers[0].stateMachine.AddState("Sample");state.motion=clip;sampler.layers[0].stateMachine.defaultState=state;
        animator.runtimeAnimatorController=sampler;animator.Rebind();animator.Play("Sample",0,0);animator.Update(0);
        var transforms=animator.GetComponentsInChildren<Transform>(true);
        var values=transforms.Select(t=>Enumerable.Range(0,10).Select(_=>new List<Keyframe>()).ToArray()).ToArray();
        var output=new AnimationClip{name=clip.name,frameRate=30};
        int frames=Math.Max(1,Mathf.CeilToInt(clip.length*30));if(clip.length>30)frames=900;float previous=0;
        var times=Enumerable.Range(0,frames+1).Select(i=>clip.length*i/frames).ToList();var extra=timing.clips.FirstOrDefault(t=>t.card==currentCard && t.clip==clip.name);if(extra!=null)times.AddRange(extra.times);times=times.Distinct().OrderBy(t=>t).ToList();for(int i=times.Count-2;i>=0;i--)if(times[i+1]-times[i]<.0001f)times.RemoveAt(i);
        for(int frame=0;frame<times.Count;frame++)
        {
            float time=times[frame];if(frame>0)animator.Update(time-previous);previous=time;
            for(int i=0;i<transforms.Length;i++)
            {
                var t=transforms[i];var p=t.localPosition;var q=t.localRotation;var s=t.localScale;
                float[] v={p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};
                for(int j=0;j<10;j++)values[i][j].Add(new Keyframe(time,v[j]));
            }
        }
        string[] properties={"localPosition.x","localPosition.y","localPosition.z","localRotation.x","localRotation.y","localRotation.z","localRotation.w","localScale.x","localScale.y","localScale.z"};
        var bindings=new List<EditorCurveBinding>();var outputCurves=new List<AnimationCurve>();
        for(int i=0;i<transforms.Length;i++)for(int j=0;j<10;j++)
        {
            var keys=values[i][j];if((j<3 || j>6) && keys.All(k=>Math.Abs(k.value-keys[0].value)<0.000001f))keys=new List<Keyframe>{keys[0],keys[keys.Count-1]};
            var curve=new AnimationCurve(keys.ToArray());for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
            bindings.Add(EditorCurveBinding.FloatCurve(Relative(transforms[i],animator.transform),typeof(Transform),properties[j]));outputCurves.Add(curve);
        }
        AnimationUtility.SetEditorCurves(output,bindings.ToArray(),outputCurves.ToArray());
        output.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(output);settings.loopTime=clip.name.IndexOf("intro",StringComparison.OrdinalIgnoreCase)<0;AnimationUtility.SetAnimationClipSettings(output,settings);
        animator.Play("Sample",0,0);animator.Update(0);
        return output;
    }
}
