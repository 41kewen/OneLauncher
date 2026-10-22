# VersionList 独立存储迁移行动报告

日期：2026-10-22。

状态：仅完成源码调查和行动方案；本次只新增本报告，未修改业务代码、项目配置或运行时数据，未运行构建或应用。

范围已由用户确认：**保留 DBManager，迁移全部版本相关引用。** DBManager 继续管理全局设置、Java 列表和服务器收藏；新管理器负责当前安装目录中的已安装游戏版本。所有下述代码均为实施草案，不代表已经实现。

## 1. 当前实现与问题

| 位置 | 当前行为 | 本次需要处理的影响 |
| --- | --- | --- |
| `OneLauncher.Core/Global/ModelDataMangers/DBManager.cs:45–63` | `AppConfig.VersionList` 与 `AvailableJavas`、`ServerList`、`OlanSettings`、`DefaultSettings` 共存 | 版本索引位于用户目录的全局 `config.json`，无法区分不同安装目录 |
| `OneLauncher.Core/Global/Init.cs:43–50` | 先读取全局配置，再根据 `OlanSettings.InstallPath` 确定 `InstalledPath`、`GameRootPath` | 新索引必须在路径确定后初始化 |
| `OneLauncher.Core/Global/UnitedCore.cs:20–46` | 初始化并持有账户、实例、Java、安装服务等管理器 | 新版本管理器应由此处创建，并作为同一个 UnitedCore 实例的属性持有 |
| `OneLauncher.Core/Helper/Models/UserVersion.cs:12–16` | `VersionPath` 根据当前 `Init.GameRootPath` 动态生成，不写入 JSON | 切换路径后，旧全局列表中的条目会被解释为新目录中的版本 |
| `OneLauncher/Views/Panes/PaneViewModels/DownloadPaneViewModel.cs:164–181` | 下载成功后由前端合并加载器标记、登记版本，再保存 DBManager | 版本登记与界面耦合；其他安装入口不能复用 |
| `OneLauncher.Core/Helper/ImportPCL2Version/ImportMain.cs:78–93` | 直接调用底层下载，最后登记游戏实例 | 没有登记已安装版本；现有 `_configManager` 字段没有实际用途 |
| `OneLauncher.Core/DefaultComponent/Mod/ModPack/ModpackImporter.cs:149–164` | 同时读取全局下载线程数和 `VersionList`，并直接调用底层下载 | 此处必须拆分两种数据来源，且统一安装后的登记 |
| `OneLauncher/Views/ViewModels/VersionPageViewModel.cs:97–109` | 订阅 DBManager 事件，版本数据异常时删除全局 `config.json` | 应订阅版本管理器；版本索引异常不能再删除全局配置 |
| `OneLauncher.Core/Global/ModelDataMangers/BasicDataManger.cs:35–65,74–88` | 缺文件时立即保存空数据；空文件按默认数据处理；保存直接截断原文件 | 迁移须先准备初始数据，版本索引需识别损坏文件，保存需避免中途写坏索引 |

`VersionList` 在 AXAML 中也用作页面集合名称。它仍是 UI 属性，无须为了存储迁移改名。Mojang 的在线版本目录 `MojangVersionList` 和 `version_manifest.json` 也属于不同数据源，保留当前职责。

## 2. 目标存储和管理器

实际路径统一计算为：

```csharp
Path.Combine(Init.GameRootPath, "versions", "version_list.json")
// 等价于 Path.Combine(Init.InstalledPath, "minecraft", "versions", "version_list.json")
```

默认安装目录时，结果为 `<Init.BasePath>/installed/minecraft/versions/version_list.json`；自定义安装目录时，结果为 `<InstallPath>/minecraft/versions/version_list.json`。不能固定拼接 `Init.BasePath/installed`，否则无法解决本次问题。

建议新增 `VersionListManager : BasicDataManager<VersionListData>`，保持现有数据管理器的继承模式。磁盘 JSON 使用一个小型根对象：

