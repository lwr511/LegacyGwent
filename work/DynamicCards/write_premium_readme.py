from pathlib import Path
import json
r=Path(__file__).resolve().parent
client=r.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
audit=json.loads((r/'premium_structure_audit.json').read_text())
coverage=json.loads((r/'client_card_coverage.json').read_text())
assert audit['cards']==1995 and not audit['issues']
assert coverage['covered']==650 and coverage['sourceVariants']==1995
delivery_path=r/'installed_premium_cache.json'
delivery=json.loads(delivery_path.read_text()) if delivery_path.exists() else None
delivery_note=('完整运行包已安装：'+str(delivery['payloadFiles'])+' 个发布文件，'+format(delivery['bytes']/1e9,'.2f')+' GB（'+format(delivery['bytes']/1024**3,'.2f')+' GiB）。`premium_full_bundle.log` 验证全部 1,995 场景；`premium_full_queue.log` 验证完整包的队列、预览暂停、跨包释放、加载途中取消和重载；`geralt_full_partition_timeline.log` 验证完整包中的杰洛特入场、切割、循环和重播。') if delivery else '完整包尚未安装，以 DELIVERY_STATUS.md 为准。'
text=f'''# 动态卡模块

游戏内设置“动态卡”开启后，有对应素材的卡牌按页面显示顺序逐张替换；没有对应素材或关闭设置时保留普通卡。无需运行额外的性能测试场景。工具栏 Login 按钮可从登录入口开始运行。

## 内容与覆盖范围

三个本地来源的场景均保留：王权之陨落 255、旧版 Gwent 461、新目录 GWENT 1,279，共 1,995 个源场景版本。这里包含同一卡牌的不同版本，不能视为 1,995 张独立卡牌。现有项目 671 个卡图 ID 中有 650 个可靠对应，另 21 个保持普通卡。项目尚未使用的场景也在目录中保留。

`Content/catalog.json` 保存对应关系。素材分别位于 `Content` 的原有数字目录、`Content/Legacy2017` 和 `Content/Latest`；通用依赖放在 `Content/CompatibilityDependencies`。模型、纹理、动画、音频、粒子和兼容脚本均隔离在本模块。来源盘点、逐卡对应和未匹配列表见 `work/DynamicCards/client_source_scene_inventory.csv`、`client_project_card_coverage.csv` 和 `client_card_coverage.json`。

杰洛特 11210300、伊格尼 11210200 优先使用已验证的 Native 版本；金龙、兰伯特、休普、终末之战使用 Latest 版本，具体记录在 `preferred_card_sources.json`。可靠图像匹配及同一原始 Template.Id 的 ArtId 关系用于补齐映射，不以相似编号任意配对。

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

全量结构检查：{audit['cards']} 个场景，{audit['particles']} 个粒子系统，{audit['animators']} 个 Animator，问题数 {len(audit['issues'])}（`premium_structure_audit.json`）。另有 {audit.get('sourceEmptyMaterialSlots',0)} 个槽位经原始场景确认本来就是空材质，单独计数；未启用的粒子拖尾槽位不视为丢失材质。741 个迁移着色器通过支持性和编译错误检查（`premium_shader_audit.log`）。

12 个重点版本做了连续画面检查。`geralt_timeline_final.log` 验证真实运行组件的完整水鬼、切割、持久循环和重播；`focused_partition_player.log` 在独立运行版验证 60 个请求的顺序、预览优先、暂停后台动画、离屏释放、跨包隔离、加载途中取消及资源包重载。该小规模测试按每包 4 个场景构建，以覆盖跨包情况；正式默认最多 32 个。`focused_partition_stage.log` 验证构建开关关闭/开启、全部分包随程序发布及原文件恢复。

{delivery_note}

这些证据不等于逐帧验收全部 1,995 个版本，也不等于完整游戏或手机真机的性能保证。部分源控制器条件转场、AvatarMask、专有脚本和音频事件仍需按卡牌继续核对。兰伯特的部分黑色前景在直接加载原始 Unity 2022 场景时同样存在（`OriginalLambert`），该对照没有包含原游戏完整收藏 UI。旧版 12230611 的 mesh_middle 第 114 根骨骼在源文件中本就缺失，按原始绑定矩阵恢复了保底姿势，不能声称恢复了该骨骼的独立动画（`source_bindpose_fallback.txt`）。Latest 15760101 则从原始 Avatar 默认姿势补回缺失骨骼并重新绑定动画。

转换与审计工具在 `work/DynamicCards`，不属于发布资源。源游戏安装目录未修改。着色器转译使用 HLSLDecompiler 及原始 Unity 参数表；重新转换应保留源导出及还原记录，避免覆盖已验证的映射与材质状态。
'''
(client/'Assets/DynamicCards/README.md').write_text(text,encoding='utf8')
print('PREMIUM_README_UPDATED')
