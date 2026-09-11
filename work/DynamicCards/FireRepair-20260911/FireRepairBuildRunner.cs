using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Assets.Script.DynamicCards.Editor;

[InitializeOnLoad]
public static class FireRepairBuildRunner
{
    const string Work = "C:/UnityProjects/LegacyGwent/work/DynamicCards/FireRepair-20260911/";
    static FireRepairBuildRunner() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Work + "build-request.txt") || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
        File.Move(Work + "build-request.txt", Work + "build-started.txt");
        try
        {
            var bundle = DynamicCardBuild.BuildBundle(BuildTarget.StandaloneWindows64);
            File.WriteAllText(Work + "build-result.txt", "PASS " + DateTime.UtcNow.ToString("O") + "\n" + bundle);
        }
        catch (Exception exception)
        {
            File.WriteAllText(Work + "build-result.txt", "FAIL\n" + exception);
            Debug.LogException(exception);
        }
    }
}
