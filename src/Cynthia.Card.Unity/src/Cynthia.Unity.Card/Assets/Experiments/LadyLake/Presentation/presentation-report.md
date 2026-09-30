# LadyLake 卡牌外观与交互返工报告（Presentation 适配层）

> **路径说明**：本报告按任务要求应位于 `work/LadyLakeLab/presentation-report.md`。
> 本次会话的文件沙箱只允许写入工程目录 `Assets/Experiments/LadyLake/Presentation/**`，
> 对 `work/` 的写入被拒绝，且升级到全盘写入"需要审批但当前没有可用审批通道"，因此先落在本目录。
> 请把本文件复制/移动到 `C:\UnityProjects\LegacyGwent\work\LadyLakeLab\presentation-report.md`。

范围：`Assets/Experiments/LadyLake/Presentation/**`（新增）。
未修改任何既有实验脚本（Builder / Runtime / Acceptance / Textures / Shaders / README），未修改生产 `Assets/DynamicCards/**`。

---

## 1. 结论

原来那张「无框全屏画面」已改为**真实卡牌收藏里的完整金色中立牌**：

- 牌框、阵营徽章、力量数字、名称与规则文本框全部来自收藏实际使用的 prefab
  `Assets/Resources/Prefab/Cards/ArtCard.prefab`（GUID `38d2279b189875d41b38948e516f872a`，Resources 路径 `Prefab/Cards/ArtCard`）**及其序列化 sprite**，
  没有任何自绘金边、没有近似色块。
- 实验场景的 2.5D 浮雕（含剑、水、焦散、气泡）实时渲染进卡面中央的卡图窗口；原来铺满全屏的实验相机被接管为 RenderTexture 输出。
- 交互与收藏一致：**入场 55° 摆动 → 空闲自动摆动 → 鼠标在卡面按下拖动转向（整卡倾斜 + 浮雕视差）→ 松手回正 → 延迟后恢复空闲摆动**。
- 整卡转向/透视除法直接使用生产组件 `DynamicCardPresentation` + `DynamicCardPerspectiveMesh`；摆动与拖动数值逐行照搬 `DynamicCardView`，没有另造一套手感参数。

---

## 2. 复用的真实收藏资产（路径 / GUID）

| 用途 | 资产 | GUID |
| --- | --- | --- |
| 完整卡牌 prefab（金色中立牌来源） | `Assets/Resources/Prefab/Cards/ArtCard.prefab` | `38d2279b189875d41b38948e516f872a` |
| 金色边框 sprite（`ArtCard.GoldBorder`） | `Assets/Resources/Sprites/GameCardInfo/ShowGoldBorder.png` | `6536616bc9c05f34ab78086900b3ae7a` |
| 中立金色阵营图标（`ArtCard.NeutralGoldIcon`） | `Assets/Resources/Sprites/GameCardInfo/NeutralShow2.png` | `6f8694de6f35cb741ad6d90967f2bd85` |
| 银框 / 铜框（同 prefab 备用） | `.../ShowSilverBorder.png` / `.../ShowCopperBorder.png` | `ff4ae83b6085b644cbffb1730d66a0c5` / `f1056ff9842ec624aa8ec855db32a9e5` |
| 名称 / 规则文本框（CardContext，prefab 内嵌套实例） | `Assets/Resources/Prefab/Cards/CardContext.prefab` | `548fb9f1d1acf745ad0fa8f0d929ad7` |
| 卡面显示着色器（与收藏同一支） | `Assets/DynamicCards/Resources/DynamicCardSurface.shader` | `ed4b5e385cd39034dbc1ec4ef435a191` |

`ArtCard.prefab` 本身就是按 Gold + Neutral 授权的（其 `CardBorder.sprite` = `ShowGoldBorder`，`FactionIcon.sprite` = `NeutralShow2`），
适配层仍然显式重设这两处，避免以后 prefab 默认值变化时牌面走样。

---

## 3. 复用的生产逻辑（文件:行号，逐条对应）

### 3.1 整卡转向 / 透视 / 入场摆动 —— 直接使用生产组件

