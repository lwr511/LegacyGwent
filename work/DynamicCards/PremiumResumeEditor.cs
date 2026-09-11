using System;
using System.IO;
public static class PremiumResumeEditor
{
 public static void Run()
 {
  Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_DATA",Path.GetFullPath("../LatestAnimationData"));
  Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_CONTENT","Assets/DynamicCards/Content/Latest");
  string remaining=Path.GetFullPath("../latest_remaining_ids.txt");
  if(File.Exists(remaining))Environment.SetEnvironmentVariable("DYNAMIC_ANIMATION_IDS",File.ReadAllText(remaining));
  SourceAnimationImporter.Run();
  UnityEngine.Debug.Log("LATEST_COMPLETE_IMPORT_FINISHED");
 }
}
