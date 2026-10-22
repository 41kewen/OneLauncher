# Forge / NeoForge AOT 适配检查

日期：2026-10-22。检查对象为当前工作树的 Forge / NeoForge 生产代码及其 Download、Launch、PCL 导入调用链，并通过真实桌面程序发布检查这些改动是否进入 Native AOT 编译。本轮不运行实验工具，不下载或启动游戏，也不运行发布后的程序。

## 结论

当前 Forge / NeoForge 生产实现未发现需要修复的 AOT 阻碍，Windows x64 桌面程序的真实 Native AOT 发布成功。此结论覆盖源码检查、裁剪分析、原生编译与链接，不表示本轮完成了 AOT 程序的下载、登录、启动或世界运行验收。

实验项目 `ConsoleTest` 和 `tests/OneLauncher.ForgeTests` 存在反射及默认 JSON 序列化调用，当前应作为 JIT 实验工具使用；它们不被桌面入口引用，其可行性实验结果不能直接视为 AOT 业务验收结果。本轮未扩大范围修改实验工具或公共架构。

## 生产代码的检查依据

下表的 Forge 内部文件均位于 `OneLauncher.Core/DefaultComponent/Mod/ModLoader/forgeseries/`。

| 位置 | 实现及 AOT 判断 |
| --- | --- |
| `JsonModel.cs` | `ForgeSeriesJsonContext` 为版本 JSON、install profile、嵌套库、processors、data 和 promotions 提供源生成元数据。条件参数与扩展 data 使用 `JsonElement`，没有将未知 JSON 内容交给运行时任意类型构造。 |
| `Installation/ForgeInstallerParser.cs` | 读取 install profile 时明确使用 `ForgeSeriesJsonContext.Default.ForgeSeriesInstallProfile` 的 `JsonTypeInfo` 重载。协议识别和分发为静态调用。 |
| `Profiles/ProfileReader.cs` | 明确使用 `ForgeSeriesJsonContext.Default.ForgeSeriesVersionJson` 读取现代版本配置。 |
| `Legacy/LegacyProfileReader.cs` | 使用已反序列化的 profile 和 `JsonElement.GetRawText()` 保留旧配置，没有反射序列化回退。 |
| `ForgeVersionListGetter.cs` | promotions 使用源生成 `ForgePromotionData`；Maven 元数据用 `XDocument` 读取。构建比较类型通过显式构造函数创建，没有按字符串动态创建 C# 类型。 |
| `ForgeSeriesUsing.cs` | 启动前以源生成版本类型读取配置，然后调用静态加载器识别、库路径及参数逻辑。 |
| `ForgeSeriesInstallTasker.cs`、`Legacy/ForgeRealmsProcessor.cs` | Mojang 元数据通过 `JsonDocument` 读取；最终版本配置直接写入安装器中的 JSON 文本，没有 options/object 序列化调用。 |
| `Processors/ForgeProcessorTokens.cs`、`Launch/ForgeLaunchArguments.cs` | JSON 规则按 `JsonElement` 枚举；正则没有使用 `RegexOptions.Compiled` 或运行时 IL 生成。静态委托、LINQ 与字符串处理均在本次实际编译链内。 |
| `Processors/ForgeProcessorRunner.cs` | 从 JAR manifest 读取的是 Java 主类名称，随后用 `ProcessStartInfo.ArgumentList` 启动外部 Java；没有将 JAR 当作 .NET 程序集加载，也没有使用 `Assembly.Load` 或 `Reflection.Emit`。 |
| `Installation/ForgePaths.cs`、`ForgeFileVerifier.cs`、`ForgePreparedInstall.cs`、`JarMod/JarModSupport.cs` | 文件、摘要、ZIP、集合及协议判断采用普通静态代码；未发现动态代码或不受约束的类型反射。JarMod 仍明确拒绝，AOT 发布成功不改变其支持状态。 |
| `ModSources/ForgeProvider.cs`、`NeoforgeProvider.cs` | Provider 直接创建并持有同一 Tasker；准备和执行均通过明确接口调用，没有反射发现插件。 |
| `Strategys/ForgeStrategy.cs`、`NeoForgeStrategy.cs` | `LaunchCommandBuilder.CreateAndInitStrategy` 的枚举 switch 直接调用各 Strategy 的 `CreateAsync`，因此裁剪器可追踪工厂、配置读取及参数构建。平台常量与 WindowsAot 的目标一致。 |
| `ModLoaderDetector.cs`、`Tools.MainClassToModEnum` | 根据字符串、Maven 构件和参数证据识别加载器，没有把 Minecraft 的 mainClass 当作 C# 类型调用 `Type.GetType`。 |
| `Helper/ImportPCL2Version/PclVersionInspector.cs`、`ImportMain.cs` | 继承链以 `JsonDocument` 解析，结果用显式 record 创建，导入接入现有下载服务；没有动态创建结果类型。 |

