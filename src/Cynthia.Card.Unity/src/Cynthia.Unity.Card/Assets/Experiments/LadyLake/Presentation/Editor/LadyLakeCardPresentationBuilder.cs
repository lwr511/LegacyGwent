using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assets.Script.DynamicCards;
using LegacyGwent.LadyLakeLab.Editor;
using LegacyGwent.LadyLakeLab.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegacyGwent.LadyLakeLab.Presentation.Editor
{
    /// <summary>
    /// 把 LadyLake 实验包装成「卡牌收藏里的完整金色中立牌」的 Editor 入口。
    ///
    /// 场景 Builder 末尾调用一次 <see cref="Attach"/> 即可（Codex 集成）：
    /// <code>
    /// LadyLakeLabRoot root;
    /// List&lt;LadyLakeLayerTexture&gt; layers;
    /// var report = LadyLakeLabBuilder.BuildScene(out root, out layers);
    /// LadyLakeCardPresentationBuilder.Attach(root);   // &lt;- 这一行
    /// </code>
    /// 也可以直接用菜单 <c>Tools/Lady Lake Lab/Presentation/*</c>，或批处理
    /// <c>-executeMethod LegacyGwent.LadyLakeLab.Presentation.Editor.LadyLakeCardPresentationBuilder.AttachCurrentScene</c>。
    ///
    /// 本文件只新增对象与序列化引用，不修改任何现有实验脚本 / 生产 DynamicCards。
    /// </summary>
    public static class LadyLakeCardPresentationBuilder
    {
        public const string MenuRoot = "Tools/Lady Lake Lab/Presentation/";

        public static readonly string ReportPath =
            @"C:\UnityProjects\LegacyGwent\work\LadyLakeLab\presentation-attach.json";

        // ==================================================================
        // 入口
        // ==================================================================
        [MenuItem(MenuRoot + "Attach To Open Scene", false, 10)]
        public static void AttachCurrentScene()
        {
            var root = UnityEngine.Object.FindObjectOfType<LadyLakeLabRoot>();
            if (root == null)
            {
                Debug.LogError("[LadyLakeCardPresentation] 当前场景没有 LadyLakeLabRoot，先打开 LadyLakeLab.unity。");
                return;
            }
            Attach(root);
            Debug.Log("[LadyLakeCardPresentation] 已挂接：" + Verify(root).Summary());
        }

        [MenuItem(MenuRoot + "Rebuild Scene With Presentation", false, 20)]
        public static void RebuildSceneWithPresentation()
        {
            LadyLakeLabRoot root;
            List<LadyLakeLayerTexture> layers;
            // 全名限定：LegacyGwent.LadyLakeLab 下还有一个同名别名类（LadyLakeLabBuilderAlias.cs）。
            LegacyGwent.LadyLakeLab.Editor.LadyLakeLabBuilder.BuildScene(out root, out layers);
            if (root == null)
            {
                Debug.LogError("[LadyLakeCardPresentation] 重建失败，未生成场景。");
                return;
            }
            Attach(root);
            Debug.Log("[LadyLakeCardPresentation] 重建 + 挂接完成：" + Verify(root).Summary());
        }

        [MenuItem(MenuRoot + "Verify Attached", false, 30)]
        public static void VerifyAttached()
        {
            var root = UnityEngine.Object.FindObjectOfType<LadyLakeLabRoot>();
            if (root == null)
            {
                Debug.LogError("[LadyLakeCardPresentation] 当前场景没有 LadyLakeLabRoot。");
                return;
            }
            var report = Verify(root);
            WriteReport(report);
            Debug.Log("[LadyLakeCardPresentation] " + report.Summary() + " -> " + ReportPath);
        }

        // ==================================================================
        // Attach
        // ==================================================================
        /// <summary>
        /// 在实验场景里建立/刷新卡牌展示层。幂等：重复调用只会复用已有对象。
        /// </summary>
        public static void Attach(LadyLakeLabRoot root)
        {
            if (root == null) throw new ArgumentNullException("root");
            Scene scene = root.gameObject.scene;

            LadyLakeCardStage stage = LadyLakeCardStage.Install(root, CreateCollectionCardPrefabInstance);
            if (stage == null)
                throw new InvalidOperationException(
                    "[LadyLakeCardPresentation] 安装卡牌展示层失败（缺实验相机，或 " +
                    "Resources/" + LadyLakeCardStage.CollectionCardPrefab + " 不存在）。");

            EditorUtility.SetDirty(stage);
            // Match collection's static art before Play creates the live render target.
            if (stage.card != null && stage.card.CardImg != null)
            {
                stage.card.CardImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Addressables/Cards/c10000000.png");
                EditorUtility.SetDirty(stage.card.CardImg);
            }
            if (stage.card != null) EditorUtility.SetDirty(stage.card);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene, scene.path);
        }

        /// <summary>
        /// Editor 专用卡牌工厂：用 PrefabUtility 实例化，保留与真实收藏 prefab 的链接。
        /// 运行时兜底（LadyLakeCardStageBootstrap）走 Object.Instantiate，两者结构一致。
        /// </summary>
        private static ArtCard CreateCollectionCardPrefabInstance(Transform parent)
        {
            GameObject prefab = Resources.Load<GameObject>(LadyLakeCardStage.CollectionCardPrefab);
            if (prefab == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = LadyLakeCardStage.CardInstanceName;
            return instance.GetComponent<ArtCard>();
        }

        // ==================================================================
        // 静态校验（不 Play）
        // ==================================================================
        public static LadyLakePresentationReport Verify(LadyLakeLabRoot root)
        {
            var report = new LadyLakePresentationReport();
            report.scenePath = root != null ? root.gameObject.scene.path : null;

            if (root == null) { report.failures.Add("没有 LadyLakeLabRoot"); return report; }

            var stage = root.GetComponentInChildren<LadyLakeCardStage>(true);
            Check(report, stage != null, "CardPresentation 上有 LadyLakeCardStage");
            if (stage == null) return report;

            Check(report, stage.root == root, "stage.root 指向实验根");
            Check(report, stage.sourceCamera != null, "stage.sourceCamera 已接线");
            Check(report, stage.artPivot != null && stage.artPivot.parent == root.transform,
                "ArtPivot 在实验根下（相机绕它视差）");
            Check(report, stage.card != null, "stage.card 指向真实收藏卡牌实例");
            Check(report, stage.cardHost != null, "stage.cardHost 已接线");

            if (stage.card == null) return report;
            ArtCard card = stage.card;
            report.cardPrefabPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(card.gameObject));
            report.borderSpritePath = card.CardBorder != null ? AssetDatabase.GetAssetPath(card.CardBorder.sprite) : null;
            report.factionSpritePath = card.FactionIcon != null ? AssetDatabase.GetAssetPath(card.FactionIcon.sprite) : null;

            Check(report, card.CardBorder != null && card.GoldBorder != null && card.CardBorder.sprite == card.GoldBorder,
                "卡框使用收藏原版 GoldBorder sprite（不是自绘金边）");
            Check(report, card.FactionIcon != null && card.NeutralGoldIcon != null &&
                card.FactionIcon.sprite == card.NeutralGoldIcon, "阵营图标使用收藏原版中立金色图标");
            Check(report, card.CardImg != null && card.CardImg.raycastTarget == false, "CardImg 不参与射线");
            Check(report, card.CardBorder != null && card.CardBorder.raycastTarget, "CardBorder 是卡面命中目标");

            var handle = card.CardBorder != null ? card.CardBorder.GetComponent<LadyLakeCardDragHandle>() : null;
            Check(report, handle != null && handle.View == stage, "CardBorder 上有拖动句柄并指向 stage");

            var presentation = card.GetComponent<DynamicCardPresentation>();
            Check(report, presentation != null, "卡牌上有生产 DynamicCardPresentation（整卡转向 + 透视除法）");

            var canvas = root.GetComponentInChildren<Canvas>(true);
            Check(report, canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                canvas.worldCamera != null, "展示 Canvas 为 Screen Space - Camera 且已接展示相机");
            Check(report, canvas != null && canvas.GetComponent<GraphicRaycaster>() != null, "Canvas 上有 GraphicRaycaster");
            Check(report, canvas != null && canvas.GetComponentInChildren<LadyLakeCardDragHandle>(true) != null,
                "展示层包含可拖动的卡面");
            Check(report, UnityEngine.Object.FindObjectOfType<EventSystem>() != null, "场景里有 EventSystem");
            Check(report, stage.sourceCamera != null &&
                (stage.sourceCamera.cullingMask & (1 << LadyLakeCardStage.UiLayer)) == 0,
                "实验相机不渲染 UI 层（卡面里不会出现卡框）");
            Check(report, CountTextOutsideCard(canvas, card.gameObject) == 0,
                "除卡牌自身文本外没有任何说明文字遮挡画面");

            Rect crop = LadyLakeCardStage.FrameCrop();
            Check(report, crop.width > 0f && crop.height > 0f && crop.width <= 1f && crop.height <= 1f,
                "卡面裁切矩形有效 = " + crop);
            report.uvRect = crop.ToString();
            report.renderSize = LadyLakeCardStage.RenderWidth + "x" + LadyLakeCardStage.RenderHeight;
            return report;
        }

        private static int CountTextOutsideCard(Canvas canvas, GameObject card)
        {
            if (canvas == null) return 0;
            var texts = canvas.GetComponentsInChildren<Text>(true);
            int count = 0;
            foreach (var text in texts)
            {
                if (text == null || string.IsNullOrEmpty(text.text)) continue;
                if (card != null && (text.gameObject == card || text.transform.IsChildOf(card.transform))) continue;
                count++;
            }
            return count;
        }

        private static void Check(LadyLakePresentationReport report, bool ok, string what)
        {
            report.checks.Add((ok ? "PASS  " : "FAIL  ") + what);
            if (!ok) report.failures.Add(what);
        }

        public static void WriteReport(LadyLakePresentationReport report)
        {
            string folder = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }

        // ==================================================================
        // 工具
        // ==================================================================
    }

    /// <summary>挂接 / 静态校验结果。写入 work/LadyLakeLab/presentation-attach.json。</summary>
    [Serializable]
    public sealed class LadyLakePresentationReport
    {
        public string generatedAtUtc;
        public string scenePath;
        public string cardPrefabPath;
        public string borderSpritePath;
        public string factionSpritePath;
        public string uvRect;
        public string renderSize;
        public List<string> checks = new List<string>();
        public List<string> failures = new List<string>();

        public LadyLakePresentationReport()
        {
            generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        public string Summary()
        {
            return string.Format("检查 {0} 项，失败 {1} 项；牌框={2}", checks.Count, failures.Count, borderSpritePath);
        }
    }
}
