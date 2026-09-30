# 湖女演出方案的原版参考

2026-09-28：实际通过项目 DynamicCardView 同时运行三个原版 prefab，全部加载后每秒采样一次，共 19 帧、跨度 18 秒，覆盖各自至少一个 catalog 循环。Codex 查看了原始画面和每张牌的 8 帧时序对照；DSH 只读追踪资源，不负责视觉判断。本轮仅研究演出方案，没有修改湖女资源或正式 catalog。未做音频试听。

## 实际观察

- **Aeschna 场景，15010100 / art 202105**：生物在水下维持相近姿态，小幅扭动；水面、植物、悬浮物、身上和河床上的水光形成多层运动。catalog 循环约 6.3 秒。Aeschna 是资源内部名，art 音频表的名称为 Glustyworp，这里不据此断言 UI 中文名。
- **席安娜，13680101 / art 202194**：一直持剑，身体转动，持剑手臂带剑改变角度；披风、头发、飞鸟、尘土和日食光共同变化。没有在采样画面中出现重新抓剑。catalog 循环约 10.7 秒。
- **欧特克尔，56180101 / art c10001000**：维持吹奏乐器的动作，人物和船体构图起伏，背景海浪与水沫持续变化。catalog 循环 16 秒。原版并非每张都使用完全相同数量的 Animator，不能把某张卡的组件数量当成共同规范。

![Aeschna 时序](C:/UnityProjects/LegacyGwent/work/LadyLakePerformanceStudy/aeschna-sequence.png)
![席安娜时序](C:/UnityProjects/LegacyGwent/work/LadyLakePerformanceStudy/syanna-sequence.png)
![欧特克尔时序](C:/UnityProjects/LegacyGwent/work/LadyLakePerformanceStudy/otkell-sequence.png)

## 基于湖女原画的建议（尚未实现）

首选“唤醒圣剑”：保持握剑与上方伸手的基本造型，身体缓慢浮动、头发延迟摆动；上手稍微靠近水面，波纹展开，水光增强；光沿剑身符文走到剑尖，持剑手臂小幅抬动；随后亮度、波纹和身体运动自然返回起始状态。建议约 10–12 秒一轮，具体时长待动作预览调整。

其他方向：

- “湖底守剑”：更安静的持续漂浮，剑上的反光偶尔扫过，环境、水草和头发承担主要变化，最利于保留原画。
- “水光成剑”：剑柄始终握在手中，剑身逐渐由水光凝成，发亮后又淡回水光；魔法叙事更强，需要额外的剑身显现/消隐材质和粒子配合。

以上方案仍使用立体网格、骨骼蒙皮、分区贴图和项目原有闪卡播放器。简化的是松手、追踪落剑与重新抓握的接触动作；人物模型和纹理仍须认真制作。

## 证据位置与限制

仓库根目录 `work/LadyLakePerformanceStudy/frame-00.png` 到 `frame-18.png` 为 Unity 原始截图，时序图仅裁剪、缩放、排列这些截图。序列时间从三张卡均加载完毕开始，不代表每张卡的精确 clip 归一化时间。截图可证明姿态、环境和光效变化，不能替代连续视频对运动流畅度的验收。

研究结束后恢复 `PremiumStudySeltkirk/SeltkirkStudy.unity`。本轮没有选定并实施新的湖女演出；方案留待用户选择或修订。
