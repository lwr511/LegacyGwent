#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Assets.Script.DynamicCards;
public class AllMappedMotionAudit : MonoBehaviour
{
    [Serializable] class Input {public string[] ids;}
    [Serializable] class Row {public string art,scene,status;public long introDelta,loopDelta;}
    [Serializable] class Report {public bool complete,bundles;public List<Row> rows=new List<Row>();}
    Report report=new Report();
    const string Output="../AnimationRegression/all-mapped-motion.json";
    static object Field(object o,string n){return o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
    public static void Run(){File.WriteAllText("Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready","Verified motion-audit build");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("All mapped motion audit").AddComponent<AllMappedMotionAudit>();EditorApplication.isPlaying=true;}
    void Awake(){Application.logMessageReceived+=(m,t,k)=>{if(k==LogType.Exception){File.WriteAllText("../AnimationRegression/all-mapped-error.txt",m+"\n"+t);EditorApplication.Exit(1);}};}
    static Texture2D Capture(DynamicCardView view)
    {var rt=(RenderTexture)Field(view,"texture");var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();RenderTexture.active=old;return t;}
    static long Delta(Color32[] a,Color32[] b){long d=0;for(int i=0;i<a.Length;i++)d+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)+Math.Abs(a[i].a-b[i].a);return d;}
    static void Advance(DynamicCardView view,float seconds)
    {
        var model=(GameObject)Field(view,"model");var animators=model.GetComponentsInChildren<Animator>(true);
        for(float left=seconds;left>.0001f;left-=.1f)foreach(var a in animators)if(a.isActiveAndEnabled && a.runtimeAnimatorController!=null)a.Update(Mathf.Min(.1f,left));
        foreach(var p in model.GetComponentsInChildren<ParticleSystem>(true))if(p.isPlaying){p.Simulate(seconds,false,false,true);p.Play(false);}
        typeof(DynamicCardView).GetField("age",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,(float)Field(view,"age")+seconds);
    }
    IEnumerator Start()
    {
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;DynamicCardSettings.Enabled=true;
        var canvas=new GameObject("Canvas",typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var image=new GameObject("Actual art binding",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();image.transform.SetParent(canvas.transform,false);image.rectTransform.sizeDelta=new Vector2(280,400);
        var ids=JsonUtility.FromJson<Input>(File.ReadAllText("../AnimationRegression/all-game-art-ids.json")).ids;
        var catalog=JsonUtility.FromJson<DynamicCardCatalog>(File.ReadAllText(DynamicCardLibrary.CatalogAsset));var entries=catalog.cards.SelectMany(c=>c.artIds.Select(a=>new {art=a,entry=c})).ToDictionary(x=>x.art,x=>x.entry);
        foreach(var id in ids)
        {
            if(!entries.ContainsKey(id)){report.rows.Add(new Row{art=id,status="unmapped"});continue;}
            image.gameObject.SetActive(true);DynamicCardView.Bind(image,id,false,true,playPreviewAudio:false);var view=image.GetComponent<DynamicCardView>();float start=Time.realtimeSinceStartup;
            while(Field(view,"surface")==null || !((RawImage)Field(view,"surface")).enabled){if(Time.realtimeSinceStartup-start>45)throw new Exception("First frame timeout: "+id);yield return null;}
            var entry=(DynamicCardEntry)Field(view,"entry");var row=new Row{art=id,scene=entry.id};
            var texture=Capture(view);var before=texture.GetPixels32();Destroy(texture);Advance(view,.731f);yield return null;yield return null;
            texture=Capture(view);row.introDelta=Delta(before,texture.GetPixels32());Destroy(texture);
            float intro=entry.introDuration; if(intro>30){var actor=((GameObject)Field(view,"model")).GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.name=="model" && a.runtimeAnimatorController!=null);var clip=actor==null?null:actor.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.EndsWith("_Intro"));if(clip!=null)intro=clip.length;} float jump=intro+.5f-(float)Field(view,"age");if(jump>0)Advance(view,jump);yield return null;yield return null;
            texture=Capture(view);before=texture.GetPixels32();Destroy(texture);Advance(view,.913f);yield return null;yield return null;
            texture=Capture(view);row.loopDelta=Delta(before,texture.GetPixels32());Destroy(texture);
            if(row.loopDelta==0){yield return new WaitForSecondsRealtime(1.2f);texture=Capture(view);row.loopDelta=Delta(before,texture.GetPixels32());Destroy(texture);}
            row.status=row.loopDelta>0?"moving":"needs-review";report.rows.Add(row);
            if(row.status!="moving"){texture=Capture(view);File.WriteAllBytes("../AnimationRegression/frozen-"+id+".png",texture.EncodeToPNG());Destroy(texture);Debug.Log("MOTION_AUDIT_REVIEW "+id);}
            if(report.rows.Count%16==0)
            {
                image.gameObject.SetActive(false);yield return null;yield return null;
                var lib=DynamicCardLibrary.Instance;yield return lib.StartCoroutine((IEnumerator)typeof(DynamicCardLibrary).GetMethod("CollectUnused",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(lib,null));
                File.WriteAllText(Output,JsonUtility.ToJson(report,true));Debug.Log("MOTION_AUDIT_PROGRESS "+report.rows.Count+"/"+ids.Length);GC.Collect();
            }
        }
        report.bundles=Field(DynamicCardLibrary.Instance,"bundle")!=null;report.complete=true;File.WriteAllText(Output,JsonUtility.ToJson(report,true));Debug.Log("MOTION_AUDIT_COMPLETE "+report.rows.Count);EditorApplication.Exit(0);
    }
}
#endif
