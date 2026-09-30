using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Assets.Script.DynamicCards;
using Assets.Script.DynamicCards.Editor;
namespace LegacyGwent.LadyLakePremium.Acceptance {
[InitializeOnLoad] public static class PremiumDelivery {
const string Folder="Assets/Experiments/LadyLakePremium/Acceptance";
const string Integration="Assets/Experiments/LadyLakePremium/Integration";
const string Dest="Assets/DynamicCards/Content/Authored/LadyLake/Card.prefab";
static double next;
static PremiumDelivery(){EditorApplication.update+=Poll;}
static void Poll(){if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;next=EditorApplication.timeSinceStartup+1;if(SessionState.GetBool("LadyLakePremium.Review",false)&&EditorApplication.isPlaying){var probe=UnityEngine.Object.FindObjectOfType<PremiumPlaybackProbe>();if(probe==null)new GameObject("__PremiumPlaybackProbe").AddComponent<PremiumPlaybackProbe>();else if(probe.Finished){SessionState.SetBool("LadyLakePremium.Review",false);EditorApplication.isPlaying=false;}}string p=Folder+"/control.txt";if(!File.Exists(p))return;string cmd=File.ReadAllText(p).Trim();File.Delete(p);try{
if(cmd=="build"){Invoke("LegacyGwent.LadyLakePremium.Editor.LadyLakePremiumBuilder","Build");Prepare();}
else if(cmd=="assemble"){Invoke("LegacyGwent.LadyLakePremium.Editor.LadyLakePremiumBuilder","AssembleExisting");Prepare();} else if(cmd=="prepare")Prepare(); else if(cmd=="environment")Invoke("LegacyGwent.LadyLakePremium.Editor.LadyLakeEnvironmentBuilder","Build");
else if(cmd=="packages")DynamicCardBuild.BuildBundle(BuildTarget.StandaloneWindows64);
else if(cmd=="source-on"){SessionState.SetBool("LadyLakePremium.PreviousSourceLoading",DynamicCardLibrary.AllowEditorSourceLoading);DynamicCardLibrary.AllowEditorSourceLoading=true;}
else if(cmd=="source-restore")DynamicCardLibrary.AllowEditorSourceLoading=SessionState.GetBool("LadyLakePremium.PreviousSourceLoading",false);
else if(cmd=="open"){EditorSceneManager.OpenScene(Integration+"/LadyLakePremium.unity");var t=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");if(t!=null)EditorWindow.GetWindow(t).Show();}
else if(cmd=="review"){SessionState.SetBool("LadyLakePremium.Review",true);EditorApplication.isPlaying=true;} else if(cmd=="play")EditorApplication.isPlaying=true;
else if(cmd=="stop")EditorApplication.isPlaying=false;
else if(cmd=="preview-size")PremiumPreviewSize.Set(); else if(cmd=="refresh")AssetDatabase.Refresh();
else if(cmd=="audit")PremiumSourceAudit.Run(); else if(cmd=="poses")PremiumPoseCapture.Run(); else if(cmd=="environment-pose")PremiumPoseCapture.EnvironmentOnly(); else if(cmd=="model")Invoke("LegacyGwent.LadyLakePremium.Editor.LadyLakeModelBuilder","Build");
else throw new Exception("Unknown command "+cmd);
File.WriteAllText(Folder+"/control-result.txt",cmd+" OK "+DateTime.UtcNow.ToString("o"));
}catch(Exception e){File.WriteAllText(Folder+"/control-result.txt",cmd+" ERROR "+e);Debug.LogException(e);}}
static void Invoke(string type,string method){var t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(type)).FirstOrDefault(x=>x!=null);if(t==null)throw new Exception("Missing "+type);t.GetMethod(method,BindingFlags.Public|BindingFlags.Static).Invoke(null,null);}
public static void Prepare(){if(EditorApplication.isPlaying)throw new Exception("Stop playback first");var source=AssetDatabase.LoadAssetAtPath<GameObject>(Integration+"/Card.prefab");if(!source)throw new Exception("Model assembly missing");Directory.CreateDirectory(Path.GetDirectoryName(Dest));AssetDatabase.Refresh();var go=UnityEngine.Object.Instantiate(source);try{go.name="Card";PrefabUtility.SaveAsPrefabAsset(go,Dest);}finally{UnityEngine.Object.DestroyImmediate(go);}var entry=JsonUtility.FromJson<DynamicCardEntry>(File.ReadAllText(Integration+"/catalog-entry.json"));entry.prefab=Dest;var catalogPath=DynamicCardLibrary.CatalogAsset;string backup=Folder+"/catalog-before.json";if(!File.Exists(backup))File.Copy(catalogPath,backup);var catalog=JsonUtility.FromJson<DynamicCardCatalog>(File.ReadAllText(catalogPath));if(catalog.cards.Any(c=>c.id!=entry.id && (c.artIds??new string[0]).Intersect(entry.artIds).Any()))throw new Exception("Art already belongs to another premium entry");var document=JObject.Parse(File.ReadAllText(catalogPath));var cards=(JArray)document["cards"];foreach(var item in cards.Where(item=>(string)item["id"]==entry.id).ToArray())item.Remove();cards.Add(JObject.Parse(JsonUtility.ToJson(entry)));File.WriteAllText(catalogPath,document.ToString(Newtonsoft.Json.Formatting.Indented));AssetDatabase.ImportAsset(catalogPath);AssetDatabase.SaveAssets();File.WriteAllText(Folder+"/catalog-delivery.json",JsonUtility.ToJson(entry,true));}
}}





