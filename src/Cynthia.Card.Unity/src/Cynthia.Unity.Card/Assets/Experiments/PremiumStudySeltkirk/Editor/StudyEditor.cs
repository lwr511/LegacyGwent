using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Assets.Script.DynamicCards;
namespace PremiumReferenceStudy {
[InitializeOnLoad] public static class StudyEditor {
const string Root="Assets/Experiments/PremiumStudySeltkirk";static double next;static StudyEditor(){EditorApplication.update+=Poll;}
static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.5;string p=Root+"/request.txt";if(!File.Exists(p))return;string command=File.ReadAllText(p).Trim();File.Delete(p);try{if(command=="build"){Build();}else if(command=="open")EditorSceneManager.OpenScene(Root+"/SeltkirkStudy.unity");else if(command=="play")EditorApplication.isPlaying=true;else if(command=="stop")EditorApplication.isPlaying=false;else if(command=="status")Status();else if(command=="capture")ScreenCapture.CaptureScreenshot(Path.GetFullPath("../../../../work/PremiumStudySeltkirk/comparison-live.png"));else throw new Exception(command);File.WriteAllText(Root+"/result.txt",command+" OK "+DateTime.UtcNow.ToString("o"));}catch(Exception e){File.WriteAllText(Root+"/result.txt",e.ToString());Debug.LogException(e);}}
[Serializable] sealed class Snapshot {public string scene,utc;public bool playing,sourceLoading,automaticCapture,modelLoaded;public int quality,skinnedRenderers,animators,particles;}
static void Status(){
 var stage=UnityEngine.Object.FindObjectOfType<SeltkirkStudy>();var view=UnityEngine.Object.FindObjectOfType<DynamicCardView>();
 var model=view==null?null:(GameObject)typeof(DynamicCardView).GetField("model",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(view);
 var s=new Snapshot{scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,utc=DateTime.UtcNow.ToString("o"),playing=EditorApplication.isPlaying,sourceLoading=DynamicCardLibrary.AllowEditorSourceLoading,quality=(int)DynamicCardSettings.Quality,automaticCapture=stage!=null&&stage.capture,modelLoaded=model!=null,skinnedRenderers=model==null?0:model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,animators=model==null?0:model.GetComponentsInChildren<Animator>(true).Length,particles=model==null?0:model.GetComponentsInChildren<ParticleSystem>(true).Length};
 File.WriteAllText(Root+"/status.json",JsonUtility.ToJson(s,true));
}
[MenuItem("Tools/Premium Reference Study/Open Seltkirk")]
public static void Build(){
 if(EditorApplication.isPlaying)throw new Exception("Stop first");
 string path=Root+"/SeltkirkStudy.unity";
 var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
 if(!scene.IsValid()||!scene.isLoaded)scene=File.Exists(path)?EditorSceneManager.OpenScene(path,OpenSceneMode.Additive):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
 UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
 SeltkirkStudy study=null;
 foreach(var root in scene.GetRootGameObjects()){study=root.GetComponentInChildren<SeltkirkStudy>(true);if(study!=null)break;}
 if(study==null){var go=new GameObject("Seltkirk original reference study");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);study=go.AddComponent<SeltkirkStudy>();}
 study.capture=false;study.staticArt=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Addressables/Cards/20161800.png");EditorUtility.SetDirty(study);
 if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("Failed to save study scene");
 LegacyGwent.LadyLakePremium.Acceptance.PremiumPreviewSize.Set();
}
}}
