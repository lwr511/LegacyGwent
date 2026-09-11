using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Assets.Script.DynamicCards;
using Assets.Script.DynamicCards.Editor;

[InitializeOnLoad]
public static class TrissVillenBuildRunner
{
    const string Work = "C:/UnityProjects/LegacyGwent/work/DynamicCards/TrissVillenFire-20260911/";
    static TrissVillenBuildRunner() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Work + "build-request.txt") || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
        File.Move(Work + "build-request.txt", Work + "build-started.txt");
        try
        {
            Application.logMessageReceived += Log;
            var bundle = DynamicCardBuild.BuildBundle(BuildTarget.StandaloneWindows64);
            File.WriteAllText(Work + "build-result.txt", "PASS " + DateTime.UtcNow.ToString("O") + "\n" + bundle);
        }
        catch (Exception exception)
        {
            File.WriteAllText(Work + "build-result.txt", "FAIL\n" + exception);
            Debug.LogException(exception);
        }
        finally { Application.logMessageReceived -= Log; }
    }
    static void Log(string text, string trace, LogType type)
    {
        if (text.StartsWith("DYNAMIC_") || type == LogType.Error || type == LogType.Exception)
            File.AppendAllText(Work + "build-progress.txt", DateTime.UtcNow.ToString("O") + " " + text + "\n");
    }
}
