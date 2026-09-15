# 闪卡双客户端联网验收

2026-09-14 补充回归：新构建、两场实际对战、612 次采样，本次条件触发的 46 项渲染检查通过。由于间谍卡分支随出牌触发，不能等同于下面历史的 50 项逐项复现。另有赛季弹窗和既有饰品错误的范围限制，详见 [本轮报告](RewardFullSuiteVerification.md#本轮双客户端渲染回归)。复跑可使用 `scripts/Test-PremiumNetworkRegression.ps1 -RunId <新名称>`；历史目录保留不覆盖。

日期：2026-09-13。目标是验证真实 Windows 客户端经过本地服务器对战后，双方看到的普通／闪卡版本与卡牌拥有者选用一致。

## 验证方式

- 同一个 Unity 2019.4.1f1 Development Build 启动两个独立进程，分别以 `premium-network-a`、`premium-network-b` 登录本地 `127.0.0.1:5005`。两个账号只用于测试。
- 通过正式注册、登录、合成、选用和密码匹配接口进入同一真人对战房间；双方使用同一套合法初始卡组，并交错选用普通卡和闪卡。
- 第一场 A 选用闪卡领袖、B 选用普通领袖；第二场交换。场上卡牌版本仍按各自账号选择。
- 探针读取战斗界面已收到的 `CardStatus`、实际 `DynamicCardView` 模型和渲染纹理，每隔约 0.5 秒采样。动画通过 64×64 实际渲染帧的像素哈希变化验证，普通卡则检查未启用动画模型和渲染层。每约 2 秒保存完整 Game 画面。
- 出牌由测试驱动调用正常界面的选择回调或拖放输入通道，仍经过正常网络发送、服务器规则校验和双方广播；不直接修改战斗数据或伪造客户端卡牌。
- 检查己方／对方领袖、己方／对方场上单位，以及对手未公开手牌不泄露闪卡状态。

## 复验入口

`Assets/Editor/PremiumNetworkBuild.cs` 读取 `work/PremiumNetwork/build.request`，构建到独立的 `work/PremiumNetwork/Client`，不覆盖发布目录。通过既有动态包构建回调验证并打入资源；构建期间的动态资源开关在结束后恢复。

`Assets/DynamicCards/Runtime/PremiumNetworkProbe.cs` 仅在编辑器／Development Build 编译，并且必须显式传入 `-premium-network-a` 或 `-premium-network-b` 才启动。开发版本该参数固定连接本机测试服务器，不支持任意服务器或账号。

从仓库根目录执行：

```powershell
./scripts/Test-PremiumNetworkPlayers.ps1 -Round 1
# 保留第一场证据、关闭第一场两个测试进程后：
./scripts/Test-PremiumNetworkPlayers.ps1 -Round 2
python ./scripts/Verify-PremiumNetwork.py
```

相同证据目录已经有采样时脚本会拒绝覆盖。测试资金通过既有管理员脚本幂等发放，仅写两个隔离测试账号；用户原有账号、卡组和收藏未修改。

## 首轮发现

原项目 `IsAutoPlay` 路径在菜单选卡时先等待自身输入通道写入，再开始读取，测试停在硬币选择。保留首轮证据于 `round1-*-initial-leaders`，后续使用正常界面输入回调推进，不把停在开场的采样视为场上卡牌验收。

首轮领袖实际渲染已验证：A 自己的闪卡领袖 164 个不同帧，B 看该对手领袖 167 个不同帧；B 的普通领袖在两端均无动画模型。正式完整结果以 `work/PremiumNetwork/verification.json` 为准。

测试日志还记录了既有初始账号饰品缺项（`CurrentTrinkets.Update`、`NoAvatarRound`）和部分原动态资源零面积粒子发射警告，未将这些日志表述为整个客户端无错误。

## 最终结果

两场各采样约 100 秒，4 个客户端视角共 634 次采样，`verification.json` 的 50 项检查全部通过。

| 场次 | 选用 | A 视角实际不同领袖动画帧 | B 视角实际不同领袖动画帧 |
| --- | --- | --- | --- |
| 1 | A 闪卡、B 普通 | 自己 157，对手 0 | 自己 0，对手 158 |
| 2 | A 普通、B 闪卡 | 自己 0，对手 155 | 自己 158，对手 0 |

0 帧的普通领袖在整个采样期间没有启用动态模型或动态渲染层。两场的每个视角都覆盖己方和对方普通单位、至少一种具有多个实际动画帧的闪卡单位；隐藏手牌未泄露闪卡版本。坎塔蕾拉进入对面场地后仍按出牌方版本展示，翻牌 0.15 秒中点前保留旧卡背、翻牌后正确启用对应动画。

构建成功，0 个构建错误；两个进程使用相同测试客户端。`Assembly-CSharp.dll` SHA-256 为 `1205D76F39B91EA9B2DB9D325B28B3D72F2E4F12E4FC874BF471463CDDADCD5E`，另存 `build-evidence.json`。测试结束后已关闭四个正式采样测试进程，本地服务器保留运行。

截图示例：`round2-a/frame-084.png`、`round2-b/frame-084.png`。完整采样、截图、错误日志和初始未完成出牌的证据均保留在 `work/PremiumNetwork`。本轮验证的是本机两个独立 Windows 客户端之间的真实联网显示，不代表公网部署、跨机器网络条件或全部卡牌资源已经验收。