| 行为 | 生产位置 | 适配层调用 |
| --- | --- | --- |
| 加挂并配置转向组件（把卡框/卡面/徽章移进旋转枢轴，文本框留在外面不倾斜） | `Assets/Script/Card/ArtCard.cs:66-68` | `LadyLakeCardStage.ConfigurePresentation()` |
| 入场曲线：55°、约 1 秒 | `DynamicCardPresentation.cs:24-29, 111` | `RestartSway()` → `presentation.Reveal(true)` |
| 稳态角度上限 `MaxPitch=1.5` / `MaxYaw=7` | `DynamicCardPresentation.cs:21-22, 111` | `presentation.Tick(current)` |
| 35° 相机投影除法（整卡含边框一起透视） | `DynamicCardPresentation.cs:55-57`、`DynamicCardPerspectiveMesh.cs:12-29` | 新卡面 `RawImage` 通过 `presentation.RegisterGraphic(surface)` 登记，与 `DynamicCardView.cs:312` 同一做法 |

### 3.2 摆动 / 拖动驱动 —— 照搬常量，不发明参数

| 参数 / 公式 | 生产位置 | 适配层 |
| --- | --- | --- |
| 归一化极限 `.7` / `1/3` | `DynamicCardView.cs:13-14` | `HorizontalLimit` / `VerticalLimit` |
| 空闲摆动 `sin(t*.47)*H, sin(t*.73)*V` + `SmoothStep` 渐入 | `DynamicCardView.cs:393-399` | `LateUpdate()` 同式 |
| 渐入时机：入场后 1s、松手后 2s | `DynamicCardView.cs:396` | `IdleBlendStart` / `ReleaseBlendStart` |
| 跟随 `Lerp(..., 1-exp(-(drag?18:12)*dt))` | `DynamicCardView.cs:400` | `DragLerp=18` / `IdleLerp=12` |
| 拖动灵敏度 `(-dx/(150*ref), +dy/(120*ref))`，`ref=max(1,Screen.height)/900` | `DynamicCardView.cs:486-490` | `DragTo()` 同式 |
| 松手回正 `target=0`、记录 `releasedAt` | `DynamicCardView.cs:492-493` | `ReturnToCenter()` |
| `age += Time.unscaledDeltaTime` | `DynamicCardView.cs:392` | `LateUpdate()` |
| 浮雕 pitch/yaw = `Lerp(xStart,xEnd,(y+1)/2)` / `Lerp(yStart,yEnd,(x+1)/2)` | `DynamicCardView.cs:401-402` | `ArtPitch` / `ArtYaw` |
| 默认转角范围 `-6,6,-2,2` | `DynamicCardCatalog.cs:43`（`DynamicCardEntry` 默认值，`pipeline-research.md:185` 亦记录） | `ArtPitchStart/End`、`ArtYawStart/End` |
| 指针五件套（Down/Up/BeginDrag/Drag/EndDrag + OnDisable 回正） | `DynamicCardDragHandle.cs:6-15` | `LadyLakeCardDragHandle` 一对一镜像 |
| 卡面锚点矩形 `(0, 1-713/1024) .. (497/1024, 1)`、`CardImg.raycastTarget=false` / `CardBorder.raycastTarget=true` | `DynamicCardView.cs:313-319`、`ArtCard.cs:69-70` | `CreateArtSurface()`、`ApplyCollectionFrame()` |

### 3.3 金色中立牌面的取用分支

`LadyLakeCardStage.ApplyCollectionFrame` / `ApplyCollectionContext` 对应：

- `ArtCard.SetCard()`（`Assets/Script/Card/ArtCard.cs:64-181`）：`Gold/Leader → GoldBorder`（:117-118）、`Neutral → NeutralGoldIcon`（:135-136）、力量文本与徽章显隐（:159-178）。
- `CardShowInfo.SetCard()`（`Assets/Script/Card/NewCard/CardShowInfo.cs:163-281`）：同一套边框（:223-228）与阵营图标（:230-241）分支。
- `CardContent.SetCard()`（`Assets/Script/Card/CardContent.cs:60-92`）：中立头/底纹、名称与规则文本、文本框高度 `preferredHeight + 115`（:91）。