```json
{
  "VersionList": [
    {
      "VersionID": "1.20.1",
      "modType": {
        "IsFabric": true,
        "IsNeoForge": false,
        "IsForge": false,
        "IsQuilt": false
      },
      "AddTime": "2026-10-22T12:00:00"
    }
  ]
}
```

采用根对象是为了保留 `.Data.VersionList` 的使用习惯，并满足 `BasicDataManager<T>` 的 `class, new()` 约束。新文件只包含版本数据，不复制整个 AppConfig。保留现有 UserVersion 和 ModType 的字段、版本排序及添加时间语义，不在本次改造加载器身份模型。

新文件中的核心代码草案：

```csharp
public sealed class VersionListData
{
    public List<UserVersion> VersionList { get; set; } = new();
}

[JsonSerializable(typeof(VersionListData))]
[JsonSerializable(typeof(UserVersion))]
[JsonSerializable(typeof(ModType))]
public partial class VersionListJsonContext : JsonSerializerContext { }

public sealed class VersionListManager : BasicDataManager<VersionListData>
{
    public string GameRootPath { get; }

    public VersionListManager(string gameRootPath)
        : base(Path.Combine(gameRootPath, "versions", "version_list.json"))
    {
        GameRootPath = Path.GetFullPath(gameRootPath);
    }

    protected override JsonTypeInfo<VersionListData> CreateJsonTypeInfo(
        JsonSerializerOptions options)
        => new VersionListJsonContext(options).VersionListData;

    // 下面是拟新增的业务接口，具体方法体在实施阶段编写。
    // Task RegisterInstalledVersionAsync(UserVersion installedVersion)
    // Task ReplaceOrderAsync(IReadOnlyList<UserVersion> orderedVersions)
    // UserVersion? FindVersion(string versionId)
}
```

`RegisterInstalledVersionAsync` 按现有 `VersionID` 去重，合并 Fabric、NeoForge、Forge、Quilt 标记，保留已登记条目的 `AddTime`。将查询、修改、保存置于同一个管理器操作锁内，防止同时安装导致重复条目或丢失加载器标记。`ReplaceOrderAsync` 只修改排序，不丢弃操作期间新增的版本。页面获取列表快照；避免继续让多个调用方直接修改同一 `List`。

序列化继续使用源生成 `JsonTypeInfo<T>` 重载，以保持项目现有 AOT 兼容方式。

## 3. 单例与初始化顺序

UnitedCore 增加：

```csharp
public required VersionListManager VersionListManager { get; init; }
```

推荐顺序：

1. `Init.Initialize()` 创建并读取现有 DBManager，继续注册其原有 DI 单例。
2. 根据 `OlanSettings.InstallPath` 确定安装目录和 Minecraft 根目录，准备下载工具。
3. 调用 `UnitedCore.Initialize()`。
4. UnitedCore 创建一个 VersionListManager，并调用迁移辅助器完成旧数据迁移及新索引初始化。
5. 初始化其余管理器，将此版本管理器传入 VersionInstallerService 构造函数。
6. 把这个管理器赋给 UnitedCore 的属性，待 UnitedCore 完整构造后再赋值 `UnitedCore.Core`。
7. `Init` 注册这个 UnitedCore 实例并返回服务集合。

初始化草案：

```csharp
var versionListManager = new VersionListManager(Init.GameRootPath);
await VersionListMigration.InitializeAsync(
    Init.ConfigManager,
    versionListManager,
    Path.Combine(Init.BasePath, "config.json"));

// UnitedCore 对象初始化器新增 / 修改：
VersionListManager = versionListManager,
VersionInstallerService = new VersionInstallerService(versionListManager),
```

初始化期间使用已创建的局部变量和 `Init.ConfigManager`，不能提前读取尚未赋值的 `UnitedCore.Core.VersionListManager`。DBManager 的创建和安装路径读取继续留在 Init。

