using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Assets.Script.DynamicCards.Editor;

[InitializeOnLoad]
public static class CompleteMotionPlayerBuildHook
{
    const string Work = "C:/UnityProjects/LegacyGwent/work/DynamicCards/CompleteMotionGoal-20260911/";
    const string Output = "C:/UnityProjects/LegacyGwent/Builds/Windows-20260911/DiyGwent.exe";
    static CompleteMotionPlayerBuildHook() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Work + "build-player.request")) return;
        File.Delete(Work + "build-player.request");
        string previous = Environment.GetEnvironmentVariable("LEGACY_GWENT_DYNAMIC_CARDS");
        File.WriteAllText(Work + "main-player-build.txt", "RUNNING " + DateTime.UtcNow.ToString("O"));
        try
        {
            Environment.SetEnvironmentVariable("LEGACY_GWENT_DYNAMIC_CARDS", "1");
            // OnPreprocessBuild verifies the freshly prepared content hash and stages the payload.
            // The bundle build and delivery verification are completed before this request is queued.
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path)).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64, locationPathName = Output, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Player build " + report.summary.result + ": " + report.summary.totalErrors + " errors");
            File.WriteAllText(Work + "main-player-build.txt", "COMPLETE " + DateTime.UtcNow.ToString("O") + "\n" + Output + "\nbytes=" + report.summary.totalSize);
        }
        catch (Exception e)
        {
            File.WriteAllText(Work + "main-player-build.txt", "FAILED " + DateTime.UtcNow.ToString("O") + "\n" + e);
            Debug.LogException(e);
        }
        finally
        {
            DynamicCardBuild.RestoreStage();
            Environment.SetEnvironmentVariable("LEGACY_GWENT_DYNAMIC_CARDS", previous);
        }
    }
}
