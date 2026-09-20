> 历史检查记录（2026-09-20）：以下问题和结论针对当时的 DIY 快照，不代表后续已适配的 AI 线当前状态。保留用于追溯设计依据；个人路径及设备信息已脱敏。

# DIY → DIY-AI 闪卡及近期改动：旧客户端兼容性审查

审查日期：2026-09-20。结论：**不放行合入上线。正常旧协议路径可用，但故障隔离、数据库回滚和跨版本卡组写入仍存在已复现的问题。**

本报告只评审和测试；没有合并分支、推送、部署、访问生产数据库或修改产品源码。新增的测试程序、Mongo 数据及日志均在本地 `work/CompatibilityAudit/20260920`；本次临时测试进程已退出。不要把下述局部通过理解为整个 AI 服已验收。

## 1. 版本与范围

| 项目 | 本次核对值 |
| --- | --- |
| 用户本地分支 | `diy`，HEAD `local DIY snapshot before the AI port` |
| 用户闪卡等改动的起点 | `3c496466b` 之后的 14 个提交，包括资源导入、闪卡权限/按张合成、每日奖励、多语言、Unity 升级、收藏 UI；也检查了尚未提交的画质和语言变更 |
| 最新上游 AI 线 | `080b995a9c77a71bb7500e5d239492d454f60bf6`，通过 `git ls-remote upstream refs/heads/diy-ai` 当场核实 |
| 两条线的共同祖先 | `171976cfc3149e0ec86c5f59594c59b1ded9f2da` |
| 本地服务端 | `netcoreapp3.0`，MongoDB.Driver 2.13.0；实测运行时 .NET Core 3.1.32 |
| AI 线服务端 | `net10.0`，MongoDB.Driver 3.9.0 |
| 本地 Unity | 2019.4.41f2；AI 线文档/工程仍有独立版本、构建及连接配置 |

范围从实际变更入手：Common 数据模型 → Hub 方法与通知 → 账户/卡组持久化 → 登录、注册、匹配 → 小局结算 → 客户端协议解析 → 两种新包的资源开关 → 上游集成与回滚。未对所有未改动的历史业务重新做全量功能测试。

**三个客户端必须分别验收：**

1. 已发给玩家的旧 AI 客户端：没有新字段、没有闪卡/每日任务接口调用。
2. 新普通包：新代码，但不带动态卡资源。
3. 新闪卡包：新代码，带动态卡资源。

`DynamicCardBuild.IncludeContent` 控制资源打入，不会把新普通包还原成旧程序。因此“新普通包与新闪卡包互打成功”不能替代旧客户端兼容测试。

## 2. 上线阻断问题

### P1-A：奖励/钱包失败会阻断旧玩家登录、注册、卡组操作及匹配

代码位置：

- `src/Cynthia.Card/src/Cynthia.Card.Server/Services/GwentGameService/GwentServerService.cs:420`：发布登录态前强制等待 `GetDailyQuests`。
- 同文件 `:520`、`:736`、`:794`：匹配、新增卡组、修改卡组同步等待 `GetPremiumCollection(...).GetAwaiter().GetResult()`。
- `GwentDatabaseService.Premium.cs:27`：读取钱包还会调用初始奖励、版本迁移、价格配置等逻辑。
- `GwentDatabaseService.cs:315`：先插入用户，再等待初始粉尘发放。

复现 1：在隔离 MongoDB 中仅给某个测试账户的 `premium_collection` 文档加入未知字段，用户集合仍可正常读取。旧格式的 `Login`、`AddDeck`、`ModifyDeck`、`MatchOfPassword` 全部收到 Hub 调用错误。移除该测试字段后登录恢复。这是钱包反序列化故障注入，不声称生产现在存在这种坏数据。

复现 2：只让下一次每日奖励 `findAndModify` 写入失败，登录仍然报错。并非整个数据库断线。

复现 3：只让注册后的初始钱包写入失败，注册 RPC 报错，但用户已经入库；用同一用户名重试返回 `false`。玩家看到注册失败，却已经占用了用户名。

