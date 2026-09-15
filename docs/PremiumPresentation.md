# 闪卡预览与详情页

## 行为

- 收藏／组牌右侧闪卡大图持续轻微转动；卡框、战力徽章与卡内真实 3D 视角共用同一组归一化角度。
- 收藏左键单击打开详情；组牌左键仍负责入组，右键打开详情。
- 详情卡按住左键拖动：向右使右边向后，向上使上边向后。卡框左右限制 ±7°，上下 ±1.5°；内部场景继续使用各卡自己的旋转范围。
- 松手或窗口失焦后指数插值回正，约 0.6 秒回到中心；停顿 2 秒后恢复自动轻摆。拖动中保留卡面动画。
- 普通版本静态显示；详情导航、关联卡与返回继续遵守账号闪卡拥有／选用状态。
- 详情按老版左侧信息、右侧大卡布局重建，原背景、分隔线、翻页箭头、滚动条来自本地旧客户端。支持 Esc 关闭及 0.3 秒淡入淡出。

## 参考来源与移植边界

读取的旧客户端为 `E:\Hbackup\FileRecv\Gwent\Gwent\Gwent_Data`，程序集 `Managed/Assembly-CSharp.dll`，界面包 `AssetBundles/gui/prefabs/global_base`、`deckbuilder_base`。

- `PerspectiveParams` / `CardRotationController`：±1.5° pitch、±7° yaw 及鼠标方向。
- `CardPerspectiveHandler` / `RotationObjectController`：外卡角度归一化映射到各卡场景旋转范围。
- `AutorotateCardController`：自动旋转与手动交互、释放后延迟恢复。当前自动轻摆使用平滑正弦轨迹；未找到原客户端序列化的自动曲线，不能声称逐帧复现该曲线。
- `SidePreviewCardAnimationSettings`：保留既有 55°、1 秒入场与原关键帧曲线。
- `UICardPreview` / `UICardPreviewPrefab`：1600×900 基准、35° 视角、0.3 秒页面淡入淡出；信息区中心 (-270,0)、卡图区中心 (407,-16)。
- `UICardPreviewPrefab` path ID `8091872733596762827`。文字矩形、布局组 padding/spacing、字号、颜色导入 `Assets/Resources/LegacyCardPreview/DetailsLayout.json`。英文使用项目已有 HalisGR / PFDin 字体，中文沿用项目字体回退。关联卡适配现有数据和 ScrollRect。
- 图集素材：`challenges_background`、`popup_divider`、`preview_arrow_inactive`、`scrollbar_middle`、`scroll_bar_sellector`。提取后的 PNG 与 Unity 导入设置置于同一资源目录。

移植到当前 UGUI，复用原卡牌字段和业务。Overlay Canvas 增加投影网格以让倾斜具有透视缩短；没有把整个原客户端 UI 程序集引入当前工程。尚无原客户端同卡、同分辨率运行截图用于像素差比较，因此这里记录的是原资源与布局数据复用，并非像素级一致性的验收结论。

## 验证入口

`Assets/Editor/PremiumPresentationVerification.cs` 仅编辑器编译，仅允许本地 `premium-ui-test` 账号。先用既有 `PremiumCraftingVerification` 的 `cn` 请求登录本地测试账号并进入收藏；再向 `work/PremiumPresentation/check.request` 原子写入 `contracts`。

验证实际 3D 模型、悬停卡框／内部视角变化、卡图射线命中、四向拖动与回正，以及详情导航、关联卡返回和淡出关闭。`native` 请求只采样当前详情页的真实输入与运行截图，便于外部鼠标操作核验。

结果与截图位于 `work/PremiumPresentation`。仅验证本地 Unity Play 模式；本次没有构建或发布新的 Windows 客户端。

2026-09-13 实测：Unity 2019.4.1f1 脚本编译完成；`contracts` 的 23 项检查全部通过（卡牌 24003，远古小雾妖）；真实鼠标向右／向上拖动各一次，运行采样记录了按住状态及随后回正。录像 `premium-drag.mp4` 来自实际 Game 画面，使用采样时间还原帧间隔；`native-input.json` 保存 720 个输入／角度样本。`git diff --check` 通过。

真实输入采样的两次松手到中心耗时分别约 0.34 秒、0.38 秒；另以实际 Esc 关闭详情、左键单击收藏中的闪卡重新打开详情，画面核验通过。

测试准备流程直接离开登录场景时，既有 `GameInit.LoadServerMessage` 异步回调仍可能记录已销毁登录控件的 MissingReferenceException。本轮详情交互断言全部通过，不将其表述为整个工程 Console 无错误。
