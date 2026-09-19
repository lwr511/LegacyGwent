# 普通卡与闪卡按张收藏

2026-09-16 的确认规则：普通铜卡固定拥有 3 张，额外逐张合成闪卡，最多 3 张。合成不消耗普通卡。银卡、金卡、领袖普通版各 1 张，闪卡最多 1 张。标准模式同名铜卡的普通版与闪卡合计最多带 3 张；已有特殊模式的组牌限制继续保留。

2026-09-17 的显示规则：收藏页不显示任何数量，未拥有的闪卡显示灰度。进入卡组编辑后，仅铜色闪卡复用已有的右上角 `CountIcon / CountText` 显示剩余可用量，格式为 `X数量`；普通铜卡、金卡、银卡和领袖均隐藏数量。卡组左侧按版本分行、分别增删，也仅铜色闪卡显示张数。详情页每次合成 1 张，达到上限后禁用合成按钮。价格继续读取 `PremiumCrafting.json`。

## 保存与迁移

- `PremiumCollection.InventoryVersion = 1`，`PremiumCopies[cardId]` 保存闪卡张数。普通数量由卡牌品质计算。
- 旧的 `OwnedCards` 全副本解锁，在首次读取时迁移为铜卡 3 张、其他 1 张；不补扣粉尘。迁移以版本号和余额修订号保护，重复登录不会反复迁移。
- `DeckModel.PremiumCards[cardId]` 保存该卡组中同名闪卡张数；`PremiumLeader` 保存领袖版本。`Deck` 仍保留原有卡牌 ID 列表供规则和卡组代码使用。
- 空字典表示明确选择全普通版；`null` 表示旧卡组，读取时按拥有量兼容。保存和匹配均检查闪卡数不能超过账户拥有量、也不能超过该卡组里的同名牌数。匹配创建独立快照。
- 版本在开局洗牌前写入每张牌的 `CardStatus.IsPremium`，之后随牌移动。AI 继续全部闪卡，隐藏牌不传递闪卡状态。
- 卡组分享码继续分享卡牌构成，不携带账号相关的外观选择；接收方按自己的收藏初始化外观。

## 合成协议

新客户端调用 `CraftPremiumCopy(cardId, requestId)`，请求 ID 是每次用户操作生成的 GUID（N 格式）。余额、张数、回执在同一次 MongoDB 文档更新中提交，以 `Revision` 比较重试。相同请求只购买一张，同 ID 换卡被拒绝，独立请求超过上限不扣费。发生通信异常时，详情页保留请求 ID 供重试。旧 `CraftPremium` 接口保留单次购买后的幂等行为，以兼容旧客户端。

Unity 的 `BoolConverter` 改为输出标准 JSON 布尔值，避免 `PremiumLeader` 被写成字符串而使服务端拒绝保存卡组。

## 原版参考与资源

2018 年官方公告记录了逐卡转化闪卡的功能：[Patch is now available](https://www.playgwent.com/en/news/15081/patch-is-now-available)。本项目按照用户确认保留全部普通卡，不执行普通卡消耗。

数量显示直接复用 `EditorUICoreCard` 预制体原有的右上角控件，包括图片、字体和布局；收藏卡不创建数量角标。条目边框 `dp_slot_bronze/silver/gold` 来自本机旧客户端 `gui/spriteatlases/high/deckpicker`。

闪卡边框使用旧客户端 `gui/prefabs/deckbuilder_base` 的 `PremiumShine_GlitterBronzeSlot / SilverSlot / GoldSlot` 与 `PremiumShine_BronzeSlot / SilverSlot / GoldSlot` 的原始纹理（`Noise27White`、`PortalNoise`、`CardShineTex`）、颜色和动画参数，替换静态金色描边与新增的闪卡小标志。`LegacyPremiumBorder.shader` 将原版双层闪光计算适配到当前 UGUI，支持 Stencil、滚动裁切和透明度，并以当前边框图片的透明区域限定效果，避免将原版横向卡组条目的遮罩拉伸到竖向卡牌上。此处是原素材和参数的移植适配，并非原版预制体原封不动复制。素材均在 `Assets/Resources/PremiumCrafting`，运行时无需访问旧客户端目录。

## 验证

- `RewardSystemTest/PremiumCopyScenarios.cs`：真实 SignalR、MongoDB 并发合成、请求防重、余额与数量上限、卡组保存校验、重连、旧数据迁移、混合副本对战和 AI 外观。
- `PremiumCraftingTest`：原有余额／授权测试，并将“一次合成全部副本”断言改为“一次合成一张”。
- `Assets/Editor/PremiumCopyVerification.cs`：使用独立本地测试账号，真实点击合成按钮 3 次，检查收藏数量、灰度、卡组分行、分版本移除和联网保存，并输出截图。
- 全量后端及联网回归：`scripts/Test-RewardSystem.ps1 -FullRegression`；运行前应构建四个测试项目。UI 验证通过 `work/PremiumCopies/ui.request` 触发，需要本地服务器、Unity Play 模式登录页。

后端最近一次全量结果：`work/RewardSystem/20260916-232808/results.json` 中 252 项检查通过，附属的 5 个回归程序全部退出成功。本次仅调整界面，未重跑后端全量；`work/PremiumCopies/ui-result.json` 中 36 项真实 Unity 界面检查通过（2026-09-17 00:14），覆盖各品质/版本的数量显示、三次合成、卡组增删和联网保存、边框 Shader 编译及遮罩设置。另检查了两帧边框动画与滚动边缘裁切截图，截图保存在同一 `PremiumCopies` 目录。未制作新的独立客户端安装包。
