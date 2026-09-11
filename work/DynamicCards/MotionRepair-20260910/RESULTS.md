# 动态卡修复与验证（2026-09-10）

## 已落地的修改

- 牌组横向条目恢复原始 `_slot` 静态图片；重新绑定时清除旧 DynamicCardView 及动态画面。正常尺寸的小卡继续使用动态卡功能。
- 修正原动画欧拉旋转顺序丢失：堕落的佛兰明妮卡、雷吉斯：高等吸血鬼、掠夺者斥候，共 4 个动画文件、48 条序列化曲线。原始 `customType` 的 XYZ 被误按 ZXY 导入，是佛兰明妮卡严重扭曲的已验证原因。
- 解码器、导入器及三套动画数据契约均保留旋转顺序。722 条欧拉轨道已补齐信息；导入器拒绝缺失顺序的数据。
- 新资源包已于 2026-09-10 05:15:22 UTC 完成构建，缓存 ready 标记已写入。二进制回读确认 4 个修正片段均为预期顺序 0。

## 用户点名问题的实际结果

| 项目 | 证据与边界 |
|---|---|
| 横向牌组条目 | 已修改为原始静态图片，最终主界面断言见 FinalMain/static-rows.txt。 |
| 伊勒瑞斯 | 当前主工程重新加载后双臂可举起并持续运动；36 帧循环、隐藏恢复、快速切换测试未重现截图中的单臂异常。原先的触发原因尚未定位。 |
| 暗算小卡 | 实际卡库小卡持续运动；重新加载及切换测试均有骨骼和像素变化。未重现此前停动。 |
| 乌鸦之母 | 小卡和预览均有角色、翅膀运动；实际界面及隔离测试均已采样。未重现此前停动。 |
| 暴风雨 | 原素材具有前后两个水面材质，两个材质均在执行流动动画；2.35 秒间隔的独立材质采样 RGB 差值分别为 67097 和 222914。山体层静止，水面几何轮廓本身无骨骼动画。此次未修改原始水面效果。 |

快速切换回归包含 3 轮重新载入、48 次预览切换。最终主界面回归另外检查 7 张小卡和选中的大预览。以上不代表全部 669 张卡已经逐张完成视觉验收，也不代表已构建独立游戏发行版。

## 关键记录

- `FinalMain/`：最终主界面截图、静态条目断言、动画采样。
- `SwitchRegression/results.txt`：重载与快速切换的骨骼/像素变化。
- `named-cards-motion.gif`：主界面四张点名卡牌的连续渲染记录。
- `TempestMaterials/`：分材质水面检查；粉色区域为诊断中临时移除其他材质产生，不是游戏最终画面。
- `applied-euler-repairs.json`：实际修改的动画文件清单。
- `packed-euler-orders.json`：打包后的旋转顺序回读。
- `contract-order-validation.json`：722 条轨道的顺序信息完整性。
- `importer-regression.txt`：XYZ、ZXY 两种顺序角度误差均为 0，缺失顺序均被拒绝。
- `before/`：修改前文件备份。此轮无提交或暂存操作。

Unity 打包回读实验确认旋转顺序进入 generic binding 的 customType。AssetRipper 的转换源码也将该字段作为欧拉旋转顺序使用：[AnimationClipConverter](https://github.com/AssetRipper/AssetRipper/blob/master/Source/AssetRipper.Processing/AnimationClips/AnimationClipConverter.cs)。旧客户端的 10/14 编码按对应 XYZ/ZXY 处理，并核对了原始骨骼静止姿态。

临时检测脚本已清除，已退出 Play Mode；清理后的编辑器编译错误为 0，资源包 ready 标记有效。记录：`final-clean-compile.txt`。
