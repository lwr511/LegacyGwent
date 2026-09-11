using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Assets.Script.DynamicCards;
using Assets.Script.DynamicCards.Editor;

[InitializeOnLoad]
public static class RaghRepairBuildRunner
{
    const string Work = "C:/UnityProjects/LegacyGwent/work/DynamicCards/RaghRepair-20260911/";
    static RaghRepairBuildRunner() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Work + "build-request.txt") || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
        File.Move(Work + "build-request.txt", Work + "build-started.txt");
        try
        {
            Application.logMessageReceived += Log;
            VerifyCloudAssignments();
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
    static void VerifyCloudAssignments()
    {
        const string prefix = "Assets/DynamicCards/Content/Old/Legacy2017/";
        var type = typeof(DynamicCardContentImporter);
        var flags = BindingFlags.NonPublic;
        var conversion = JsonUtility.FromJson(File.ReadAllText(prefix + "20005601/conversion.json"), type.GetNestedType("Conversion", flags));
        var materialType = type.GetNestedType("MaterialInfo", flags);
        var apply = type.GetMethod("ApplyTextureAssignments", flags | BindingFlags.Static);
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "Shared/20005601_13982_20005601.png");
        var cloud = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "Shared/11220501_39440_clouds_4x4.png");
        if (atlas == null || cloud == null) throw new Exception("Missing cloud test inputs");
        foreach (string file in new[] { "20005601_310072__20005601_AguaraFoxForm_Cloud.mat", "20005601_310086__20005601_AguaraFoxForm_Cloud.mat" })
        {
            string path = prefix + "Shared/" + file;
            var info = Activator.CreateInstance(materialType, true);
            materialType.GetField("asset").SetValue(info, path);
            materialType.GetField("originalName").SetValue(info, "[20005601]AguaraFoxForm_Cloud");
            bool particle = file.Contains("310072");
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(path));
            try
            {
                material.SetTexture("_MainTex", particle ? atlas : cloud);
                apply.Invoke(null, new object[] { material, info, conversion });
                if (material.GetTexture("_MainTex") != (particle ? cloud : atlas)) throw new Exception("Same-name cloud texture assignment regression: " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }
        File.WriteAllText(Work + "cloud-contract-result.txt", "PASS: particle and flowmap materials with identical original names recover their distinct source textures.");
    }
}
