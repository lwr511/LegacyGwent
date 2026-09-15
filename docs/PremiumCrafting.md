# 闪卡粉尘合成与收藏

实现日期：2026-09-13。范围：粉尘余额、永久合成、普通／闪卡筛选、账号选用、对局外观同步。任务系统和管理员网页未加入。

## 玩家使用

- 基础星尘现由 `InitialPowder.json` 配置，新老账号一次性领取，默认 5000；每日奖励独立，详见 `InitialPowder.md`。
- 收藏默认「全部」，每个普通版本右侧紧跟一个闪卡版本；未合成闪卡的卡图、卡框和阵营徽章呈黑白，且不加载动态模型。
- 点击未合成闪卡会在收藏页选中它，在右侧点击「合成闪卡」后由服务器扣粉尘。没有确认弹窗或中央合成页面；原位聚能后由灰度卡变成动态卡。铜卡 100，银卡 200，金卡／领袖 400。
- 每种卡合成一次，所有副本通用。合成成功自动选用闪卡，三张铜卡共享一次解锁和一次费用，不显示闪卡 X3 合成/拥有计数。
- 普通版保留为静态对照；从普通版本加入卡组不会自动取消该牌的闪卡选用。未合成闪卡不能直接加入卡组。卡组本身的数量上限仍按原规则处理。
- 本机「动态卡牌」画质开关仍有效，关闭只停止动画，不改变账号所有权。
- 没有动态资源的卡显示「此卡暂无闪卡版本」，不能扣费。目录由服务器 `PremiumCrafting.json` 的 `AvailableArtIds` 限定。

## 数据与接口

MongoDB 使用实际项目数据库 `gwentdiy` 中的独立集合 `premium_collection`。文档 `_id` 为 `user._id`，即 `UserInfo.Id`，不是玩家昵称。粉尘与所有权不放进会被老代码整份覆盖的 `UserInfo`。

`MeteoritePowder` 为余额，`OwnedCards` 为拥有的卡牌 ID，`SelectedCards` 为选用闪卡的卡牌 ID，`Revision` 防止客户端倒序响应覆盖新状态，`Rewards` 记录管理员发放的幂等 ID、金额、原因和时间。

SignalR 新接口为 `GetPremiumCollection()`、`CraftPremium(cardId)`、`SelectPremium(cardId, premium)`。版本参数使用整数 0／1，兼容旧 Unity 客户端将 bool 写成字符串的转换器。均按已登录连接确定账号，不接收玩家名、玩家 ID、价格或可自行上传的拥有列表。合成用 MongoDB 单文档条件更新，同时检查未拥有及余额、扣款、解锁和选用；重复请求不会重复扣费。

匹配开始时，服务端读取所选且已拥有的闪卡快照，将 `CardStatus.IsPremium` 发送给双方。开场领袖、卡牌、右键详情按服务器状态显示；盖牌状态不传已拥有的外观。新生成卡按生成玩家的账号选择确定外观。收藏和本机动态开关不能给对手解锁卡牌。

价格配置位于 `src/Cynthia.Card/src/Cynthia.Card.Server/PremiumCrafting.json`，修改后重新构建并重启服务端。客户端从服务端读取费用。

## 管理员按玩家 ID 发放

在拥有数据库管理权限的机器运行：

```powershell
./scripts/Grant-MeteoritePowder.ps1 -PlayerId '<user._id>' -Amount 500 -RewardId 'reward-20260913-001' -Reason '活动奖励'
```

默认仅连接本地开发 MongoDB。实际服务器请显式传 `-MongoUri` 和 `-MongoShell`。同一玩家同一个 `RewardId` 只发一次；每次独立奖励应使用新 ID。账户不存在时拒绝发放。玩家重新进入收藏时刷新余额。此脚本不向普通客户端开放发奖接口。

## 老版客户端参考

直接检查了本机旧客户端 `E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/gui`，不是新版商店界面。