前端页面已有 UnitedCore 参数时，从该参数取新管理器并保存 `private readonly` 字段。工厂改为接收 UnitedCore，再将其中的管理器传给 Pane；这是项目当前“由 UnitedCore 持有管理器”的做法。**不额外 `new` 管理器，也不增加独立静态 Instance。**

相关工厂目前还直接请求 AccountManager、GameDataManager，而 Init 只注册 UnitedCore。修改本次涉及的两个工厂时，一并从注入的 UnitedCore 获取这些已有实例，保证新构造函数可以实际被 DI 解析；不扩大到其他工厂重构。

## 4. 旧 config.json 的迁移

迁移必须先读旧数据，再决定新文件的初始内容。不能直接删除 AppConfig 的原属性，也不能先调用当前 `InitializeAsync()` 写一个空新文件，再以“新文件已存在”为理由跳过迁移。

建议在 AppConfig 中暂时保留只用于兼容的入口：

```csharp
[JsonPropertyName("VersionList")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public List<UserVersion>? LegacyVersionList { get; set; }
```

替换原业务属性 `VersionList`；其默认值为 null。旧 JSON 的 `VersionList` 仍能读入，未完成迁移时仍会保存这部分数据，避免其他配置保存提前丢失旧列表。迁移成功后置 null 并保存 DBManager，此后全局 JSON 不再写出 `VersionList`。业务调用全部使用新管理器；兼容字段后续可在明确的兼容周期结束后删除。

迁移辅助器按以下分支运行：

| 启动时状态 | 行动 |
| --- | --- |
| 新文件不存在，旧列表不存在或为空 | 初始化空 `VersionListData`；若旧 JSON 有空列表，成功后清除兼容字段 |
| 新文件不存在，旧列表非空 | 先备份原 config，准备迁移数据，再用这些数据初始化新管理器；保存并重新读取验证成功后清除旧字段 |
| 新文件已存在且结构有效，包括合法空列表 | 新文件为当前目录的权威数据源；不自动把旧全局列表重新导入。若仍有旧字段，先备份再清除；差异保留在备份供恢复 |
| 新文件为空字节、JSON 损坏、根对象为 null、列表显式为 null或条目无有效 VersionID | 抛出包含实际路径的 OlanException，保留新旧文件；不能自动覆盖成空列表，也不能当作迁移未执行 |
| 新文件写入或验证失败 | 不清除旧字段，不保存清理后的全局配置，移除本次未完成的临时文件，保留可重试状态 |
| 新文件写入成功，但清理旧配置失败 | 新索引仍可恢复；下次启动读取有效新文件后重试清理，不重复导入 |

备份建议放在 `<Init.BasePath>/config.pre-version-list-migration.<timestamp>.<unique-id>.json`，不覆盖既有备份。备份和迁移都是实施后的运行时行为，本次不读写用户的真实配置。

旧全局列表没有来源安装目录，迁移不能凭列表判断所有条目都属于当前目录。至少检查当前根目录中的 `versions/<VersionID>/version.json`；只把与当前目录匹配的条目纳入新索引，其余保留在备份并给出迁移结果提示。路径必须限制在当前 `versions` 目录内，不能把 VersionID 当任意文件路径。这个检查是目录归属检查，不等于完整安装或 SHA1 验证；继续保留原条目的加载器标记和时间，不据此推断新信息。

迁移发生于发布新索引之前。新索引一旦有效建立，就不再因切换目录而从全局配置恢复同一批条目。

## 5. BasicDataManager 的配套改动

为让新管理器复用现有基类，需要修改 `BasicDataManger.cs`，保持既有调用兼容：

