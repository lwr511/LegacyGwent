using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LegacyGwent.LadyLakeLab.Editor
{
    /// <summary>本实验用到的全部自定义着色器（名称 + 资产路径 + 用途）。</summary>
    public static class LadyLakeShaders
    {
        public struct Entry
        {
            public string name;
            public string path;
            public string usedBy;
        }

        public const string Layer = "LegacyGwent/LadyLake/Layer";
        public const string Sword = "LegacyGwent/LadyLake/Sword";
        public const string SwordMetal = "LegacyGwent/LadyLake/SwordMetal";
        public const string Backdrop = "LegacyGwent/LadyLake/Backdrop";
        public const string Caustics = "LegacyGwent/LadyLake/Caustics";
        public const string LightBeam = "LegacyGwent/LadyLake/LightBeam";
        public const string Bubble = "LegacyGwent/LadyLake/Bubble";
        public const string Mote = "LegacyGwent/LadyLake/Mote";
        public const string WaterRefraction = "LegacyGwent/LadyLake/WaterRefraction";

        public static readonly Entry[] All =
        {
            Make(Layer, "LadyLakeLayer.shader", "背景 / 人物 / 前景浮雕层"),
            Make(Sword, "LadyLakeSword.shader", "剑正面（剑刃 / rune 发光）"),
            Make(SwordMetal, "LadyLakeSwordMetal.shader", "剑背面与侧壁（暗银）"),
            Make(Backdrop, "LadyLakeBackdrop.shader", "深海暗色背景"),
            Make(Caustics, "LadyLakeCaustics.shader", "水底流动焦散"),
            Make(LightBeam, "LadyLakeLightBeam.shader", "水下体积光束"),
            Make(Bubble, "LadyLakeBubble.shader", "气泡"),
            Make(Mote, "LadyLakeMote.shader", "漂浮微尘"),
            Make(WaterRefraction, "LadyLakeWaterRefraction.shader", "全屏水折射 / 涟漪"),
        };

        private static Entry Make(string name, string file, string usedBy)
        {
            Entry e;
            e.name = name;
            e.path = LadyLakeLabConfig.LabFolder + "/Shaders/" + file;
            e.usedBy = usedBy;
            return e;
        }
    }

    /// <summary>材质工厂：统一落在 Generated/Materials 下，并写死渲染队列保证透明排序稳定。</summary>
    public static class LadyLakeLabMaterials
    {
        public static Material Create(string assetName, string shaderName, int renderQueue, List<string> errors)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                errors.Add("找不到着色器：" + shaderName);
                return null;
            }

            string path = LadyLakeLabConfig.GeneratedMaterialFolder + "/" + assetName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                material.name = assetName;
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.renderQueue = renderQueue;
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void SetFloat(Material m, string property, float value)
        {
            if (m == null) return;
            if (m.HasProperty(property)) m.SetFloat(property, value);
        }

        public static void SetColor(Material m, string property, Color value)
        {
            if (m == null) return;
            if (m.HasProperty(property)) m.SetColor(property, value);
        }

        public static void SetVector(Material m, string property, Vector4 value)
        {
            if (m == null) return;
            if (m.HasProperty(property)) m.SetVector(property, value);
        }

        public static void SetTexture(Material m, string property, Texture value)
        {
            if (m == null) return;
            if (m.HasProperty(property)) m.SetTexture(property, value);
        }
    }
}
