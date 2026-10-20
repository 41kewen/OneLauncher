# PCL2 版本导入的 UI 进度报告修复

日期：2026-10-20。基线：上游 `master` 的 `34fb71f4dc0aed0aa49adfbea66e34dd93f508ea`。

## 问题与原因

主页 `HomePageViewModel.ImportVersionByPCL2()` 将导入进度转换为
`MainWindowShowFlyoutMessage`。每次文件进度都会产生新通知，导致通知堆叠、长文件名换行并遮挡主页。
原方法也没有创建 TaskManager 任务、向导入器传递任务取消令牌，且直接丢弃了加载器安装阶段的进度。

## 最小修复

生产代码只修改 `OneLauncher/Views/ViewModels/HomePageViewModel.cs` 中的
`ImportVersionByPCL2()`，共增加 36 行、删除 16 行；另增加本报告。

1. 有效目录选定后，通过已有构造函数注入的 `_unitedCore.TaskManager.CreateTask("PCL2版本导入")`
   创建一个任务，再调用已有 `OpenTaskCenter()`。继续使用注入的任务中心工厂，不新建全局管理器或读取静态 UnitedCore。
2. 取消令牌只取自该任务的 `task.Token.Token`，同时交给后台执行和 `PCL2Importer`。
   UI 层不自行创建、释放或替换 `CancellationTokenSource`，任务中心取消按钮操作的就是同一令牌源。
3. 开始、逐文件进度和完成状态均显示在同一任务卡片，移除这些阶段的弹窗消息及逐文件调试输出。
   保留文件计数，按现有回调计数计算百分比，处理总数为零及比例越界；显示加载器安装进度。
4. 在 UI 线程创建 `Progress<T>`，将进度回调切回 UI 线程；将导入放入 `Task.Run`，避免本地同步复制占用 UI 线程。
5. 忽略下载器的中间 `Done`，只有整个 `ImportAsync` 正常返回且令牌未取消时，才写入 100% 并调用 `SetDone()`。
6. 与任务令牌有关的取消保留 `Canceled`；其他导入异常通过 `SetFailed(ex)` 保留原始异常，在任务中心展示。
   任务离开 `Running` 后忽略迟到回调，防止覆盖完成、失败或取消状态。

保留文件选择器取消、`PCL/Setup.ini` 目录校验及其单次无效目录提示；任务创建前的异常继续使用原处理方式。
本次没有修改 `.mrpack` 导入、PCL2 核心迁移逻辑、TaskManager、依赖注入注册或其他页面，也没有增加依赖。

## 验证结果

环境：Windows x64、.NET SDK 10.0.302、Avalonia 11.3.18。

| 检查 | 结果 |
| --- | --- |
| 桌面项目 Debug 构建 | 成功，0 错误；已有警告保留 |
| 上游原有 UI/JSON smoke | 34 项断言通过，反射 JSON 禁用 |
| PCL2 UI 隔离行为验证 | 25 项断言通过 |
| 旧代码问题复现 | 1000 次文件回调产生 1002 条通知（含开始和完成），任务队列为空 |
| 相同回调在修复后执行 | 仅一个任务，导入期间无通知，阶段和文件计数持续更新 |
| 任务中心视觉检查 | 实际 XAML 渲染的运行中、完成、失败、取消卡片均正常；长文件名、错误详情及取消按钮可用 |
| 目标平台配置检查 | 6 个发布配置、48 次项目求值、6 组覆盖参数及 7 组预期失败检查均输出 PASS |
| 差异检查 | `git diff --check` 通过 |

隔离验证在仓库外的临时夹具中直接抽取并编译修改前后的方法，替换文件选择器、实际导入器及弹窗接口，
使用真实 `TaskManager`、真实任务中心 ViewModel/XAML 和取消命令，不执行网络下载或读取用户实例。
覆盖文件选择取消、无效目录、同一令牌传递、后台执行、安装阶段、零总数/计数越界、密集回调、
提前 `Done` 后失败、取消后正常返回、无关取消异常、迟到回调和任务创建前异常。
这些临时验证文件和截图未加入 PR，保持提交范围为一个生产文件及本报告。

上游 `global.json` 锁定 SDK 10.0.402，本机仅安装 10.0.302，未修改锁定配置。
构建和 smoke 从仓库外以绝对项目路径执行，并显式使用仓库已有 `NuGet.sandbox.config`，绕过本机无效的离线包目录。
平台检查使用原脚本的本地副本，仅将 SDK 调用定向到已安装的 10.0.302 并指定工作目录，未改动断言。
这不代表已验证锁定 SDK、跨平台发布或 Native AOT。

使用仓库锁定 SDK 时，可在项目根目录复查：

```powershell
dotnet build OneLauncher.Desktop/OneLauncher.Desktop.csproj -c Debug -p:RestoreConfigFile=NuGet.sandbox.config
dotnet build tests/OneLauncher.AotSmoke/OneLauncher.AotSmoke.csproj -c Debug -p:RestoreConfigFile=NuGet.sandbox.config
dotnet tests/OneLauncher.AotSmoke/bin/Debug/net10.0/OneLauncher.AotSmoke.dll
./build/Test-TargetPlatform.ps1
```

## 人工验收与范围

1. 在主页选择有效 PCL2 版本目录，确认任务中心自动打开，只增加一个“PCL2版本导入”任务。
2. 观察文件计数、阶段文字及进度条，确认下载、迁移和安装期间没有连续弹窗。
3. 点击任务卡片“取消”，确认令牌传递给导入器，任务显示“已取消”，不会变成“已完成”。
4. 分别验证成功和导入失败：成功显示 100%/已完成；失败显示异常及具体错误。
5. 取消文件选择不产生任务；无效目录仍只提示一次。

本次未执行真实整合包的完整联网导入/启动；核心取消响应速度、已复制文件的清理以及下载计数语义仍由原导入器决定。
进度条依据当前回调计数展示，并非整个导入耗时的线性百分比。此 PR 是 UI 层的最小修复，与已有 #12 的入口改动存在重叠，#12 保留不变。
