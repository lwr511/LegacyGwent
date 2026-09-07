# 动态卡模块

游戏内设置“动态卡”开启后，有对应素材的卡牌按页面显示顺序逐张替换；没有对应素材或关闭设置时保留普通卡。无需运行额外的性能测试场景。工具栏 Login 按钮可从登录入口开始运行。

## 内容与覆盖范围

2026-09-07 起仅保留新版 GWENT 的 1,279 个场景，每个源场景 ID 和每个卡图 ID 在目录中都只出现一次。现有项目 671 个卡图 ID 中有 618 个可靠对应，另 53 个保持普通卡；其中 32 个此前依靠旧版素材显示动态卡。新版中项目尚未使用的场景也保留，以便以后添加卡牌。

`Content/catalog.json` 保存唯一对应关系。模型、纹理、动画、音频及粒子素材集中在 `Content/Latest`；共享贴图和声音位于其子目录。新版依赖闭包不需要旧版 Content 目录；旧版卡场景和未引用素材已从正式 Content 移除。当前整理记录在 `work/DynamicCards/LatestOnly`，早期三来源盘点文件仅作历史记录。

杰洛特 11210300、伊格尼 11210200 等也统一使用新版映射，不再保留 Native 优先例外。匹配沿用已有可靠图像匹配及 Template.Id 证据，不用相似编号猜测缺失卡牌。打包器拒绝旧来源场景或重复映射，附加音频也只从 Latest 目录收集。

## 动画与画面

原始压缩曲线经解码后重建控制器层、默认状态、速度和无条件转场，保留入场、切割和循环时序。蒙皮使用四骨骼权重，场景偏移放在动画根节点之外。材质恢复源渲染队列、纹理通道和常量寄存器绑定；粒子保留源组件和阶段参数。长特效曲线不再受旧采样帧数上限限制。

大卡支持有限角度拖动、卡框同步旋转、松手回正，上下范围维持源范围的一半。声音遵循游戏音效开关；文件保留与运行时事件还原是不同层次，尚未完整复现原游戏的全部音频事件系统。

## 统一构图

所有动态卡使用统一的垂直构图基准，并扣除已有顶部裁切所带来的上移量，避免重复修正；无逐卡 ID 白名单。首帧、大卡、小卡和拖动回正使用同一规则。`camera_alignment_global_final.log` 通过全部 1,995 个版本的投影参数及缩略图构图一致性检查，并保存了 12 张跨来源、跨阵营卡的首帧和循环对比图（`work/DynamicCards/CameraAlignment/GlobalFinal`）。这不等于逐帧验收全部卡牌。

## 加载与释放

页面先显示普通卡，等待布局稳定后按从上到下、从左到右的顺序串行加载。滚动和交互期间延后普通队列，大卡预览优先。预览存在时，后续小卡不再加载，已加载的小卡暂停模型动画、粒子和重绘，保留最后一帧。离屏卡释放实例，返回时重新排队；回收操作避开预览期。每个内容包最多包含 32 个卡牌场景，显示中的卡牌及加载中的请求持有引用；无人使用的包会释放，重新显示时再异步加载。

编辑器优先使用 `Library/DynamicCardsBundles/StandaloneWindows64` 中的异步包：`cards.bundle` 保存目录，`cards.index.json` 指向 `cards-*.bundle`。只有完整生成后才写入 `cards.bundle.editor-ready`。内容或着色器变动会使标记失效；没有有效缓存时回退 AssetDatabase，首次读取仍可能同步停顿。通过 `Tools > Dynamic Cards > Build Options > 仅构建动态卡资源包` 重建缓存，输入未改变的分包会复用，失败后可继续构建。异步加载不能消除 Unity 原生实例化和 GPU 上传的所有主线程开销。

## 可选构建

`Tools > Dynamic Cards > Build Options` 控制是否随游戏打包素材，默认不包含。开启时按目标平台构建 LZ4 分包，将目录和全部分包临时放入 `StreamingAssets/DynamicCards`，玩家构建结束恢复原文件。关闭时不随玩家程序带入这些动态包。运行时设置和构建开关独立。

CI 可设置 `LEGACY_GWENT_DYNAMIC_CARDS=1` 或 `0`。程序调用 BuildPipeline.BuildPlayer 前调用 `DynamicCardBuild.PrepareForBuild()`，并在 finally 调用 `RestoreStage()`。不同平台的资源包不能互换。

## 验证边界

