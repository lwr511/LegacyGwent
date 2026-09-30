using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LegacyGwent.LadyLakeLab.Editor
{
    /// <summary>
    /// 一张分层图的运行时分析结果：像素、alpha、距离场、有效包围盒。
    /// 由 Codex 提供的 PNG 读取而来；本实验不生成 / 不修改任何位图。
    /// </summary>
    public sealed class LadyLakeLayerTexture
    {
        public LadyLakeLayerRole role;
        public string assetPath = "";
        public string fileName = "";
        public Texture2D texture;
        public int width;
        public int height;
        public bool hasAlpha;
        public Color32[] pixels;
        public byte[] alpha;
        public int[] distance;

        public int activeMinX;
        public int activeMinY;
        public int activeMaxX;
        public int activeMaxY;

        public void BuildDerived()
        {
            distance = LadyLakeLabMeshes.BuildDistanceField(alpha, width, height, 128);
            GetActiveBounds(24, out activeMinX, out activeMinY, out activeMaxX, out activeMaxY);
        }

        public void GetActiveBounds(byte threshold, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = width;
            minY = height;
            maxX = -1;
            maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (alpha[row + x] <= threshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0)
            {
                minX = 0;
                minY = 0;
                maxX = width - 1;
                maxY = height - 1;
            }
        }

        /// <summary>完全不透明像素占比（用于判断这张图是否真的抠过）。</summary>
        public float OpaqueRatio()
        {
            int opaque = 0;
            for (int i = 0; i < alpha.Length; i++)
            {
                if (alpha[i] >= 250) opaque++;
            }
            return alpha.Length > 0 ? opaque / (float)alpha.Length : 0f;
        }

        public void ReleasePixels()
        {
            pixels = null;
            distance = null;
        }
    }

    /// <summary>
    /// 图层注册覆盖（可选）。放在 Assets/Experiments/LadyLake/Textures/layout.json
    /// （或 asset-layout.json），格式：
    /// {"figure":{"x":0.11,"yFromTop":0.098,"width":0.76,"height":0.752},
    ///  "sword":{"x":0.052,"yFromTop":0.093,"width":0.87,"height":0.86},
    ///  "swordAutoFit":true,"note":"..."}
    /// 归一化坐标，左上原点。缺失时使用代码内默认值。
    /// </summary>
    [System.Serializable]
    public class LadyLakeLayoutRect
    {
        public float x;
        public float yFromTop;
        public float width;
        public float height;
    }

    [System.Serializable]
    public class LadyLakeLayoutOverride
    {
        public LadyLakeLayoutRect figure;
        public LadyLakeLayoutRect sword;
        public bool swordAutoFit = true;
        public string note = "";
        public string sourcePath = "";

        public bool HasFigure { get { return figure != null && figure.width > 0.01f && figure.height > 0.01f; } }
        public bool HasSword { get { return sword != null && sword.width > 0.01f && sword.height > 0.01f; } }
    }

    /// <summary>分层 PNG 的定位、导入设置与校验。</summary>
    public static class LadyLakeLabTextures
    {
        public static readonly LadyLakeLayerRole[] AllRoles =
        {
            LadyLakeLayerRole.Background,
            LadyLakeLayerRole.Figure,
            LadyLakeLayerRole.Foreground,
            LadyLakeLayerRole.Sword,
        };

        private static readonly string[] LayoutOverrideNames = { "layout.json", "asset-layout.json" };

        /// <summary>该层是否必须有 alpha（必须被抠过）。</summary>
        public static bool RequiresAlpha(LadyLakeLayerRole role)
        {
            return role != LadyLakeLayerRole.Background;
        }

        public static string AssetPathFor(LadyLakeLayerRole role)
        {
            return LadyLakeLabConfig.TextureFolder + "/" + LadyLakeLabConfig.RoleToFileName(role);
        }

        public static string ToAbsolutePath(string assetPath)
        {
            if (assetPath.StartsWith("Assets/"))
            {
                return Application.dataPath + assetPath.Substring("Assets".Length);
            }
            return assetPath;
        }

        /// <summary>读取可选的 layout.json / asset-layout.json 覆盖。</summary>
        public static LadyLakeLayoutOverride LoadLayoutOverride(List<string> warnings)
        {
            for (int i = 0; i < LayoutOverrideNames.Length; i++)
            {
                string assetPath = LadyLakeLabConfig.TextureFolder + "/" + LayoutOverrideNames[i];
                string absolute = ToAbsolutePath(assetPath);
                if (!File.Exists(absolute)) continue;

                try
                {
                    string json = File.ReadAllText(absolute);
                    LadyLakeLayoutOverride parsed = JsonUtility.FromJson<LadyLakeLayoutOverride>(json);
                    if (parsed != null)
                    {
                        parsed.sourcePath = assetPath;
                        return parsed;
                    }
                }
                catch (System.Exception ex)
                {
                    warnings.Add("layout 覆盖文件解析失败：" + assetPath + " -> " + ex.Message);
                }
            }
            return null;
        }

        /// <summary>载入可选贴图（例如 Aeschna 焦散 / 流动噪声）。缺失只记录警告并回退为纯程序效果。</summary>
        public static Texture2D TryLoadOptionalTexture(string fileName, List<string> warnings)
        {
            string assetPath = LadyLakeLabConfig.TextureFolder + "/" + fileName;
            if (!File.Exists(ToAbsolutePath(assetPath)))
            {
                warnings.Add("可选贴图缺失（相关效果回退为纯程序生成）：" + assetPath);
                return null;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }
                if (importer.wrapMode != TextureWrapMode.Repeat)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;   // 需要平铺滚动
                    changed = true;
                }
                if (changed) AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null) warnings.Add("可选贴图无法载入：" + assetPath);
            return texture;
        }

        /// <summary>
        /// 载入四张分层图并做严格校验。
        /// 任何一张缺失 / 读不出 / 关键层没有 alpha / 有效区域为空，都会写入 errors。
        /// </summary>
        public static List<LadyLakeLayerTexture> LoadAll(
            List<string> missing,
            List<string> errors,
            List<string> warnings)
        {
            List<LadyLakeLayerTexture> result = new List<LadyLakeLayerTexture>();

            for (int i = 0; i < AllRoles.Length; i++)
            {
                LadyLakeLayerRole role = AllRoles[i];
                string assetPath = AssetPathFor(role);
                string absolute = ToAbsolutePath(assetPath);

                if (!File.Exists(absolute))
                {
                    missing.Add(role + ": " + assetPath);
                    errors.Add(string.Format(
                        "缺少分层图 {0}（期望路径 {1}）。请先由 Codex 准备好四张 PNG，未就绪时不会生成任何伪造资产。",
                        role, assetPath));
                    continue;
                }

                LadyLakeLayerTexture layer = Load(role, assetPath, errors, warnings);
                if (layer != null) result.Add(layer);
            }

            return result;
        }

        private static LadyLakeLayerTexture Load(
            LadyLakeLayerRole role,
            string assetPath,
            List<string> errors,
            List<string> warnings)
        {
            bool needsAlpha = RequiresAlpha(role);
            ApplyImportSettings(assetPath, needsAlpha);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                errors.Add("无法载入贴图：" + assetPath);
                return null;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            bool hasAlpha = importer != null && importer.DoesSourceTextureHaveAlpha();

            if (needsAlpha && !hasAlpha)
            {
                errors.Add(string.Format(
                    "{0} 需要 alpha 通道（{1}），当前源图没有透明信息，抠像无法进行。",
                    role, assetPath));
                return null;
            }

            if (!texture.isReadable)
            {
                // 上面已经写入 isReadable=true 并重新导入，这里再兜底一次
                ApplyImportSettings(assetPath, needsAlpha, true);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null || !texture.isReadable)
                {
                    errors.Add("贴图不可读（CPU 无法取像素）：" + assetPath);
                    return null;
                }
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (System.Exception ex)
            {
                errors.Add("读取像素失败：" + assetPath + " -> " + ex.Message);
                return null;
            }

            LadyLakeLayerTexture layer = new LadyLakeLayerTexture();
            layer.role = role;
            layer.assetPath = assetPath;
            layer.fileName = LadyLakeLabConfig.RoleToFileName(role);
            layer.texture = texture;
            layer.width = texture.width;
            layer.height = texture.height;
            layer.hasAlpha = hasAlpha;
            layer.pixels = pixels;

            layer.alpha = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                layer.alpha[i] = hasAlpha ? pixels[i].a : (byte)255;
            }

            layer.BuildDerived();

            int activePixels = 0;
            for (int i = 0; i < layer.alpha.Length; i++)
            {
                if (layer.alpha[i] > 24) activePixels++;
            }

            if (activePixels == 0)
            {
                errors.Add(string.Format(
                    "{0} 的有效区域为空（alpha 全部接近 0）：{1}", role, assetPath));
                return null;
            }

            if (needsAlpha)
            {
                float opaque = layer.OpaqueRatio();
                if (opaque > 0.97f)
                {
                    errors.Add(string.Format(
                        "{0} 有 alpha 通道但几乎整幅不透明（{1:P1}），看起来没有抠图：{2}",
                        role, opaque, assetPath));
                    return null;
                }
            }

            float coverage = activePixels / (float)layer.alpha.Length;
            if (coverage < 0.002f)
            {
                warnings.Add(string.Format("{0} 有效像素占比很低（{1:P2}）。", role, coverage));
            }

            return layer;
        }

        /// <summary>
        /// 写入适合本实验的导入设置：不做压缩 / 不生成 mipmap / 双线性 / Clamp / 可读 /
        /// alphaIsTransparency（Unity 会做颜色扩张，避免半透明边缘出现白边或黑边）。
        /// </summary>
        public static void ApplyImportSettings(string assetPath, bool needsAlpha, bool force = false)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool changed = false;

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }
            if (importer.textureShape != TextureImporterShape.Texture2D)
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                changed = true;
            }
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }
            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (importer.crunchedCompression)
            {
                importer.crunchedCompression = false;
                changed = true;
            }
            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                changed = true;
            }
            if (importer.maxTextureSize < 2048)
            {
                importer.maxTextureSize = 2048;
                changed = true;
            }
            if (needsAlpha)
            {
                if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
                {
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    changed = true;
                }
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }
            }

            if (changed || force)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
