using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEditor;using UnityEngine;using Assets.Script.DynamicCards;using Assets.Script.DynamicCards.SourceParticles;
public static class HayateSourceImport
{
    [Serializable] public class Doc{public Record[] records;}
    [Serializable] public class Record{public string source,scene,prefab;public ComponentData[] components;}
    [Serializable] public class ComponentData{public string path,dataJson;public long sourceObjectId;public int[] ordinals;public bool enabled;public Reference[] references;}
    [Serializable] public class Reference{public string field,path;public int[] ordinals;}
    [Serializable] public class Evidence{public string scene,path,savedJson;public long sourceObjectId;public bool enabled,hasParticles;public int attractors;}
    [Serializable] public class Report{public bool complete;public List<Evidence> components=new List<Evidence>();}
    static string Arg(string name){var args=Environment.GetCommandLineArgs();return args[Array.IndexOf(args,name)+1];}
    static int SameNameOrdinal(Transform node)
    {
        int ordinal=0;if(node.parent==null)return ordinal;
        foreach(Transform sibling in node.parent){if(sibling==node)return ordinal;if(sibling.name==node.name)ordinal++;}
        throw new Exception("Invalid transform hierarchy");
    }
    static Transform Find(Transform root,string path,int[] ordinals)
    {
        var expected=path.Split('/');if(ordinals==null || ordinals.Length!=expected.Length)throw new Exception("Missing source object identity "+path);
        var matches=new List<Transform>();
        foreach(var candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if(candidate.name!=expected[expected.Length-1])continue;
            var parents=new List<Transform>();for(var n=candidate;n!=null&&n!=root;n=n.parent)parents.Add(n);parents.Reverse();int index=0;Transform last=null;
            foreach(var n in parents)if(index<expected.Length&&n.name==expected[index]&&SameNameOrdinal(n)==ordinals[index]){index++;last=n;}
            if(index==expected.Length&&last==candidate)matches.Add(candidate);
        }
        if(matches.Count!=1)throw new Exception("Ambiguous source object identity "+path+" matches="+matches.Count);
        return matches[0];
    }
    public static void Run()
    {
        var doc=JsonUtility.FromJson<Doc>(File.ReadAllText(Arg("-hayateData")));var report=new Report();
        foreach(var record in doc.records)
        {
            var root=PrefabUtility.LoadPrefabContents(record.prefab);
            try
            {
                foreach(var prior in root.GetComponentsInChildren<Hayate>(true))UnityEngine.Object.DestroyImmediate(prior);
                foreach(var data in record.components)
                {
                    var target=Find(root.transform,data.path,data.ordinals);if(target==null)throw new Exception("Missing Hayate target "+record.scene+" "+data.path);
                    var component=target.GetComponent<Hayate>();if(component==null)component=target.gameObject.AddComponent<Hayate>();
                    JsonUtility.FromJsonOverwrite(data.dataJson,component);component.enabled=data.enabled;
                    foreach(var reference in data.references)
                    {
                        var parts=reference.field.Split('.');var field=typeof(Hayate).GetField(parts[0]);
                        if(field==null)throw new Exception("Unknown source object field "+reference.field);
                        var node=string.IsNullOrEmpty(reference.path)?null:Find(root.transform,reference.path,reference.ordinals);
                        if(!string.IsNullOrEmpty(reference.path)&&node==null)throw new Exception("Missing source reference "+reference.path);
                        if(parts.Length==2)
                        {
                            if(field.FieldType!=typeof(List<GameObject>))throw new Exception("Unexpected source list "+reference.field);
                            var list=(List<GameObject>)field.GetValue(component);int index=int.Parse(parts[1]);while(list.Count<=index)list.Add(null);list[index]=node==null?null:node.gameObject;
                        }
                        else if(node==null)field.SetValue(component,null);
                        else if(field.FieldType==typeof(GameObject))field.SetValue(component,node.gameObject);
                        else if(field.FieldType==typeof(Transform))field.SetValue(component,node);
                        else throw new Exception("Unsupported source object type "+field.FieldType);
                    }
                    if(component.attractorPositions.Count<component.attractors.Count || (component.useAttractor && (component.attractorStrength.Count<component.attractors.Count || component.attractorAttenuation.Count<component.attractors.Count)))
                        throw new Exception("Original attractor arrays are inconsistent at "+data.path);
                    if(target.GetComponent<ParticleSystem>()==null)throw new Exception("Original particle system missing at "+data.path);
                    report.components.Add(new Evidence{scene=record.scene,path=data.path,sourceObjectId=data.sourceObjectId,savedJson=JsonUtility.ToJson(component),enabled=component.enabled,hasParticles=true,attractors=component.attractors.Count});
                }
                PrefabUtility.SaveAsPrefabAsset(root,record.prefab);Debug.Log("SOURCE_HAYATE_IMPORTED "+record.scene+" components="+record.components.Length);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();report.complete=true;File.WriteAllText(Arg("-hayateReport"),JsonUtility.ToJson(report,true));Debug.Log("SOURCE_HAYATE_COMPLETE "+report.components.Count);
    }
}
