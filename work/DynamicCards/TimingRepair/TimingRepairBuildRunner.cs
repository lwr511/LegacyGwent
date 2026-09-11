using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using UnityEditor;using UnityEditor.Animations;using UnityEngine;using Assets.Script.DynamicCards;using Assets.Script.DynamicCards.Editor;
[InitializeOnLoad] public static class TimingRepairBuildRunner {
 const string Work="C:/UnityProjects/LegacyGwent/work/DynamicCards/TimingRepair/";static bool busy;
 static TimingRepairBuildRunner(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||File.Exists(Work+"delivery-result.txt"))return;if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}busy=true;
 try{File.WriteAllText(Work+"delivery-progress.txt","IMPORT "+DateTime.UtcNow);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
 if(!File.Exists(Work+"controllers-done.txt")){
 foreach(var kind in new[]{"Native","Legacy"}){Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_DATA",Work+kind);Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_CONTENT","Assets/DynamicCards/Content/Old/"+(kind=="Native"?"Thronebreaker":"Legacy2017"));SourceAnimationImporter.Run();}
 Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_DATA",null);Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_CONTENT",null);
 var type=typeof(SourceAnimationImporter);var doc=JsonUtility.FromJson(File.ReadAllText(Work+"controller-repairs.json"),type.GetNestedType("Document",BindingFlags.NonPublic));
 foreach(var record in (IEnumerable)doc.GetType().GetField("animators").GetValue(doc)){
 var rt=record.GetType();var id=(string)rt.GetField("id").GetValue(record);var path=(string)rt.GetField("path").GetValue(record);string prefix="Source_"+unchecked((uint)Animator.StringToHash(path)).ToString("x8");var folder="Assets/DynamicCards/Content/Old/Thronebreaker/"+id;
 var file=Directory.GetFiles(folder,"*"+prefix+".controller").Single();var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(file);var clips=new Dictionary<string,AnimationClip>();
 foreach(var clip in (IEnumerable)rt.GetField("clips").GetValue(record)){var name=(string)clip.GetType().GetField("name").GetValue(clip);var safe=new string(name.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_').ToArray());var asset=Directory.GetFiles(folder,"*"+prefix+"_"+safe+".anim").FirstOrDefault();if(asset!=null)clips[name]=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);}
 type.GetMethod("RestoreLayers",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{controller,record,clips});EditorUtility.SetDirty(controller);}
 AssetDatabase.SaveAssets();File.WriteAllText(Work+"controllers-done.txt","PASS");}
 File.WriteAllText(Work+"delivery-progress.txt","BUILD "+DateTime.UtcNow);DynamicCardBundleBuilder.Build(BuildTarget.StandaloneWindows64);
 var hash=(string)typeof(DynamicCardBuild).GetMethod("ContentHash",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);File.WriteAllText("Library/DynamicCardsBundles/StandaloneWindows64/content.hash",hash);
 File.WriteAllText(Work+"delivery-progress.txt","MANIFEST "+DateTime.UtcNow);var psi=new System.Diagnostics.ProcessStartInfo("C:/Program Files/Python312/python.exe","\""+Work+"write_manifest.py\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Directory.GetCurrentDirectory()};using(var proc=System.Diagnostics.Process.Start(psi)){proc.WaitForExit();if(proc.ExitCode!=0)throw new Exception("Manifest failed");}
 File.WriteAllText("Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready",DateTime.UtcNow.ToString("O"));File.WriteAllText(Work+"delivery-result.txt","PASS "+DateTime.UtcNow);
 }catch(Exception e){File.WriteAllText(Work+"delivery-result.txt","FAIL "+e);Debug.LogException(e);}finally{Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_DATA",null);Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_CONTENT",null);}}
}
