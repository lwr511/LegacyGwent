using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Assets.Script.DynamicCards;
public static class PremiumFinalRepairs
{
 [Serializable] class Document{public Skin[] skins;}
 [Serializable] class Skin{public string source,id,name,path;public string[] bones;public uint[] hashes;public Mapping[] mappedPaths;}
 [Serializable] class Mapping{public uint hash;public string path;}
 [Serializable] class Conversion{public MaterialInfo[] materials;}
 [Serializable] class MaterialInfo{public string asset,shader,renderType;public int queue;}
 [Serializable] class Nodes{public Node[] nodes;}
 [Serializable] class Node{public string path;public Vector3 position,scale;public Quaternion rotation;}
 public static void Run()
 {
  var doc=JsonUtility.FromJson<Document>(File.ReadAllText(Path.GetFullPath("../source_skin_binding_contracts.json")));
  var notes=new List<string>();int restored=0;
  string barnaby="Assets/DynamicCards/Content/Latest/15760101/Card.prefab";
  var actor=PrefabUtility.LoadPrefabContents(barnaby);
  foreach(var bone in JsonUtility.FromJson<Nodes>(File.ReadAllText(Path.GetFullPath("../barnaby_avatar_nodes.json"))).nodes.OrderBy(n=>n.path.Split('/').Length))
  {
   if(actor.transform.Find(bone.path)!=null)continue;
   int slash=bone.path.LastIndexOf('/');var parent=actor.transform.Find(bone.path.Substring(0,slash));
   if(parent==null)throw new Exception("Missing avatar parent "+bone.path);
   var node=new GameObject(bone.path.Substring(slash+1)).transform;node.SetParent(parent,false);
   node.localPosition=bone.position;node.localRotation=bone.rotation;node.localScale=bone.scale;
  }
  PrefabUtility.SaveAsPrefabAsset(actor,barnaby);PrefabUtility.UnloadPrefabContents(actor);
  foreach(var group in doc.skins.GroupBy(s=>s.source+"/"+s.id))
  {
   var first=group.First();string folder="Assets/DynamicCards/Content/"+(first.source=="Native"?"":first.source=="Legacy"?"Legacy2017/":"Latest/")+first.id;
   var root=PrefabUtility.LoadPrefabContents(folder+"/Card.prefab");
   try
   {
    foreach(var item in group)
    {
     var node=DynamicCardPaths.Find(root.transform,item.path);var skin=node==null?null:node.GetComponent<SkinnedMeshRenderer>();
     if(skin==null)throw new Exception("Missing source skin "+item.path);
     var bones=skin.bones;Array.Resize(ref bones,Math.Max(bones.Length,skin.sharedMesh.bindposes.Length));
     var used=new HashSet<int>();foreach(var w in skin.sharedMesh.boneWeights){if(w.weight0>0)used.Add(w.boneIndex0);if(w.weight1>0)used.Add(w.boneIndex1);if(w.weight2>0)used.Add(w.boneIndex2);if(w.weight3>0)used.Add(w.boneIndex3);}
     foreach(int index in used)
     {
      if(index<bones.Length && bones[index]!=null)continue;
      if(index>=bones.Length){notes.Add(group.Key+" "+item.name+" index out of bindpose range "+index);continue;}
      Transform target=index<item.bones.Length?DynamicCardPaths.Find(root.transform,item.bones[index]):null;
      if(target==null && index<item.hashes.Length)
      {
       var candidates=(item.mappedPaths??new Mapping[0]).Where(m=>m.hash==item.hashes[index]).Select(m=>DynamicCardPaths.Find(root.transform,m.path)).Where(t=>t!=null).Distinct().ToArray();
       if(candidates.Length==1)target=candidates[0];
       if(target==null)
       {
        var matches=new HashSet<Transform>();
        foreach(var candidateNode in root.GetComponentsInChildren<Transform>(true))
        {
         var parts=AnimationUtility.CalculateTransformPath(candidateNode,root.transform).Split('/');
         for(int start=0;start<parts.Length;start++)if(unchecked((uint)Animator.StringToHash(string.Join("/",parts.Skip(start))))==item.hashes[index])matches.Add(candidateNode);
        }
        if(matches.Count==1)target=matches.First();
       }
      }
      if(target==null && first.source=="Legacy" && first.id=="12230611" && item.name=="mesh_middle" && index==114 && item.hashes[index]==1819506819)
      {
       // This weighted slot is null in the original bundle and absent from its Avatar.
       // Preserve its authored bind pose instead of collapsing vertices onto the scene origin.
       var parent=skin.rootBone!=null?skin.rootBone:root.transform;
       var local=parent.worldToLocalMatrix*skin.transform.localToWorldMatrix*skin.sharedMesh.bindposes[index].inverse;
       target=new GameObject("RestoredSourceBindPose_114").transform;target.SetParent(parent,false);
       target.localPosition=local.GetColumn(3);target.localRotation=local.rotation;target.localScale=local.lossyScale;
       File.WriteAllText(Path.GetFullPath("../source_bindpose_fallback.txt"),"Legacy2017/12230611 mesh_middle bone 114 hash 1819506819: source m_Bones is null; no matching source Transform or Avatar path. Restored authored bind pose under root bone. Independent missing-bone animation cannot be recovered from this source.");
      }
      if(target!=null){bones[index]=target;restored++;}
      else notes.Add(group.Key+" "+item.name+" unresolved index="+index+" hash="+(index<item.hashes.Length?item.hashes[index]:0));
     }
     skin.bones=bones;
    }
    PrefabUtility.SaveAsPrefabAsset(root,folder+"/Card.prefab");
   }
   finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  foreach(string id in new[]{"11331901","13221401"})
  {
   var conversion=JsonUtility.FromJson<Conversion>(File.ReadAllText("Assets/DynamicCards/Content/Legacy2017/"+id+"/conversion.json"));
   foreach(var info in conversion.materials)
   {
    var material=AssetDatabase.LoadAssetAtPath<Material>(info.asset);
    if(material.shader!=null && material.shader.isSupported)continue;
    var shader=Shader.Find("DynamicCards/Legacy/"+System.Text.RegularExpressions.Regex.Replace(info.shader,"[^A-Za-z0-9_-]","_"));
    if(shader==null)throw new Exception("Missing converted shader "+info.shader);
    material.shader=shader;material.renderQueue=info.queue;material.SetOverrideTag("RenderType",info.renderType??"");EditorUtility.SetDirty(material);
   }
  }
  string particleCard="Assets/DynamicCards/Content/11250100/Card.prefab";var particleRoot=PrefabUtility.LoadPrefabContents(particleCard);
  foreach(var particle in particleRoot.GetComponentsInChildren<ParticleSystem>(true)){var main=particle.main;main.useUnscaledTime=true;var renderer=particle.GetComponent<ParticleSystemRenderer>();if(renderer!=null)renderer.enableGPUInstancing=false;}
  PrefabUtility.SaveAsPrefabAsset(particleRoot,particleCard);PrefabUtility.UnloadPrefabContents(particleRoot);
  foreach(var pair in new[]{new[]{"Legacy2017/14210801","14210801_173344_glow2.mat"},new[]{"Latest/11950101","11950101_24991874_glow2.mat"}})
  {
   string path="Assets/DynamicCards/Content/"+pair[0]+"/Card.prefab";
   string materialPath="Assets/DynamicCards/Content/"+pair[0].Split('/')[0]+"/Shared/"+pair[1];
   AssetDatabase.ImportAsset(materialPath,ImportAssetOptions.ForceUpdate);
   var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(material==null)throw new Exception("Glow material cannot load "+materialPath);
   var root=PrefabUtility.LoadPrefabContents(path);
   foreach(var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true).Where(v=>v.name=="gloww (1)"))
   {var values=renderer.sharedMaterials;values[0]=material;renderer.sharedMaterials=values;}
   PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
  }
  Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_DATA",Path.GetFullPath("../LatestAnimationData"));
  Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_CONTENT","Assets/DynamicCards/Content/Latest");
  Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_IDS","15760101");SourceAnimationImporter.Run();
  AssetDatabase.SaveAssets();File.WriteAllLines(Path.GetFullPath("../remaining_skin_bindings.txt"),notes);Debug.Log("PREMIUM_BONES_RESTORED "+restored+" unresolved="+notes.Count);
  PremiumValidationEditor.Audit();
 }
}
