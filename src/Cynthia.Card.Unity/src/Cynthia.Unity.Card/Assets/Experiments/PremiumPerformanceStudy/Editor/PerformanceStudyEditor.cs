using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PremiumReferenceStudy
{
    [InitializeOnLoad] public static class PerformanceStudyEditor
    {
        const string Root="Assets/Experiments/PremiumPerformanceStudy";
        static double next;
        static PerformanceStudyEditor(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.5;
            string request=Root+"/request.txt";if(!File.Exists(request))return;
            string cmd=File.ReadAllText(request).Trim();File.Delete(request);
            try{
                if(cmd=="build")Build();else if(cmd=="play")EditorApplication.isPlaying=true;else if(cmd=="stop")EditorApplication.isPlaying=false;
                else if(cmd=="restore")EditorSceneManager.OpenScene("Assets/Experiments/PremiumStudySeltkirk/SeltkirkStudy.unity");
                else throw new Exception(cmd);
                File.WriteAllText(Root+"/result.txt",cmd+" OK "+DateTime.UtcNow.ToString("o"));
            }catch(Exception ex){File.WriteAllText(Root+"/result.txt",ex.ToString());Debug.LogException(ex);}
        }
        static void Build()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
            var old=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var stage=new GameObject("Original performance comparison").AddComponent<PerformanceReferences>();
            stage.art=new[]{AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Addressables/Cards/202105.png"),AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Addressables/Cards/202194.png"),AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Addressables/Cards/c10001000.png")};
            if(Array.Exists(stage.art,s=>s==null))throw new Exception("Static art missing");
            if(!EditorSceneManager.SaveScene(scene,Root+"/PerformanceReferences.unity"))throw new Exception("Save failed");
            EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(old);
            EditorSceneManager.OpenScene(Root+"/PerformanceReferences.unity");
            LegacyGwent.LadyLakePremium.Acceptance.PremiumPreviewSize.Set();
        }
    }
}