以下 1,995 版本及 9.03 GiB 数据是整理前的历史验证，不能作为当前新版唯一目录的验收结果。新版映射及缺失卡图记录位于 `work/DynamicCards/LatestOnly`，最新压缩包结果位于 `work/DynamicCards/Optimization`。此次依赖扫描包含动画曲线，发现四份已存在的 Source 动画有同一个缺失脚本 GUID（`3e080548dacc61344a5969d8317f5acc`）；它不是删除旧目录造成的，尚未在此次资源整理中修复。场景去重不等于所有视觉效果已验收，也不等于跨分包共享依赖已经消除重复。

新版整理验证：正式 Content 恰有 1,279 个 Card.prefab，卡图无重复绑定；19,643 个 prefab/material/controller 引用检查无缺失 GUID 和重复 GUID。Windows 包内场景全为 Latest，总计 43 个发布文件（42 个 bundle 加索引），已替换本机编辑器缓存。隔离 PlayMode 使用新包验证杰洛特、伊格尼、金龙的动态首帧及随后画面变化，并验证旧版独有卡回退静态；这不是全量卡牌视觉验收。证据分别为 `content-result.json`、`client_guid_audit.json`（上一级目录）、`build-result.json`、`runtime-result.json` 和 `delivery-result.json`；完整卡图变更清单是 `card-mapping.csv`。

全量结构检查：1995 个场景，20302 个粒子系统，3493 个 Animator，问题数 0（`premium_structure_audit.json`）。另有 134 个槽位经原始场景确认本来就是空材质，单独计数；未启用的粒子拖尾槽位不视为丢失材质。741 个迁移着色器通过支持性和编译错误检查（`premium_shader_audit.log`）。

12 个重点版本做了连续画面检查。`geralt_timeline_final.log` 验证真实运行组件的完整水鬼、切割、持久循环和重播；`focused_partition_player.log` 在独立运行版验证 60 个请求的顺序、预览优先、暂停后台动画、离屏释放、跨包隔离、加载途中取消及资源包重载。该小规模测试按每包 4 个场景构建，以覆盖跨包情况；正式默认最多 32 个。`focused_partition_stage.log` 验证构建开关关闭/开启、全部分包随程序发布及原文件恢复。

整理前的运行包曾有 66 个发布文件、9.70 GB（9.03 GiB）。`premium_full_bundle.log` 验证当时全部 1,995 场景；`premium_full_queue.log` 验证当时完整包的队列、预览暂停、跨包释放、加载途中取消和重载；`geralt_full_partition_timeline.log` 验证当时完整包中的杰洛特入场、切割、循环和重播。纹理优化前，新版唯一场景包的重建结果为 1,279 场景、7.01 GB（约 6.53 GiB），记录于 `work/DynamicCards/LatestOnly/build-result.json`。

这些证据不等于逐帧验收全部 1,995 个版本，也不等于完整游戏或手机真机的性能保证。部分源控制器条件转场、AvatarMask、专有脚本和音频事件仍需按卡牌继续核对。兰伯特的部分黑色前景在直接加载原始 Unity 2022 场景时同样存在（`OriginalLambert`），该对照没有包含原游戏完整收藏 UI。旧版 12230611 的 mesh_middle 第 114 根骨骼在源文件中本就缺失，按原始绑定矩阵恢复了保底姿势，不能声称恢复了该骨骼的独立动画（`source_bindpose_fallback.txt`）。Latest 15760101 则从原始 Avatar 默认姿势补回缺失骨骼并重新绑定动画。

转换与审计工具在 `work/DynamicCards`，不属于发布资源。源游戏安装目录未修改。着色器转译使用 HLSLDecompiler 及原始 Unity 参数表；重新转换应保留源导出及还原记录，避免覆盖已验证的映射与材质状态。

## 2026-09-07：静态重影与看似停播修复

动态卡使用独立 UI 合成材质：场景内的透明图层已经合成到相机 RGB，最终卡面不再重复使用相机 Alpha 混入底下的静态占位图。保留 UI 颜色、透明度、遮罩和关闭设置后的静态回退；大卡和小卡共用。Shader 放在模块自己的 Resources 中，无需重建动态素材包。

旧版场景的背面停放朝向在没有原版显式启动变换时恢复正面，修复亨赛特 12110300 的模型朝后问题；保留三份原版显式指定的 180 度变换。全量检查了 1,995 个场景根节点。哈罗德 15110300 的源动画在实际素材包中能够播放，本次验证了其大卡、小卡骨骼和渲染画面随时间变化。

