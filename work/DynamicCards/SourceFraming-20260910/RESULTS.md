# 动态卡取景标准核对与修正（2026-09-10）

## 结论

三个已安装客户端的原始 CardRTRenderer 都把动态画面挂在 `Card/Appereance` 节点，位置为 `(0,2,0)`；渲染相机起始位置为 `(0,0,-30)`，默认视野角度为 `25.61000061°`。原客户端的 `CameraValuesChanger.BindCamera` 保留相机 X/Y，只覆盖 Z、FOV 和裁剪面。当前导入运行时漏掉了这层 Y=2 的挂载节点，导致画面整体偏低。此前用画面百分比和逐牌补偿调整构图，不能等价恢复不同深度、不同 FOV 的原始投影。

完整牌面现已恢复原挂载关系；取消完整牌面和拖动时的经验投影补偿。大卡与普通完整小卡采用同一原始取景基准。横向牌组缩略条保留原有独立相机/裁切行为。

## 原始来源

| 客户端 | 原始资源位置 | 已核实的设置 |
|---|---|---|
| 旧版 | `E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/sharedassets2.assets` | Camera transform 239；Appereance transform 274，Y=2；CardRTRenderer 挂载引用及 UseAppearance/BindCamera IL |
| 王权的陨落 | `C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/sharedassets3.assets` | Camera Y=0、Z=-30；Appereance Y=2 |
| 新版 | `C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/dependencies/shared/global` | Camera Y=0、Z=-30；Appereance Y=2；CardRTRenderer 的挂载引用 |

可复核的导出记录：[旧版节点](old-renderer-hierarchy.json)、[王权节点](Thronebreaker-renderer-hierarchy.json)、[新版节点](latest-renderer-hierarchy.json)、[旧版调用链](old-render-chain.il.txt)。三个游戏安装目录均只读。

## 统一裁切如何计算

旧版 `cardassets/prefabs` 内的 `Card_Default/ElementName_ImagePlane`：网格半宽 `9.903009305`、半高 `14.234063148`，缩放 `0.47`，位置 `(0.1,-2.079999924,0)`。加上父节点 Y=2，再按默认相机 Z=-30、FOV=25.61 投影，得到完整原画 UV：

`Rect(0.16602942, 0.0035620682, 0.68260696, 0.98114324)`

这取代了此前带逐牌 topMargin 的经验裁切。[网格导出](old-meshes.txt)和[静态图/原裁切/来源裁切对比](20153900-sourcecrop.jpg)保留了依据。

## 全量数据核对

- 669 个已映射原画 ID 对应 663 个动态场景。
- 660 个场景有 CameraValuesChanger；其导入相机距离、FOV 与对应来源一致。
- 另外 3 个场景（13240100、15560101、19220101）没有这个组件，应该继承原始渲染器默认值。运行时已修正其完整牌面的 FOV=25.61、Z=-30；裁剪面继承各自版本的默认值。
- 139 个当前使用较新素材的原画在旧版中也有动态来源。其中 138 个的 FOV、距离与旧版一致；剩余一个对应上述默认相机场景。核对镜头以旧数据优先，现有贴图和动画资源未重新替换。不同版本的裁剪面与效果资源仍按实际素材来源处理。
- 数据记录：[所有来源相机](all-source-cameras.json)、[旧版优先核对](legacy-priority-audit.json)。

## 本轮实际验证

隔离 Unity 2019.4 项目中完成：

- 11 个原画，完整大小卡、横向缩略条、静止、四角拖动、回正，共 198 次采样。其中完整牌面及拖动为 132 次采样。
- 保留横向缩略条原行为后，11 个原画的三种展示方式再次验证，共 33 次。
- 3 个默认相机场景的三种展示方式补测，共 9 次。
- 总计 14 个不同原画进行了针对性抽查。所有运行完成报告均成功写出，最终测试脚本编译未报错。
- 人工查看了静态/动态构图对比；药膏顶部原来的有色露底条已消失，完整牌面构图明显接近静态参考。[药膏原问题对比](20153900-compare.jpg)、[其他样本](baseline-rest-sheet.jpg)、[默认相机样本](default-source-sheet.jpg)。

透明边缘计数只作为线索，不能据此宣称所有牌视觉正确。202283 在修改前后都存在明显模型变形，已记录为独立的模型/骨骼导入问题，本次相机修正没有解决它。没有声称全 669 原画完成视觉验收。

## 落地范围与生效状态

本轮只修改主项目的 `DynamicCardFraming.cs` 和 `DynamicCardView.cs`；此前已有其他修改均保留。未改 catalog、未重建素材包、未提交 Git。旧的完整牌面补偿字段仍保留在 catalog 作为历史数据，但本轮完整牌面代码不再使用它们。原文件备份位于 `main-before/`，应用前后哈希见 [applied-files.json](applied-files.json)。`git diff --check` 通过。

隔离项目的运行验证已完成。主编辑器还没有确认刷新并重编译；当前打开的主编辑器仍可能运行旧代码。退出播放后在 Unity 执行 Assets → Refresh，等待编译完成才会使用本轮代码。素材包未变化，本次不要求重建素材包。

[机器可读验证摘要](verification-summary.json)
