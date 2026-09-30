using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Assets.Script.DynamicCards;

namespace PremiumReferenceStudy
{
    public class PerformanceReferences : MonoBehaviour
    {
        public Sprite[] art;
        public bool finished;
        readonly string[] ids = { "202105", "202194", "c10001000" };
        readonly string[] labels = { "Aeschna / 15010100", "Syanna / 13680101", "Water scene / 56180101" };
        DynamicCardView[] views = new DynamicCardView[3];
        DynamicCardQuality previousQuality;
#if UNITY_EDITOR
        bool previousSource;
#endif
        string output;
        void Awake()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../../work/LadyLakePerformanceStudy"));
            Directory.CreateDirectory(output);
            previousQuality = DynamicCardSettings.Quality;
#if UNITY_EDITOR
            previousSource = DynamicCardLibrary.AllowEditorSourceLoading;
            DynamicCardLibrary.AllowEditorSourceLoading = true;
#endif
            DynamicCardSettings.Quality = DynamicCardQuality.High;
            var canvasGo = new GameObject("Original premium performances", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900);
            scaler.matchWidthOrHeight = .5f;
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform,false);
            var rect = (RectTransform)bg.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(.02f,.025f,.035f);
            for (int i=0;i<3;i++)
            {
                var card = new GameObject(labels[i],typeof(RectTransform),typeof(Image));
                card.transform.SetParent(canvasGo.transform,false);
                rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
                rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(-715+i*500,315);
                rect.sizeDelta = Vector2.one*1024*.88f;
                var img = card.GetComponent<Image>(); img.sprite = art[i];
                DynamicCardView.Bind(img,ids[i],false,true,null,false,false,null,null,true);
                views[i] = card.GetComponent<DynamicCardView>();
                var title = new GameObject("Title",typeof(RectTransform),typeof(Text));
                title.transform.SetParent(canvasGo.transform,false);
                rect = (RectTransform)title.transform; rect.anchoredPosition = new Vector2(-495+i*500,360); rect.sizeDelta = new Vector2(480,40);
                var text = title.GetComponent<Text>();text.text=labels[i];text.font=Resources.GetBuiltinResource<Font>("Arial.ttf");text.fontSize=22;text.alignment=TextAnchor.MiddleCenter;
            }
        }
        IEnumerator Start()
        {
            var field = typeof(DynamicCardView).GetField("model",BindingFlags.NonPublic|BindingFlags.Instance);
            float timeout = Time.realtimeSinceStartup+45;
            while (Array.Exists(views,v=>v==null||field.GetValue(v)==null))
            {
                if(Time.realtimeSinceStartup>timeout){File.WriteAllText(Path.Combine(output,"capture-error.txt"),"Original model load timeout");yield break;}
                yield return null;
            }
            float started=Time.realtimeSinceStartup;
            for(int frame=0;frame<19;frame++)
            {
                while(Time.realtimeSinceStartup-started<frame)yield return null;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"frame-"+frame.ToString("00")+".png"));
            }
            yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(output,"complete.txt"),DateTime.UtcNow.ToString("o")+"; 19 samples, one-second intervals, original DynamicCardView");finished=true;
        }
        void OnDestroy()
        {
            DynamicCardSettings.Quality=previousQuality;
#if UNITY_EDITOR
            DynamicCardLibrary.AllowEditorSourceLoading=previousSource;
#endif
        }
    }
}
