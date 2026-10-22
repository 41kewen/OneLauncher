# DefaultComponent 迁移后的后端编译修复

## 用意与问题原因

组件已迁入 `OneLauncher.Core/DefaultComponent`，但 Core 和 ConsoleTest 仍导入旧共享项目的 `.projitems`，使新旧组件同时参与编译，产生重复类型及 JSON 源生成器名称冲突。部分组件的命名空间声明和引用仍使用原有路径；账户、游戏数据、Java 和 MSAL 管理器迁入 UnitedCore 后，后端仍从 Init 读取已删除的属性。

本次只处理后端项目及相关解决方案配置，没有修改 OneLauncher 前端或 OneLauncher.Desktop。

## 修改逻辑

- 移除 Core、ConsoleTest 对 `OneLauncher.Core.Net.projitems` 的导入，并移除解决方案中的共享项目导入元数据。旧目录文件未删除，新组件由 Core 的 SDK 默认编译项纳入构建。
- 将后端组件引用统一到 `OneLauncher.Core.DefaultComponent`，包括账户认证、Java 提供商、服务器、联机、下载、启动、模组和整合包组件。修复 DownloadMinecraft、DownloadInfo 和 LaunchCommandBuilder 各 partial 文件的命名空间，保证它们仍组成同一个类型。
- 将账户、游戏数据、Java 和 MSAL 的后端调用改为 `UnitedCore.Core` 上的对应属性，保留这些管理器由 UnitedCore 创建和持有的结构，没有恢复独立的依赖注入注册。
- 将内部 Core 引用改为带私有 setter 的属性，由 UnitedCore.Initialize 在初始化完成后赋值。Init 继续先初始化配置、目录和下载工具，再创建并注册 UnitedCore。
- 控制台加入服务器时使用已经取得的 UnitedCore.GameDataManager；修复服务器启动代码中的 JavaManger 拼写。
- ConsoleTest 从 Init 返回的服务集合取得已创建的 UnitedCore，避免重复初始化账户文件、MSAL 缓存和任务管理器。

## 后续扩展约定

新增默认组件继续放入 DefaultComponent，并使用相应命名空间。拆分 partial 类型时，各文件的命名空间必须保持一致，不能仅按子目录修改声明。Modrinth 组件现在位于 `DefaultComponent.Mod.ModService.Modrinth`；Fabric 模组 JSON 模型位于 `DefaultComponent.Mod.ModManager`。

对外调用者取得 UnitedCore 后，通过其属性访问管理器；Core 内部旧组件可使用内部 Core 引用，但须在 Init.Initialize 完成后调用。新增管理器应由 UnitedCore 初始化并持有，初始化阶段需要的配置和下载工具仍由 Init 先准备。

## 验证与范围

以下 Debug 构建均成功，0 个错误：

```powershell
dotnet build OneLauncher.Core/OneLauncher.Core.csproj --no-restore --nologo -v minimal
dotnet build OneLauncher.Console/OneLauncher.Console.csproj --no-restore --nologo -v minimal -clp:ErrorsOnly
dotnet build ConsoleTest/ConsoleTest.csproj --no-restore --nologo -v minimal -clp:ErrorsOnly
```

Core 首次修复后构建有 218 个警告；整理内部 Core 属性后，后续完整 Core 编译有 217 个警告，主要为可空性等警告，本次没有扩大范围处理。ConsoleTest 的最后一次构建为 0 警告、0 错误（其 Core 引用使用增量构建结果）。

后端源码及项目配置扫描未发现旧组件命名空间和迁出管理器的 Init 调用残留。`git diff --check` 通过。未进行前端构建、应用运行、在线登录、游戏下载或跨平台发布验证。
