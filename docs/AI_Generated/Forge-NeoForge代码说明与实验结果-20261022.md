# Forge / NeoForge 代码说明与实验结果

日期：2026-10-22。本文对应本次局部实现及最后修复。安装与启动继续使用现有服务、Provider 和 Strategy；没有新增全局服务，也没有改变实例存储模型。本轮收尾只进行构建，不继续游戏实验。

## 1、所有 Forge 代码文件的作用

以下路径相对 `OneLauncher.Core/DefaultComponent/Mod/ModLoader/forgeseries/`。

| 文件 | 作用 |
| --- | --- |
| `JsonModel.cs` | 定义版本清单、旧 install、现代 profile、库、条件参数、data、processor、outputs 和源生成 JSON context；Forge/NeoForge 构建版本的比较类型也在此处。 |
| `ForgeVersionListGetter.cs` | 从官方 Maven 与 promotions 选择完整构建；保留历史 Minecraft 后缀；处理早期 NeoForge 的 forge 构件、beta 筛选及 26.2 日历版本；校验指定版本和 URL。无构建时明确失败。 |
| `ForgeSeriesInstallTasker.cs` | Provider 持有的一次安装执行器。准备输入快照、在临时目录安装、校验完整库与产物、最后发布配置；同一游戏根的 Forge/NeoForge 发布串行。另处理 Mojang mappings 的元数据下载。 |
| `ForgeSeriesUsing.cs` | 读取已安装的加载器配置，检查主类、继承版本和加载器身份；根据客户端规则提供运行库，保留 classifier，缺失库明确失败。 |
| `Installation/ForgePreparedInstall.cs` | 保存一次安装的归档、版本 JSON、profile、客户端处理器和库分类；下载列表排除内嵌与生成产物。包含协议枚举和库文件记录。 |
| `Installation/ForgeInstallerParser.cs` | 拆包、识别 legacy/profile、核对 MC 和加载器；按嵌入文件、HTTPS 来源、处理器输出区分库，检测路径与摘要冲突。 |
| `Installation/ForgePaths.cs` | Maven 坐标转换及目录边界约束，允许合法构建标识中的 `+`；防止相对引用越界。 |
| `Installation/ForgeFileVerifier.cs` | 非空、长度、SHA-1、归档结构检查；通过同目录临时文件替换发布库与配置。 |
| `Legacy/LegacyProfileReader.cs` | 读取历史 `install + versionInfo`，保留旧版本 JSON。 |
| `Legacy/ForgeRealmsProcessor.cs` | 为旧 `DEOBF_REALMS` 补充原版 JSON、Realms 库及显式 `--json/--libs`，避免工具推导到工作目录之外。 |
| `Profiles/ProfileReader.cs` | 读取 spec 0/1 profile 引用的版本 JSON，拒绝未知 spec 或缺失配置。 |
| `Processors/ForgeProcessorTokens.cs` | 解析客户端 data、上下文、Maven 引用及安装器资源，检测未知/循环占位符与路径越界。 |
| `Processors/ForgeProcessorRunner.cs` | 从 manifest 取得处理器主类，使用 ArgumentList 启动 Java，同时读取两路输出；按退出码和 outputs 判定结果，取消时停止进程并收尾。 |
| `Launch/ForgeLaunchArguments.cs` | 计算条件参数和平台规则，解析旧参数，仅提取 legacy tweakClass 增量；为现有字符串命令合同编码单个参数。 |
| `JarMod/JarModSupport.cs` | 明确拒绝未实现的历史 JarMod；1.1 仍缺前置组件与旧启动适配，不能视为完成支持。 |

相关入口与配套文件：

