using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Cynthia.Card;
using Assets.Script.DynamicCards;

[InitializeOnLoad]
public static class CompleteMotionGameUiAudit
{
    public const string Work = "C:/UnityProjects/LegacyGwent/work/DynamicCards/CompleteMotionGoal-20260911/";
    static CompleteMotionGameUiAudit() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists(Work + "request.json") || UnityEngine.Object.FindObjectOfType<CompleteMotionGameUiRecorder>() != null) return;
        var editor = UnityEngine.Object.FindObjectOfType<EditorInfo>();
        if (editor == null || editor.EditorStatus != EditorStatus.ShowCards) return;
        var request = JsonUtility.FromJson<CompleteMotionGameUiRecorder.Request>(File.ReadAllText(Work + "request.json"));
        File.Delete(Work + "request.json");
        var recorder = new GameObject("Actual game UI motion audit").AddComponent<CompleteMotionGameUiRecorder>();
        recorder.Begin(editor, request);
    }
}

public class CompleteMotionGameUiRecorder : MonoBehaviour
{
    [Serializable] public class Request { public string run; public string[] cards; public float[] waits; public bool screenshots = true; public bool reopen; }
    [Serializable] class Skin { public string path; public int vertices; public float maxDelta; }
    [Serializable] class Row
    {
        public string card, art, uiPath, prefab, bundleRoot, hash, screen;
        public int sample, frame, pixelChanges;
        public bool enabled, visible, surfaceEnabled, modelActive;
        public float age, timeScale;
        public string[] animators;
        public string[] sourceBehaviors, unsupportedShaders;
        public Skin[] skins;
    }
    [Serializable] class CardResult { public string card, art, status; public List<Row> rows = new List<Row>(); }
    [Serializable] class Report { public bool complete; public string startedUtc, finishedUtc; public List<CardResult> cards = new List<CardResult>(); }
    [Serializable] class MapRow { public string card, art, name; public bool mapped; }
    [Serializable] class MapReport { public List<MapRow> cards = new List<MapRow>(); }
    readonly Dictionary<int, Vector3[]> firstVertices = new Dictionary<int, Vector3[]>();
    Report report = new Report();
    Color32[] priorPixels;
    string output;
    static object Field(object obj, string name) { return obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj); }
    static string PathOf(Transform t) { return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name; }
    public void Begin(EditorInfo editor, Request request)
    {
        output = CompleteMotionGameUiAudit.Work + request.run + "/";
        Directory.CreateDirectory(output);
        report.startedUtc = DateTime.UtcNow.ToString("O");
        Application.logMessageReceived += Log;
        StartCoroutine(Run(editor, request));
    }
    void Log(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
            File.AppendAllText(output + "errors.txt", DateTime.UtcNow.ToString("O") + " " + message + "\n" + trace + "\n");
    }
    void OnDestroy() { Application.logMessageReceived -= Log; }
    void Save() { File.WriteAllText(output + "results.json", JsonUtility.ToJson(report, true)); }
    IEnumerator Run(EditorInfo editor, Request request)
    {
        var catalog = JsonUtility.FromJson<DynamicCardCatalog>(File.ReadAllText("Assets/DynamicCards/Content/catalog.json"));
        var mapped = new HashSet<string>(catalog.cards.SelectMany(c => c.artIds));
        var map = new MapReport();
        foreach (var pair in GwentMap.CardMap)
            map.cards.Add(new MapRow { card = pair.Key, art = pair.Value.CardArtsId, name = pair.Value.Name, mapped = mapped.Contains(pair.Value.CardArtsId) });
        File.WriteAllText(output + "game-card-map.json", JsonUtility.ToJson(map, true));
        string[] cards = request.cards != null && request.cards.Length > 0 ? request.cards :
            map.cards.GroupBy(c => c.art).Select(g => g.First().card).OrderBy(c => c).ToArray();
        var waits = request.waits ?? new[] { .1f, 1f, 3f, 6f };
        foreach (var id in cards)
        {
            if (File.Exists(CompleteMotionGameUiAudit.Work + "stop.txt")) break;
            var core = new CardStatus(id);
            var result = new CardResult { card = id, art = core.CardArtsId, status = "loading" };
            report.cards.Add(result); priorPixels = null; firstVertices.Clear();
            // The actual production hover path and the existing on-screen ArtCard are used.
            // Do not set DynamicCardSettings, construct an alternative Canvas, or call Camera.Render.
            editor.SelectSwitchUICard(core);
            float started = Time.realtimeSinceStartup;
            DynamicCardView view = null;
            while (true)
            {
                view = editor.ShowArtCard.CardImg.GetComponent<DynamicCardView>();
                var raw = view == null ? null : Field(view, "surface") as RawImage;
                if (raw != null && raw.enabled && Field(view, "model") != null) break;
                if (!mapped.Contains(core.CardArtsId)) { result.status = "no-source-mapping"; break; }
                if (!DynamicCardSettings.Enabled) { result.status = "dynamic-disabled"; break; }
                if (Time.realtimeSinceStartup - started > 35) { result.status = "ui-load-timeout"; break; }
                yield return null;
            }
            if (result.status == "loading")
            {
                result.status = "sampled";
                for (int i = 0; i < waits.Length; i++)
                {
                    yield return new WaitForSecondsRealtime(waits[i]);
                    yield return new WaitForEndOfFrame();
                    Sample(view, result, i, request.screenshots && (request.reopen || i == 0 || i == waits.Length - 1));
                }
                if (request.reopen)
                {
                    editor.ShowArtCard.gameObject.SetActive(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    editor.SelectSwitchUICard(core);
                    yield return new WaitForSecondsRealtime(2f);
                    yield return new WaitForEndOfFrame();
                    Sample(editor.ShowArtCard.CardImg.GetComponent<DynamicCardView>(), result, waits.Length, request.screenshots);
                }
                if (result.rows.Skip(1).Take(waits.Length - 1).All(r => r.pixelChanges == 0)) result.status = "surface-not-changing";
            }
            Save();
            File.WriteAllText(output + "progress.txt", report.cards.Count + "/" + cards.Length + " " + id + " " + result.status + " " + DateTime.UtcNow.ToString("O"));
        }
        report.complete = report.cards.Count == cards.Length;
        report.finishedUtc = DateTime.UtcNow.ToString("O"); Save();
        File.WriteAllText(output + "finished.txt", report.complete ? "COMPLETE" : "STOPPED");
        Destroy(gameObject);
    }
    void Sample(DynamicCardView view, CardResult result, int sample, bool screenshot)
    {
        if (view == null) throw new Exception("Actual ArtCard lost DynamicCardView");
        var raw = Field(view, "surface") as RawImage;
        var model = Field(view, "model") as GameObject;
        var entry = Field(view, "entry") as DynamicCardEntry;
        var row = new Row { card = result.card, art = result.art, sample = sample, frame = Time.frameCount,
            enabled = DynamicCardSettings.Enabled, timeScale = Time.timeScale, age = (float)Field(view, "age"),
            uiPath = PathOf(view.transform), surfaceEnabled = raw != null && raw.enabled,
            modelActive = model != null && model.activeInHierarchy, prefab = entry == null ? "" : entry.prefab,
            bundleRoot = (string)Field(DynamicCardLibrary.Instance, "bundleRoot"),
            visible = (bool)typeof(DynamicCardView).GetMethod("IsVisible", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null) };
        if (raw != null && raw.texture is RenderTexture)
        {
            var rt = (RenderTexture)raw.texture;
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply(); RenderTexture.active = previous;
            var pixels = image.GetPixels32();
            if (priorPixels != null && pixels.Length == priorPixels.Length)
                for (int i = 0; i < pixels.Length; i++)
                    if (Math.Abs(pixels[i].r-priorPixels[i].r)>2 || Math.Abs(pixels[i].g-priorPixels[i].g)>2 || Math.Abs(pixels[i].b-priorPixels[i].b)>2 || Math.Abs(pixels[i].a-priorPixels[i].a)>2) row.pixelChanges++;
            using (var sha = SHA256.Create()) row.hash = Convert.ToBase64String(sha.ComputeHash(image.GetRawTextureData()));
            priorPixels = pixels; Destroy(image);
        }
        var skins = new List<Skin>();
        if (model != null)
        {
            row.sourceBehaviors = model.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(c => c != null && (c.GetType().FullName.Contains(".Source") || c is DynamicCardPostEffect))
                .Select(c => PathOf(c.transform) + ":" + c.GetType().FullName + ":" + (c.enabled && c.gameObject.activeInHierarchy)).ToArray();
            row.unsupportedShaders = model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(m => m != null && (m.shader == null || !m.shader.isSupported))
                .Select(m => m.name + ":" + (m.shader == null ? "null" : m.shader.name)).Distinct().ToArray();
            row.animators = model.GetComponentsInChildren<Animator>(true).Select(a => PathOf(a.transform) + ":" + (a.runtimeAnimatorController == null ? "none" : string.Join(";", a.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name)) + "@" + a.GetCurrentAnimatorStateInfo(0).normalizedTime)).ToArray();
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh(); renderer.BakeMesh(mesh); var vertices = mesh.vertices;
                Vector3[] first; float delta = 0;
                if (!firstVertices.TryGetValue(renderer.GetInstanceID(), out first)) firstVertices[renderer.GetInstanceID()] = vertices;
                else if (first.Length == vertices.Length) for (int i=0;i<vertices.Length;i++) delta = Mathf.Max(delta,Vector3.Distance(first[i],vertices[i]));
                skins.Add(new Skin { path = PathOf(renderer.transform), vertices = vertices.Length, maxDelta = delta }); Destroy(mesh);
            }
        }
        row.skins = skins.ToArray();
        if (screenshot)
        {
            row.screen = result.card + "-" + sample + ".png";
            ScreenCapture.CaptureScreenshot(output + row.screen);
        }
        result.rows.Add(row); Save();
    }
}
