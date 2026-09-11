using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Script.DynamicCards;
using UnityEngine;
using UnityEngine.UI;

public sealed class QueueSmoke : MonoBehaviour
{
    private readonly List<Image> cards = new List<Image>();
    private bool original;
    private int errors;
    private IEnumerator Start()
    {
        Application.runInBackground = true;
        gameObject.AddComponent<AudioListener>();
        original = DynamicCardSettings.Enabled;
        Application.logMessageReceived += Log;
        DynamicCardSettings.Enabled = false;
        var canvas = new GameObject("Queue test", typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(canvas.transform, false); viewport.sizeDelta = new Vector2(400,400);
        for (int i=0;i<60;i++)
        {
            var art = new GameObject("Card "+i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            art.transform.SetParent(viewport,false); art.rectTransform.sizeDelta = new Vector2(70,100);
            art.rectTransform.anchoredPosition = i<8 ? new Vector2((i%4-1.5f)*80,(i/4-.5f)*110) : new Vector2(0,1000+i*100);
            DynamicCardView.Bind(art,"202887");cards.Add(art);
        }
        DynamicCardSettings.Enabled = true;
        // The last visible request is the preview and must get priority over older list requests.
        DynamicCardView.Bind(cards[7],"202887",false,true);
        cards[6].gameObject.SetActive(false);
        yield return null;
        if (Count()!=0) { Fail("page did not start static"); yield break; }
        float previewStart=Time.realtimeSinceStartup;
        while(!Ready(cards[7]) && Time.realtimeSinceStartup-previewStart<45)yield return null;
        if(!Ready(cards[7]) || Count()!=1){Fail("preview did not load first");yield break;}
        yield return new WaitForSecondsRealtime(1);
        if(Count()!=1){Fail("background loads continued during preview");yield break;}
        Debug.Log("QUEUE_PREVIEW_BACKGROUND_PAUSED");
        cards[7].gameObject.SetActive(false);yield return null;
        int previous=0;float last=0, start=Time.realtimeSinceStartup;
        var order=new[]{4,5,0,1,2,3};
        while (Count()<6 && Time.realtimeSinceStartup-start<90)
        {
            int count=Count();
            if(count>previous)
            {
                if(count!=previous+1 || (previous>0 && Time.realtimeSinceStartup-last<.14f)) { Fail("loads were not serialized/spaced count="+count+" previous="+previous+" interval="+(Time.realtimeSinceStartup-last)+" frame="+Time.frameCount);yield break; }
                if(!Ready(cards[order[previous]])) {Fail("visual row/column order is wrong, expected "+order[previous]);yield break;}
                previous=count;last=Time.realtimeSinceStartup;
                Debug.Log("QUEUE_CARD_READY count="+count+" frame="+Time.frameCount);
            }
            yield return null;
        }
        if(Count()!=6) {Fail("visible cards did not finish");yield break;}
        cards[7].gameObject.SetActive(true);
        start=Time.realtimeSinceStartup;
        while(!Ready(cards[7]) && Time.realtimeSinceStartup-start<30)yield return null;
        if(!Ready(cards[7])){Fail("second preview did not load");yield break;}
        yield return null;yield return null;
        var modelField=typeof(DynamicCardView).GetField("model",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        foreach(int index in order)
        {
            var model=modelField.GetValue(cards[index].GetComponent<DynamicCardView>()) as GameObject;
            if(model==null || model.activeSelf){Fail("thumbnail animation kept running behind preview");yield break;}
        }
        Debug.Log("QUEUE_PREVIEW_THUMBNAILS_SUSPENDED");
        cards[7].gameObject.SetActive(false);yield return null;yield return null;
        for(int i=8;i<60;i++) if(Ready(cards[i])) {Fail("offscreen card loaded");yield break;}
        if(Ready(cards[6])) {Fail("cancelled card loaded");yield break;}
        // Scrolling an already queued card into the mask must resume it without rebinding.
        cards[8].rectTransform.anchoredPosition=Vector2.zero;
        start=Time.realtimeSinceStartup;
        while(!Ready(cards[8]) && Time.realtimeSinceStartup-start<30)yield return null;
        if(!Ready(cards[8])) {Fail("scroll did not resume load");yield break;}
        cards[8].rectTransform.anchoredPosition=new Vector2(0,2000);
        yield return new WaitForSecondsRealtime(2);
        if(Ready(cards[8])) {Fail("offscreen model was not released");yield break;}
        cards[8].rectTransform.anchoredPosition=Vector2.zero;
        start=Time.realtimeSinceStartup;
        while(!Ready(cards[8]) && Time.realtimeSinceStartup-start<30)yield return null;
        if(!Ready(cards[8])) {Fail("released card did not reload");yield break;}
        // Recycle a waiting view to an unmatched ID; the previous request must not return.
        DynamicCardView.Bind(cards[9],"202887");
        cards[9].rectTransform.anchoredPosition=Vector2.zero;
        DynamicCardView.Bind(cards[9],"no-such-card");
        yield return new WaitForSecondsRealtime(1);
        if(Ready(cards[9])) {Fail("stale binding returned");yield break;}
        DynamicCardSettings.Enabled=false;
        yield return null;yield return null;
        if(Count()!=0 || errors!=0) {Fail("disable did not clean up or runtime errors");yield break;}
        Debug.Log("DYNAMIC_QUEUE_PASS 60-requests static-first visual-order serial spaced preview-priority offscreen-release reload scroll cancellation rebind disable");
        Finish(0);
    }
    private bool Ready(Image art) { var raw=art.GetComponentInChildren<RawImage>();return raw!=null && raw.enabled; }
    private int Count() {int count=0;foreach(var card in cards)if(Ready(card))count++;return count;}
    private void Log(string message,string stack,LogType type) {if(type==LogType.Error || type==LogType.Exception)errors++;}
    private void Fail(string message) {Debug.LogError("DYNAMIC_QUEUE_FAIL "+message);Finish(1);}
    private void Finish(int code)
    {
        DynamicCardSettings.Enabled=original;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
}
