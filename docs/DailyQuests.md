# 每日登录与小王冠任务

## 已实现规则

- 全球统一按中国时间（UTC+8，Asia/Shanghai）00:00 重置，使用服务器 UTC 时钟换算，不依赖主机所在时区或玩家电脑日期。
- 登录成功自动发 20 陨星粉尘；当天多次登录、刷新或断线重连不重复发放。保持在线的账号在跨日后的首次任务同步也会领取当天登录奖励。
- 真人对局每赢一个实际结算的小局得到 1 个小王冠，累计 2／4／6 冠分别额外发 25／35／45 粉尘，当日总额最多 125。第 6 冠以后进度和奖励封顶。
- 人机练习、自我对局、平局不计每日王冠。投降／断线只保留此前已经结算赢下的小局，不凭整场胜利补发未进行的小局。匹配和密码真人房均计入。
- 王冠按服务器处理奖励时的中国日期归属，不按小局结束日期归属。零点前结束、零点后首次处理的小局计入新一天，并按新一天的进度与上限发奖；服务器提供的未来结算时间仍不接受。
- 同一账号同一小局只处理一次，跨日和重启重试也不重复计冠；当天已经封顶后处理的小局同样记入去重记录，不能通过次日重试获取奖励。服务器时钟倒退到已保存日期之前时停止发放。

配置位于 `src/Cynthia.Card/src/Cynthia.Card.Server/DailyQuests.json`，服务器启动后缓存配置；改动后需重启服务。当前界面按本轮确认的 6 冠、3 档设计。

## 服务端与持久化

`GwentServerGame.BigRoundEnd` 从双方实际场面得分确定胜者，通过 `GwentMatchs` 绑定的服务端回调计冠。每次对局生成随机标识，加小局编号作为稳定幂等键。客户端不能提交用户名、时区、日期、胜场数或奖励金额；唯一公开任务接口为认证连接上的 `GetDailyQuests()`。

每日日期、登录已领取标记、小王冠进度、当日已得粉尘、最多 6 个当日计冠小局键，嵌入既有 `premium_collection` 账号文档。`DailyQuests.ProcessedRoundIds` 另保存所有已处理小局键（包括封顶后的小局），跨日保留。数据库保留完整历史；服务端专用 JSON 转换器将其排除在 SignalR/MVC 响应之外，Newtonsoft 路径继续使用 `JsonIgnore`。任务状态、去重记录和钱包余额以同一 Mongo 原子更新提交，并以 Revision 比较交换处理并发；冲突重试会重新读取服务器日期，以成功更新那次处理所采样的日期归属。旧的 UserInfo 整体保存不会覆盖任务或粉尘；与闪卡合成和管理员发奖并发时不会丢失余额更新。

旧文档缺少 `ProcessedRoundIds` 时，首次更新会先保留当前 `RoundIds`，再重置日期；旧版本已经清除的更早日期记录以及当时未保存的封顶小局无法追溯。跨日去重记录目前随已处理小局数持续增长，不按日清理；长期运行需另行规划归档，不能直接删除后继续接受任意旧事件。此改动不增加自动补发队列，历史上已经丢弃且未重试的结算不会凭空补回。

登录在服务端主动触发当天奖励，不依赖客户端先打开任务页。小局结算持久化完成后向客户端发送 `DailyQuestsChanged`，客户端重新获取认证状态；进度没有单独的客户端发奖入口。

## 客户端

- 主菜单右上角新增每日任务入口、圆环时间进度、小时／分钟重置倒计时。
- 任务页显示每日登录、6 枚小王冠、连续进度条、3 档到账状态、当日获得上限和当前粉尘余额。奖励自动入账，页面无需点击领取。
- 登录、进入 Game／GamePlay、打开收藏、服务端结算通知时同步；在线每 60 秒同步一次。普通入口 5 秒节流，同一时刻合并未完成请求。
- 服务器返回当前 UTC 与下次重置 UTC；客户端用单调运行时间推进倒计时，不采用电脑日期。钱包和任务共用 Revision，旧响应不能回滚新余额。退出／切换账号清理任务缓存。

## 老客户端参考

本地来源：`E:\Hbackup\FileRecv\Gwent\Gwent\Gwent_Data\Managed\Assembly-CSharp.dll` 及 `StreamingAssets/AssetBundles/gui`。

