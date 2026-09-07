using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Assets.Script.DynamicCards.Editor
{
    // Small, resumable builds avoid loading every premium scene into the editor at once.
    public static class DynamicCardBundleBuilder
    {
        public const int CardsPerPart = 32;

        public static string Build(BuildTarget target)
        {
            var catalog = JsonUtility.FromJson<DynamicCardCatalog>(File.ReadAllText(DynamicCardLibrary.CatalogAsset));
            var cards = catalog.cards.OrderBy(c => c.prefab, StringComparer.Ordinal).ToArray();
            int batchSize;
            if (!int.TryParse(Environment.GetEnvironmentVariable("DYNAMIC_CARDS_PER_PART"), out batchSize)) batchSize = CardsPerPart;
            batchSize = Math.Max(1, Math.Min(CardsPerPart, batchSize));
            string directory = "Library/DynamicCardsBundles/" + target;
            Directory.CreateDirectory(directory);
            string ready = directory + "/" + DynamicCardLibrary.BundleFile + ".editor-ready";
            if (File.Exists(ready)) File.Delete(ready);
            var parts = new List<DynamicCardBundlePart>();
            var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int offset = 0; offset < cards.Length; offset += batchSize)
            {
                var group = cards.Skip(offset).Take(batchSize).ToArray();
                var assets = group.SelectMany(c => new[] { c.prefab, c.audio }).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToArray();
                foreach (var asset in assets) included.Add(asset);
                var part = new DynamicCardBundlePart { file = "cards-" + (offset / batchSize).ToString("000") + ".bundle", prefabs = group.Select(c => c.prefab).ToArray() };
                BuildOne(directory, part.file, assets, part.prefabs, target);
                parts.Add(part);
                Debug.Log("DYNAMIC_PART_PROGRESS cards=" + Math.Min(offset + batchSize, cards.Length) + "/" + cards.Length);
            }
            var extras = Directory.GetFiles(DynamicCardLibrary.ContentRoot, "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).Where(p => (p.EndsWith(".wav") || p.EndsWith(".bytes")) && !included.Contains(p)).OrderBy(p => p).ToArray();
            for (int offset = 0; offset < extras.Length; offset += CardsPerPart)
            {
                var part = new DynamicCardBundlePart { file = "cards-extra-" + (offset / CardsPerPart).ToString("000") + ".bundle", prefabs = new string[0] };
                BuildOne(directory, part.file, extras.Skip(offset).Take(CardsPerPart).ToArray(), part.prefabs, target);
                parts.Add(part);
            }
            BuildOne(directory, DynamicCardLibrary.BundleFile, new[] { DynamicCardLibrary.CatalogAsset }, new string[0], target);
            File.WriteAllText(directory + "/" + DynamicCardLibrary.BundleIndexFile, JsonUtility.ToJson(new DynamicCardBundleIndex { parts = parts.ToArray() }, true));
            Debug.Log("DYNAMIC_PARTITION_BUILD_DONE cards=" + cards.Length + " parts=" + parts.Count);
            return directory + "/" + DynamicCardLibrary.BundleFile;
        }

        private static void BuildOne(string directory, string file, string[] assets, string[] prefabs, BuildTarget target)
        {
            string path = directory + "/" + file;
            string stamp = path + ".inputs";
            string input = Application.unityVersion + "\n" + target + "\npartition-v1\n" + string.Join("\n", assets.Select(p => p + ":" + AssetDatabase.GetAssetDependencyHash(p)));
            string hash;
            using (var sha = SHA256.Create()) hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
            bool reusable = File.Exists(path) && File.Exists(stamp) && File.ReadAllText(stamp) == hash + ":" + new FileInfo(path).Length;
            if (!reusable)
            {
                Debug.Log("DYNAMIC_PART_BUILD " + file + " assets=" + assets.Length);
                var build = new AssetBundleBuild { assetBundleName = file, assetNames = assets };
                var result = BuildPipeline.BuildAssetBundles(directory, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target);
                if (result == null) throw new BuildFailedException("Dynamic card part failed: " + file);
            }
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) throw new BuildFailedException("Dynamic card part cannot be read: " + file);
            try
            {
                var actual = new HashSet<string>(bundle.GetAllAssetNames().Where(p => p.EndsWith("/card.prefab")), StringComparer.OrdinalIgnoreCase);
                if (!actual.SetEquals(prefabs)) throw new BuildFailedException("Dynamic card part has incorrect scenes: " + file);
            }
            finally { bundle.Unload(true); }
            File.WriteAllText(stamp, hash + ":" + new FileInfo(path).Length);
            Debug.Log("DYNAMIC_PART_READY " + file + " bytes=" + new FileInfo(path).Length + " reused=" + reusable);
            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
        }

        public static string[] PayloadFiles(string directory)
        {
            var names = new List<string> { DynamicCardLibrary.BundleFile };
            string indexPath = Path.Combine(directory, DynamicCardLibrary.BundleIndexFile);
            if (File.Exists(indexPath))
            {
                var index = JsonUtility.FromJson<DynamicCardBundleIndex>(File.ReadAllText(indexPath));
                if (index == null || index.version != 1 || index.parts == null) throw new BuildFailedException("Invalid dynamic card bundle index.");
                names.Add(DynamicCardLibrary.BundleIndexFile);
                foreach (var part in index.parts)
                {
                    if (Path.GetFileName(part.file) != part.file || !part.file.StartsWith("cards-") || !part.file.EndsWith(".bundle")) throw new BuildFailedException("Invalid dynamic card part filename.");
                    names.Add(part.file);
                }
            }
            var paths = names.Distinct().Select(name => Path.Combine(directory, name)).ToArray();
            foreach (var path in paths) if (!File.Exists(path)) throw new BuildFailedException("Missing dynamic card package: " + path);
            return paths;
        }
    }
}
