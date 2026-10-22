# 默认 Debug 构建平台判定修复

## 用意与原因

上次构建缺少 build/TargetPlatform.props；本次处理时 build 目录及相关文件已存在于工作区。重新构建出现 Cannot determine OneLauncher target OS。

TargetPlatform.props 由 Directory.Build.props 提前导入，此时 SDK 尚未为未指定的 Configuration 设置默认 Debug。原有本机平台回退要求 Configuration 等于 Debug，因此普通 dotnet build 未选择目标 OS，也可能漏选 Core 的 Windows 专属依赖。

## 修改与扩展

在 build/TargetPlatform.props 的平台判断之前，仅当 Configuration 为空时设置为 Debug，与 SDK 默认值一致。显式 Configuration 和提前导入的发布配置继续优先；带 RID 的构建继续按 RID 选择目标平台。未来添加平台规则时，应继续在依赖包求值之前确定目标系统，并保留 Release 必须指定 RID 的限制。

## 验证

- `dotnet build OneLauncher/OneLauncher.csproj --no-restore` 成功，0 个错误，324 个现有警告；包含任务中心事件刷新修改的编译验证。
- `dotnet build OneLauncher.Desktop/OneLauncher.Desktop.csproj --no-restore -consoleloggerparameters:ErrorsOnly` 在沙箱外成功，0 个错误。沙箱内曾因 AvaloniaStatsTask 无权访问用户本地 Licensing/Tickets 目录失败，未修改项目来绕过该任务。
- 属性求值确认：默认构建为 Debug/Windows；显式 Release/linux-x64 为 Linux；MacOSJit 发布配置为 Release/osx-arm64/macOS；各自使用对应的平台编译常量。

本次未执行发布或界面运行验证。
