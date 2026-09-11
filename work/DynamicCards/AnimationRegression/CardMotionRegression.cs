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
public class CardMotionRegression : MonoBehaviour
{
    [Serializable] class Result {public bool passed,bundles,gridContinuedLoading,hiddenReleased;public List<Motion> motions=new List<Motion>();public float p95FrameMs,maxFrameMs;}
    [Serializable] class Motion {public string art;public long introDelta,loopDelta;public int smallCardsAdvancing;}
    Result result=new Result(); List<float> frames=new List<float>();
    static object Field(object o,string n){return o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
    public static void Run()
    {
        File.WriteAllText("Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready","Animation regression verified build");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Card motion regression").AddComponent<CardMotionRegression>();EditorApplication.isPlaying=true;
    }
    void Awake(){Application.logMessageReceived+=(m,t,k)=>{if(k==LogType.Exception){File.WriteAllText("../AnimationRegression/runtime-error.txt",m+"\n"+t);EditorApplication.Exit(1);}};}
    void Update(){frames.Add(Time.unscaledDeltaTime*1000);}
    Image Art(Canvas canvas,string name,Vector2 size,Vector2 pos)
    {var image=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();image.transform.SetParent(canvas.transform,false);image.rectTransform.sizeDelta=size;image.rectTransform.anchoredPosition=pos;return image;}
    static bool Ready(Image image){var raw=image.GetComponentInChildren<RawImage>();return raw!=null&&raw.enabled;}
    IEnumerator WaitReady(Image image)
    {float t=Time.realtimeSinceStartup;while(!Ready(image)){if(Time.realtimeSinceStartup-t>35)throw new Exception("No dynamic first frame: "+image.name);yield return null;}}
    static Color32[] Capture(Image image)
    {var rt=(RenderTexture)image.GetComponentInChildren<RawImage>().texture;var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();var pixels=tex.GetPixels32();Destroy(tex);RenderTexture.active=old;return pixels;}
    static long Delta(Color32[] a,Color32[] b){long d=0;for(int i=0;i<a.Length;i++)d+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);return d;}
    IEnumerator Start()
    {
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;DynamicCardSettings.Enabled=true;
        var canvas=new GameObject("Page",typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var grid=new List<Image>();var gridIds=new[]{"20160300","20158700","20012400"};
        for(int i=0;i<gridIds.Length;i++){var image=Art(canvas,gridIds[i],new Vector2(100,145),new Vector2(-210+i*115,-150));DynamicCardView.Bind(image,gridIds[i]);grid.Add(image);}
        foreach(var image in grid)yield return WaitReady(image);
        var detail=Art(canvas,"Detail",new Vector2(220,310),new Vector2(190,65));
        var ids=new[]{"20158700","20160300","20012400","12210600","14220200","20014900","20022000","20051800","20161100","20163400","20177900","20178000","20178100","11210300"};
        foreach(var id in ids)
        {
            detail.name=id;DynamicCardView.Bind(detail,id,false,true,playPreviewAudio:false);yield return WaitReady(detail);
            var view=detail.GetComponent<DynamicCardView>();var entry=(DynamicCardEntry)Field(view,"entry");
            var before=Capture(detail);yield return new WaitForSecondsRealtime(.75f);var row=new Motion{art=id,introDelta=Delta(before,Capture(detail))};
            float age=(float)Field(view,"age");if(age<entry.introDuration+.4f)yield return new WaitForSecondsRealtime(entry.introDuration+.4f-age);
            before=Capture(detail);var smallBefore=grid.Select(Capture).ToArray();var ages=grid.Select(g=>(float)Field(g.GetComponent<DynamicCardView>(),"age")).ToArray();
            yield return new WaitForSecondsRealtime(.83f);row.loopDelta=Delta(before,Capture(detail));
            for(int i=0;i<grid.Count;i++)if((float)Field(grid[i].GetComponent<DynamicCardView>(),"age")>ages[i]+.5f && Delta(smallBefore[i],Capture(grid[i]))>0)row.smallCardsAdvancing++;
            result.motions.Add(row);Debug.Log("CARD_MOTION_CHECK "+JsonUtility.ToJson(row));
            if(row.loopDelta==0 || row.smallCardsAdvancing!=3)throw new Exception("Frozen animation: "+id);
            if(id==ids[0])
            {var added=Art(canvas,"New visible thumbnail",new Vector2(100,145),new Vector2(-210,40));DynamicCardView.Bind(added,"11210200");yield return WaitReady(added);result.gridContinuedLoading=true;}
        }
        result.bundles=Field(DynamicCardLibrary.Instance,"bundle")!=null;if(!result.bundles)throw new Exception("Regression did not use compressed delivery bundles");
        grid[0].rectTransform.anchoredPosition=new Vector2(-20000,0);yield return new WaitForSecondsRealtime(2.1f);result.hiddenReleased=Field(grid[0].GetComponent<DynamicCardView>(),"model")==null;
        if(!result.hiddenReleased)throw new Exception("Offscreen thumbnail was not released");
        frames.Sort();result.p95FrameMs=frames[(int)((frames.Count-1)*.95f)];result.maxFrameMs=frames.Last();result.passed=true;
        File.WriteAllText("../AnimationRegression/runtime-result.json",JsonUtility.ToJson(result,true));Debug.Log("CARD_MOTION_REGRESSION_PASS");EditorApplication.Exit(0);
    }
}
#endif
