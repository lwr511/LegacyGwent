# 分层图像来源

## 当前启用的人物版本

最终人物是 `figure-v5.png`，来自内置 image_gen 的 `exec-fe624535-9d44-45c6-bf79-7733df208e16.png`（1049×1500 RGBA）。用户随后明确选定此版本，工程文件与其上传的 `codex-clipboard-f4936ab9-fdc3-41e2-ab7c-f1cc5ba76ef2.png` 经 RGBA 逐像素比较完全相同；选定后未继续修改图像。

本版最终编辑提示是：只纠正下方握拳手的左右手方向，将拳掌与手指在局部翻转，使拇指和鱼际移到画面左侧、靠身体的一边；保持拳心朝上，并把手腕接回前臂；整个人物作为一张透明图输出，保持其他部分。工具为内置 image_gen，未使用外部 API。

场景不再使用两只独立替换手，也不再在人物材质上切除旧手。`HandOverlay` 取自同一张完整人物图，只决定手指在剑柄前方、掌心在剑柄后方的遮挡关系，与身体共用相同的变形。

下文的独立手修正和早期 `figure.png` 为历史记录，均非当前启用的人物。按原图像素擦剑的中间试验在用户选择 v5 后停止，输出存于 `work/LadyLakeLab/unused-source-extraction`，不被场景引用。

源卡图：`Assets/Addressables/Cards/c10000000.png`。当前卡牌 70006 / 70011 均使用该卡图。原文件保持不变。

这些图层由 Codex 内置 image_gen 图像编辑工具依据源图提取并补绘，生成日期 2026-09-27。它们不是从旧客户端提取的原生分层，也不是逐像素无损抠图。工具补全了原画被遮挡的水下背景、头发和剑柄，局部绘画细节可能不同。所有项目依赖的 PNG 已复制到本目录。

共同提示约束：只使用源 atlas 左上 497×710 的有效绘画区域，忽略 1024×1024 中的黑色填充；输出 7:10 竖向画布；保持原画绿色、金色、绘画风格和人物身份，不添加文字、边框或新角色。

各次提示的具体目标：

- `background.png`：删除整个人物、头发、剑、前景水草和顶部荷叶；补全水下湖床、远处树影和水面光纹；保持原构图和水面透视。
- `figure.png`：保留人物的脸、头发、四肢、双手、姿态；移除剑、水、气泡、植物与背景；补全剑挡住的头发和手指；要求真正透明的 alpha。
- `foreground.png`：只保留顶部荷叶、两边长茎和底部右侧弯曲水草；补全被遮挡的细茎；画面中央透明；移除人物、剑和水。
- `sword.png`：只保留银色带青金纹样长剑，补全手挡住的剑柄；保持剑尖朝左上、剑柄朝右下；真正透明的 alpha；要求剑尖、护手、剑柄底分别位于源画归一化坐标 (0.155,0.387)、(0.672,0.699)、(0.785,0.775)。

生成器没有完全遵循像素注册要求，因此人物和剑需要在 Unity 网格上校正放置范围，而不能假定整图 UV 已经精确重合。原始生成 PNG 保留，不通过二次位图缩放损失透明边缘。最终位置以场景视觉验收为准。

生成记录（工具默认输出目录 `C:/Users/11464/.codex/generated_images/01a0e2f8-c577-7311-b870-f23cd8a10408/`）：

- background：`exec-cb4579c8-ed11-4916-b2bf-68cc69bc1d75.png`
- figure：`exec-b7ed1436-ee97-4708-bcc9-d23705dfc2ec.png`
- foreground：`exec-32ebf737-2301-4555-82a5-9ac87bb01102.png`
- sword：`exec-a12a45c5-bdb5-48d5-bb14-63cfeaa64aa7.png`

## 用户反馈后的手部修正

用户指出：上方手指的弯曲来自水面折射，不应该保留为人物的骨骼形状；下方握剑手在首次补绘中画反了方向。

整个人物的手部修图被 image_gen 的输出内容过滤拦截。随后改为只制作不含人物躯干的两只独立手部 sprite，生成成功。提示要求：左侧手自然伸展、五指正常、无折射形变；右侧握剑手恢复原图的手背/拇指朝向，握住一根不可见的斜向剑柄；只输出手和短腕、透明背景、保持原图金绿色绘画风格。

`hands-repaired.png` 来自 `exec-c692d506-0122-4dc1-9065-3f160b433172.png`，尺寸 1774×887。它在 Unity 中作为独立局部网格替换旧手部；旧 `figure.png` 保留作来源记录，最终人物由人物网格与修正手部共同组成。上手的自然手形与水面折射效果分开控制，下手跟随前臂并在剑柄之前绘制。

## 从项目原版闪卡复制的特效贴图

- `AeschnaCaustics.png`：`Assets/DynamicCards/Content/Old/Thronebreaker/OriginalTextures/74809d523530d3f7696d6e455aeca9dd.png`，由 Aeschna 的 `15010100_164296__15010300_Aeschna_RiverbedCaustics.mat` 引用。
- `AeschnaFlowNoise.png`：`Assets/DynamicCards/Content/Old/Thronebreaker/Shared/14990100_163662_Noise3D_ForceTiling.png`，同一焦散材质引用的噪声图。
- `AeschnaBubbles.png`：`Assets/DynamicCards/Content/Old/Thronebreaker/OriginalTextures/963cbb367e53ed41e9e55e8a50c83fc5.png`，由 `15010100_164274__15010300_Aeschna_FX_Bubbles.mat` 引用。

这些文件为原资源的逐字节复制，不是生成图像；仅用于本项目已有素材的实验复用。复用的具体材质绑定以最终场景为准。