- 将初始化扩展为 `InitializeAsync(T? initialData = null)`。文件不存在时使用迁移器准备的初始数据，否则仍使用 `new T()`；文件存在时始终读取文件，不使用初始数据覆盖它。
- 提供一个默认为关闭的严格文档检查选项，由 VersionListManager 启用；拒绝空字节文件和 JSON null。新管理器在初始化时验证列表与条目结构。其他管理器的既有空文件策略不因版本迁移改变。
- `Save()` 改为先写同目录唯一临时文件，完成序列化并关闭流，再替换目标文件；失败时保留原文件。无需新依赖或平台专有 API。
- `OnDataChanged` 在最终文件提交、流关闭、保存锁释放后触发。失败不发事件；事件处理器能够立即读取完整索引，并避免在保存锁内重入保存。
- 保存锁负责文件提交；新版本管理器的操作锁负责列表修改和去重，两者职责不同。版本页事件继续通过 Dispatcher 投递到 UI 线程，事件处理器不再直接写索引。

共享 Save 行为有变化，实施时需要验证现有 DBManager、AccountManager、GameDataManager 的保存和事件刷新，而不是仅测试新 JSON 文件。

## 6. 全部版本调用的改动

### 6.1 版本页面

`VersionPageViewModel.cs` 删除 DBManager 字段和构造函数参数，从 UnitedCore 取得版本管理器：

```csharp
_versionListManager = unitedCore.VersionListManager;
VersionList = _versionListManager.Data.VersionList
    .Select(x => new VersionItem(x, _unitedCore)).ToList();
```

订阅 `_versionListManager.OnDataChanged` 刷新列表。排序命令改为可等待的异步命令，调用 `ReplaceOrderAsync`；不再修改和保存 DBManager。同步更新旧注释。

删除该文件中“版本数据异常后删除 config.json”的恢复回调。加载和迁移错误由后端抛出带索引路径的 OlanException，前端交给 OlanExceptionWorker；显式恢复流程如后续需要，再由版本管理器提供，不在 ViewModel 删除数据文件。

### 6.2 下载与版本登记

将 `DownloadPaneViewModel.cs:164–176` 的按版本查找、追加、合并加载器逻辑移到 `VersionListManager.RegisterInstalledVersionAsync`。安装成功的登记由 VersionInstallerService 完成。

`VersionInstallerService.InstallNewVersion` 改为异步：

```csharp
await new DownloadMinecraft(info, progress, cancelToken).MinecraftBasic();
(cancelToken ?? CancellationToken.None).ThrowIfCancellationRequested();
await _versionListManager.RegisterInstalledVersionAsync(info.VersionInstallInfo);
```

同时检查 `info.GameRootPath` 与管理器根目录一致，不允许把安装到其他根目录的版本登记进当前索引。使用规范化绝对路径及对应平台的路径比较规则。现有自定义根目录 API 的全面改造不在本次范围；不一致时在开始下载前报告错误。

目前底层下载在取消时有多处普通 `return`，仅等待方法返回不足以证明安装成功，登记前必须检查取消状态。将“下载完成”进度移到公共服务的索引保存成功之后；`DownloadMinecraft` 最后的 Done 报告移除，避免版本未登记时 UI 已宣布整个安装完成。

下载面板保留游戏实例的添加、默认实例映射和通知；删除版本合并、DBManager 字段、DBManager 参数及 `_configManager.Save()`。实例保存继续使用 GameDataManager，不能机械替换它的 Save。

### 6.3 PCL2 与整合包导入

- PCL2Importer 移除未使用的 DBManager 字段；保留底层下载器用于制定复制计划，把正式安装调用改为 `UnitedCore.Core.VersionInstallerService.InstallNewVersion(...)`。只有正式安装成功后登记版本；不要在生成计划时登记。
- ModpackImporter 同时保留全局配置管理器和新增的版本管理器／安装服务引用。下载线程数仍读取 DBManager，版本查找读取 VersionListManager，正式安装改走 VersionInstallerService。
- 整合包“已安装加载器”查询不能继续只比较 `modType.ToModEnum()`，因为已有条目可以同时标记多个加载器。按请求类型检查对应布尔标记，再保留现有加载器元数据版本检查。
- 校验传入整合包 `gameRoot` 与当前管理器根目录一致；不同目录不能混用当前索引。现有前端传入 `Init.GameRootPath`，无需修改调用处。

