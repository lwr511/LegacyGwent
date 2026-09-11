# 狐妖：真身（20005600）云层方块修复

用户指出的左上角黑方块来自 `CloudsTop` / `CloudsBot`，不是 `PatchesFront` 的正常碎屑。隔离渲染见 `baseline-8s.png`、`hide-3-CloudsBot.png`、`hide-5-PatchesFront.png`。

旧客户端 `BuildPlayer-20005601.sharedAssets` 中有两个名为 `[20005601]AguaraFoxForm_Cloud` 的材质：编号 4 的粒子材质原本引用 `BuildPlayer-20164701.sharedAssets:12` 的 `clouds_4x4`；编号 11 的流动背景材质由 `PremiumCardsMeshMaterialHandler` 注入卡面图集。转换记录丢失材质身份，只按名字应用图集，把编号 4 也覆盖了。

修正粒子材质 `20005601_310072__20005601_AguaraFoxForm_Cloud.mat` 的 `_MainTex` 为原始导出的 `11220501_39440_clouds_4x4.png`。背景材质 `20005601_310086__20005601_AguaraFoxForm_Cloud.mat` 继续使用卡面图集。两组云粒子、正常碎屑、网格、粒子参数、相机及主体动画保持原设置。

转换记录增加可选 `materialAsset`，导入器和构建校验器以材质资产路径区分同名绑定。未指定此字段的既有记录继续使用原来的名称匹配。针对两个同名材质，用错误的初始纹理运行真实导入方法，确认分别恢复到各自正确的纹理；结果见相邻 `RaghRepair-20260911/cloud-contract-result.txt`。

`source-materials.json` 为原始材质引用，`repair.json` 为修正路径；`before-*` 为备份。`Reference` 是隔离进程中的纹理修正预览，不作为正式资源包交付证据。正式缓存的运行结果和哈希校验将记录在相邻 `RaghRepair-20260911/Delivered` 与 `delivery-audit.json`。

最终交付验证通过：2026-09-11 18:53:56（UTC+8）构建完成；独立 Unity 2019 进程直接读取主工程缓存，完成两张卡大小预览的 20 次采样，覆盖持续播放超过 18 秒及关闭后重开。终末之战骨骼和网格持续变化，狐妖两种云材质分别绑定正确纹理，实际画面无原报告的云层黑方块。86 个源文件/元数据与缓存清单哈希匹配；27 个发布文件共 1,743,396,631 字节，未涉及的分包哈希不变。临时主工程构建脚本已移除，未创建新游戏安装包。