这不满足“没有新闪卡功能的玩家仍可正常游玩”。即使客户端从不调用任何新接口，服务器仍把它接入这条依赖链。同步等待还占用处理线程；本次未做生产并发容量压测，不能给出无吞吐影响的结论。

修复要求：普通登录/注册/保存卡组/排队不能以粉尘结算或外观同步成功为前提。钱包暂不可用时，合成仍应安全失败；正常游戏应按可用的普通外观继续，不能伪造拥有权、扣款或覆盖持久化选择。奖励需要可重试且幂等的补发机制，注册应消除“账号创建了但返回失败”的窗口。

### P1-B：小局推进仍同步等待奖励；“重试三次”不等于有时间上限

代码位置：`GwentServerModels/GwentServerGame.cs:197`、`:243`。

`BigRoundEnd` 在增加 `CurrentRoundCount` 和进入后续流程前，`await TryAwardDailyRound`；内部直接等待每次奖励任务，没有应用层超时/取消边界。

隔离测试把奖励任务保持未完成，500 ms 后小局结束任务仍未完成、`CurrentRoundCount` 仍为 0；释放奖励任务后回合立即继续。测试证明的是同步阻塞关系，不是声称已经测出生产延迟。数据库/网络缓慢时，奖励耗时会直接进入双方对局流程。既有测试只对“立即抛异常后重试”做了保障，不能覆盖这种慢请求。

修复要求：将奖励结算与正常对局推进解耦，并保留稳定回合键、持久重试和幂等性。不能仅改成丢弃任务的 fire-and-forget，以免进程退出后奖励永久丢失。增加慢请求、超时、重启补偿的实际测试。

### P1-C：新卡组 BSON 使当前 AI 服务器无法直接回滚读取

代码位置：`Common/Models/DeckModel.cs:11`；`GwentDatabaseService.cs:233`、`:245`；账户更新还有多处整文档 `ReplaceOne`。

新增 `PremiumCards`、`PremiumLeader` 不仅用于网络 DTO，也随 `UserInfo.Decks` 写入原来的 `gwentdiy.user` 文档。没有隔离为单独的外观存储 DTO。

将新服务器实际写入的测试用户文档交给从上游 `080b995` 提取的旧 `UserInfo`/`DeckModel`，反序列化报错：

```text
An error occurred while deserializing the Decks property of class LegacyClient.UserInfo:
Element 'PremiumCards' does not match any field or property of class LegacyClient.DeckModel.
```

除本地旧驱动外，还单独使用 **.NET 10.0.2 + MongoDB.Driver 3.9.0** 重现，和当前 AI 线的驱动版本一致。上游源代码未找到忽略额外 BSON 字段的注册约定。

AI 线 `deploy/diy-ai/deploy.sh` 的自动回滚是切回上一发行目录并重启，并不会把已写入的数据库结构同步回滚。`/healthz` 注册也没有实际登录/读取用户的业务探针。因此仅切回旧程序或看到 HTTP 健康检查成功，不能证明玩家能恢复登录。

修复要求：优先让外观持久化与原 `user.Decks` 结构分离；或先发布兼容读写的过渡版本，再引入新结构。仅加“忽略未知字段”还不够：旧程序整文档替换时可能丢弃这些字段。必须在隔离数据副本上完整验证新写入 → 回滚到指定 AI 版本 → 登录/编辑/匹配 → 再升级，且保留余额与外观选择。

### P2-D：旧客户端编辑卡组会改变新客户端的外观选择

代码位置：`Common/Models/PremiumCollection.cs:47`、`GwentServerService.cs:794`。

复现：拥有一张闪卡 → 新接口保存显式普通版本（`PremiumCards={}`）→ 用旧 DTO 只改/保存同一卡组 → 服务器把缺少的字段当成待初始化的旧卡组，自动选择已有闪卡。实测普通选择变为 `{"34022":1}`。