这样下载面板、PCL2、整合包和 ConsoleTest 的公共安装入口都会复用同一套版本登记。不同游戏实例仍由各入口按现有语义保存，本次不把实例登记强行统一进版本服务。

### 6.4 新建游戏实例面板与工厂

`NewGameDataPaneViewModel` 的 DBManager 字段和参数改为 VersionListManager，`AvailableBaseVersions` 从新索引取得快照。账号列表和游戏实例保存保持原管理器。

`DownloadPaneViewModelFactory` 移除 DBManager 依赖，改为仅从注入的 UnitedCore 取得其所需服务并传给 Pane。`NewGameDataPaneViewModelFactory` 同样接收 UnitedCore，更新两个 Create 重载，传入 VersionListManager、AccountManager、GameDataManager。

## 7. 文件清单

### 7.1 需要新建

| 文件 | 内容 |
| --- | --- |
| `OneLauncher.Core/Global/ModelDataMangers/VersionListManager.cs` | VersionListData、AOT JSON 上下文、VersionListManager；版本查询、登记、排序和初始化验证 |
| `OneLauncher.Core/Global/ModelDataMangers/VersionListMigration.cs` | 旧列表兼容迁移、备份、当前目录归属检查、重读验证、清理旧字段和重试分支 |
| `docs/AI_Generated/VersionList独立存储迁移行动报告-20261022.md` | 本次已经新增的行动报告 |

运行时生成的新数据文件是 `<InstalledPath>/minecraft/versions/version_list.json`，不是应提交进源码仓库的配置样例或项目文件。

### 7.2 必须修改的 13 个源码文件

| 文件 | 具体改动 |
| --- | --- |
| `OneLauncher.Core/Global/ModelDataMangers/DBManager.cs` | 原 VersionList 业务属性替换为可清理的 LegacyVersionList 兼容入口；保留其他 AppConfig 字段和 DBManager 类型 |
| `OneLauncher.Core/Global/ModelDataMangers/BasicDataManger.cs` | 支持迁移初始数据和严格文档校验；临时文件提交保存；成功提交后触发事件 |
| `OneLauncher.Core/Global/Init.cs` | 明确路径计算及初始化顺序；继续创建全局 DBManager，版本管理器交由 UnitedCore 初始化 |
| `OneLauncher.Core/Global/UnitedCore.cs` | 增加 required VersionListManager 属性；创建并迁移初始化；将同一实例传给安装服务并持有 |
| `OneLauncher.Core/Services/VersionInstallerService.cs` | 构造函数接收版本管理器；校验目标根目录；安装完成且未取消后登记、保存索引，再发 Done |
| `OneLauncher.Core/DefaultComponent/Downloader/DownloadMinecraftProviders/DownloadMinecraft.cs` | 删除未使用的 DBManager 字段；把最终 Done 报告移交公共安装服务 |
| `OneLauncher.Core/Helper/ImportPCL2Version/ImportMain.cs` | 删除无用途的 DBManager 引用；实际安装走公共服务，计划生成保持原入口 |
| `OneLauncher.Core/DefaultComponent/Mod/ModPack/ModpackImporter.cs` | 拆分全局配置和版本索引读取；改版本查找与加载器判断；安装走公共服务；检查根目录 |
| `OneLauncher/Views/ViewModels/VersionPageViewModel.cs` | 替换数据读取、事件订阅与排序保存；去掉版本异常时删除 config.json 的回调；同步注释 |
| `OneLauncher/Views/Panes/PaneViewModels/DownloadPaneViewModel.cs` | 删除 DBManager 字段、参数、版本登记和配置 Save；保留实例保存和默认实例设置 |
| `OneLauncher/Views/Panes/PaneViewModels/NewGameDataPaneViewModel.cs` | DBManager 参数与字段换为 VersionListManager，基础版本选项从新索引取得 |
| `OneLauncher/Views/Panes/PaneViewModels/Factories/DownloadPaneViewModelFactory.cs` | 移除 DBManager，改注入 UnitedCore，并更新 Create 参数传递 |
| `OneLauncher/Views/Panes/PaneViewModels/Factories/NewGameDataPaneViewModelFactory.cs` | 改注入 UnitedCore，通过其属性取得版本、账户、实例管理器，更新两个 Create 重载 |