- `prefabs/deckbuilder_base`：`CraftPanel`、`FilterDropdowns`，保留现有三栏收藏结构，在搜索旁放版本筛选、详情下放合成动作。
- `spriteatlases/high/deckbuilder`：提取 `db_filter_premium_standard`、`db_filter_premium_premium`、`db_filter_premium_all`。
- `spriteatlases/high/shared` / `prefabs/global_base`：提取粉尘 `Powder` 图标。
- `prefabs/global_base`：`CraftPremium` 动画 path ID `3151497866744131522`，60 Hz，`HandleCardUpdate` 事件在 2.9666667 秒，`DisableObject` 在 6.6 秒。
- 原合成层包含 Circle、Ring、Charge、Flash、Blast、ShootingStars、Dust、TealDust。复用其 `Glow_green`、`VFX_halfringSlash1`、`VFX_SphericalGlow`、`Streak`、`StarGlow` 纹理，以 Unity UI 加法混合重做聚能、星芒、闪光和揭示；并非直接运行原客户端整个粒子预制体。原声音事件 `ui_mainmenu_collection_transmute` 已定位，本次未移植声音。

2026-09-14 修正合成位置：上述纹理现在附着于列表中原有闪卡和右侧原有预览卡，限制在卡框内；移除了全屏遮罩、中央复制卡、额外标题和确认弹窗。保留原片段约 2.97 秒揭示、6.6 秒收尾的节奏。列表不会因合成重建，动态首帧准备好前继续展示已经加载的灰度卡；此例外只针对原位升级，首次打开收藏仍隐藏未加载完成的整张卡。

导入资源位于 `Assets/Resources/PremiumCrafting`，随 Git 提交即可还原这些新增 UI 资源。原动态卡包仍沿用项目原有打包／迁移流程。

## 验证与发布边界

- 服务端／公共模型编译通过。
- `PremiumCraftingTest`：18 项数据库与对局状态检查，包括 20 个并发合成请求仅扣一次、持久化、跨账号隔离、普通版选择、隐藏卡和双方不同外观。
- `scripts/Test-PremiumCraftingHub.ps1`：12 项真实本地 SignalR 检查，包括未登录拒绝、扣款、重复请求、选用、无效版本参数与重新登录。
- Unity Play 模式 6 项界面检查通过：普通版静态、已解锁闪卡动画、全部筛选双版本、未解锁禁止入组，以及点击普通／闪卡版本分别保存选用状态。
- Unity 2019.4.1f1 实际 Play 模式运行收藏和合成：测试账号 500 → 400 → 300 → 200，合成三个不同铜卡。最终光效纹理与大图动画揭示均已截图核对。截图和状态记录位于 `work/PremiumCrafting`。
- `Assets/Editor/PremiumCraftingVerification.cs` 仅为本地编辑器验证入口，不编入玩家客户端。验证数据只写测试账号；没有向普通玩家发放粉尘。
- 本次未部署线上服务，也未替换之前发布的 Windows 客户端。线上使用必须同时构建／部署新增服务端和兼容客户端。旧客户端不具备账号闪卡限制，不能仅部署服务端就宣称旧客户端显示已受控。

复验命令（从仓库根目录）：

```powershell
./scripts/Start-LocalServer.ps1
./scripts/Test-PremiumCraftingHub.ps1
```

运行数据库与对局测试前应避免正在运行的服务端占用构建输出 DLL；可先停止本地服务端，构建测试后启动 MongoDB，再运行已构建测试程序。Unity 所用公共 DLL 由 `scripts/refresh.py` 的既有流程构建并复制到 `Assets/Assemblies`。

2026-09-13 补充双客户端联网验收：已构建独立 Windows 开发测试包，两场交换普通／闪卡领袖，50 项检查全部通过，覆盖双方领袖、双方场上普通／闪卡单位、间谍牌原持有者版本和隐藏手牌。详见 `docs/PremiumNetworkVerification.md`；这补足了此前仅验证服务端状态和单客户端的边界，仍未部署线上或替换正式发布包。
