# PCL 实例导入及启动修复

## 问题

原导入器按主类识别加载器，但 Forge 和 NeoForge 都可能使用 `BootstrapLauncher`；随后调用普通下载和安装入口，又重新选择加载器，丢失原整合包精确版本、继承链和启动参数。

真实的 Minecraft 1.20.1／Forge 47.4.20 实例还依赖版本 JSON 未显式列出的六个运行 JAR。原目标目录遗漏这些文件，Java 在 `fmlcore` 扫描阶段以退出码 1 结束，游戏窗口尚未创建。嵌套启动任务和错误日志路径又使诊断容易丢失。

## 修改

- 解析精确版本和继承链，按库坐标、FML 参数区分 Forge／NeoForge，保留完整实例 `launch.json`。
- 使用原清单迁移已安装资源，不重新运行另一个加载器安装器；源文件与缓存校验后复用，缺少可下载项才补下载。
- 将隐式 Forge 资源纳入文件迁移，不追加到 classpath；六项必需检查仅应用于已验证的 1.20.1／Forge 47 布局。
- 保留 Mods、配置和存档，拒绝越界路径与链接，验证完成后才发布 staging 实例；失败、取消、登记失败不伪造成功。
- 启动入口使用实例参数和实际游戏目录，单层任务等待进程退出及输出 EOF；保留 stderr、退出码和容错日志，隐藏认证值。
- 长命令参数文件持有到 Java 结束后清理；启动前列出缺失或不可读的关键 Forge 文件。
- 取消以 `PCL/Setup.ini` 为唯一有效性判断，由版本解析给出明确错误。

本 PR 不包括首页通知刷屏修复，也不修改 Forge 的下载安装处理器。

## 独立验证

桌面项目及 ConsoleTest 构建成功，均为 0 错误，原有警告仍存在。独立分支通过 13 项导入回归和 4 项启动生命周期回归。

```powershell
dotnet build ConsoleTest/ConsoleTest.csproj -c Debug
dotnet ConsoleTest/bin/Debug/net10.0/ConsoleTest.dll --pcl-import-regression-tests
dotnet ConsoleTest/bin/Debug/net10.0/ConsoleTest.dll --launch-regression-tests
```

拆分前完整修复曾真实导入 453 个 Mod、3575 个资源对象和六项隐式 Forge 资源，并用最终 EXE 到达主菜单、正常退出码 0。该真实启动没有在本次独立分支上重复，不能等同于拆分后 UI 全流程验收。

## 与其他 PR 的关系

三个 PR 均从同一上游 master 基线独立提出。PCL 和 Forge 使用完全一致的 `DownloadFileOperations`、测试可见性声明及最小测试分发入口；可选部分方法使每组回归在另一组未合并时仍能编译，合并后也不会引入不同版本的重复工具。

未验证 Linux／macOS、Native AOT 或进入存档后的长期运行。本 PR 不上传游戏内容、账号、日志、二进制或本机路径报告。