- `GwentUnity.Progression.CrownPiecesHelper`、`RewardsPreviewController`、`UIRewardsPreviewDailyTab`：小王冠递进、分档奖励和每日奖励表。
- `DailyQuestSectionRenderer`、`DailyQuestSlotRenderer`：任务条目、进度和已完成状态。
- `mainmenu_base/UIMainMenuRewardsPreviewPrefab`（path ID `-3599517124505096909`）：1600×900 设计基准、约 930×562 中部内容区、左右原装饰和原背景。
- 原素材 `mainmenu_background`、`ranked_play_season_left_white/right_white`、`current_player_bg`、`Crown`、`crown-empty`、`Progression_bar_bg/fill`、`timer_preview_icon` 已提取并导入 `Assets/Resources/DailyQuests`；粉尘图标复用已导入的原 Powder。

原客户端并不包含我们服务器的数据库实现。这里按它的任务／奖励表现移植到现有 UGUI，并按本轮需求缩为两类任务、统一粉尘奖励；不是导入其整套网络服务，也没有做原客户端运行截图的像素差验收。

## 已验证与复验

2026-09-14 最新修复已经解决旧数据空字段、异常结算、登录认证、内部历史暴露和客户端刷新/初始化等 8 类问题，奖励全量回归 269/269 通过，包含真实 Unity 界面。详见 [修复报告](RewardFixVerification.md)；以下保留历史记录。

2026-09-14 按处理日期计冠改动已通过完整奖励矩阵 100 项和独立每日任务 22 项检查，包含跨日去重、封顶重试、旧文档升级、跨零点并发冲突与数据库重启；详见 [本次验证记录](RewardSystemVerification.md)。以下条目保留 2026-09-13 原规则及界面的历史验收信息，其中原有每日任务 20 项现扩展为 22 项。

- `src/Cynthia.Card/test/DailyQuestTest`：20 项数据库／规则／真实 BigRoundEnd 结算测试通过，涵盖 24 并发登录、20 重复小局回调、各档累计奖励、超额上限、零点边界、服务重建、时钟倒退、跨账户隔离、合成并发，以及双方胜负／平局。
- `scripts/Test-DailyQuestsHub.ps1`：10 项实际本地 SignalR 测试通过，含登录主动发奖、重复登录、认证身份、禁止客户端指定日期与伪造王冠。
- 既有 `Test-PremiumCraftingHub.ps1` 更新新账号登录奖励预期后，12 项合成／选用／持久化回归通过。
- `Assets/Editor/DailyQuestVerification.cs` 仅本地编辑器测试使用，验证真实同步、本地倒计时以及反复刷新不重复领取，状态和 Game 截图保存于 `work/DailyQuests`。
- 已在 Unity Play 模式检查 0／2／4／6 冠画面，确认金色进度条、各档到账状态和今日 10／20／40／70 粉尘；截图为 `ui-zero/two/four/six.png`。任务组件挂在主菜单对象上，避免收藏页隐藏后停止倒计时和键盘更新。
- 修复后重新编译并通过主菜单组件启用状态检查；实际鼠标点击入口、关闭按钮、再次打开和 Esc 关闭均已验证。

这些检查包含真实结算方法和真实网络接口，但尚未完整走一场双真人联网对局。编辑器快速登录测试还记录到现有 `GameInit.LoadServerMessage` 在切场景后访问已销毁登录界面的异常；该原有登录脚本未在本轮修改。

本轮仅构建和运行本地服务器／Unity 编辑器；未部署线上、未发布新的 Windows 客户端。服务端与客户端应一起更新，新客户端的公共模型 DLL 也已经按现有流程刷新。

从仓库根目录复验：

```powershell
./scripts/Start-LocalServer.ps1
./scripts/Test-DailyQuestsHub.ps1
./scripts/Test-PremiumCraftingHub.ps1
./work/LocalServer/dotnet/dotnet.exe ./src/Cynthia.Card/test/DailyQuestTest/bin/Debug/netcoreapp3.0/DailyQuestTest.dll
```

单元／集成测试固定使用本机 28020 测试库和随机隔离账号，模拟时间仅注入测试服务实例，既不修改操作系统时间，也不改变正在运行的服务器时钟。
