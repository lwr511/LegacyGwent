using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QueueSmokeEditor
{
    public static void Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Queue verification").AddComponent<QueueSmoke>();
        const string scene="Assets/QueueSmoke.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scene);
        Assets.Script.DynamicCards.Editor.DynamicCardBuild.IncludeContent=false;
        var output=System.IO.Path.GetFullPath("../BuildVerification/queue/Queue.exe");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new[]{scene},output,BuildTarget.StandaloneWindows64,BuildOptions.None);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Queue player build failed");
        Debug.Log("DYNAMIC_QUEUE_BUILD_PASS");
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Queue verification").AddComponent<QueueSmoke>();
        EditorApplication.isPlaying=true;
    }
}
