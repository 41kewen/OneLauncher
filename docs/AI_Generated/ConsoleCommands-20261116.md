# 控制台命令结构与安装入口

## 目的与调用方式

控制台支持安装 Minecraft、指定 Fabric 版本、导入 Modrinth 整合包，以及原有的快速启动、加入服务器和释放内存。直接使用命令，不需要 `--console` 前缀。`OneLauncher.Console` 现在生成独立控制台程序；桌面入口仍通过 `Boot.RunBoot(args)` 调用相同命令。

```powershell
OneLauncher.Console.exe --installGameVersion fabric:true version:1.21.5
OneLauncher.Console.exe --installGameVersion recommendedMods:true java:true fabricApi:false version:1.21.5 fabric:true
OneLauncher.Console.exe --installGameVersion quilt:true version:1.21.5 fabric:true quiltQsl:false
OneLauncher.Console.exe --installGameVersion version:1.21 fabric:true fabricVersion:0.18.3 name:1.21-Fabric
OneLauncher.Console.exe --importPack "path:F:/dos/1.21-纯生存.zip" "dataPath:C:/Users/wwwin/OneLauncher"
OneLauncher.Console.exe --quicklyPlay instance:40198947
OneLauncher.Console.exe --joinServer "server:服务器名称"
OneLauncher.Console.exe --help
```

命令参数（例如 `--installGameVersion`）位于首位，其余 `key:value` 参数顺序任意，参数名称不区分大小写。未指定加载器或全部为 `false` 时安装原版。支持同时选择 `fabric:true`、`forge:true`、`neoforge:true`、`quilt:true`，由核心按 Fabric、Forge、NeoForge、Quilt 的顺序为选中的加载器分别创建独立实例；第一个实例设为该游戏版本的默认项，与 UI 一致。支持为已有版本补充安装加载器或重新安装，与 UI 共用版本目录和登记服务。Fabric 可使用 `fabricVersion` 指定版本；未指定时沿用现有下载器的版本选择。`dataPath` 指向启动器数据目录，从该目录的 `config.json` 解析游戏安装路径，默认仍使用当前用户的 OneLauncher 目录。包含空格的 `key:value` 参数应整体加引号。多实例安装时指定 `name` 会自动附加加载器名称。

安装选项与 UI 对应，默认值取自 `DownloadPaneViewModel`：

| 参数 | UI 功能 | 默认值 |
| --- | --- | --- |
| `fabricApi` | 同时安装 Fabric API | `true` |
| `quiltQsl` | 同时安装 QSL | `true` |
| `allowBetaNeoforge` | 允许 NeoForge Beta 版本 | `false` |
| `useRecommendedForge` | 优先使用 Forge 推荐版 | `true` |
| `java` | 自动安装匹配的 Java 运行环境 | `false` |
| `recommendedMods` | 安装兼容的推荐模组和依赖 | `false` |

加载器最低游戏版本限制与 UI 一致：Fabric、Quilt 从 1.14 开始，Forge 从 1.2 开始，NeoForge 从 1.20.2 开始。指定推荐模组需要至少选中一个加载器。推荐模组在游戏实例登记成功后作为独立任务执行，控制台等待其完成；失败或取消时保留已经安装的实例，同时返回非零退出码。

交互式终端中，单个游戏文件的全部下载源失败时可输入 `y` 只重试该文件，其他文件继续下载；使用后端现有的最多五次尝试限制。标准输入重定向时失败直接退出，避免自动化脚本等待输入。Ctrl+C 通过任务中心的取消令牌取消当前安装或推荐模组任务。

## 文件与职责

- `OneLauncher.Console/boot.cs`：帮助文本、命令分发、错误输出与进程退出码。
- `OneLauncher.Console/Program.cs` 与项目文件：独立控制台程序入口。
- `Commands/InstallGameVersion/InstallGameVersionOptions.cs`：类型化安装选项、UI 默认值和版本限制，将选项按名称传入 `DownloadInfo.Create`。
- `Commands/InstallGameVersion/InstallGameVersionCommand.cs`：加载器版本选择、调用 `VersionInstallerService`，成功后登记所有实例并设置默认实例，按需等待推荐模组安装。
- `Commands/ImportPack/ImportPackCommand.cs`：导入 `.mrpack` 或包含一个 `.mrpack` 的 ZIP。只提取整合包，不执行 ZIP 中附带的程序；临时包在结束时清理。
- `Commands/QuicklyPlay`、`Commands/JoinServer`、`Commands/ReleaseMemory`：各自维护相应命令的行为。
- `Infrastructure/CommandArguments.cs`：统一解析 `key:value`，只按第一个冒号拆分，保留 Windows 盘符与值中的冒号；拒绝重复参数、空值、未知选项和非法布尔值。原有快速启动和加入服务器的单位置参数仍兼容。
- `Infrastructure/CommandContext.cs`：核心初始化及服务容器生命周期。
- `Infrastructure/InstallationTask.cs`：接入任务中心、安装进度输出、Ctrl+C 取消与任务状态。
- `Infrastructure/DownloadRetryPrompt.cs`：串行处理并行下载中的失败询问，用户放弃后停止后续询问，取消可中断等待输入。
- `OneLauncher.Core/Global/Init.cs`：初始化时可指定数据目录，并把控制台模式传递到核心。
- `OneLauncher.Core/Global/UnitedCore.cs`：控制台不自动发起启动时资源更新或版本列表加载，由安装、导入命令显式获取列表。桌面继续原有后台加载行为。
- `OneLauncher.Core/DefaultComponent/Resource/Pack/ModpackImporter.cs`：Fabric 安装遵循整合包清单版本，并关闭额外下载最新 Fabric API，避免与包内模组重复。

## 扩展方式与 AOT

增加命令时在 `Commands` 下建立独立目录，实现命令处理类，并在 `Boot` 显式添加分发分支。使用共用参数解析、初始化和任务组件。未引入反射式命令扫描或新的 JSON 序列化模型；配置与整合包解析仍使用项目现有生成的 JSON 上下文，保留 Native AOT 兼容的实现方式。本次未进行完整 Native AOT 发布验证。

## 验证与实际导入状态

控制台与桌面项目均通过 Debug 构建。独立的 `ConsoleTest/agent_test/OneLauncher.ConsoleTests` 离线检查读取 UI 实际默认值进行比较，验证 28 种参数排列、无效选项拒绝、安装选项到核心下载参数的传递、四加载器分别生成实例以及原版下载计划。测试输出位于 `build/console-ui-parity-20261116`，结束后使用 `build/clean.ps1 -Task console-ui-parity-20261116` 清理。该检查不使用真实账户和安装目录，也不重新联网安装全部加载器。现有综合回归项目仍有一处过时的 `AppSettings.IsVersionPageVisible` 引用，未修改该无关代码。

用户提供的 ZIP 内整合包名为“1.21-纯生存”，要求 Minecraft 1.21、Fabric 0.18.3，清单包含 105 个文件。2026-11-16 使用新控制台入口实际导入成功，进程退出码为 0；游戏安装阶段完成 3916 项校验，实例登记 ID 为 `c27f0a32`，目录为 `C:/Users/wwwin/OneLauncher/installed/minecraft/instance/c27f0a32`。随后重新读取持久化索引及 Fabric 元数据，确认版本正确，并对清单的 105 个文件逐一核对 SHA1，全部通过。已打开的启动器需要重新加载实例列表或重启才能显示由独立控制台进程登记的实例。
