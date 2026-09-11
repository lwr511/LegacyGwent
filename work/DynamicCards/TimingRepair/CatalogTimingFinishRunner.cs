using System;using System.IO;using System.Reflection;using System.Collections.Generic;using UnityEditor;using UnityEngine;using Assets.Script.DynamicCards;using Assets.Script.DynamicCards.Editor;
[InitializeOnLoad]public static class CatalogTimingFinishRunner {
 const string Work="C:/UnityProjects/LegacyGwent/work/DynamicCards/TimingRepair/";static bool busy;
 static CatalogTimingFinishRunner(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||File.Exists(Work+"catalog-delivery-result.txt"))return;if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}busy=true;
 try{File.WriteAllText(Work+"catalog-delivery-progress.txt","IMPORT "+DateTime.UtcNow);AssetDatabase.ImportAsset(DynamicCardLibrary.CatalogAsset,ImportAssetOptions.ForceSynchronousImport);string dir="Library/DynamicCardsBundles/StandaloneWindows64";var type=typeof(DynamicCardBundleBuilder);type.GetMethod("LoadSourceCache",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{dir});
 type.GetMethod("BuildOne",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{dir,DynamicCardLibrary.BundleFile,new[]{DynamicCardLibrary.CatalogAsset},new string[0],BuildTarget.StandaloneWindows64,new Dictionary<string,string>()});
 File.WriteAllText(dir+"/content.hash",(string)typeof(DynamicCardBuild).GetMethod("ContentHash",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null));File.WriteAllText(Work+"catalog-delivery-progress.txt","MANIFEST "+DateTime.UtcNow);
 using(var proc=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("C:/Program Files/Python312/python.exe","\""+Work+"write_manifest.py\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Directory.GetCurrentDirectory()})){proc.WaitForExit();if(proc.ExitCode!=0)throw new Exception("manifest failed");}
 File.WriteAllText(dir+"/cards.bundle.editor-ready",DateTime.UtcNow.ToString("O"));File.WriteAllText(Work+"catalog-delivery-result.txt","PASS "+DateTime.UtcNow);
 }catch(Exception e){File.WriteAllText(Work+"catalog-delivery-result.txt","FAIL "+e);Debug.LogException(e);}}
}
