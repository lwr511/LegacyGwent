using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace LegacyGwent.LadyLakePremium.Acceptance {
[InitializeOnLoad] public static class PremiumSourceAudit {
const string Folder="Assets/Experiments/LadyLakePremium/Acceptance";
static double next;
static PremiumSourceAudit(){EditorApplication.update+=Poll;}
static void Poll(){ if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlaying)return;next=EditorApplication.timeSinceStartup+1;string p=Folder+"/audit-request.txt";if(!File.Exists(p))return;File.Delete(p);try{Run();}catch(Exception e){File.WriteAllText(Folder+"/source-audit-error.txt",e.ToString());}}
[Serializable] class Row {public string prefab;public int skinnedRenderers,animators,particles;public MeshRow[] meshes;public string[] clips;}
[Serializable] class MeshRow {public string name;public int vertices,triangles,bones,weightedVertices,multiWeightedVertices;public Vector3 posedSize;public bool closed;public int boundaryEdges;}
[Serializable] class Report {public Row[] rows;}
static string Key(int a,int b){return a<b?a+":"+b:b+":"+a;}
public static void Run(){var paths=new[]{"Assets/DynamicCards/Content/Old/Legacy2017/20161801/Card.prefab","Assets/DynamicCards/Content/Old/Thronebreaker/15010100/Card.prefab"};var rows=new List<Row>();foreach(var path in paths){var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!source)throw new Exception(path);var go=UnityEngine.Object.Instantiate(source);go.hideFlags=HideFlags.HideAndDontSave;try {foreach(var a in go.GetComponentsInChildren<Animator>(true)){a.Rebind();a.Update(0);}var ms=new List<MeshRow>();foreach(var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){var mesh=s.sharedMesh;var baked=new Mesh();s.BakeMesh(baked);var v=baked.vertices;var bounds=new Bounds();for(int i=0;i<v.Length;i++){var point=go.transform.InverseTransformPoint(s.transform.TransformPoint(v[i]));if(i==0)bounds=new Bounds(point,Vector3.zero);else bounds.Encapsulate(point);}var weights=mesh.boneWeights;var edges=new Dictionary<string,int>();var tri=mesh.triangles;for(int i=0;i<tri.Length;i+=3)for(int e=0;e<3;e++){var key=Key(tri[i+e],tri[i+(e+1)%3]);int n;edges.TryGetValue(key,out n);edges[key]=n+1;}int boundary=edges.Count(e=>e.Value==1);ms.Add(new MeshRow{name=s.name,vertices=mesh.vertexCount,triangles=tri.Length/3,bones=s.bones.Length,posedSize=bounds.size,weightedVertices=weights.Count(w=>w.weight0>0),multiWeightedVertices=weights.Count(w=>w.weight1>0),boundaryEdges=boundary,closed=boundary==0});UnityEngine.Object.DestroyImmediate(baked);}rows.Add(new Row{prefab=path,skinnedRenderers=ms.Count,animators=go.GetComponentsInChildren<Animator>(true).Length,particles=go.GetComponentsInChildren<ParticleSystem>(true).Length,meshes=ms.ToArray(),clips=go.GetComponentsInChildren<Animator>(true).Where(a=>a.runtimeAnimatorController!=null).SelectMany(a=>a.runtimeAnimatorController.animationClips).Select(c=>c.name+" "+c.length.ToString("F3")+"s").Distinct().ToArray()});}finally{UnityEngine.Object.DestroyImmediate(go);}}File.WriteAllText(Folder+"/source-audit.json",JsonUtility.ToJson(new Report{rows=rows.ToArray()},true));}
}}
