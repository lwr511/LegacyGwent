# 旧客户端金银铜筛选控件

收藏页使用 `Assets/Resources/LegacyCollectionFilters/TierTemplate.prefab`，来源是旧客户端 `gui/prefabs/deckbuilder_base` 的 `Filters/Center/TierContainer/TierTemplate`。

- 图标：`db_filter_tier_all / gold / silver / bronze`。
- 原背景、边框和状态：`btn_filter_idle / hovered / down / bg / bg_hovered / toggle_frame`。
- 贴图来自原客户端 `gui/spriteatlases/high/deckbuilder`、`filterbuttons` 和 `buttons`；按图集坐标还原，没有重新绘制。
- Prefab 保留原模板的六个图层、锚点、图标比例、背景色、Sprite border 和 50×40 尺寸。金银铜实例统一放大到 1.4 倍，内部图标再缩至 80% 给边框留出空间；与普通／闪卡／全部／已拥有共同放在右侧两行四列的固定图标区，不显示文字。
- `LegacyTierFilterButton` 仅用当前项目的 UGUI Button 接入原来的悬停、按下、选中图层。筛选仍由 `EditorInfo.SetShowGroup` 负责，金卡包含金框领袖。

本次导出的原层级、贴图 pathID / SHA-256、导入脚本及运行验证存放于 `work/CollectionReadability-20260919`。详情页文字和小卡战力修复不属于此次原版控件替换。