表中 `ModSources`、`Strategys`、`ModLoaderDetector.cs` 等路径见[完整代码说明](Forge-NeoForge代码说明与实验结果-20261022.md)。本轮没有新增源文件、保留程序集描述符或 AOT 警告抑制。

## 实际发布证据

环境：Windows x64，.NET SDK 10.0.402，net10.0，WindowsAot 配置。发布使用以下命令，完整日志在当前工作树 `build/aot-forge-audit/publish.log`。

```powershell
dotnet publish OneLauncher.Desktop/OneLauncher.Desktop.csproj `
  -p:PublishProfile=WindowsAot `
  -p:JsonSerializerIsReflectionEnabledByDefault=false `
  --nologo -v minimal -clp:ErrorsOnly `
  -o build/aot-forge-audit/publish `
  -fl '-flp:logfile=build/aot-forge-audit/publish.log;verbosity=normal'
```

| 检查 | 本次结果 |
| --- | --- |
| 发布退出码 | 0；日志汇总为 0 错误、243 警告，耗时 59.75 秒。 |
| 真实原生编译 | 日志有 `IlcCompile`、`Generating native code`、ILCompiler 调用及 MSVC `link.exe` 链接步骤。 |
| JSON 反射关闭 | 实际 `.ilc.rsp` 同时包含 `--feature:System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault=false` 和对应 `--runtimeknob`，未依赖默认反射序列化完成编译。 |
| 动态代码关闭 | 实际编译参数包含 `System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported=false`。 |
| AOT / 裁剪诊断 | 未发现 ILxxxx 或 SYSLIBxxxx 警告/错误；`PublishValidation.targets` 启用分析且把链接器和原生编译器警告设为错误。工具链已有的 IL2121 抑制仍在编译参数中，本轮没有修改它。 |
| 原生入口 | `OneLauncher.Desktop.exe`，25,994,240 字节；只读取 PE 头检查，Machine=8664（x64），CLR 数据目录 RVA 和 Size 均为 0。 |
| 发布目录 | 入口 EXE 及 `av_libglesv2.dll`、`libHarfBuzzSharp.dll`、`libSkiaSharp.dll`、`msalruntime.dll`；没有入口托管 DLL、CoreCLR 或 clrjit。应分发整个目录。 |

入口 EXE 的 SHA-256：`6B1AD2D9818DC6D4CE6D2151C421932B0BBFF12487416FDCB0E6E47577258113`。日志、响应文件和发布目录是本地构建证据，未将原生二进制或 obj/bin 文件加入源码迁移。

## 限制与后续维护

243 项警告不是 AOT 检查零警告：其中有可空性、枚举 switch、命名、未使用变量、MVVM Toolkit 等现存源码警告，以及两个窗口的 AVLN3001 资源加载警告。它们不属于本次 Forge 动态代码/裁剪阻碍，但本次编译成功也不代表这些历史问题全部解决。

本轮没有在 Linux/macOS 或 ARM64 工具链上进行原生编译，没有执行 Windows AOT 程序。Java 安装器处理器和 Minecraft 仍由外部 Java 运行，.NET 的 AOT 发布不会替代 Java。历史游戏实验的范围和结果继续以[实验结果文档](Forge-NeoForge代码说明与实验结果-20261022.md)为准。

后续增加 profile 字段或新 JSON 根模型时，继续注册源生成上下文，并传具体 `JsonTypeInfo<T>`；保持 Provider、协议解析器与 Strategy 的显式调用，避免把 Java 主类识别转换为 .NET 反射加载。新增实际动态代码需求时，应重新评估并进行同配置原生发布；无需为当前已通过的实现增加广泛保留或抑制告警。
