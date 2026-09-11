using System;using System.IO;using System.Reflection;using UnityEditor;using UnityEngine;using Assets.Script.DynamicCards;using Assets.Script.DynamicCards.Editor;
[InitializeOnLoad] public static class VisualRepairBuildRunner {
 const string Work="C:/UnityProjects/LegacyGwent/work/DynamicCards/VisualRepair/";static bool busy;
 static VisualRepairBuildRunner(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||File.Exists(Work+"delivery-result.txt"))return;if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}busy=true;
 try{File.WriteAllText(Work+"delivery-progress.txt","IMPORT "+DateTime.UtcNow);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
 var card=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DynamicCards/Content/Old/Legacy2017/11210701/Card.prefab");var tile=card.GetComponentInChildren<DynamicCardTileMotion>(true);if(tile==null||tile.TilePrefabs.Length!=2||tile.TilePrefabs[0]==null||tile.TilePrefabs[1]==null||tile.WorldSpaceCamPos==null)throw new Exception("Tile references missing");
 DynamicCardTextureCompression.Apply();File.WriteAllText(Work+"delivery-progress.txt","BUILD "+DateTime.UtcNow);DynamicCardBundleBuilder.Build(BuildTarget.StandaloneWindows64);
 var hash=(string)typeof(DynamicCardBuild).GetMethod("ContentHash",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);File.WriteAllText("Library/DynamicCardsBundles/StandaloneWindows64/content.hash",hash);
 File.WriteAllText(Work+"delivery-progress.txt","MANIFEST "+DateTime.UtcNow);var psi=new System.Diagnostics.ProcessStartInfo("C:/Program Files/Python312/python.exe","\""+Work+"write_manifest.py\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Directory.GetCurrentDirectory()};using(var proc=System.Diagnostics.Process.Start(psi)){proc.WaitForExit();if(proc.ExitCode!=0)throw new Exception("Manifest failed");}
 File.WriteAllText("Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready",DateTime.UtcNow.ToString("O"));File.WriteAllText(Work+"delivery-result.txt","PASS "+DateTime.UtcNow);
 }catch(Exception e){File.WriteAllText(Work+"delivery-result.txt","FAIL "+e);Debug.LogException(e);}}
}
