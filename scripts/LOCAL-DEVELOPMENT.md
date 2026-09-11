# 本机战斗测试

已按《老昆特参与开发流程指导20200501.pdf》第 2–3 页搭建：

- MongoDB 4.2.25：`127.0.0.1:28020`，独立数据目录 `work/LocalServer/data`。
- .NET SDK 3.1.426：`work/LocalServer/dotnet`，仅启动脚本使用，不修改系统 SDK。
- 服务端：`http://127.0.0.1:5005`，数据库 `gwent-diy`。
- Unity 保持项目指定的 2019.4.1f1，不降级到旧教程中的 2019.2。

## 开始测试

1. 在仓库根目录运行 `powershell -ExecutionPolicy Bypass -File scripts/Start-LocalServer.ps1`。
2. Unity 菜单选择 `Tools > Legacy Gwent > Server > Local`。编辑器默认使用本地；更改后退出 Play，再点 `▶ Login`。
3. 本机测试账号：用户名 `localtest`，密码 `LocalTest123`。账号已附带一套初始牌组；也可自行注册新账号。远端账号不会自动出现在本地数据库中。
4. 进入普通匹配，选中初始牌组，在匹配口令填写 `ai#f` 后开始匹配，强制与 AI 对局。它是匹配口令，不是账号密码。
5. 在设置中开启动态卡，然后检查战场、查看牌组和墓地等入口。

`Tools > Legacy Gwent > Server > Remote` 可恢复远端连接；这个选择只影响 Unity 编辑器，常规打包客户端仍使用原来的远端地址。

## 停止与重建

停止：`powershell -ExecutionPolicy Bypass -File scripts/Stop-LocalServer.ps1`。保留数据库数据。

修改服务端后，先停止，再运行 `powershell -ExecutionPolicy Bypass -File scripts/Start-LocalServer.ps1 -Rebuild`。

日志和本地连通性验证：`work/LocalServer/logs`、`work/LocalServer/verification.json`。已验证注册、登录、初始牌组、AI 匹配成功以及收到 GameOperation；这不代替 Unity 战斗画面的人工验收。

工具来自官方归档，下载地址与校验值保存在 `work/LocalServer/downloads`。旧工具只绑定本机回环地址供此项目调试。未复制或覆盖 Unity 的 Common.dll，也未改远端数据库。