牌面数据取自 `GwentMap.CardMap["70006"]`（`Cynthia.Card.Common/GwentGame/GwentMap.cs:11252-11272`）：
**湖中仙女，Gold / Neutral，力量 25，立绘 `c10000000`，规则文本「对自身造成削弱…」。**

### 3.4 为什么不直接复用 `DynamicCardView`

`DynamicCardView` 只会为 catalog 里有 `DynamicCardEntry` 的动态卡加载 3D prefab（`DynamicCardLibrary`），
而 DIY 立绘 `c10000000`（70006）在 `Assets/DynamicCards/Content/catalog.json` 里没有任何动态卡条目
（`pipeline-research.md` 也已记录：导入目录中没有该牌的可直接复用动态场景）。
因此按其任务允许的做法：**精确提取它的驱动**（上表逐行对应），而不是另造参数。
此外 `DynamicCardView.Refresh()` 在 `premiumAllowed=false` 时会直接返回，既不会产生摆动也不会产生拖动，
留着它只会多一个空转组件，所以适配层不创建它。

### 3.5 卡面取景（RT 裁切）

生产把「方形源相机渲染」中的 `ArtRegion`（`DynamicCardFraming.cs:31-37`）映射到卡图窗口；
本实验相机是**直接按竖构图取景**的（`LadyLakeLabConfig.CameraFieldOfView=35`、`CameraDistance=12.4`、
`FrameHeightUnits=7.10`、`FrameWidthUnits=4.97`），所以不用 `ArtRegion`，而是从实验自身相机常量推出
「497×710 有效画面」在 RT 中的边距：

```
halfHeight = tan(35°/2) * 12.4
marginY = (halfHeight - 7.10/2) / (2*halfHeight)
marginX = (halfHeight*rtAspect - 4.97/2) / (2*halfHeight*rtAspect)
```

RT 取 `714 × 1024`（与收藏卡图窗口 497:713 同比，避免拉伸），得到 `uvRect ≈ (0.0442, 0.0460, 0.9115, 0.9080)`。
这样保留原画构图与透视，不缩放、不裁掉画面内容。

---

## 4. 交付文件

| 文件 | 职责 |
| --- | --- |
| `Presentation/Runtime/LadyLakeCardStage.cs` | 展示适配层：装配金框卡面、创建 RT 活动卡面、接管实验相机、驱动摆动/拖动/视差、公开采样与诊断接口 |
| `Presentation/Runtime/LadyLakeCardDragHandle.cs` | 卡面指针转发（镜像 `DynamicCardDragHandle`），并校验「只从卡面命中开始」 |
| `Presentation/Runtime/LadyLakeCardStageBootstrap.cs` | 运行时兜底：场景未挂接时按 Play 自动安装展示层；非实验场景空操作 |
| `Presentation/Editor/LadyLakeCardPresentationBuilder.cs` | `Attach(LadyLakeLabRoot)` + 菜单 + 静态校验 `Verify` + 报告写出 |

写文件全部落在 `Presentation/**`，未触碰既有文件。

---

## 5. 调用点 / 集成方式

**推荐（场景 Builder 末尾一行）：**

```csharp
LadyLakeLabRoot root;
List<LadyLakeLayerTexture> layers;
var report = LadyLakeLabBuilder.BuildScene(out root, out layers);
LadyLakeCardPresentationBuilder.Attach(root);   // <- 新增这一行
```

`Attach` 幂等，重复调用只复用已有对象；会建立并保存这些层级（全部可检查）：

```
LadyLakeLab
├── ArtPivot                  (相机绕画面中心做浮雕视差)
├── PresentationCamera        (只渲染 UI 层，depth 1)
├── CardPresentation          (Canvas: Screen Space - Camera @ planeDistance 5, Scaler 1600x900,
│   ├── Backdrop                  GraphicRaycaster, LadyLakeCardStage)
│   └── CardHost
│       └── ArtCard           (真实收藏 prefab 实例；CardBorder 上挂 LadyLakeCardDragHandle)
└── Camera                    (实验相机，targetTexture = 卡面 RT, cullingMask 去掉 UI 层)
+ EventSystem                 (缺失时创建)
```

