using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegacyGwent.LadyLakeLab.Acceptance
{
    // Independent, opt-in acceptance runner. Inactive unless a request file exists.
    [InitializeOnLoad]
    public static class LadyLakeAcceptance
    {
        private const string ScenePath = "Assets/Experiments/LadyLake/LadyLakeLab.unity";
        private const string ActiveKey = "LadyLake.Acceptance.Active";
        private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../../work/LadyLakeLab"));
        private static double nextPoll;
        private static int errors;
        static LadyLakeAcceptance()
        {
            EditorApplication.update += Poll;
            Application.logMessageReceived += OnLog;
        }
        private static void OnLog(string condition, string trace, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            File.AppendAllText(Path.Combine(Root, "play-errors.log"), condition + "\n" + trace + "\n");
        }
        public static void StartVerification()
        {
            Directory.CreateDirectory(Root);
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetInt("LadyLake.Acceptance.Frame", 0);
            SessionState.SetBool(ActiveKey, true);
            File.WriteAllText(Path.Combine(Root, "play-errors.log"), "");
            EditorApplication.isPlaying = true;
        }
        public static void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var gameView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Show();
        }
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + (SessionState.GetBool(ActiveKey, false) ? .1 : 1.0);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string request = Path.Combine(Root, "editor-request.txt");
            if (SceneManager.GetActiveScene().path != ScenePath && !File.Exists(request) && !SessionState.GetBool(ActiveKey, false)) return;
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "editor-state.json"),
                "{\"playing\":" + (EditorApplication.isPlaying ? "true" : "false") +
                ",\"scene\":\"" + SceneManager.GetActiveScene().path + "\",\"utc\":\"" + DateTime.UtcNow.ToString("o") + "\"}");
            if (File.Exists(request))
            {
                var cmd = File.ReadAllText(request).Trim(); File.Delete(request);
                try
                {
                    if (cmd == "verify") StartVerification();
                    else if (cmd == "review-card")
                    {
                        SessionState.SetBool("LadyLake.Acceptance.CardReview", true);
                        OpenScene();
                        EditorApplication.isPlaying = true;
                    }
                    else if (cmd == "open") OpenScene();
                    else if (cmd == "stop") EditorApplication.isPlaying = false;
                    else if (cmd == "play") EditorApplication.isPlaying = true;
                    else if (cmd == "refresh") AssetDatabase.Refresh();
                    else if (cmd == "snapshot")
                    {
                        if (!EditorApplication.isPlaying) throw new Exception("snapshot requires Play Mode");
                        ScreenCapture.CaptureScreenshot(Path.Combine(Root, "game-view.png"));
                    }
                    else if (cmd == "quit") EditorApplication.Exit(0);
                    else if (cmd == "build" || cmd == "capture")
                    {
                        var builder = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("LegacyGwent.LadyLakeLab.Editor.LadyLakeLabBuilder")).FirstOrDefault(t => t != null);
                        if (builder == null) throw new Exception("Builder missing");
                        builder.GetMethod(cmd == "build" ? "Build" : "BuildAndCapture").Invoke(null, null);
                    }
                    else throw new Exception("Unknown request " + cmd);
                    File.WriteAllText(Path.Combine(Root, "editor-result.txt"), cmd + " OK " + DateTime.UtcNow.ToString("o"));
                }
                catch (Exception ex) { File.WriteAllText(Path.Combine(Root, "editor-result.txt"), ex.ToString()); Debug.LogException(ex); }
            }
            if (SessionState.GetBool("LadyLake.Acceptance.CardReview", false) && EditorApplication.isPlaying && !EditorApplication.isPaused)
            {
                var probe = UnityEngine.Object.FindObjectOfType<LadyLakePresentationProbe>();
                if (probe == null) new GameObject("__OptInPresentationAcceptance").AddComponent<LadyLakePresentationProbe>();
                else if (probe.Finished)
                {
                    SessionState.SetBool("LadyLake.Acceptance.CardReview", false);
                    EditorApplication.isPlaying = false;
                }
            }
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            int index = SessionState.GetInt("LadyLake.Acceptance.Frame", 0);
            float seconds = Time.timeSinceLevelLoad;
            if (seconds < 1 + index * 2) return;
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindObjectOfType<Camera>();
            if (cam == null) { Debug.LogError("No live scene camera"); return; }
            string outDir = Path.Combine(Root, "play-captures"); Directory.CreateDirectory(outDir);
            Capture(cam, Path.Combine(outDir, "play-" + index.ToString("D2") + ".png"), 700, 1000);
            var transforms = UnityEngine.Object.FindObjectsOfType<Transform>();
            File.AppendAllText(Path.Combine(Root, "play-samples.tsv"),
                index + "\t" + seconds.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "\t" + Time.frameCount + "\t" + transforms.Length + "\n");
            SessionState.SetInt("LadyLake.Acceptance.Frame", index + 1);
            if (index >= 13)
            {
                SessionState.SetBool(ActiveKey, false);
                File.WriteAllText(Path.Combine(Root, "play-result.json"), "{\"framesCaptured\":14,\"secondsObserved\":" + seconds.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + ",\"errors\":" + errors + ",\"scene\":\"" + ScenePath + "\"}");
                EditorApplication.isPlaying = false;
            }
        }
        public static void Capture(Camera cam, string path, int width, int height)
        {
            var priorTarget = cam.targetTexture; var priorActive = RenderTexture.active;
            float priorAspect = cam.aspect; Rect priorRect = cam.rect;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt; cam.aspect = (float)width / height; cam.rect = new Rect(0, 0, 1, 1);
                cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = priorTarget; cam.aspect = priorAspect; cam.rect = priorRect;
                RenderTexture.active = priorActive; UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
