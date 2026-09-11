# 2026-09-11 火焰细条与矩形色块修复

## 定位与修正

| 卡牌 | 游戏卡图 | 实际动态来源 |
| --- | --- | --- |
| King Henselt / 亨赛特 | 12110300 | Legacy2017 / 12110301 |
| Brouver Hoog / 布罗瓦尔·霍格 | 14110300 | Thronebreaker / 11870100 |

问题来自四张共享火焰序列图的 `TextureImporter.alphaIsTransparency = true`。这个导入选项向透明像素扩散 RGB，而火焰材质使用 `Blend One OneMinusSrcAlpha` 的预乘透明混合；这些本应不可见的扩散颜色会被直接画出来，形成细条与矩形色块。

原客户端回读与当前 PNG 检查排除了贴图错绑。`fire_13x5` 虽然名称如此，实际排列是 10×10；`small_fire_looped10x4` 为 10×4，当前粒子分帧正确。两张卡的火焰粒子尺寸、朝向与源数据一致，因此没有更改粒子、动画或着色器。

修正只关闭四个导入开关。`metadata-change-audit.json` 验证 `.meta` 中仅该字段改变，PNG 文件哈希保持不变，DXT5 Crunch 质量 80、分辨率、色彩空间、mipmap 及其他导入设置均保留。原 `.meta` 保存在本目录 `before-*` 下。

## 验证

- 先在同一帧替换为原始 PNG，条状色块消失；再使用相同压缩与导入参数、只关闭 alphaIsTransparency 的对照纹理，复现相同修正效果。
- 旧包负向检查：亨赛特大火焰 1,048,576 像素中，831,340 个像素与正确导入结果不同，单通道最大差值 170；检查按预期拒绝旧包。见 `baseline-texture-pixel-checks.txt`。
- 新的两个实际分包回读：四张纹理共 7,340,032 像素与关闭扩散后的导入结果完全一致，差异数量与最大差值均为 0。见 `delivery-result.txt`。
- 独立 Unity 2019.4.1f1 使用项目正式运行脚本及重建分包，未替换材质、粒子参数或纹理。两张卡各自然播放 10 秒，采样 4 帧，隐藏后恢复再采样 1 帧，共 10 张实际显示区域截图，见 `Delivered/`。火焰条状色块未再出现。
- 主项目完整构建与缓存清单的最终结果见 `build-result.txt`、`delivery-audit.json`。`verify_delivery.py` 核对四份新 `.meta` 的缓存哈希、全部载荷文件，以及测试分包与主项目分包的 SHA-256 一致性。

这次验证针对两张指定卡及四张共享火焰纹理，不等于全卡视觉验收；未构建独立玩家发行版。

## 换机恢复

从仓库根目录运行 `python work/DynamicCards/repair_fire_texture_import.py` 只读检查；追加 `--apply` 会验证四个固定 GUID、备份原 `.meta`，再关闭这四个导入开关。随后通过 `Tools > Dynamic Cards > Build Options > 仅构建动态卡资源包` 重建。

Git 不跟踪上述原始 Content 与资源包，因此应一并复制匹配的本地素材、`.meta` 与缓存，或者在目标机器执行修复并重建。
