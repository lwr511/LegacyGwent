using UnityEditor;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 别名入口。真正的实现位于 LegacyGwent.LadyLakeLab.Editor.LadyLakeLabBuilder。
    /// 这里同时提供不带 .Editor 的命名空间入口，便于两种 -executeMethod 写法都能工作：
    ///   -executeMethod LegacyGwent.LadyLakeLab.Editor.LadyLakeLabBuilder.BuildAndCapture
    ///   -executeMethod LegacyGwent.LadyLakeLab.LadyLakeLabBuilder.BuildAndCapture
    /// </summary>
    public static class LadyLakeLabBuilder
    {
        /// <summary>生成场景 + 写 validation.json。</summary>
        public static void Build()
        {
            Editor.LadyLakeLabBuilder.Build();
        }

        /// <summary>生成场景 + 静态取样截图 + validation.json。</summary>
        public static void BuildAndCapture()
        {
            Editor.LadyLakeLabBuilder.BuildAndCapture();
        }

        /// <summary>打开已生成的场景。</summary>
        public static void OpenScene()
        {
            Editor.LadyLakeLabBuilder.OpenScene();
        }

        /// <summary>只对当前场景取样。</summary>
        public static void Capture()
        {
            Editor.LadyLakeLabCapture.CaptureFromOpenScene();
        }
    }
}
