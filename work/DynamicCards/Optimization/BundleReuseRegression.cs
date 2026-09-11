using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Assets.Script.DynamicCards.Editor;
public static class BundleReuseRegression
{
    public static void Run()
    {
        const string root="Assets/__BundleCacheRegression";
        const string output="Library/BundleCacheRegression";
        if(Directory.Exists(root))throw new Exception("Unexpected existing fixture");
        Directory.CreateDirectory(root);Directory.CreateDirectory(output);
        typeof(DynamicCardBundleBuilder).GetMethod("LoadSourceCache",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{output});
        try
        {
            var texture=new Texture2D(4,4,TextureFormat.RGBA32,false);
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)texture.SetPixel(x,y,((x+y)%2)==0?Color.red:Color.blue);
            texture.Apply();File.WriteAllBytes(root+"/texture.png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(root+"/texture.png",ImportAssetOptions.ForceSynchronousImport);
            var material=new Material(Shader.Find("Unlit/Texture"));material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/texture.png");
            AssetDatabase.CreateAsset(material,root+"/material.mat");AssetDatabase.SaveAssets();
            var build=typeof(DynamicCardBundleBuilder).GetMethod("BuildOne",BindingFlags.Static|BindingFlags.NonPublic);
            Action run=()=>build.Invoke(null,new object[]{output,"test.bundle",new[]{root+"/material.mat"},new string[0],BuildTarget.StandaloneWindows64,new Dictionary<string,string>(StringComparer.Ordinal)});
            run();string before=File.ReadAllText(output+"/test.bundle.inputs");var writeTime=File.GetLastWriteTimeUtc(output+"/test.bundle");
            run();if(File.GetLastWriteTimeUtc(output+"/test.bundle")!=writeTime)throw new Exception("Unchanged bundle was rebuilt");
            // This edit is on a dependency importer, not the root material.
            var importer=(TextureImporter)AssetImporter.GetAtPath(root+"/texture.png");
            importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            run();if(File.ReadAllText(output+"/test.bundle.inputs")==before)throw new Exception("Dependency importer edit was ignored");
            var bundle=AssetBundle.LoadFromFile(output+"/test.bundle");
            try {if(bundle.LoadAsset<Material>(root+"/material.mat").mainTexture.wrapMode!=TextureWrapMode.Clamp)throw new Exception("Rebuilt bundle contains stale texture settings");}
            finally {bundle.Unload(true);}
            File.WriteAllText("../Optimization/bundle-cache-test.json","{\"passed\":true,\"unchangedReused\":true,\"dependencyImporterRebuilt\":true,\"rebuiltTextureSettingsVerified\":true}");
            Debug.Log("BUNDLE_REUSE_REGRESSION_PASS");
        }
        finally {AssetDatabase.DeleteAsset(root);}
    }
}