新增 C# 文件由 SDK 默认编译项包含，不需要修改 `.csproj` 或解决方案。前端 AXAML 绑定名称不变，也无需修改 `OneLauncher.Desktop` 启动项目。

### 7.3 已审查、继续保留的 DBManager 引用

这些调用读取其他职责的数据，不迁移到版本管理器：

| 文件 | 保留理由 |
| --- | --- |
| `OneLauncher.Core/Global/JavaManager.cs` | AvailableJavas 及其保存 |
| `OneLauncher.Core/DefaultComponent/Downloader/DownloadMinecraftProviders/DownloadInfoCreater.cs` | 下载线程数、SHA1 和下载源设置 |
| `OneLauncher.Core/Minecraft/VersionsList.cs` | Mojang 在线清单的刷新时间及全局配置保存 |
| `OneLauncher.Core/DefaultComponent/Launcher/GameLauncher.cs` | 默认 JVM 参数 |
| `OneLauncher.Core/DefaultComponent/Server/MinecraftServerManger.cs` | 服务器启动默认 JVM 参数 |
| `OneLauncher.Core/DefaultComponent/Account/Microsoft/MojangProfile.cs` | Crafatar 回退地址 |
| `OneLauncher/Views/ViewModels/HomePageViewModel.cs` | 默认实例 ID |
| `OneLauncher/Views/ViewModels/SettingsPageViewModel.cs` | 启动器设置、服务器收藏和默认参数 |
| `OneLauncher/Views/ViewModels/ServerPageViewModel.cs` | 服务器列表、默认服务器及事件订阅 |
| `OneLauncher/Views/Panes/PaneViewModels/AddServerPaneViewModel.cs` | 添加服务器收藏 |
| `OneLauncher/Views/Panes/PaneViewModels/EditServerPaneViewModel.cs` | 编辑、删除服务器收藏及默认服务器设置 |
| `OneLauncher/Views/Panes/PaneViewModels/Factories/AddServerPaneViewModelFactory.cs` | 传递服务器收藏配置管理器 |
| `OneLauncher/Views/Panes/PaneViewModels/Factories/EditServerPaneViewModelFactory.cs` | 传递服务器收藏配置管理器 |
| `OneLauncher/Views/Panes/PaneViewModels/InstallModPaneViewModel.cs` | 模组下载的 SHA1 设置 |
| `OneLauncher/Views/MainWindow.axaml.cs` | UI 设置读取及全局配置事件订阅 |
| `OneLauncher.Console/Boot.cs` | 服务器收藏查找 |

整合包导入器的下载线程设置、Init 的全局配置初始化、迁移辅助器的兼容读取也保留 DBManager。验收标准是没有**业务版本数据**再经过 DBManager，而不是 DBManager 字样完全消失。

全文搜索还命中 `OneLauncher.Core.Net/Account/Microsoft/MojangProfile.cs` 和 `OneLauncher.Core.Net/Server/MinecraftServerManger.cs`。该旧共享目录仍在磁盘，但当前 Core 与 ConsoleTest 项目及解决方案已经移除其导入；两处命中也是全局配置，无 VersionList 业务访问，本次无需修改。不能把旧目录与实际编译的 DefaultComponent 文件当成两套待迁移入口。

## 8. 安装目录切换行为

当前 `InstalledPath` 在 Init 初始化时确定，普通构建中不可由页面直接赋值；SettingsPageViewModel 当前也没有安装目录编辑命令。推荐本次保持**修改配置后重启生效**：

