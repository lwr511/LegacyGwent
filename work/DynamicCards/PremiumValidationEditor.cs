using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Assets.Script.DynamicCards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PremiumValidationEditor
{
 [Serializable] public sealed class Report { public int cards,renderers,particles,animators,sourceEmptyMaterialSlots,unusedBoneSlots,sourceAuthoringMaterials; public List<string> issues=new List<string>(); }
 [Serializable] public sealed class SourceSlots {public SourceSlot[] slots;}
 [Serializable] public sealed class SourceSlot {public string source,id,type,name,path;public int slot;}
 public static void PrepareLegacy()
 {
  string remaining=Path.GetFullPath("../legacy_prepare_remaining.txt");
  Environment.SetEnvironmentVariable("DYNAMIC_PREPARE_IDS",File.Exists(remaining)?File.ReadAllText(remaining):null);
  Environment.SetEnvironmentVariable("DYNAMIC_PREPARE_ROOT","Assets/DynamicCards/Content/Legacy2017/");
  Assets.Script.DynamicCards.Editor.DynamicCardContentImporter.Prepare();
 }
 public static void Audit()
 {
  var report=new Report();
  var sourceSlots=JsonUtility.FromJson<SourceSlots>(File.ReadAllText(Path.GetFullPath("../source_null_material_slots.json")));
  var originalEmpty=sourceSlots.slots.ToLookup(s=>s.source+"/"+s.id+"/"+s.type+"/"+s.name+"/"+s.slot);
  var catalog=JsonUtility.FromJson<DynamicCardCatalog>(File.ReadAllText(DynamicCardLibrary.CatalogAsset));
  foreach(var entry in catalog.cards)
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab);
   if(prefab==null){report.issues.Add(entry.prefab+": missing prefab");continue;}
   report.cards++;
   foreach(var component in prefab.GetComponentsInChildren<Component>(true))
    if(component==null)report.issues.Add(entry.prefab+": missing script");
   foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
   {
    report.renderers++;
    var materials=renderer.sharedMaterials;
    for(int slot=0;slot<materials.Length;slot++)
    {
     // Particle renderers serialize a second, unused material slot when trails are disabled.
     var particle=renderer.GetComponent<ParticleSystem>();
     if(renderer is ParticleSystemRenderer && slot==1 && particle!=null && !particle.trails.enabled)continue;
     var material=materials[slot];
     string kind=entry.prefab.Contains("/Latest/")?"Latest":entry.prefab.Contains("/Legacy2017/")?"Legacy":"Native";
     if(material==null && originalEmpty[kind+"/"+entry.id+"/"+renderer.GetType().Name+"/"+renderer.name+"/"+slot].Any(s=>PathMatches(s.path,AnimationUtility.CalculateTransformPath(renderer.transform,prefab.transform))))
     {report.sourceEmptyMaterialSlots++;continue;}
     if(material!=null && AssetDatabase.GetAssetPath(material)=="Assets/DynamicCards/Content/Latest/Shared/10720101_9752214_henselt_ref_plane.mat" && !renderer.gameObject.activeSelf)
     {report.sourceAuthoringMaterials++;continue;}
     if(material==null || material.shader==null || !material.shader.isSupported)
      report.issues.Add(entry.prefab+": unsupported material on "+renderer.name+" slot="+slot+" / "+(material==null?"null":material.name));
    }
    var skin=renderer as SkinnedMeshRenderer;
    if(skin!=null && skin.sharedMesh!=null && skin.sharedMesh.bindposes.Length>0 && skin.bones.Any(b=>b==null))
    {
     var used=new HashSet<int>();
     foreach(var w in skin.sharedMesh.boneWeights){if(w.weight0>0)used.Add(w.boneIndex0);if(w.weight1>0)used.Add(w.boneIndex1);if(w.weight2>0)used.Add(w.boneIndex2);if(w.weight3>0)used.Add(w.boneIndex3);}
     if(used.Any(i=>i>=skin.bones.Length || skin.bones[i]==null))report.issues.Add(entry.prefab+": missing skin bone "+skin.name);
     report.unusedBoneSlots+=Enumerable.Range(0,skin.bones.Length).Count(i=>skin.bones[i]==null && !used.Contains(i));
    }
   }
   report.particles+=prefab.GetComponentsInChildren<ParticleSystem>(true).Length;
   report.animators+=prefab.GetComponentsInChildren<Animator>(true).Length;
   foreach(var animator in prefab.GetComponentsInChildren<Animator>(true))
    if(animator.updateMode!=AnimatorUpdateMode.UnscaledTime || !animator.keepAnimatorControllerStateOnDisable)
     report.issues.Add(entry.prefab+": animation pause settings on "+animator.name);
   foreach(var particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
    if(!particle.main.useUnscaledTime || (particle.GetComponent<ParticleSystemRenderer>()!=null && particle.GetComponent<ParticleSystemRenderer>().enableGPUInstancing))
     report.issues.Add(entry.prefab+": particle timing/settings on "+particle.name);
   if(report.cards%20==0){EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();}
   if(report.cards%100==0)Debug.Log("PREMIUM_STRUCTURE_PROGRESS "+report.cards);
  }
  File.WriteAllText(Path.GetFullPath("../premium_structure_audit.json"),JsonUtility.ToJson(report,true));
  Debug.Log("PREMIUM_STRUCTURE_DONE cards="+report.cards+" issues="+report.issues.Count);
  if(report.issues.Count>0)throw new Exception("Premium structural validation failed; see premium_structure_audit.json");
 }
 private static bool PathMatches(string expected,string actual)
 {
  int index=0;var parts=expected.Split('/');foreach(string part in actual.Split('/'))if(index<parts.Length && part==parts[index])index++;
  return index==parts.Length;
 }
 public static void Timeline()
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  new GameObject("Geralt source timeline test").AddComponent<GeraltTimelineSmoke>();
  EditorApplication.isPlaying=true;
 }
 public static void Bundle()
 {
  string path=Assets.Script.DynamicCards.Editor.DynamicCardBuild.BuildBundle(BuildTarget.StandaloneWindows64);
  int expected=JsonUtility.FromJson<DynamicCardCatalog>(File.ReadAllText(DynamicCardLibrary.CatalogAsset)).cards.Length;
  int actual=0;long bytes=0;int count=0;
  foreach(var file in Assets.Script.DynamicCards.Editor.DynamicCardBundleBuilder.PayloadFiles(Path.GetDirectoryName(path)))
  {
   bytes+=new FileInfo(file).Length;
   if(!file.EndsWith(".bundle"))continue;
   var bundle=AssetBundle.LoadFromFile(file);
   if(bundle==null)throw new Exception("Could not open fresh premium bundle "+file);
   actual+=bundle.GetAllAssetNames().Count(p=>p.EndsWith("/card.prefab"));count++;
   bundle.Unload(true);
  }
  if(actual!=expected)throw new Exception("Premium bundle scene count "+actual+" expected "+expected);
  Debug.Log("PREMIUM_BUNDLE_PASS cards="+actual+" bundles="+count+" bytes="+bytes);
 }
}