卡牌 ID、数量及钱包余额没有因此变化，但跨客户端切换后，玩家明确保存的外观被覆盖。它不满足“旧客户端的写入不破坏新字段”的要求。

修复要求：旧请求缺少新字段时保留服务器已有选择；只对移除的卡、减少的数量做收缩校验。区分“字段缺失”与“新客户端显式清空”，不能都走重新初始化。

## 3. AI 线集成阻断与额外影响

对共同祖先、本地 HEAD、上游 AI HEAD 的文件做了隔离三方文本合并探测，未运行 `git merge`，未改变分支/index。34 个双方改动的同路径文件中，所检查的文本类型有 **20 个文件发生冲突**。完整列表在 `inventory.json`。

关键冲突包括 `Startup.cs`、`Program.cs`、`GwentServerService.cs`、`GwentMap.cs`、`GwentLocalizationService.cs`、`Bootstrapper.cs`、登录流程、语言文件及客户端构建工作流。不能选择“全部采用本地”解决：

| 必须保留的 AI 线能力 | 覆盖风险 |
| --- | --- |
| .NET 10 / MongoDB.Driver 3.9 与当前部署方式 | 当前本地构建成功不等于目标 AI 运行时构建/测试成功 |
| `AsciiSafeJsonHubProtocol` 及注册位置 | 这是 AI 线专门保留的旧 Unity 帧兼容处理，不能被本地 `Startup` 覆盖 |
| `ASPNETCORE_URLS` 与 5010、Mongo 28021 隔离配置 | 本地 `Program` 使用另一环境变量；本地发布客户端 `Bootstrapper` 仍默认 5005 |
| AI 线卡池、卡牌效果、卡组编码索引和版本更新策略 | `DiyAiCardPool` 明确要求保留原 CardMap 键序以解码历史卡组/战绩；整表覆盖可能改变实际玩法/历史解释 |
| AI 客户端产品名、存档键、包 ID、多平台构建 | 两种新包都必须继续指向 AI 环境并保留其隔离方式 |

此外，**整个本地分支相对 AI 的差异不全是用户闪卡提交**：用户改动起点之前的 DIY 历史里还有 `DatabaseMigrationService`，会运行 `restore_all_time_highest_mmr_v1`，根据赛季历史回填最高 MMR。直接把整条 DIY 线合入可能把这一迁移一并带入 AI，不能当作纯闪卡变更审查。

当前配置还会在服务启动后对全部符合条件的旧账户补发一次 5000 粉尘（独立钱包、固定奖励 ID），登录另发 20，每日小局奖励为 25/35/45。这些是明确的全服行为，不受客户端是否带动画资源控制。幂等补发相关测试通过；生产玩家数、首次补发耗时和数据库负载未测。

## 4. 数据与接口逐项结论