回归：Unity 2019.4.1f1 隔离项目加载正式素材包，对艾瑞汀、暗影长者、亨赛特、哈罗德分别验证大卡/小卡，共 8 项通过；静态占位图换色后动态区域差异像素均为 0，动画前后均有骨骼及画面变化，关闭动态设置恢复静态图。详见 work/DynamicCards/PremiumIssues/verification.json 和 premium_regression.log。未逐张运行全部 1,995 个版本。

## 2026-09-07：选中大卡加载优先级

大卡不再等待小卡的 0.25 秒防抖、2 帧布局等待和 0.15 秒队列间隔，使用正常异步加载优先级；已有小卡加载任务会在完成读取后让出显示任务。大卡省去额外的分帧显示等待。小卡列表继续串行、分段加载。

已加载的模型、声音按素材分包缓存，选中同一张小卡时复用资源；分包没有使用者后同时释放缓存，不常驻保留全部卡牌。仍使用异步素材读取，不强制同步磁盘加载。

正式素材包实测：已加载小卡打开大卡 11.8ms，重访 18.0ms；该次测试另一张首次预览 46.1ms、快速切换 85.2ms。数字是本机测试结果，不代表所有首次磁盘读取。60 个请求的排序、分段间隔、预览优先、离屏卸载、分包隔离、取消和重载检查通过。记录位于 work/DynamicCards/PreviewLoading/verification.json。

### 悬停预览入口补齐

收藏页/牌组的 EditorInfo.SelectSwitchUICard 经 ArtCard.SetCard 显示右侧大卡；它与 righclickLogic 的右键查看是不同入口。ArtCard 现显式使用 largePreview=true，接入即时动态首帧加载；保持原先悬停不自动播放音效的行为。此前仅加速 largePreview 队列并未覆盖此调用点。牌组/墓地共用的 ArtCard 同样生效，列表 CardShowInfo 仍走分段加载。

验证计时终点是动态 RawImage 首次启用，不是静态大卡的出现。正式素材包测试暖资源 12.3ms、重访 16.3ms，并检查快速切换后最终卡 ID、关闭设置回退及无悬停音效。详见 work/DynamicCards/HoverPreview/verification.json。

## 2026-09-07：动态就绪后显示、原版侧翻与更积极的小卡加载

有动态资源的预览在等待首帧时隐藏整张卡（含框）；完成动态渲染后才整体显示。等待时保留布局和加载资格，仍尊重外层隐藏状态。禁用动态卡或没有对应资源时显示原静态卡，不永久隐藏。ArtCard 悬停入口传入整个展示根节点；右键查看使用已有整卡根节点。

原版 E:/Hbackup/FileRecv/Gwent/Gwent 的 UISidePreviewCard 与 deckbuilder_base 中 SidePreviewCardAnimationSettings 提供了右侧入场的 55 度转角、1 秒回弹曲线；主要转动集中在前段。当前保留该角度及曲线，将原世界坐标移动适配为 UI 相对位移/缩放。框和内容同时旋转，快速换牌丢弃旧请求；拖动会接管未完成的入场动画。原版客户端未进入收藏界面，本次依据本地代码与序列化配置适配，未作原客户端动态画面的逐帧对照。

小卡初始等待由 250ms 缩短为 80ms、布局等待降为 1 帧；卡间间隔由固定 150ms 改为根据平滑帧耗时在 25–100ms 调整。保持逐张加载、优先预览、离屏释放。

Unity 2019.4.1f1 使用正式素材包通过：首帧前隐藏、整卡侧翻回正、快速切换仅最后一张可见、关闭/缺资源回退、外层隐藏、拖动接管，以及 60 请求排序/分段/卸载/取消/重载。详见 work/DynamicCards/PresentationDelivery/verification.json、presentation_final.log 与 adaptive_thumbnail_queue.log。


### 2026-09-07 Preview pivot and stationary description

ArtCard now creates a runtime DynamicCardVisualPivot centred on CardBorder, moves card graphics beneath it, and keeps CardContent outside it. Serialized references and the existing layout root remain intact. DynamicCardPresentation still gates the whole preview until the first dynamic render, but rotates only the visual pivot. Removed the previous arbitrary 6% horizontal translation and shrink. Entry uses the extracted 55-degree, one-second curve. Idle oscillation is a video-based adaptation bounded to the original PerspectiveParams defaults (pitch 1.5 degrees, yaw 7 degrees), not an extracted original idle curve. PreviewEdgeGlow is a lightweight procedural cyan UI outline, not the original particle asset. Static fallback hides the glow and restores rotation.

