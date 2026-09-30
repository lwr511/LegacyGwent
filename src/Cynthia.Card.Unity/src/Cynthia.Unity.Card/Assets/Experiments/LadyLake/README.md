# 湖中仙女动态卡实验

打开 `LadyLakeLab.unity`，点击 Unity 的普通 **Play** 按钮即可播放。无需启动游戏服务器。菜单 `Tools > Lady Lake Lab > Open Scene` 可重新打开；`Rebuild Scene` 重建资产，`Build And Capture` 重建并输出关键帧。

## 本次制作内容

- 使用当前 DIY 卡图 `c10000000`（70006 / 70011），原卡图和正式动态卡目录不变。
- 背景补全、人物、剑、前景荷叶水草分别成层。人物与植物是带浅纵深的细分网格，人物采用局部手臂蒙皮、头发与呼吸运动；剑有正面、背面和薄侧壁。
- 人物最终使用用户明确选定的完整 `Textures/figure-v5.png`，RGBA 像素与用户选图一致。双手与前臂在同一图层里，不再切除旧手或拼接两张手部图。握剑遮挡层仅复用同一人物贴图和相同网格变形。
- 完整金色中立牌面直接实例化收藏使用的 `ArtCard.prefab`，引用其 `ShowGoldBorder` 与 `NeutralShow2`。整卡入场、透视使用现有 `DynamicCardPresentation` / `DynamicCardPerspectiveMesh`；自动摆动、鼠标左键拖动、松手回正的参数与 `DynamicCardView` 一致。
- 12 秒循环：剑在低处漂浮 → 伸手 → 剑靠近并进入握点 → 手和剑共同抬起、短暂停留发光 → 放低、释放、回到起始状态。
- 水下光束、焦散、水流噪声、前后层气泡、漂浮微尘和随剑的水流分别控制。动作周期与持续流动的水环境分开，防止动作回卷导致水面跳帧。

这是为原画视角制作的 **2.5D 浮雕人物和有厚度的剑**，不是具有完整背面拓扑与手指骨骼的通用 3D 角色。画面中没有被原画提供的区域经过补绘；不是无损恢复了原作者的分层文件。

## 可编辑内容

- `Textures/`：分层图、独立手部修正图、复制的原版水下特效贴图。具体生成提示、补绘范围及原资源路径见 `Textures/PROVENANCE.md`。
- `Generated/Meshes/`、`Generated/Materials/`：实际保存的网格和材质，可在场景中检查。
- 场景 `Timeline`：`duration` 为 11.4 秒，`pause` 为峰值额外停留 0.6 秒，`autoPlay` 控制播放，`intensity` 控制整体特效。
- `Stage/Figure`、`Stage/HandOverlay`、`Stage/Sword`、`Stage/Foreground`：独立层级，`HandOverlay` 只是同一完整人物的手指遮挡副本。
- `CardPresentation`：收藏卡牌实例、显示比例和拖动驱动。`ArtPivot` 将相同输入传递给画内视差。原实验相机的独立自动横移已关闭，避免叠加两套摆动。

## 从现有闪卡学习和复用

研究了 Aeschna `15010100` 的河床、焦散、水面、角色、前景植物及前后层气泡结构，Tempest `16550101` 的流场材质，以及 Seltkirk `20161801` 的腕部剑光挂接。焦散和水流噪声实际来自本项目已经提取的 Aeschna 相关资源；本实验的动画曲线、人物网格、剑厚度、光束和粒子调度为新制作。

当前导入目录中没有发现可直接用于此自选画面的 Lady of the Lake 动态场景；这不等于断言原游戏从未有过该牌的闪卡。完整研究与逐项路径核对记录：仓库根 `work/LadyLakeLab/pipeline-research.md`。

## 验证与范围

`BuildAndCapture` 使用与真实播放相同的 `SampleAt` 入口，输出多个关键帧和 `validation.json`。独立验收脚本另行进入真实 Play Mode，采集至少 27 秒运行画面与错误记录，结果在仓库根 `work/LadyLakeLab/`。静态截图检查、实际播放检查分别记录，不互相替代。

本实验没有接入正式卡牌目录，也没有构建、发布新客户端或做移动设备性能验收。工程启动时存在旧 `Cynthia.Card.Common.dll` 与当前源码不一致的编译错误；为运行实验，已用当前 Common 源码重建本地 DLL，旧文件备份于 `work/LadyLakeLab/BaselineAssembly/`，未修改业务源码。