| 对象/路径 | 新数据或行为 | 兼容性结论 |
| --- | --- | --- |
| `UserInfo` 顶层身份、密码、MMR、饰品字段 | 用户闪卡提交没有新增/改名这些顶层字段 | 不能据此宣称 BSON 完全不变；嵌套 DeckModel 已变 |
| `DeckModel.Id/Name/Deck/Leader` | 原 string/string/List<string>/string 保留 | 旧 JSON 请求、增删改排序与匹配正常路径实测通过 |
| `DeckModel.PremiumCards` | 可空 `Dictionary<string,int>` | 缺失与空字典有不同含义；旧请求覆盖选择与回滚问题见 P2-D/P1-C |
| `DeckModel.PremiumLeader` | 可空 `bool` | 需要同样的缺失/显式 false 兼容规则 |
| `CardStatus.IsPremium` | 可空 `bool`；对局通常由服务器赋值 | 旧 Newtonsoft 和 System.Text.Json DTO 可忽略新字段；原 CardId/战力仍能解析 |
| `premium_collection` | string 账户 ID、Int64 粉尘/Revision、Int32 版本/数量、拥有/选择列表、合成回执、奖励账本 | 单独集合有效隔离了旧整用户替换；按当前连接身份授权，不从客户端接收奖励金额 |
| `DailyQuestProgress` | 日期 string、bool 登录标志、Int32 进度、回合 ID 列表 | BSON 保存去重历史；Newtonsoft Ignore +服务端 System.Text.Json converter 不向客户端发送 `ProcessedRoundIds`；AI 合入时须保留 converter |
| `Register` | CLR `bool` 变 `Task<bool>`，参数不变 | SignalR 正常线上返回仍是 bool，旧客户端实测通过；失败原子性另见 P1-A |
| 原有 Hub 方法 | 源码核对未发现被移除或参数变化 | 行为新增钱包依赖，签名兼容不等于可用性兼容 |
| 新 Hub 方法 | `GetPremiumCollection`、`GetDailyQuests`、`CraftPremium`、`CraftPremiumCopy`、`SelectPremium`、`GetGameLocalesVersion`、`GetLocalizedNotes` | 增量方法；旧客户端不需要调用。SelectPremium 用 0/1，避免旧 bool 输出格式差异 |
| 新推送 `DailyQuestsChanged` | 无参数的缓存失效通知 | 旧 SignalR 客户端没有订阅时仍保持连接，后续 RPC 成功 |
| 对局 `GameOperation` | 原枚举编号、字符串参数包装结构未因这些提交更改；卡牌 JSON 多出 IsPremium | 旧操作 converter 与旧 CardStatus 解析通过；未运行完整旧 Unity 对局 |
| 隐藏牌 | 仍创建隐藏卡背状态 | 测试未暴露 premium=true；不代表已覆盖所有卡效的隐私分支 |
| 新普通包缺资源 | 本地库加载失败保留静态卡图 | 源码有降级逻辑；本次未构建普通/闪卡包做视觉验收 |
| 未提交画质改动 | PlayerPrefs、RenderTexture、模型/渲染与基准统计 | 未新增服务器 DTO、Hub 或 Mongo 写入，影响范围在新客户端本地；本次不是画质效果验收 |

另有静态长期风险：`ProcessedRoundIds` 会跨天永久保留，`CraftReceipts` 和 `Rewards` 也存于同一钱包文档；结算复制并重写去重历史，钱包查询读取该对象。尚未看到分离/分段/容量管理。不能简单清空回合记录“优化”，否则破坏幂等。需要单独的增长和并发预算测试；本次没有以未测的性能猜测作为已复现故障。

## 5. 本次实际验证及局限

| 验证 | 结果 | 能证明什么 |
| --- | --- | --- |
| 当前源码服务端及 RewardSystemTest 重新构建 | 0 编译错误；保留构建警告 | 仅本地 netcoreapp3.0 工程可编译，不是 AI 目标构建 |
| 新增旧客户端兼容/故障注入测试 | **32 项，23 通过、9 失败** | 复现前述登录/卡组/匹配、注册、回滚、外观覆盖及慢奖励问题；部分通过项是确认故障触发条件，不是独立功能数 |
| 旧版 SignalR 库 + 上游旧 DTO/converter 真实 WebSocket | **9 条检查记录全部通过**（其中 1 条记录实际程序集元信息） | 旧库正常注册登录/保存/匹配取消、多语言往返、新通知忽略及卡牌载荷解析 |
| 当前配置的既有奖励测试矩阵 | **247 项，186 通过、61 失败** | 现有测试不能当作当前版本的绿色发布门槛 |
| 相同新编译测试/服务端二进制，复制到审计目录，换回历史 10/10/20/30 配置 | **247/247 通过** | 隔离确认上述 61 项失败由旧测试数值/余额场景假设与新配置不一致引起；不是把生产奖励改回旧值，也不是新配置验收通过 |
| 上游旧模型 + .NET 10.0.2/MongoDB.Driver 3.9.0 读取新 BSON | 失败，明确报 PremiumCards 不识别 | P1-C 在 AI 驱动版本下复现 |
| 源码哈希复查 | 审查开始时记录的产品文件未改变 | 审查没有悄悄修复产品代码 |

