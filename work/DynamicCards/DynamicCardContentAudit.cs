using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Script.DynamicCards.Editor
{
    public static class DynamicCardContentAudit
    {
        [Serializable] private class Result { public int cards, particles; public List<string> issues = new List<string>(); }
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = JsonUtility.FromJson<DynamicCardCatalog>(File.ReadAllText(DynamicCardLibrary.CatalogAsset));
            string output = Path.GetFullPath("../../../../work/DynamicCards/RenderAudit");Directory.CreateDirectory(output);
            var result = new Result();
            var camera = new GameObject("Audit camera").AddComponent<Camera>();camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = Color.clear;camera.allowHDR = false;
            var rt = new RenderTexture(384,384,24);rt.Create();camera.targetTexture = rt;
            foreach (var entry in catalog.cards)
            {
                GameObject model = null;
                try
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab);
                    if (prefab == null) throw new Exception("prefab missing");
                    model = Object.Instantiate(prefab);model.SetActive(true);
                    foreach (Transform child in model.transform) if(child.localPosition.sqrMagnitude>1000000)child.localPosition=Vector3.zero;
                    var renderers = model.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length==0)throw new Exception("no renderers");
                    foreach (var renderer in renderers)
                        foreach (var material in renderer.sharedMaterials)
                            if(material==null || material.shader==null || material.shader.name!="DynamicCards/PortableCard")result.issues.Add(entry.id+": material "+renderer.name);
                    foreach(var pair in entry.transformPairs ?? new DynamicCardTransformPair[0])
                        if(DynamicCardPaths.Find(model.transform,pair.source)==null || DynamicCardPaths.Find(model.transform,pair.target)==null)result.issues.Add(entry.id+": transform pair "+pair.source+" -> "+pair.target);
                    foreach(var evt in entry.particleEvents ?? new DynamicCardParticleEvent[0])
                        if(DynamicCardPaths.Find(model.transform,evt.path)==null)result.issues.Add(entry.id+": particle event "+evt.path);
                    foreach(var animator in model.GetComponentsInChildren<Animator>(true))
                    { animator.Rebind();animator.Update(0);animator.Update(entry.introDuration+1); }
                    var pivot=DynamicCardPaths.Find(model.transform,entry.pivot);
                    if(pivot!=null)pivot.localRotation*=Quaternion.Euler((entry.xStart+entry.xEnd)*.5f,(entry.yStart+entry.yEnd)*.5f,0);
                    foreach(var particle in model.GetComponentsInChildren<ParticleSystem>(true))
                    { result.particles++;if(particle.main.playOnAwake)particle.Simulate(1,false,true); }
                    var effects=model.AddComponent<DynamicCardEffects>();effects.Initialize(entry);effects.Tick(entry.introDuration+1);
                    camera.transform.position=new Vector3(0,0,entry.cameraDistance);camera.fieldOfView=entry.fieldOfView;
                    camera.nearClipPlane=entry.nearClip;camera.farClipPlane=entry.farClip;
                    camera.Render();RenderTexture.active=rt;
                    var image=new Texture2D(384,384,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,384,384),0,0);image.Apply();
                    if(image.GetPixels32().Count(c=>c.a>50 && c.r+c.g+c.b>20)<1000)result.issues.Add(entry.id+": nearly empty render");
                    File.WriteAllBytes(output+"/"+entry.id+".png",image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=null;
                    result.cards++;
                }
                catch(Exception e) { result.issues.Add(entry.id+": "+e.Message); }
                finally { if(model!=null)Object.DestroyImmediate(model); }
                if(result.cards%25==0)Debug.Log("DYNAMIC_AUDIT_PROGRESS "+result.cards);
            }
            camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
            File.WriteAllText(output+"/audit.json",JsonUtility.ToJson(result,true));
            Debug.Log("DYNAMIC_AUDIT_DONE cards="+result.cards+" particles="+result.particles+" issues="+result.issues.Count);
        }
        public static void Bundle()
        {
            string path=DynamicCardBuild.BuildBundle(BuildTarget.StandaloneWindows64);
            var bundle=AssetBundle.LoadFromFile(path);if(bundle==null)throw new Exception("Bundle failed to open");
            int count=bundle.GetAllAssetNames().Count(p=>p.EndsWith("/card.prefab"));
            if(count!=255)throw new Exception("Bundle card count: "+count);
            bundle.Unload(true);Debug.Log("DYNAMIC_FULL_BUNDLE_PASS cards="+count+" bytes="+new FileInfo(path).Length);
        }
    }
}
