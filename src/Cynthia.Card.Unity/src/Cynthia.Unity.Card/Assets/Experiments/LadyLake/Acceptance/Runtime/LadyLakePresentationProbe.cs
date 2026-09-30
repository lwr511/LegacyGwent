using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LegacyGwent.LadyLakeLab.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LegacyGwent.LadyLakeLab.Acceptance
{
    // Added only by the opt-in editor acceptance command, never saved into the scene.
    public sealed class LadyLakePresentationProbe : MonoBehaviour
    {
        public bool Finished { get; private set; }
        private readonly List<string> failures = new List<string>();
        private readonly List<string> evidence = new List<string>();
        private LadyLakeCardStage stage;
        private string folder;
        private int errors;
        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }
        private void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { errors++; failures.Add(message); }
        }
        private IEnumerator Start()
        {
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../../work/LadyLakeLab/review-v3"));
            Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(3);
            stage = FindObjectOfType<LadyLakeCardStage>();
            if (stage == null || !stage.IsReady) { failures.Add("Presentation not ready"); Finish(); yield break; }
            var card = stage.CollectionCard;
            Check(card.CardBorder.sprite == card.GoldBorder, "Collection gold frame reference");
            Check(card.FactionIcon.sprite == card.NeutralGoldIcon, "Collection neutral badge reference");
            Check(stage.Surface != null && stage.Surface.texture == stage.RenderTarget, "Rendered artwork is bound to visible card surface");
            Check(FindObjectOfType<EventSystem>() != null, "EventSystem active");
            var first = stage.Current;
            yield return Shot("01-idle");
            yield return new WaitForSecondsRealtime(3);
            Check(Vector2.Distance(first, stage.Current) > .03f, "Automatic movement changes normalized angle");
            yield return Shot("02-summon");
            yield return new WaitForSecondsRealtime(3);
            yield return Shot("03-held");
            yield return new WaitForSecondsRealtime(3);
            var border = card.CardBorder.rectTransform;
            var canvas = border.GetComponentInParent<Canvas>();
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, border.TransformPoint(border.rect.center));
            var data = new PointerEventData(EventSystem.current) { position = origin, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            GameObject hit = hits.Count > 0 ? hits[0].gameObject : null;
            Check(hit != null, "Raycast hits card center");
            if (hit != null)
            {
                data.pointerPressRaycast = hits[0];
                ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.beginDragHandler);
                Check(stage.IsDragging, "PointerDown and BeginDrag reach actual card handle");
                data.position = origin + new Vector2(300, 100) * Screen.height / 900f;
                ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.dragHandler);
                yield return new WaitForSecondsRealtime(.6f);
                Check(stage.Current.x < -.65f && stage.Current.y > .30f, "Drag right reaches production angle limits");
                yield return Shot("04-drag-right");
                data.position = origin + new Vector2(-300, -100) * Screen.height / 900f;
                ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.dragHandler);
                yield return new WaitForSecondsRealtime(.6f);
                Check(stage.Current.x > .65f && stage.Current.y < -.30f, "Drag left reaches opposite angle limits");
                yield return Shot("05-drag-left");
                ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.endDragHandler);
                yield return new WaitForSecondsRealtime(.8f);
                Check(!stage.IsDragging && stage.Current.magnitude < .01f, "Release recenters before idle resumes");
                yield return Shot("06-recentered");
                yield return new WaitForSecondsRealtime(3);
                Check(stage.Current.magnitude > .04f, "Automatic movement resumes after release delay");
            }
            while (Time.timeSinceLevelLoad < 28) yield return null;
            yield return Shot("07-second-cycle");
            Finish();
        }
        private void Check(bool value, string message)
        { (value ? evidence : failures).Add(message); }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            evidence.Add(name + " time=" + Time.timeSinceLevelLoad.ToString("F3") + " frame=" + Time.frameCount + " angle=" + stage.Current);
            yield return null;
        }
        private void Finish()
        {
            var result = new Result { runtimeErrors = errors, secondsObserved = Time.timeSinceLevelLoad,
                checks = evidence.ToArray(), failures = failures.ToArray(), passed = failures.Count == 0 };
            File.WriteAllText(Path.Combine(folder, "result.json"), JsonUtility.ToJson(result, true));
            Finished = true;
        }
        [Serializable] private class Result
        { public bool passed; public int runtimeErrors; public float secondsObserved; public string[] checks; public string[] failures; }
    }
}