其它入口：

- 菜单 `Tools/Lady Lake Lab/Presentation/` → `Attach To Open Scene` / `Rebuild Scene With Presentation` / `Verify Attached`
- 批处理 `-executeMethod LegacyGwent.LadyLakeLab.Presentation.Editor.LadyLakeCardPresentationBuilder.AttachCurrentScene`
- **运行时兜底**：`LadyLakeCardStageBootstrap` 通过 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`
  检测「有 `LadyLakeLabRoot` 但没有 `LadyLakeCardStage`」时自动安装，所以即使没有重跑 Builder，直接按 Play 也能得到完整金框卡牌。

---

## 6. 运行依赖

必需：

- `Resources/Prefab/Cards/ArtCard`（收藏卡牌 prefab）与其 sprite 依赖
- `Resources/DynamicCardSurface`（着色器）
- `GwentMap.CardMap`（客户端静态卡表；`70006`）——**不需要服务器、不需要登录**

**刻意不依赖**：`DependencyResolver`（实验场景没有登录容器，`CardContent.SetCard` 会在这里 NRE，
所以文本框由适配层直接按 `GwentMap` 填写）、`Addressables`（不加载 `c10000000` 位图）、
`PremiumCollectionClient` / `DynamicCardLibrary` / `DynamicCardSettings`（因此与「动态卡画质开关」互不影响）。

`GwentMap` 访问包了 try/catch：即使卡表不可用，牌框与阵营仍按收藏资产装配，只退化为 prefab 自带数值。

---

## 7. 交互与验收接口

- **真实鼠标**：`LadyLakeCardDragHandle` 实现 `IPointerDownHandler / IPointerUpHandler / IBeginDragHandler / IDragHandler / IEndDragHandler`，
  挂在 `CardBorder` 上（收藏里唯一 `raycastTarget=true` 的整卡图元）。命中校验同时接受
  `pointerCurrentRaycast`（Unity 常规管线）与 `pointerPressRaycast`（脚本化 `ExecuteEvents` 驱动）。
- **公开输入接口**：`BeginDragAt(Vector2 screen)`、`DragTo(Vector2 screen)`、`EndDrag()`、`ReturnToCenter()`。
- **采样 / 诊断**：`IsReady`、`IsDragging`、`Current`、`Target`、`ArtPitch/ArtYaw`、`FrameEuler`、
  `CollectionCard`、`RenderTarget`、`Sample()`（结构化快照）、`Describe()`（JSON 一行）。

### 与 `Acceptance/Runtime/LadyLakePresentationProbe.cs` 的对应

| 探针调用 | 提供方 |
| --- | --- |
| `FindObjectOfType<LadyLakeCardStage>()` | `CardPresentation` 上的本组件（Awake 即注册） |
| `stage.IsReady` | 装配完成后为 true |
| `stage.CollectionCard.CardBorder.sprite == .GoldBorder` | 金框来自收藏 sprite |
| `stage.CollectionCard.FactionIcon.sprite == .NeutralGoldIcon` | 中立金色徽章 |
| `stage.Current` / `stage.IsDragging` | 归一化角度 / 拖动状态 |

数值推演（探针把屏幕位移除以 `Screen.height/900` 再送进 `PointerEventData`）：

- 拖 `+(300,100)`：`target = (-300/150, +100/120)` → 夹到 `(-0.7, +0.333)`，
  `0.6s` 后 `1-exp(-18*0.6) ≈ 0.99998` → 满足 `x < -0.65 && y > 0.30`。
- 拖 `+(-300,-100)`：对称地 `(+0.7, -0.333)` → 满足 `x > 0.65 && y < -0.30`。
- 松手 `0.8s` 后：`exp(-12*0.8) ≈ 6.7e-5` → `|Current| < 0.01`。
- 松手 `3s` 后：`idleTime = min(age-1, Δ-2) = 1`，`target ≈ (0.317, 0.222)` → `|Current| > 0.04`。
- 自动移动：`t≈3s` 与 `t≈6s` 的 `Current` 距离约 `0.49` → 满足 `> 0.03`。

---

## 8. 验证记录

### 8.1 源码编译（本机 csc，工程自己的程序集引用集）

用 `work/CollectionInteraction/compile.rsp` 的 378 条 reference（Unity 2019.4 托管程序集 + `Assets/Assemblies/*` + `Cynthia.Card.Common.dll`）：

- 运行时：`LadyLakeCardStage.cs` + `LadyLakeCardDragHandle.cs` + `LadyLakeCardStageBootstrap.cs`
  **+ Codex 的 `LadyLakePresentationProbe.cs`（用它反证契约）** → `0 error`。
- 编辑器：`LadyLakeCardPresentationBuilder.cs`（对 Unity 已编译出的 `Assembly-CSharp.dll` / `Assembly-CSharp-Editor.dll`）→ `0 error`。

命令与响应文件在 `%TEMP%\ladylake-presentation-check\`（未写入仓库）。

### 8.2 Unity 真实编译

`Library/ScriptAssemblies/Assembly-CSharp.dll` 与 `Assembly-CSharp-Editor.dll` 在本次实现后由 Unity 重新编译
（`2026-09-27 22:12:55`），并已包含：

- `Assembly-CSharp`：`LadyLakeCardStage`、`LadyLakeCardDragHandle`、`LadyLakeCardStageBootstrap`、`LadyLakePresentationProbe`
- `Assembly-CSharp-Editor`：`LadyLakeCardPresentationBuilder`、`LadyLakePresentationReport`

即 Unity 编译器已实际接受本适配层（含探针契约）。

### 8.3 未验证（明确边界）

- **真实 PlayMode 视觉与截图**：由 Codex 统一验收（任务规定）。本次没有 Build、没有切场景、没有 Play。
- 8.2 之后又改了一处（`LateUpdate` 的 `animate=false` 静态对照分支），该次改动仅经 8.1 的 csc 校验；
  Unity 尚未收到 refresh 请求重编译（不影响 Play 行为，`animate` 默认为 true）。
- 本报告写出时场景 `LadyLakeLab.unity`（mtime 22:07:25）**尚未跑过 `Attach`**：场景文件里暂时没有展示层级。
  这不影响 Play（运行时兜底会自动安装），但要让层级烘焙进场景、在编辑器里直接可检查，需要跑一次
  `Tools/Lady Lake Lab/Presentation/Attach To Open Scene`（或 Codex 在 Builder 末尾加 `Attach` 后重建）。

---

## 9. 已知限制 / 设计取舍

1. **`DependencyResolver` 不可用** → 卡牌名称与规则文本直接取 `GwentMap`（中文原文），标签行留空（避免显示未翻译的 key）。
   正式游戏里如果需要多语言，可在有容器时改走 `LocalizationService`。
2. **不加载 `c10000000` 静态位图**：卡面窗口由 RT 实时填充，静态位图只作为 prefab 自带的兜底层。
3. **`animate=false`** 是静态对照开关（角度归零并让 55° 入场收敛），仅供诊断，不改变默认行为。
4. **相机保持 `enabled` + `targetTexture`**（不手动 `Render()`）：这样 `Camera.main` 仍然有效，
   现有 `LadyLakeAcceptance` 的相机截图路径不会失效；屏幕画面由 `PresentationCamera` 负责。
5. 展示 Canvas 用 **Screen Space - Camera**（与游戏内 UI 一致，可被相机渲染截图），
   `CanvasScaler` 参考分辨率 `1600×900`；卡片高度取画布高度的 `72%`（`cardHeightFraction`，可调）。
6. Editor 路径用 `PrefabUtility.InstantiatePrefab` 保留卡牌 prefab 链接；运行时兜底用 `Object.Instantiate`，
   两者层级一致，仅链接性不同。
7. 实验相机 `cullingMask` 去掉了 UI 层（layer 5），避免卡框被画进卡面 RT。
