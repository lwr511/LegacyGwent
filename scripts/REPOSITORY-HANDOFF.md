# 仓库换机与本地文件（2026-09-12）

## 本次提交的范围

保存动态卡材质实例绑定、目录映射、原始粒子/闪电/蜡烛行为、相机后处理代码和 Shader，以及本地服务脚本、历史修复工具与续接说明。这是开发状态快照，不是新的客户端发布，也不表示闪卡验收完成。

最新进度以 `work/DynamicCards/本周闪卡异常与续接记录-2026-09-11.md` 为准。该记录中的“未提交、未暂存”描述的是 9 月 11 日收尾时点；本次已整理提交。89 条目主界面复测和新 Windows 客户端构建仍未完成。

`work/DynamicCards` 中的工具保留了历史运行方式，部分使用原机器绝对路径、旧进程编号或依赖忽略的数据文件。它们用于追溯和后续维护，不是可以直接依次重跑的安装脚本；全量旧导入器可能覆盖后续修复。主工程的两个 `CompleteMotion` Editor 工具仍保留，按原续接计划在下一次验收后处理。

## 通过局域网复制

1. 关闭 Unity，暂停 Git 操作；复制整个 `LegacyGwent`，包含隐藏的 `.git`。
2. GitHub Desktop 使用 **File > Add Local Repository**，选择复制后的仓库根目录。
3. Unity Hub 选择其中的 `src/Cynthia.Card.Unity/src/Cynthia.Unity.Card`，使用项目指定的 Unity 版本。
4. 保留已经准备的外部资源和本地依赖；新电脑导入、编译及实际运行仍需另行确认。

## Git 干净不等于删除本地资源

以下文件保留在磁盘，通过忽略规则排除，不上传到源码仓库：

- `Assets/DynamicCards/Content` 的提取资源（目录映射除外）。
- Unity `Library`、动态卡资源包、`Assets/StreamingAssets` 的生成载荷和 Addressables 构建状态。
- `Builds` 中的客户端、日志、隔离工程、修复备份及生成的验证数据。
- `work/LocalServer` 的数据库、MongoDB、.NET SDK 和下载文件；源码脚本及 SDK 版本配置单独跟踪。
- 误将仓库根目录作为 Unity 工程打开时产生的根级 `Packages`、`ProjectSettings`、`Library` 和 `assets/*.meta`。

本次整理前已将未提交差异和恢复前的本地化、字体、DOTween、包锁、GraphicsSettings 文件备份到 `.git/commit-preparation/20260912-024734/`。这些无关运行/编辑器改动已恢复到提交前的 HEAD；备份不上传，完整复制 `.git` 时会随同带走。
