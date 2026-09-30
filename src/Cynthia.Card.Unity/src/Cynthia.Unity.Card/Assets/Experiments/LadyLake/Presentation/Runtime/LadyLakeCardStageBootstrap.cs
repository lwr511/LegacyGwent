using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegacyGwent.LadyLakeLab.Presentation
{
    /// <summary>
    /// 兜底安装：如果打开的实验场景还没有挂接展示层（例如没有重跑 Builder 末尾的
    /// <c>LadyLakeCardPresentationBuilder.Attach</c>），直接按 Unity 的 Play 也能得到完整的
    /// 金色中立卡牌，而不是旧的无框全屏画面。
    ///
    /// 场景里已经有 <see cref="LadyLakeCardStage"/> 时本方法什么都不做，因此在正式游戏流程里
    /// （没有 <see cref="LadyLakeLabRoot"/> 的场景）是空操作，不会影响其它页面。
    /// </summary>
    public static class LadyLakeCardStageBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallIfMissing()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;

            LadyLakeLabRoot root = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                root = go.GetComponentInChildren<LadyLakeLabRoot>(true);
                if (root != null) break;
            }
            if (root == null) return;
            if (root.GetComponentInChildren<LadyLakeCardStage>(true) != null) return;

            LadyLakeCardStage.Install(root);
        }
    }
}