| 文件（相对仓库根） | 作用 |
| --- | --- |
| `OneLauncher.Core/DefaultComponent/Downloader/DownloadMinecraftProviders/ModSources/ForgeProvider.cs` | Forge 下载入口：选择构建、取得依赖、调用同一 Tasker 执行安装并转发进度。 |
| 同目录 `NeoforgeProvider.cs` | NeoForge 下载入口：使用独立身份与配置文件，复用安装协议实现。 |
| 同目录 `IModLoaderConcreteProviders.cs` | 依赖准备与安装合同；准备阶段允许文本回调。FabricProvider/QuiltProvider 的修改用于兼容该合同和准备进度。 |
| `OneLauncher.Core/DefaultComponent/Downloader/DownloadMinecraftProviders/DownloadMinecraftPlanner.cs` | 创建原版和加载器文件清单，转发准备细节；每个 Provider 的新增条目只汇总一次。 |
| `OneLauncher.Core/DefaultComponent/Launcher/Strategys/ForgeStrategy.cs` | 为 Forge 提供主类、运行库、JVM/game 增量参数，使用传入游戏根与 MC ID。 |
| 同目录 `NeoForgeStrategy.cs` | 为 NeoForge 提供相同合同，读取 `version.neoforge.json`。 |
| `OneLauncher.Core/DefaultComponent/Mod/ModLoader/ModLoaderDetector.cs` | 结合主类、加载器自身 Maven 库与 tweak/fml 参数识别加载器；共享主类证据不足、身份冲突时失败。早期 NeoForge 的 Forge 共存库不会夺走 Neo 身份。 |
| `OneLauncher.Core/Helper/Tools.cs` 中 `MainClassToModEnum` | 保留单参数兼容入口，同时支持传入库与游戏参数，委托 Detector。其他工具职责不变。 |
| `OneLauncher.Core/Helper/ImportPCL2Version/PclVersionInspector.cs` | 收集 PCL 清单继承链证据、基础版本与加载器；缺父版本、循环、过深链与缺主类失败。 |
| 同目录 `ImportMain.cs` | PCL 导入接入 Inspector；导入安装仍使用现有服务，不承诺保留原精确构建。 |
| `OneLauncher/Views/Panes/PaneViewModels/DownloadPaneViewModel.cs` | 显示准备细节，阶段切换及时刷新，终态不受迟到回调覆盖。 |
| `OneLauncher.Core/DefaultComponent/Mod/ModManager/ForgeSeriesModInfoHelper.cs` | 原有 Forge 系列模组信息读取器：读取模组 TOML 的名称、版本、描述和图标；不负责加载器安装，本次未改。 |

实验代码不属于生产安装逻辑：`ConsoleTest/Program.cs` 初始化核心及捕获异常；`ForgeIntegrationTests.cs` 串行调用真实安装/启动入口并保存结果；`ForgeProtocolChecks.cs` 检查保存的官方归档；`ForgeProbeMod.cs` 与 `ProbeMod.java` 生成实验模组；`tests/OneLauncher.ForgeTests/Program.cs`、`RegressionChecks.cs`、`Probe.java` 用离线 HTTP fixture 与真实 Java 处理器检查协议、失败、取消、重试和 PCL。最后收尾不运行这些实验。

## 2、从 Download 和 Launch 入口按时间顺序说明运作

### Download

1. 调用方创建 `DownloadInfo`，取得 MC 元数据、游戏根、加载器选择及下载策略，然后调用 `VersionInstallerService.InstallNewVersion`。
2. 服务校验游戏根，创建 `DownloadMinecraft`。`MinecraftBasic` 启动必要的 Java 安装，并调用 Planner。
3. Planner 收集原版库、资源索引、资源文件、主 JAR 和日志配置，然后选择 ForgeProvider 或 NeoforgeProvider。准备文本经现有 progress 回调到界面。
4. Provider 从指定版本或 Getter 的官方列表选择完整构建。Forge 推荐/最新偏好必须匹配 Maven 中真实完整坐标；早期 NeoForge 1.20.1 使用 `net.neoforged:forge`。
5. Tasker 下载 installer，在内存中解析。Parser 按归档内容选择 LegacyProfileReader 或 ProfileReader；检查 MC、身份、路径，解析客户端处理器及输出，将库分为远程、内嵌、生成三类。此时不发布版本配置。
6. Planner 返回远程下载清单。现有下载编排下载 client、原版库、assets、日志与加载器依赖；模组安装任务可能与原版资源任务并行。Provider 的 RunInstaller 等待 Java 就绪后进入 Tasker。
7. Tasker 获取同一游戏根的安装发布锁，在系统临时目录写 installer 和原版 JAR 副本，准备内嵌库，验证或重新下载远程库，有可信摘要的生成缓存才复用。
8. 按 profile 原顺序运行客户端处理器。Tokens 解析参数和资源；可用官方元数据直接下载 Mojmap；DEOBF_REALMS 补齐上下文。Runner 只有在 outputs 全部摘要有效时跳过；无 outputs 仍运行，退出码非零直接失败。
9. 校验已知 patched/slim/extra 与完整依赖，先复制库，最后替换 `versions/<MC>/version.forge.json` 或 `version.neoforge.json`。finally 清理本次临时目录、释放锁。失败不会发布新的配置，但不承诺全部库的跨文件回滚。
10. 现有编排等待安装任务，按设置进行文件校验；服务登记版本索引后报告 Done。用户应在此完成后启动，避免共享目录的下载/启动竞态。

### Launch

