using System.IO;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
namespace Assets.Script.DynamicCards.Editor
{
    public sealed class DynamicCardEditorCache : AssetPostprocessor
    {
        private const string Bundle="Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle";
        [Serializable] private class FileEntry {public string path,hash;}
        [Serializable] private class FileManifest {public FileEntry[] files;}
        private static Dictionary<string,string> hashes;
        private static HashSet<string> directories;
        private static DateTime manifestTime;

        public static void WriteManifest()
        {
            var files=new[]{"Assets/DynamicCards/Content","Assets/DynamicCards/Shaders"}
                .SelectMany(root=>Directory.GetFiles(root,"*",SearchOption.AllDirectories))
                .Where(path=>!path.EndsWith(".meta") || !Directory.Exists(path.Substring(0,path.Length-5)))
                .Select(path=>new FileEntry{path=path.Replace('\\','/'),hash=Hash(path)}).ToArray();
            File.WriteAllText(Bundle+".editor-files.json",JsonUtility.ToJson(new FileManifest{files=files}));
            hashes=null;
        }

        private static string Hash(string path)
        {
            using(var stream=File.OpenRead(path))
            using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(stream));
        }

        private static bool Unchanged(string path)
        {
            string expected;
            return File.Exists(path) && hashes.TryGetValue(path,out expected) && Hash(path)==expected;
        }

        private static bool MatchesDeliveredPath(string path)
        {
            // Unity can report the sidecar itself. Do not look for a fictitious .meta.meta.
            if(path.EndsWith(".meta",StringComparison.Ordinal))path=path.Substring(0,path.Length-5);
            if(Directory.Exists(path))return true; // Folder metadata is deliberately omitted from the manifest.
            if(!File.Exists(path) && !File.Exists(path+".meta"))
            {
                // An import refresh may finish after installing a newer package. Deletions
                // already absent from that package are expected, not evidence of stale content.
                return !hashes.ContainsKey(path) && !hashes.ContainsKey(path+".meta") &&
                    !directories.Contains(path);
            }
            return Unchanged(path) && Unchanged(path+".meta");
        }

        private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] previous)
        {
            if(!File.Exists(Bundle+".editor-ready"))return;
            var changed=imported.Concat(deleted).Concat(moved).Concat(previous)
                .Where(p=>p.StartsWith("Assets/DynamicCards/Content/") || p.StartsWith("Assets/DynamicCards/Shaders/")).Distinct().ToArray();
            if(changed.Length==0)return;
            try
            {
                string manifest=Bundle+".editor-files.json";
                if(File.Exists(manifest))
                {
                    var time=File.GetLastWriteTimeUtc(manifest);
                    if(hashes==null || time!=manifestTime)
                    {
                        hashes=JsonUtility.FromJson<FileManifest>(File.ReadAllText(manifest)).files.ToDictionary(f=>f.path,f=>f.hash);
                        directories=new HashSet<string>(StringComparer.Ordinal);
                        foreach(var file in hashes.Keys)
                        {
                            var parent=Path.GetDirectoryName(file).Replace('\\','/');
                            while(!string.IsNullOrEmpty(parent) && directories.Add(parent))
                                parent=Path.GetDirectoryName(parent)?.Replace('\\','/');
                        }
                        manifestTime=time;
                    }
                    // Reimporting unchanged delivered files does not invalidate a prebuilt cache.
                    // Actual content or importer-setting changes still invalidate it.
                    var mismatch=changed.FirstOrDefault(p=>!MatchesDeliveredPath(p));
                    if(mismatch==null)return;
                    Debug.LogWarning("Dynamic card package cache invalidated by changed content: "+mismatch);
                }
            }
            catch(Exception exception){Debug.LogWarning("Dynamic card cache validation: "+exception.Message);}
            File.Delete(Bundle+".editor-ready");
        }
    }
}