- 原目录有自己的 `version_list.json`，切到另一目录后加载另一目录的文件。
- 新目录没有索引且没有待迁移的旧字段时，显示空版本列表，不自动沿用原目录的版本。
- 切回原目录时恢复原列表和顺序。
- 用户自行搬迁整个 installed 目录时，索引跟随目录搬迁；UserVersion 的路径继续按当前根目录计算，无需迁移 JSON 中的绝对路径。

本次不移动游戏 JAR、libraries、assets、实例文件或 Java 运行时，也不自动扫描所有目录重建索引。Java 和默认实例、服务器数据对安装目录的潜在关联仍属于它们各自的后续任务。

如果后续要支持运行中切换安装目录，需要协调停止安装与启动任务、重载实例管理器及索引、刷新页面和重新绑定路径；仅给新管理器改一个文件路径不足以实现该能力。

## 9. 执行与验证顺序

1. 先落实新数据模型、基类保存能力和迁移器，验证旧列表不会提前丢失。
2. 接入 UnitedCore 初始化和公共安装服务，打通下载／PCL2／整合包的登记。
3. 更新三个 ViewModel 和两个工厂，替换事件及排序保存。
4. 全量扫描 `.Data.VersionList`、DBManager、Init.ConfigManager、Init.ConfigManger，逐项核对本报告的“迁移／保留”清单。
5. 构建 Core、Console、ConsoleTest、前端 OneLauncher；检查 JSON 源生成及 MVVM 命令生成。按现有项目配置验证 AOT 发布中的新序列化上下文。
6. 在隔离的临时配置目录中验证以下场景，不以真实用户安装目录作为测试数据。

| 场景 | 通过标准 |
| --- | --- |
| 首次安装，无旧新数据 | 新文件位于当前安装目录，包含合法空列表 |
| 旧列表迁移 | 版本 ID、时间、加载器标记和顺序保留；新文件重读成功；全局配置其他字段不丢失 |
| 已有新文件／合法空列表 | 不重导入旧列表，不覆盖已有顺序；冲突旧数据可从备份找回 |
| 写入中断、无权限、清理旧配置失败 | 原文件和备份保留；重启能够重试；失败不会发成功事件 |
| 新文件损坏或显式 null | 报错包含新文件路径；不静默重置，不删除 config.json |
| 切换目录后重启，再切回 | 各自读取独立索引，无跨目录混入 |
| 下载安装成功、失败、取消 | 成功登记一次；失败与取消不登记；Done 在索引提交后发出 |
| 同版本并发安装多个加载器 | 无重复版本，所有成功安装的加载器标记保留 |
| PCL2、mrpack 与公共服务入口 | 已安装版本均登记到同一管理器，实例保存行为保持原入口语义 |
| 新建实例、排序、页面刷新 | 选项从新文件加载；排序重启后保留；仅版本事件刷新版本列表 |
| 全局配置回归及 DI 解析 | 设置、服务器、账号、实例保存仍正常；相关页面和工厂可以从服务容器解析 |

迁移、写入失败和并发登记适合增加有实际断言的自动化测试；项目当前只有 ConsoleTest 手动程序，没有独立单元测试项目。是否新建测试项目及具体框架可在实施阶段确定，本次不把该可选基础设施列入上述 13 个必改源码文件。

## 10. 完成判据与后续扩展

完成后，版本数据的读、写、排序和事件通知都只经过 UnitedCore 持有的 VersionListManager；全局配置仅保留兼容入口直到迁移成功。目录切换后的索引隔离、旧数据可恢复、所有实际安装入口登记一致，是此次验收重点。

后续新增安装入口应调用 VersionInstallerService；新增版本查询使用 VersionListManager。迁移器只负责旧格式过渡，不能成为每次缺索引都恢复全局旧数据的常驻后门。若将来支持多根目录同时管理或更精确的加载器身份，可扩展根数据模型与管理器接口，同时保持当前 JSON 和迁移规则的版本兼容。