旧库探针的四个 SignalR/连接 DLL 与上游 AI 对应 Git blob 一致；`Client.Core` 文件版本为 `5.0.821.31805`。运行在桌面 .NET 5.0.7，System.Text.Json 来自该运行时，并使用提取的旧 `AOThelper`，Unity Debug/Preserve/AOT 保留辅助使用无行为桩。**它不是发布旧包在 Unity/IL2CPP 中的运行证明。**

操作载荷探针由隔离测试主机通过正式 Hub 发送构造帧，验证封包/converter/DTO 解析，不是声称打完了旧新客户端混合对局。新模型复用了未因用户闪卡提交改变的 Common 依赖，但旧 UserInfo/DeckModel/CardStatus 的源码来自固定 AI HEAD。

没有执行的发布前项目：解决冲突后的 AI 候选版本构建、AI 全部既有回归、真实旧 Windows/Android 发布包、两种新包、旧旧/旧新/新新完整对局、观战与掉线/重登、真实数据匿名副本升级回滚、生产规模补发/负载验证。没有生产游戏或生产账号操作。

## 6. 修复后的放行条件

1. 将当前 AI HEAD 作为移植基线，逐项移植用户自己的提交；单独决定是否带入旧 DIY 的其他历史行为。解决所有冲突，保留 AI 卡池、索引、协议、端口及部署配置。
2. 修复 P1-A、P1-B 和 P1-C，再修复 P2-D；钱包错误/慢请求时旧玩家仍能注册、登录、编辑合法普通卡组、匹配、结束小局。
3. 让奖励测试以当前 20/25/35/45 配置通过，并保留独立校验具体金额的断言；不能以删断言或旧配置全绿替代。
4. 对实际旧包、新普通包、新闪卡包跑版本矩阵；验证双方出牌、领袖、生成/复制/变形卡、手牌隐藏、观战、完整结算和再次登录。旧包应保持静态表现，不能被要求下载闪卡资源才能继续游戏。
5. 在隔离的生产数据副本演练前滚、指定旧版回滚、再前滚；逐项检查账户/卡组/余额/饰品/战绩。健康检查应增加关键业务验收，不能只有 `/healthz`。
6. 在隔离预发布环境测补发与对局共存、异常注入和连接稳定性。完成后才能讨论正式合入/上线。

## 7. 证据文件与复跑

审计目录：`<LOCAL_REPOSITORY>/work/CompatibilityAudit/20260920/`。

- `inventory.json`：固定提交、三方冲突列表、初始 dirty 状态与文件哈希。
- `contract-manifest.json`：接口增减、关键模型 diff、旧客户端 DLL blob、产品源码哈希复核。
- `compatibility-results.json`、`harness.log`：32 项兼容性/故障隔离测试。
- `legacy-wire-results.json`：旧客户端库的协议探针。
- `ai-driver-rollback.json`：AI 版本驱动的 BSON 回滚错误。
- `build-current.log`、`build-harness.log`、`build-legacy-wire.log`：本轮编译结果。
- `reward-suite.log`：当前配置测试结果；完整矩阵结果位于 `work/RewardSystem/20260920-183320/results.json`。
- `historical-config-suite.log`：历史配置诊断；完整矩阵结果位于 `work/RewardSystem/20260920-183716/results.json`。
- `merge-probes/`：只供检查的三方文件快照，不是已经解决冲突的候选产品。

复跑新增测试：先构建 `Harness/Harness.csproj`，再执行 `run-harness.ps1`；旧库探针先构建 `LegacyWire/LegacyWire.csproj`，再执行 `run-harness.ps1 -Wire`。只使用本机回环地址 5029/28029；脚本在端口占用时拒绝运行，数据目录保留供核查。现有奖励矩阵使用另外的隔离 5016/28021，本次均已停止。

以上所有测试使用合成账号。没有读取或复制实际玩家数据。
