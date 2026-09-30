using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegacyGwent.LadyLakePremium.Editor
{
    /// <summary>
    /// Opt-in editor file bridge for the LadyLakePremium integration scene.
    ///
    /// The bridge does NOTHING unless <c>Integration/editor-request.txt</c> contains a command line.
    /// It never rebuilds and never enters Play Mode on its own. Root drives it by writing one
    /// command per line:
    ///     build | open | play | stop | capture [path] | validate | sample | status | refresh
    ///
    /// Results:
    ///     Integration/editor-result.txt  (last command outcome)
    ///     Integration/editor-state.json  (playing/scene/utc, written after each command)
    ///     Integration/stage-sample.json  (runtime DynamicCardView snapshot, written by 'sample')
    ///     Integration/capture.png        (default capture target)
    ///
    /// The file is ignored while its first non-space character is '#', so the shipped placeholder
    /// never triggers work.
    /// </summary>
    [InitializeOnLoad]
    public static class LadyLakePremiumEditorBridge
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath,
            "Experiments/LadyLakePremium/Integration"));
        private static readonly string RequestPath = Path.Combine(Root, "editor-request.txt");
        private static readonly string ResultPath = Path.Combine(Root, "editor-result.txt");
        private static readonly string StatePath = Path.Combine(Root, "editor-state.json");
        private static readonly string SamplePath = Path.Combine(Root, "stage-sample.json");
        private static double nextPoll;

        static LadyLakePremiumEditorBridge()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(RequestPath)) return;
            string command;
            try { command = File.ReadAllText(RequestPath).Trim(); }
            catch (IOException) { return; }
            if (command.Length == 0 || command[0] == '#') return;
            try { File.Delete(RequestPath); }
            catch (IOException) { return; }
            try
            {
                Execute(command);
                File.WriteAllText(ResultPath, command + " OK " + DateTime.UtcNow.ToString("o"), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                File.WriteAllText(ResultPath, exception.ToString(), new UTF8Encoding(false));
                Debug.LogException(exception);
            }
            finally
            {
                WriteState();
            }
        }

        private static void Execute(string command)
        {
            var parts = command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0].ToLowerInvariant())
            {
                case "build":
                    RequireEditMode("build");
                    LadyLakePremiumBuilder.Build();
                    break;
                case "open":
                    RequireEditMode("open");
                    LadyLakePremiumBuilder.Open();
                    break;
                case "play":
                    if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
                    break;
                case "stop":
                    if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                    break;
                case "capture":
                    Capture(parts.Length > 1 ? parts[1] : Path.Combine(Root, "capture.png"));
                    break;
                case "validate":
                    RequireEditMode("validate");
                    LadyLakePremiumBuilder.ValidateAndWrite();
                    break;
                case "sample":
                    WriteSample();
                    break;
                case "status":
                    break;
                case "refresh":
                    AssetDatabase.Refresh();
                    break;
                default:
                    throw new InvalidOperationException("Unsupported command '" + command +
                        "'. Use build|open|play|stop|capture [path]|validate|sample|status|refresh.");
            }
        }

        private static void RequireEditMode(string command)
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("'" + command + "' requires Edit Mode. Send 'stop' first.");
        }

        private static void Capture(string path)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("'capture' requires Play Mode. Send 'play' first.");
            path = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(path);
        }

        private static void WriteSample()
        {
            var stage = UnityEngine.Object.FindObjectOfType<LadyLakePremiumStage>();
            File.WriteAllText(SamplePath, stage != null ? stage.Describe() : "{\"stage\":null}", new UTF8Encoding(false));
        }

        private static void WriteState()
        {
            var scene = SceneManager.GetActiveScene();
            File.WriteAllText(StatePath,
                "{\"playing\":" + (EditorApplication.isPlaying ? "true" : "false") +
                ",\"compiling\":" + (EditorApplication.isCompiling ? "true" : "false") +
                ",\"updating\":" + (EditorApplication.isUpdating ? "true" : "false") +
                ",\"scene\":\"" + scene.path.Replace("\\", "/") + "\"" +
                ",\"utc\":\"" + DateTime.UtcNow.ToString("o") + "\"}", new UTF8Encoding(false));
        }
    }
}
