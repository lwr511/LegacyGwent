using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class PremiumClipStorage
{
 public static void Compact()
 {
  var paths=Directory.GetFiles("Assets/DynamicCards/Content","*.anim",SearchOption.AllDirectories)
   .Where(p=>Path.GetFileName(p).StartsWith("Source_") || Path.GetFileName(p).StartsWith("Global_Source_")).Select(p=>p.Replace('\\','/')).ToArray();
  long before=paths.Sum(p=>new FileInfo(p).Length);
  var previous=EditorSettings.serializationMode;
  try
  {
   EditorSettings.serializationMode=SerializationMode.ForceBinary;
   for(int i=0;i<paths.Length;i+=40)
   {
    foreach(string path in paths.Skip(i).Take(40))
    {
     var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
     if(clip==null)throw new Exception("Missing clip "+path);
     string guid;long local;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip,out guid,out local);
     float duration=clip.length;int bindings=AnimationUtility.GetCurveBindings(clip).Length;
     UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[]{clip},path,false);
     AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
     var check=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);string checkGuid;long checkLocal;
     AssetDatabase.TryGetGUIDAndLocalFileIdentifier(check,out checkGuid,out checkLocal);
     if(check==null || guid!=checkGuid || local!=checkLocal || duration!=check.length || bindings!=AnimationUtility.GetCurveBindings(check).Length)
      throw new Exception("Clip storage round-trip mismatch "+path);
    }
    EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
    Debug.Log("PREMIUM_CLIP_STORAGE_PROGRESS "+Math.Min(i+40,paths.Length)+"/"+paths.Length);
   }
  }
  finally{EditorSettings.serializationMode=previous;}
  long after=paths.Sum(p=>new FileInfo(p).Length);
  Debug.Log("PREMIUM_CLIP_STORAGE_DONE clips="+paths.Length+" before="+before+" after="+after);
 }
 public static void Focused()
 {
  Compact();PremiumValidationEditor.Timeline();
 }
}
