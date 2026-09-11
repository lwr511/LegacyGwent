using System;
using System.IO;
using UnityEngine;
public static class PremiumPostImportEditor
{
 public static void Run()
 {
  PremiumValidationEditor.PrepareLegacy();
  Repair("Native","Assets/DynamicCards/Content","12800100");
  Repair("Legacy","Assets/DynamicCards/Content/Legacy2017","15320101,16210801,16310101,20023501");
  Repair("Latest","Assets/DynamicCards/Content/Latest","10820101,12920101,14740101,20260101");
  PremiumValidationEditor.Audit();
  Debug.Log("PREMIUM_POST_IMPORT_PASS");
 }
 private static void Repair(string kind,string content,string ids)
 {
  Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_DATA",Path.GetFullPath("../"+kind+"AnimationData"));
  Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_CONTENT",content);
  Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_IDS",ids);
  SourceAnimationImporter.Run();
  Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_IDS",null);
 }
}