1. 调用 `GameLaunchService.CreateLaunchService` 创建会话，再调用 `Play`，传入实例或 GameData。
2. 现有 LaunchService 准备登录依赖和语言设置；GameLauncher 读取实例、账户、原版版本清单，建立 LaunchCommandBuilder，并设置游戏路径、ModEnum 与额外参数。
3. Builder 按 ModEnum 创建 ForgeStrategy 或 NeoForgeStrategy。Strategy 以游戏根和 MC ID 调用 ForgeSeriesUsing，读取对应加载器配置并校验身份与继承版本。
4. Builder 构建 classpath：原版库字典由加载器运行库覆盖/补充，classifier 分开保留，最后加入原版主 JAR。安装工具依赖不会全部作为运行库引入。
5. Strategy 提供主类；legacy 清单仅追加 tweakClass，账户和路径由既有 Builder 生成。现代配置提供条件 game/jvm 参数、模块参数，替换库目录等占位符并编码单个参数。
6. Builder 将额外 JVM、基础 JVM、主类和游戏参数构成 LaunchCommand，现有 GameLauncher 创建 Java 进程，转发 stdout/stderr、退出事件，LaunchService 更新任务状态。
7. 调用 Stop 时停止会话进程；实验工具等待 Play 收尾后才进入下一项。进程存在、渲染初始化、主菜单、世界保存是不同完成层级。

边界：现有存储为每个 MC/加载器一个配置槽；安装锁只覆盖 Tasker 安装发布，不能让整个公共下载流程或运行中的游戏变成事务。旧 JVM Builder 的策略增量、超长命令临时文件寿命及所有跨平台路径仍有公共入口限制；本次不通过大规模架构改造隐含解决它们。

## 3、实验结果

以下只汇总保存证据与用户确认，不把最终构建替代游戏实验。

| 项目 | 实际结果与来源 |
| --- | --- |
| Forge 1.7.10、1.8.9、1.12.2、1.15、1.16.5、1.18.2、1.19.2、1.20.1 | 用户确认下载与启动通过；其中六个 installer 样本另完成离线 fixture 的真实处理器/内嵌库与 Strategy 检查。 |
| 六个 fixture 样本 | 历史实现说明记录 113 项断言通过，含生成 SHA-1、中文/空格根目录、原版摘要不变、失败/取消/重试、outputs 和 PCL 继承判断。不是本轮重新运行。 |
| Forge 1.20.4、1.21.1、1.21.4、1.21.9、26.2 | `build/forge-expanded-20261022/integration/results.json` 记录安装成功及渲染标记；1.20.4 为时限后主动停止，其余为渲染后退出码 0。不能仅据此确认世界验收。 |
| NeoForge 1.21.1、26.2 | 同一历史集成记录为安装成功、渲染后退出码 0。 |
| Forge 1.14.3 | `retest-0b33/results.json` 记录 27.0.60 安装通过，启动退出码 0 但未出现渲染标记，自动启动判定失败。按用户要求不再复测。 |
| NeoForge 1.20.1 | 同一复测记录为 47.1.106 安装成功、渲染后退出码 0，修复版本别名解析后贯通；按要求不再复测。 |
| Forge 1.13.1 | 保存的官方元数据及集成结果没有对应构建；实现报告明确“没有可用构建”，不伪造支持。 |
| Forge 1.1 | ZIP 叠加类加载实验缺 BaseMod/ModLoaderMP；原版 JAR 未修改。当前明确拒绝 JarMod，仍未实现完整支持。 |
| 自编 1.21.1 实验模组 | world-probe-v2 记录 Forge 两次 MOD_LOADED 且有渲染标记，截图显示实验 JAR 缺 pack 元数据警告；两次无 MENU_READY/WORLD_READY/WORLD_SAVED，均为 probe-failed。NeoForge 世界实验被停止，没有完成结果。不得宣称世界/重启验收全部通过。 |
| 最终构建 | 本轮 OneLauncher 前端及 Core 编译通过，0 错误，仍有项目已有警告；不继续安装、启动、世界实验。 |

历史结果路径中的 dd6b、0b33、ae76 是产生记录的工作树，不代表 5b16 又重跑了一次。研究归档、游戏库和缓存不作为生产源文件迁移；报告保留实验结论和相对证据位置。

本次最终修复包括：多 Provider 清单重复汇总、同游戏根 Forge/NeoForge 共享库发布串行、Maven 空值/括号约束、启动身份/继承检查、Realms 临时库目录及缓存复用、promotions 流释放、版本集合与比较类型的空值合同。未去掉产品预览标记，也未扩大到 1.1 的新实现。
