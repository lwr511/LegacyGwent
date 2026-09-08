# 2026-09-08 动态卡修正与换机交接

本次代码提交包含旧版资源目录规则、播放/粒子时序、金龙背景运行组件、动态卡加载与显示修正，以及本机/远端登录选择和错误阶段诊断。未重新打 Windows 安装包。

## 必须另行复制的数据

Git 不包含动态卡原始素材和 Unity Library。仅 clone/pull 本提交不能还原本机全部闪卡。
在关闭 Unity 后，从 C:\UnityProjects\LegacyGwent 复制下列目录到新设备仓库的相同相对位置：

- src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content：复制完整目录及 .meta，包含修复后的 Old 素材、动画、控制器、背景 Tiles 和依赖；Git 仅跟踪其中的 catalog.json。
- src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Library/DynamicCardsBundles：复制完整目录，包含 StandaloneWindows64 下所有 bundle、索引、内容哈希和 editor-ready/editor-files 文件。当前 22 个运行载荷文件合计 1,344,434,418 字节（约 1.25 GiB）。这些文件是一套，不要混用旧缓存。
- work/DynamicCards：继续提取、修复或重建时需复制完整工作数据。提交中的 3 个转换脚本依赖本目录的 AnimationData、source_effects 等本地数据，并非独立下载后即可运行；原游戏路径和 Python 依赖路径也需按新设备调整。

使用项目 ProjectVersion.txt 指定的 Unity 版本。素材和缓存移入后，让 Unity 完成导入。若缓存被判定失效，用项目现有动态卡构建入口重新生成；不要只复制部分 bundle。新设备的 EditorPrefs 登录模式和本地数据库不会随 Git 迁移，本机模式需要另行启动本地服务及数据库。

## 已完成的验证

- 全量实际资源包：580 场景、1316 动画片段，循环/时长契约检查通过。
- 修复 115 个循环标记（97 个恢复单次播放、18 个恢复循环），4 个场景同名片段覆盖，601 个粒子事件按源状态时序核对。
- 卡希尔刀光结束后 12 次采样保持最终状态，主体动画继续。
- 验证摘要：work/DynamicCards/TimingRepair/summary.json；完整日志、图片、审计表随上述工作目录另行复制。

本次提交不包括自动改写的本地化文件、字体缓存、包锁定/图形设置变动，以及生成的 Windows 安装包和 Addressables 构建产物。未执行 push；换机前需将提交推送到你的远端仓库，并单独传输上述资源。