Evidence: work/DynamicCards/PreviewPivot/test.log has PRESENTATION_PASS from isolated PlayMode using installed card bundles: gate, frame/art rotation, stationary description, stable centre and scale, idle/glow, rapid switching, disabled/missing fallbacks and ancestor visibility. Main client's collection page was not visually exercised in this verification. Original hierarchy read from deckbuilder_base: UISidePreviewCard root -> CardTransform -> CardContainerTransform; CardRotationController modifies CardContainerTransform. Original video contact sheet is in PreviewPivot/contact.jpg.

## Desktop source checkpoint and external content

The source includes the integration, runtime/editor scripts, shaders and latest-only catalog. Extracted models, textures, animations, audio and particle assets in `Assets/DynamicCards/Content/Latest` are local external content (approximately 43.76 GiB after texture consolidation), excluded from Git. Their existing files and Unity .meta GUIDs must be backed up together. The catalog alone cannot recreate them. The earlier commit a852fc646 documented a three-source, approximately 78 GiB content snapshot.

To reproduce this workstation's dynamic cards on another machine, copy the complete current Content directory with its .meta files into the same project path. For editor playback, also copy the matching `Library/DynamicCardsBundles/StandaloneWindows64` (including index and editor-ready marker), or rebuild it through the module's Build Options menu. Do not reuse the old 1,995-scene bundles with the new catalog. Windows player builds can optionally include these bundles. Without the external content/bundles, this source checkpoint is not a complete dynamic-art distribution; static-card fallback remains available.

The unfinished local Android build workspace has been removed and its runtime external-content loader reverted. Desktop evidence above is limited to the stated audits and isolated tests; it does not establish full-game or all-card visual acceptance.

## 2026-09-07：Windows 包体积与小卡加载修复

保留全部 1,279 个新版场景及唯一映射，发布包由 7,011,978,254 字节（6.53 GiB）降到 3,285,218,316 字节（3.06 GiB），减少 53.1%。43 个发布文件均经过读取和场景名单验证。

只合并 PNG 内容及导入设置完全一致的重复贴图：10,024 份减至 5,075 份，材质引用和转换记录同步更新。Windows 常规贴图改用 DXT5 Crunch、质量 80，保留原分辨率上限、mipmap、色彩空间及 alpha 设置，极小常量贴图保持原格式。剩余源 PNG 像素未改写；运行包压缩是有损的。8 张大贴图抽样的最大平均 RGBA 误差为 0.009865，尺寸不变；这不是全量卡牌的视觉验收。动画曲线、模型和粒子参数未为压体积而删减。

编辑器缓存现在正确处理 `.meta` 自身的刷新及新包已不包含的旧文件删除；真实内容或导入设置改动仍使缓存失效。修复了错误失效后退回同步 AssetDatabase 读取的退化。无可用包时会输出明确警告。逐张加载、显示顺序、预览优先及离屏释放策略保持现有行为。

分包复用同时改为依赖文件及 `.meta` 的 SHA256 指纹；按文件大小和修改时间缓存已计算指纹，依赖变更时强制重建该分包，未变更则复用。回归实际验证了未变更包保持不动，以及依赖贴图的导入设置改动会重建并进入最终包。

同一台机器、Unity 2019.4.1f1 隔离 PlayMode、同样 20 张中立金卡的页面：

| 加载路径 | 全部动态首帧就绪 | P95 帧耗时 | 最大帧耗时 |
| --- | ---: | ---: | ---: |
| 缓存失效，直接读取编辑器素材 | 22.52 秒 | 346.5 ms | 9914.1 ms |
| 原有效异步包 | 2.87 秒 | 17.5 ms | 217.7 ms |
| 本次压缩后的有效异步包 | 2.80 秒 | 17.5 ms | 205.7 ms |

每次使用新 Unity 进程，未控制操作系统磁盘缓存；结果不代表所有设备或正式收藏 UI 的保证。新包另外通过杰洛特、伊格尼、金龙的动态画面变化及缺失资源静态回退检查。缓存误失效、依赖变更重建、19,643 个场景/材质/控制器引用检查均通过。详细本地证据在 `work/DynamicCards/Optimization`：`build-result.json`、`page-optimized.json`、`cache-test.json`、`bundle-cache-test.json`、`runtime-result.json`、`final-guid-audit.json`、`delivery-result.json`。

当前源 Content 约 43.76 GiB；这是编辑用模型、动画和源贴图，不能与压缩后的发布包混为一个口径。源码提交仍不包含被忽略的 Content 素材及 Library 资源包；完整素材及其 `.meta` 需要单独备份。本机正式缓存已替换为本次验证的版本。首次编辑器刷新导入设置可能产生一次性导入开销。
