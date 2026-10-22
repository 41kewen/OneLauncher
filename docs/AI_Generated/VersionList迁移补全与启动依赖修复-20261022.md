# VersionList 迁移补全与启动依赖修复

日期：2026-10-22。

## 范围

按用户提供的 `VersionList独立存储迁移行动报告-20261022.md` 检查已存在的迁移代码，并补齐遗漏。报告中的示例作为参考；不删除已有实现，也不读写真实用户配置。版本业务继续使用 UnitedCore 持有的 VersionListManager，全局设置、Java 和服务器数据继续使用 DBManager。

## 已核对的现有实现

- 当前根目录索引为 `<GameRootPath>/versions/version_list.json`，管理器提供列表快照、查询、并发合并登记和排序保存。
- BasicDataManager 支持迁移初始数据、版本索引严格校验、同目录临时文件提交；提交成功后才通知观察者。
- 旧全局 VersionList 通过 LegacyVersionList 兼容入口读取，迁移前备份、检查当前目录版本元数据、新索引重读成功后清理旧字段。
- 合法的新索引包括空列表均优先；损坏索引不自动清空。清理旧配置失败时保留旧字段，重启可重试。
- UnitedCore 在创建其他服务前初始化同一个版本管理器；安装服务持有它。
- 版本页面、下载面板、新建实例面板和相关工厂已迁到新管理器。
- 下载、PCL2、mrpack 的正式安装都调用 VersionInstallerService；底层下载器不再直接报告最终 Done。
- 前端和实际编译的 Core 中，没有遗留通过 DBManager 读写业务 VersionList 的调用。

## 本次补全

1. `VersionListMigration.cs`：备份失败转换成包含实际路径的 OlanException；已有索引和旧全局列表并存时也生成迁移结果提示。
2. `MainWindow.axaml.cs`：主窗口完成页面初始化后显示迁移结果和备份位置。
3. `DownloadInfoCreater.cs`：创建下载信息时，先校验根目录与版本 ID，再读取或下载元数据；元数据路径明确按请求根目录计算。避免安装服务最终检查之前已经向错误目录写文件。
4. `NewGameDataPaneViewModel.cs`：预选版本通过可观察属性赋值，初始化其可用加载器及实例名称，避免只有底层字段有值。
5. `GameDataManger.cs`：默认实例查询安全处理空列表、失效映射和自定义版本 ID，配合服务器收藏默认可见与导航保存。

## 启动异常与修复

用户报告 PowerPlayPaneViewModelFactory 无法解析 GameDataManager。根因是 Init 只注册 DBManager、下载工具和 UnitedCore，而部分前端组件仍要求容器分别提供 UnitedCore 内部的管理器。

本次将以下组件改成依赖 UnitedCore，并从中取现有管理器，不额外创建或注册第二套实例：

- PowerPlayPaneViewModelFactory：游戏实例管理器。
- InstallModPaneViewModelFactory：游戏实例管理器。
- AddServerPaneViewModelFactory：DBManager 保持注入，实例管理器改从 UnitedCore 获取。
- EditGameDataPaneViewModelFactory：实例与账户管理器。
- UserModelLoginPaneViewModelFactory：账户管理器和微软身份验证器。
- GameDataPageViewModel：实例管理器。

NewGameDataPaneViewModelFactory、DownloadPaneViewModelFactory 和 TaskCenterPaneViewModelFactory 已符合此模式，继续复用。没有更改桌面启动项目和 settings.axaml.cs。

## 回归检查

新增可单独运行的 `tests/OneLauncher.RegressionTests`，不调用 Init.Initialize 或真实身份验证、下载、联机。数据都写在指定输出目录的唯一子目录。

检查包含：

- 迁移合并加载器标记，保留添加时间，其他全局配置不丢失，备份内容与原配置一致。
- 非当前目录与非法路径条目跳过；合法空新索引不重复导入；不同目录索引相互隔离。
- 空文件、JSON null、缺失列表、语法损坏、显式 null 列表和非法 ID 均报错并保留源数据。
- 使用文件共享锁模拟清理旧配置失败；重启逻辑可重试。
- 同版本并发登记多个加载器不重复、不丢标记；快照不能反向修改管理器。
- 旧排序快照不丢失刚登记的版本，排序重读后仍一致。
- 使用文件共享锁模拟索引提交失败，磁盘原件、内存列表和事件计数不被错误更新，临时文件清理。
- 已取消登记与安装不登记版本；安装目录和元数据目录不匹配在下载前拒绝。
- DBManager、AccountManager、GameDataManager 保存及读取回归。
- 使用 MainWindow 实际追加的服务注册，ValidateOnBuild 检查完整启动依赖图；实际解析全部九个工厂和六个可离线构造的页面，检查联机工厂使用同一个游戏实例管理器。
- 设置页行为、浮窗生命周期与导航栏行为见配套文档。

## 验证范围和后续扩展

Debug 解决方案和回归项目构建通过，离线回归全部通过。完整重新编译仍有已有的空引用、未等待调用及源生成相关警告；本次没有将它们全部消除。

没有执行真实网络下载安装、PCL2 或 mrpack 全流程、微软登录、联机和 Native AOT 发布；这些入口的迁移通过源码调用链检查和编译验证，离线测试覆盖目录、取消和索引保存边界。

后续新增 UI 工厂应注入 UnitedCore，使用其现有管理器；若确需直接注入管理器，先在集中服务注册中明确映射同一实例，并补充启动依赖图检查。新增安装入口应调用 VersionInstallerService，新增业务查询使用 VersionListManager。安装目录切换仍按报告约定重启生效。
