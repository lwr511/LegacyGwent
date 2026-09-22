# GG 粉尘奖励验收（2026-09-21）

本地实现：收到对手 GG 每次 5 粉尘，每个中国日最多 6 次／30 粉尘；第 7 次起不增加粉尘或任务进度。原登录20、王冠25/35/45不变，任务总上限155。每场双方可各送一次；保留原 GG 提示，封顶后的有效 GG 仍可保留社交计数。

## 独立验证

Codex 审阅 DSH 实际修改并独立构建、运行以下检查，未以 DSH 的退出码或报告代替验收。

| 检查 | 结果 | 本地证据 |
| --- | --- | --- |
| GG 规则、身份、并发、跨日、重建和序列化 | 54/54 | `work/GGRewards/independent-GGRewardsTest.log` |
| 每日登录／王冠／GG 与合成并发 | 34/34 | `work/GGRewards/independent-DailyQuestTest.log` |
| 闪卡合成及副本回归 | 21/21 | `work/GGRewards/independent-PremiumCraftingTest.log` |
| 客户端缓存、账号切换、旧响应、通知刷新 | 21/21 | `work/GGRewards/independent-RewardClientTest.log` |
| 实际 SignalR 双账号匹配、投降结算和 GG | 71/71 | `work/GGRewards/network-acceptance.json` |
| 四语言、三份语言库一致性 | 通过 | `work/GGRewards/localization-final.json` |
| 原有四份未提交语言文件内容保留 | 通过，仅增加6个GG键 | `work/GGRewards/preexisting-preservation.json` |
| 最终服务端构建 | 退出0、编译错误0；保留原有警告 | `work/GGRewards/server-build-final.log` |

网络验收使用独立注册的3个测试账号和原 `SendGG(string,string)` 接口，实际进行7场密码匹配及投降结算：接收方前6场累计30，第7场不再发粉尘；每场另重试12次均不重复支付；发送者自己不获奖，反向 GG 独立生效。未认证、伪造发送者、错误接收者、自我和非参赛者请求均拒绝。新任务响应由本次修改前备份的 `Cynthia.Card.Common.dll` 成功反序列化，`DisplayGG` 仍为字符串，`DailyQuestsChanged` 通知实际到达。此检查不是旧版完整 Unity 客户端双开、自然打完三小局的端到端发布验证。

数据库规则测试由 Codex 另起 `127.0.0.1:28028` 隔离 Mongo 执行，使用随机测试账号，结束后停止。网络测试使用本地5005服务和新建的专用测试账号。没有改动普通账号的钱包、操作系统时钟或线上数据。

## Unity 与素材

Unity 2019.4.41f2 成功编译并进入真实 Game 场景；打开任务页读取网络测试接收方的真实账户状态，显示 GG 6/6、30/30，今日50/155（登录20+GG30）。实际画面：`work/GGRewards/ui-gg-live.png`；对应 `ui-gg-gg-live.json` 的 `simulated=false`。

另用纯内存布局夹具检查0/6、3/6、6/6与“王冠完成但GG未满”状态，夹具不写服务器。1920×1080和960×540均已查看，中文、英文、俄文和波兰语标签均未遮挡进度、钱包或关闭按钮。截图分别为 `ui-cn-960-partial.png`、`ui-en-960-crowns-done.png`、`ui-ru-960-full.png`、`ui-pl-960-partial.png`；所有布局夹具标记 `simulated=true`，不作为真实发奖证据。

GG条目复用此前从老昆特客户端提取、已在每日任务页使用的 `Assets/Resources/DailyQuests/current_player_bg`、`Progression_bar_bg/fill`、`divider`及原背景；粉尘图标沿用 `PremiumCrafting/Powder`。没有生成替代美术。素材来源链见 [每日任务文档](DailyQuests.md) 的“老客户端参考”。刻度已修正为六等分。

保留的 `Assets/Editor/GGRewardsVerification.cs` 为编辑器验证入口，只接受本地专用测试账号；自动登录／分辨率控制的临时 `GGAcceptanceControl` 已移除，测试添加的 Game View 分辨率已清理，并恢复原8K分辨率与初始 LoginScene 非Play状态。客户端 Common DLL 与最终本地服务端共享模型已同步。

## 范围与限制

- 本地服务器已重新启动并完成 SignalR HTTP200 检查；未部署线上，未打包或发布新客户端，未提交或推送 Git。
- 没有重跑历史完整 `RewardSystemTest` 的全部矩阵；上表为本轮实际运行的检查。客户端旧测试缺少本地化占位类型的问题已在测试替身中补齐，并新增3条GG缓存／通知检查。
- 对局回执目前在内存保留最近一场、有效6小时；服务器重启前尚未发送的GG不能补领，已处理奖励的持久去重仍有效。去重历史与原王冠账本一样持续增长，尚无归档机制。
- 社交计数／提示与粉尘事务分离；它们失败时会记录日志但没有补发队列，粉尘不会重复或回滚。
