# DefaultComponent 迁移后的前端引用修复

## 用意

后端组件迁入 DefaultComponent 后，前端的 ViewModel、Pane、工厂、转换器和设置页仍存在旧命名空间引用，导致类型及 MVVM 源生成代码无法编译。本次将引用与当前 Core 的实际命名空间对齐。

## 修改逻辑

- 将下载、服务器、账户、联机、整合包及 Modrinth 相关 using 更新为 `OneLauncher.Core.DefaultComponent` 下的对应命名空间。
- Modrinth 搜索模型已经与服务位于同一命名空间，移除失效的 `Modrinth.JsonModelSearch` 引用。
- 清理迁移后重复的 Core using，避免重复导入警告。
- 更新 `settings.axaml` 的 `downmc` 命名空间，使下载源枚举继续用于 AXAML 中的类型引用。
- 构建继续发现服务器页调用了已移除的 `Init.GameDataManger`；改为通过页面已有的 `_unitedCore.GameDataManager` 获取游戏实例。

未修改 OneLauncher.Desktop 启动项目、后端实现或界面布局。

## 后续扩展

前端使用新组件时直接导入 Core 中声明的完整命名空间；模型文件名不等于命名空间名。组件迁移时同时检查 C# using 和 AXAML 的 `using:` / `clr-namespace:` 引用。管理器通过已有 UnitedCore 实例访问。

## 验证

- `dotnet build OneLauncher/OneLauncher.csproj --no-restore --nologo -v minimal -clp:ErrorsOnly` 成功，97 个警告、0 个错误。
- `dotnet build OneLauncher.Desktop/OneLauncher.Desktop.csproj --no-restore --nologo -v minimal -clp:ErrorsOnly` 成功，0 个警告、0 个错误；其中引用项目使用增量构建结果。
- 前端源码扫描未发现旧 `OneLauncher.Core.Net`、`Downloader`、`Launcher` 或 `Mod` 命名空间引用。
- `git diff --check -- OneLauncher` 通过。

首次沙箱内构建被 AvaloniaStatsTask 读取本机 Licensing/Tickets 目录的权限限制阻止；后续通过沙箱外正常构建完成验证，没有调整构建任务。未启动应用，未验证页面运行或登录、下载等在线功能。
