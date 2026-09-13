> 已于 2026-09-13 收尾：临时收藏核对功能及参考图库已完整移除，以下是开发期的历史验证记录。最终状态见相邻 ImportClosure-20260913/README.md。

# 2026-09-13 收藏素材清理与三张牌修复

用户反馈的三个源场景为征服者奥贝伦 `Latest/21870101`、Wraith SP `Old/Thronebreaker/13870100`、Eyck of Denesle SP `Old/Thronebreaker/14830100`。

## 修复

- 奥贝伦：`VFX/VFX_Mesh/Clouds` 的材质与静态背景同名，原导入按名称给两者都绑定了人物图集。按源材质身份恢复云层的 `VFXC_Cloud_TiledWhite_01`，复用已有纹理；与源解码图逐像素比较，最大压缩舍入差 4/255。conversion.json 也拆分为两个准确的 materialAsset，构建校验不再要求云层误用图集。
- Wraith：本地渲染中的双遍 Bloom 把亮部截成纯白。仅此卡的 blurIntensity 从 1 调为 0.1，保留源动画、材质、光晕与其他卡的后处理。
- Eyck：龙使用的 `mat_10_1` 以不透明方式绘制图集，而 `_Cutoff=0` 让全透明像素也写入颜色与深度。将该材质阈值改为 0.5，剔除透明空隙产生的黑块。

`before/`、`after/` 为实际收藏页渲染与界面截图，每张卡采样四帧，间隔两秒。原始素材修改前的文本存于 `visual-originals.json`；`repair_visuals.py` 记录准确修复路径。其修改属于原先被 Git 忽略的大型 Content 数据，换机时需同时同步 Content 和重建后的资源包。

## 去重与依赖

128 个调试条目先查名称，再与普通收藏原图比对。英文 SP/教程名称和中文游戏名不能直接匹配，因此以同一原画的人工并排确认为最终依据。55 个极近似候选，以及有裁切差异的卡西尔、尼弗迦德大门、奎特家族劫掠者、黑豹、侦察，共 60 个条目与正常收藏重复；另有蛋、牛尸、熊的 3 组调试内部重复。

保留正常收藏已经使用的场景。调试内部的三组保留 Thronebreaker 场景，并合并旧版卡图别名、中文名及阵营信息。完整配对见 `consolidation.json`。场景数 740 → 677，调试对数 128 → 65；670 个卡图 ID 无丢失、无多重映射。

Unity AssetDatabase 对 1,605 个入口扫描依赖，涵盖全部卡场景、音频与 Content 外的项目资产。仅删除重复场景及不再被任何保留入口引用的资源；两轮分别保留 363、26 个共享依赖。对 1,344 个保留场景/音频入口检查，无已删除或缺失依赖。

已删除 3,982 个动态资源及 meta 文件，合计 1,988,841,629 字节（约 1.85 GiB）；同时移除 63 张调试参考图及 meta，23,587,960 字节。清理包括重复预制体、动画片段、控制器、材质、贴图、mesh/数据与音频，实际文件已删除。

`deletion-plan.json` 记录逐文件大小与 SHA-256；PowerShell 删除脚本先验证整个清单的路径边界和哈希，再逐文件删除，没有递归删除整个 Shared 或场景目录。安装的源游戏、其他历史构建与工作备份不属于本次删除范围。

## 验证

实际结果以 `collection-check.txt`、`retained-dependencies-check.json`、`deletion-result.json`、`build-result.txt` 及最终资源包核对报告为准。`prepare_references.py` 会拦截已确认重复的场景重新出现在导入目录中的情况。

第一轮源资源 PlayMode 回归通过：67 对、四个已删除源 ID 搜索为空、普通怪物阵营无调试对子、返回全部仍为 67 对。追加黑豹和侦察后为 65 对，最终包的测试结果单独记录。三张牌各四帧验证通过；Wraith 全 RGB 通道均达到 250/255 的截白像素占比从 8.02% 降为 0%。修改前后并排图为 `visual-comparison.png`。

清理分两轮完成：第二轮是在逐张检查余下高差异图片时发现的两张裁切变体，依据与逐文件清单在 `second-pass/`；`finalise_cleanup.py` 合并两轮审计结果。`consolidation.json` 是最终 63 份重复资源的统一记录。同名但原画不同的其他版本仍保留。

对剩余 65 张原图又做了两两相似度检查，人工查看最接近的 20 组，没有发现新增的同画重复；本地证据为 `retained-internal-similarity.json` 和 `internal-0.jpg`、`internal-1.jpg`。

最终源文件删除统计：150 个动画片段、99 个控制器、873 个材质、399 张贴图、282 个 mesh/数据资产、60 个音频、65 个预制体（63 个重复卡场景和 2 个专属子预制体），以及 63 份 conversion.json；另计对应 meta。

最终资源包静态核对通过（`cache-check.json`）：677 个场景的索引与 catalog 完全一致，63 个退役预制体均不在包索引内；670 个 artId 唯一，65 对参考资料共 66 个文件。包内实际读取到 Wraith blurIntensity=0.1、Eyck _Cutoff=0.5，以及奥贝伦云层使用正确的 Cloud_TiledWhite 纹理。

最终 Windows 构建成功（0 errors）。`delivery-check.json` 确认客户端内 26 个动态包/索引文件以及 66 个参考文件与项目来源逐一 SHA-256 一致，三个修复参数直接从客户端包解码确认。`final-cleanup-check.json` 确认 3,982 个退役源文件及 63 张退役参考图均已不存在，客户端仅剩 65 张参考图；编辑器缓存另删除 50,622,258 字节过期分包及索引辅助文件。最终动态载荷为 1,452,290,778 字节。

`packaged/done.txt` 为实际资源包 PlayMode 验证 PASS：明确禁止原资源回退，三张牌各采样四帧并检查材质/光晕参数；画面在 `packaged/`。这项验证使用与客户端哈希一致的资源包，不等同于独立播放器登录实测。

最终收藏页实测 PASS（`collection-check.txt`）：65 对、六个退役源编号搜索为空、普通怪物阵营无调试对子，返回全部仍为 65 对。`cleaned-collection.png` 可见 1/65，`normal-collection.png` 记录正常阵营收藏。
