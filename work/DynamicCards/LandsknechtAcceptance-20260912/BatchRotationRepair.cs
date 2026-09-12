using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BatchRotationRepair
{
    [Serializable] class Request {public Job[] jobs;}
    [Serializable] class Job {public string original,staged,data,sha256;public int frames,columns;public float duration;public Target[] targets;}
    [Serializable] class Target {public string path;public int offset,sample;public float beforeExpected;}
    [Serializable] class Row {public string original,staged,beforeHash,afterHash;public int bones,components;public double beforeError,afterError,seconds;public bool preservedOtherCurves,preservedKeys;}
    static readonly MethodInfo BatchCurve=typeof(AnimationUtility).GetMethod("Internal_SetEditorCurve",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(AnimationClip),typeof(EditorCurveBinding),typeof(AnimationCurve),typeof(bool)},null);
    public static void Run()
    {
        string work=Path.GetFullPath("..");var request=JsonUtility.FromJson<Request>(File.ReadAllText(work+"/rotation-repair-jobs.json"));
        string report=work+"/rotation-repair-engine.jsonl";
        var completed=File.Exists(report)?File.ReadAllLines(report).Where(s=>!string.IsNullOrEmpty(s)).Select(s=>JsonUtility.FromJson<Row>(s)).GroupBy(r=>r.original).ToDictionary(g=>g.Key,g=>g.Last()):new Dictionary<string,Row>();
        Directory.CreateDirectory("Assets/RepairWork");AssetDatabase.Refresh();
        for(int n=0;n<request.jobs.Length;n++)
        {
            var job=request.jobs[n];var start=DateTime.UtcNow;
            if(Hash(job.original)!=job.sha256)throw new Exception("Source changed after audit: "+job.original);
            Row previous;
            if(completed.TryGetValue(job.original,out previous) && File.Exists(job.staged))
            {
                if(Hash(job.staged)!=previous.afterHash)throw new Exception("Verified staging file changed: "+job.staged);
                continue;
            }
            string asset="Assets/RepairWork/"+Path.GetFileName(job.original);
            File.Copy(job.original,asset,false);File.Copy(job.original+".meta",asset+".meta",false);
            AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);if(clip==null)throw new Exception("Clip import failed: "+asset);
            var targets=new HashSet<string>(job.targets.Select(t=>t.path));
            string otherBefore=CurveSignature(clip,targets,false),keysBefore=CurveSignature(clip,targets,true);
            string settingsBefore=JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(clip));
            var eulerOrders=EulerOrders(clip);
            var sourceBytes=File.ReadAllBytes(job.data);var values=new float[sourceBytes.Length/4];Buffer.BlockCopy(sourceBytes,0,values,0,sourceBytes.Length);
            double before=Sample(clip,job,values);
            EditorCurveBinding lastBinding=default(EditorCurveBinding);AnimationCurve lastCurve=null;int count=0;
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if(!targets.Contains(binding.path) || !binding.propertyName.StartsWith("m_LocalRotation.",StringComparison.Ordinal))continue;
                var curve=AnimationUtility.GetEditorCurve(clip,binding);
                for(int k=0;k<curve.length;k++)
                {
                    AnimationUtility.SetKeyBroken(curve,k,true);
                    AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);
                }
                if(BatchCurve!=null)BatchCurve.Invoke(null,new object[]{clip,binding,curve,false});else AnimationUtility.SetEditorCurve(clip,binding,curve);
                lastBinding=binding;lastCurve=curve;count++;
            }
            if(count!=targets.Count*4 || lastCurve==null)throw new Exception("Incomplete quaternion binding set: "+asset);
            AnimationUtility.SetEditorCurve(clip,lastBinding,lastCurve);
            RestoreEulerOrders(clip,eulerOrders);
            if(otherBefore!=CurveSignature(clip,targets,false))throw new Exception("Unrelated curves changed: "+asset);
            if(keysBefore!=CurveSignature(clip,targets,true))throw new Exception("Authored key values or times changed: "+asset);
            if(settingsBefore!=JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(clip)))throw new Exception("Clip settings changed: "+asset);
            double after=Sample(clip,job,values);
            if(after>.03)throw new Exception("Engine sample differs from source by "+after+" degrees: "+asset);
            EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(job.staged));File.Copy(asset,job.staged,false);File.Copy(asset+".meta",job.staged+".meta",false);
            var row=new Row{original=job.original,staged=job.staged,beforeHash=job.sha256,afterHash=Hash(job.staged),bones=targets.Count,components=count,beforeError=before,afterError=after,seconds=(DateTime.UtcNow-start).TotalSeconds,preservedOtherCurves=true,preservedKeys=true};
            File.AppendAllText(report,JsonUtility.ToJson(row)+"\n");
            File.WriteAllText(work+"/rotation-repair-progress.txt",(n+1)+"/"+request.jobs.Length+" "+Path.GetFileName(job.original)+" "+DateTime.UtcNow.ToString("O"));
            if(!asset.StartsWith("Assets/RepairWork/",StringComparison.Ordinal))throw new Exception("Unexpected scratch asset path");
            AssetDatabase.DeleteAsset(asset);
            if(n%10==9){EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();}
        }
        File.WriteAllText(work+"/rotation-repair-complete.txt",DateTime.UtcNow.ToString("O"));
    }
    static string Hash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    static Dictionary<string,int> EulerOrders(AnimationClip clip)
    {
        var serialized=new SerializedObject(clip);var curves=serialized.FindProperty("m_EulerCurves");var result=new Dictionary<string,int>();
        for(int i=0;i<curves.arraySize;i++)
        {
            var curve=curves.GetArrayElementAtIndex(i);result.Add(curve.FindPropertyRelative("path").stringValue,curve.FindPropertyRelative("curve.m_RotationOrder").intValue);
        }
        return result;
    }
    static void RestoreEulerOrders(AnimationClip clip,Dictionary<string,int> orders)
    {
        var serialized=new SerializedObject(clip);var curves=serialized.FindProperty("m_EulerCurves");
        if(curves.arraySize!=orders.Count)throw new Exception("Euler curve count changed during quaternion rebuild");
        for(int i=0;i<curves.arraySize;i++)
        {
            var curve=curves.GetArrayElementAtIndex(i);string path=curve.FindPropertyRelative("path").stringValue;
            curve.FindPropertyRelative("curve.m_RotationOrder").intValue=orders[path];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static string CurveSignature(AnimationClip clip,HashSet<string> targets,bool selectedKeys)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            foreach(var b in AnimationUtility.GetCurveBindings(clip).OrderBy(b=>b.path,StringComparer.Ordinal).ThenBy(b=>b.propertyName,StringComparer.Ordinal))
            {
                bool selected=targets.Contains(b.path)&&b.propertyName.StartsWith("m_LocalRotation.",StringComparison.Ordinal);
                if(selected!=selectedKeys)continue;var c=AnimationUtility.GetEditorCurve(clip,b);
                writer.Write(b.path);writer.Write(b.propertyName);writer.Write(b.type==null?"null":b.type.FullName);writer.Write(c.length);
                foreach(var k in c.keys)
                {
                    writer.Write(k.time);writer.Write(k.value);
                    if(!selectedKeys){writer.Write(k.inTangent);writer.Write(k.outTangent);writer.Write(k.inWeight);writer.Write(k.outWeight);writer.Write((int)k.weightedMode);}
                }
            }
            writer.Flush();using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(stream.ToArray()));
        }
    }
    static double Sample(AnimationClip clip,Job job,float[] values)
    {
        var root=new GameObject("Original sample verification");var nodes=new Dictionary<string,Transform>{{"",root.transform}};
        foreach(var b in AnimationUtility.GetCurveBindings(clip))
        {
            if(b.type!=typeof(Transform))continue;
            string path="";var parent=root.transform;
            foreach(string part in b.path.Split('/'))
            {
                if(part.Length==0)continue;path=path.Length==0?part:path+"/"+part;Transform child;
                if(!nodes.TryGetValue(path,out child)){child=new GameObject(part).transform;child.SetParent(parent,false);nodes[path]=child;}parent=child;
            }
        }
        double peak=0;
        foreach(var group in job.targets.GroupBy(t=>t.sample))
        {
            clip.SampleAnimation(root,group.Key*job.duration/(job.frames-1));
            foreach(var t in group)
            {
                var q=nodes[t.path].localRotation;int offset=t.sample*job.columns+t.offset;
                double x=values[offset],y=values[offset+1],z=values[offset+2],w=values[offset+3];
                double dot=q.x*x+q.y*y+q.z*z+q.w*w;
                double norm=Math.Sqrt((q.x*(double)q.x+q.y*(double)q.y+q.z*(double)q.z+q.w*(double)q.w)*(x*x+y*y+z*z+w*w));
                double error=2*Math.Acos(Math.Min(1,Math.Abs(dot/norm)))*180/Math.PI;peak=Math.Max(peak,error);
            }
        }
        UnityEngine.Object.DestroyImmediate(root);return peak;
    }
}
