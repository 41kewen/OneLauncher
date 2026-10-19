# Download 组件安全问题分析与解决方案报告

## 1. 审计结论

本次审计基于当前仓库代码，对 ZIP 解压路径穿越和 SHA1 校验两个反馈问题进行了调用链核查。

| 问题 | 结论 | 建议等级 | 核心原因 |
| --- | --- | --- | --- |
| ZIP 解压路径穿越（Zip Slip，CWE-22） | **确认存在** | **高危 / P1** | 自定义解压函数直接将 `entry.FullName` 与目标目录拼接，未做规范化后的目录边界检查，并允许覆盖文件；该函数用于 Java ZIP 和 Minecraft native 库解压。 |
| SHA1 校验机制缺陷 | **确认存在** | **高危 / P1** | 校验集合只包含下载前“不存在或长度不符”的文件，部分文件在校验前已被解压或执行；`.mrpack` 文件下载流程完全未使用清单中的 SHA1。 |
| SHA-1 算法本身强度不足 | **存在，但不是当前最直接的缺陷** | 中低危 / P2 | SHA-1 已不适合作为新设计中的抗碰撞安全散列；但当前更紧迫的问题是校验被绕过和校验时机错误。 |

综合判断：两个反馈均有事实依据。ZIP 路径穿越可导致启动器进程权限范围内的任意文件覆盖；SHA1 相关问题会使恶意或损坏文件在未通过完整性校验时落盘、解压，甚至作为 Java 处理器执行。两者还可能形成风险叠加，不能只通过“解压后再校验”解决。

---

## 2. 审计范围与主要调用链

重点核查文件：

- `OneLauncher.Core/Downloader/Download.cs`
- `OneLauncher.Core/Downloader/RaceDownloader.cs`
- `OneLauncher.Core/Downloader/DownloadMinecraftProviders/DownloadMinecraft.cs`
- `OneLauncher.Core/Downloader/DownloadMinecraftProviders/DownloadMinecraftPlanner.cs`
- `OneLauncher.Core/Downloader/DownloadMinecraftProviders/DownloadMinecraftProvider.cs`
- `OneLauncher.Core.Net/JavaProviders/BaseJavaProvider.cs`
- `OneLauncher.Core/Mod/ModPack/ModpackImporter.cs`
- `OneLauncher.Core/Mod/ModPack/MrpackParser.cs`
- `OneLauncher.Core/Mod/ModLoader/forgeseries/ForgeSeriesInstallTasker.cs`

主要流程如下：

1. Minecraft 下载：生成下载计划 → 竞速下载客户端/库/资源 → 解压 native 库 → 运行 Forge/NeoForge 安装处理器 → 最后统一做 SHA1 校验。
2. Java 下载：下载压缩包到临时文件 → 仅验证 ZIP 能否打开 → 解压到 `runtimes/<version>`，未做散列校验。
3. `.mrpack` 导入：使用框架 API 解压整合包 → 应用 overrides → 按清单 URL 下载文件；清单中的 SHA1 被解析，但下载时未校验。

---

## 3. 问题一：ZIP 解压路径穿越

### 3.1 代码证据

`Download.ExtractFile` 在 `OneLauncher.Core/Downloader/Download.cs:22-49` 中遍历 ZIP 条目，并执行：

```csharp
string destinationPath = Path.Combine(extractPath, entry.FullName);
Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
entry.ExtractToFile(destinationPath, overwrite: true);
```

这里没有对最终路径调用 `Path.GetFullPath` 后验证其仍位于 `extractPath` 内，也没有拒绝绝对路径。恶意条目名例如：

```text
../../outside.txt
C:/Users/<user>/AppData/Roaming/.../payload
/absolute/path/payload
```

可使最终写入位置逃逸目标目录。`overwrite: true` 进一步允许覆盖已有文件。

该函数存在两条实际调用链：

- `OneLauncher.Core.Net/JavaProviders/BaseJavaProvider.cs:72-77`：下载 Java ZIP 后解压。
- `OneLauncher.Core/Downloader/DownloadMinecraftProviders/DownloadMinecraftProvider.cs:71-78`：解压 Minecraft native 库。

因此这不是不可达的工具函数问题。

### 3.2 `.mrpack` 解压是否同样存在问题

`OneLauncher.Core/Mod/ModPack/ModpackImporter.cs:69` 使用的是目标框架 `net10.0` 的 `ZipFile.ExtractToDirectory`，而不是上述自定义逐条目拼接逻辑。现代 .NET 的该 API 会拒绝解析后逃逸目标目录的条目。因此，本次确认的 Zip Slip 主要位于 `Download.ExtractFile` 两条调用链，不能将 `.mrpack` 的这一行与自定义函数等同判断为漏洞。

不过 `.mrpack` 解压仍应增加条目数量、解压总大小、单文件大小和压缩比限制，以防 ZIP 炸弹造成磁盘或内存资源耗尽。

### 3.3 可利用条件与影响

攻击者需要让启动器处理恶意 ZIP/JAR。可能的入口包括：

- Java 下载 API、下载源或上游发布内容被控制；
- Minecraft 元数据、native 库源或配置的竞速镜像被控制；
- HTTPS 信任链、代理环境或本机网络环境被控制；
- 后续新增功能直接调用公开的 `Download.ExtractFile` 处理用户提供的归档。

成功利用后，可在启动器当前用户权限内创建或覆盖任意文件。根据可写位置不同，影响包括配置篡改、启动项或脚本植入、游戏/启动器文件替换，以及后续代码执行。若用户以管理员/root 身份运行启动器，影响范围随进程权限扩大；漏洞本身不直接构成权限提升。

### 3.4 严重性

评定为 **高危 / P1**，理由如下：

- 写入目标可逃逸预期目录；
- 使用 `overwrite: true`，具备覆盖能力；
- 代码位于真实下载/安装路径；
- native 库会在完整性校验前解压，事后发现散列不匹配也无法撤销已经发生的目录外写入；
- Java ZIP 当前没有内容散列校验，主要依赖 HTTPS 和上游供应链可信度。

### 3.5 修复方案

#### 必须修复

建立唯一的安全归档解压入口，所有 ZIP/JAR 解压统一调用。逐条目解压前至少执行：

```csharp
string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(extractPath));
string rootPrefix = root + Path.DirectorySeparatorChar;
StringComparison comparison = OperatingSystem.IsWindows()
    ? StringComparison.OrdinalIgnoreCase
    : StringComparison.Ordinal;

if (Path.IsPathRooted(entry.FullName))
    throw new InvalidDataException("ZIP 条目使用了绝对路径。");

string destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
if (!destination.StartsWith(rootPrefix, comparison))
    throw new InvalidDataException("ZIP 条目试图逃逸目标目录。");
```

同时应：

1. 默认禁止覆盖目标目录中已有文件，确需覆盖时只允许覆盖本次安装的受控暂存目录。
2. 解压到新建的私有暂存目录，全部检查和解压成功后再移动/替换到正式目录。
3. 防止借助目标目录中已有符号链接、junction/reparse point 逃逸；最稳妥方式是使用新建且不可被非预期主体修改的暂存目录，并在提升或移动前检查路径链。
4. 限制条目数、单文件解压大小、总解压大小和压缩比，防御 ZIP 炸弹。
5. 遇到非法条目立即终止整个归档处理，清理暂存目录，不允许“跳过后继续安装”。

#### 调用顺序要求

归档必须先在 `.downloading` 临时文件上完成长度和散列校验，再进入解压逻辑。不能依赖解压后的统一校验，因为路径穿越副作用已经发生。

---

## 4. 问题二：SHA1 校验问题

这部分不是单一缺陷，而是四个相互关联的问题。

### 4.1 校验集合错误，已有同长度文件被跳过

`DownloadMinecraftPlanner.CreateDownloadPlan` 在 `OneLauncher.Core/Downloader/DownloadMinecraftProviders/DownloadMinecraftPlanner.cs:69-75` 中将：

```csharp
AllFilesGoVerify: info.DownloadTool.CheckFilesExists(allFiles, cancelToken)
```

作为最终校验集合。

而 `CheckFilesExists`（`Download.cs:555-571`）会把已经存在且长度相同的文件从返回列表移除。因此最终 `CheckAllSha1`（`DownloadMinecraft.cs:103-108`）并不是校验“所有文件”，只校验生成计划时缺失或长度不符的文件。

影响：

- 已有文件只要长度相同，即使内容被篡改也不会被发现。
- 客户端主 JAR 在后续仍会被 `DownloadClientTasker` 下载，但若生成计划时已有同长度文件，它不在最终校验集合内；新下载内容也不会被校验。
- 方法名和进度提示均表达“校验所有文件”，与实际行为不符，容易掩盖问题。

结论：**确认存在**。

### 4.2 校验时机过晚：先解压、先执行，后校验

Minecraft 安装主流程在 `DownloadMinecraft.cs:71-108` 中先完成下载与安装，最后才统一校验。期间已经发生：

- `DownloadMinecraftProvider.cs:71-78`：native JAR 被解压；
- `DownloadMinecraftProvider.cs:80-99`：Forge/NeoForge 等安装器依赖下载完成后运行 Java 处理器；
- `ForgeSeriesInstallTasker.cs:129-159`：从下载的 JAR 读取主类并启动 Java 进程。

因此即使最终 SHA1 不匹配，恶意内容也可能已被解压或执行。完整性校验在安全边界之后发生，不能形成有效阻断。

结论：**确认存在，且是 SHA1 问题中最严重的一项**。

### 4.3 `.mrpack` 下载流程解析了 SHA1，但完全没有校验

`MrpackParser.cs:80-85` 将清单中的 `sha1` 写入 `NdDowItem`。但 `ModpackImporter.cs:138-153` 调用 `DownloadListAsync`，后者在 `Download.cs:408-458` 中始终调用普通 `DownloadFile`，没有检查 `item.sha1`，也没有在下载完成后调用 `CheckAllSha1`。

结果是 `.mrpack` 声明的散列值实际未生效；下载中断后形成的内容错误、源站异常或传输/供应链篡改无法由清单散列发现。

需要说明：若 `.mrpack` 清单本身就是攻击者提供的，攻击者可以同时提供恶意文件和对应散列，因此散列只提供完整性，不提供发布者身份或整合包可信性。要解决来源真实性，需要签名或可信发布渠道。

结论：**确认存在**。

### 4.4 校验函数异常路径可能导致等待不结束

`CheckAllSha1` 在 `Download.cs:573-605` 中获取 `SemaphoreSlim` 后，没有用 `try/finally` 保证释放：

```csharp
await semaphore.WaitAsync();
// 计算散列，失配时抛异常
semaphore.Release();
```

当多个任务散列失配或读取异常时，许可不会归还；等待许可的其他任务可能永久等待。同时 `WaitAsync()` 未传入取消令牌。这会使一次本应快速失败的完整性检查表现为卡死，构成本地拒绝服务和不可恢复的安装体验问题。

结论：**确认存在，严重性中等 / P1-P2**。

### 4.5 SHA-1 算法本身如何定性

SHA-1 已不再适合新系统中的抗碰撞安全用途，应优先使用 SHA-256 或更强算法。但需要区分：

- 当前场景是将下载文件与可信元数据中的固定摘要比较，主要依赖第二原像/原像抗性；
- 已知的 SHA-1 碰撞能力不等同于攻击者可轻易为任意既定摘要制造恶意文件；
- 当前现实风险更高的是“根本未校验”“校验集合漏项”和“使用后才校验”。

对于 Mojang、Modrinth 等上游协议只提供 SHA1 的情况，可以保留 SHA1 作为兼容性校验，但不能把它描述为数字签名或来源认证；对于项目自有元数据和新接口，应迁移到 SHA-256，并为元数据本身建立可信传输或签名机制。

### 4.6 综合严重性

SHA1 机制缺陷综合评定为 **高危 / P1**。主要依据不是 SHA-1 算法弱这一点，而是：

- 校验覆盖不完整；
- 下载内容可能在校验前作为代码执行；
- native 归档可能在校验前触发路径穿越写入；
- `.mrpack` 下载声明的散列被完全忽略。

---

## 5. 推荐的统一修复设计

### 5.1 下载事务模型

将所有下载入口统一为以下顺序：

```text
解析并验证目标路径
  → 下载到同目录随机临时文件
  → 校验实际长度
  → 校验期望散列（优先 SHA-256，兼容 SHA-1）
  → 校验通过后原子替换正式文件
  → 才允许解压、加载或执行
```

建议新增统一接口，例如：

```csharp
Task DownloadVerifiedAsync(
    Uri source,
    string destination,
    long? expectedSize,
    string? expectedSha1,
    string? expectedSha256,
    CancellationToken token);
```

约束：

1. 有期望摘要时，任何下载路径都不得降级到无校验的 `DownloadFile`。
2. 散列不匹配时删除临时文件，保留原正式文件，不运行重试后的未校验内容。
3. 比较摘要前验证格式和长度；摘要字节使用固定时间比较。
4. 下载完成立即校验，不要等全部安装步骤结束后统一校验。
5. 竞速下载源只能决定“从哪里取数据”，不能改变期望长度和期望摘要；胜出响应也必须通过相同校验。

### 5.2 已有文件处理

`CheckFilesExists` 不应把“存在且长度相同”等同于可信。建议逻辑为：

- 有散列：校验散列；匹配才复用，不匹配则重新下载。
- 无散列但有长度：长度只能用于发现明显损坏，不能作为安全完整性证明。
- 无散列、无长度：只能按“不具备完整性保证”处理，并在高风险执行/解压场景拒绝或要求可信签名来源。

下载计划应分别维护：

- `AllRequiredFiles`：安装所需完整集合；
- `FilesToDownload`：确需下载的集合；
- 不应再以 `FilesToDownload` 代替最终完整性集合。

### 5.3 各调用链改造重点

1. **Minecraft 客户端、库、资源**：竞速下载写临时文件，逐文件验证后原子替换。
2. **native 库**：只有对应 JAR 验证通过后才能调用安全解压函数。
3. **Forge/NeoForge 安装器及处理器依赖**：安装器本体和所有处理器 JAR 必须先验证，再读取配置或启动 Java。
4. **`.mrpack` 文件**：`DownloadListAsync` 必须使用清单提供的 SHA1；整合包本身如需来源认证，应另加签名验证。
5. **Java Runtime**：优先从供应商 API 获取 SHA-256/校验文件并验证；若提供商接口确实无摘要，至少限制为 HTTPS 白名单来源并使用安全解压和暂存目录，不能把“ZIP 可打开”视为完整性校验。
6. **日志配置等单文件下载**：同样通过统一校验入口，不保留绕过接口。

### 5.4 修复 `CheckAllSha1` 的并发可靠性

若仍保留批量校验函数，应：

- 使用 `await semaphore.WaitAsync(token)`；
- 在 `finally` 中释放 semaphore；
- 不需要再用 `Task.Run` 包裹异步文件 I/O；
- 任一失败时取消剩余校验，并确保所有任务可结束；
- 返回结构化失败结果或抛出包含文件路径的聚合异常。

但该函数只能作为安装前扫描或诊断工具，不能替代“每个文件下载后、使用前”的强制校验。

---

## 6. 修复优先级与实施计划

### P0/P1：立即处理

1. 替换 `Download.ExtractFile` 为带规范化目录边界校验的安全解压实现。
2. 调整 native 和处理器流程：校验通过前禁止解压或执行。
3. 修复下载计划的集合语义，不再用 `CheckFilesExists` 的返回值充当 `AllFilesGoVerify`。
4. 让 `RaceDownloader`、`DownloadListAsync` 和单文件下载统一走“临时文件 → 长度/散列验证 → 原子替换”。
5. 在 `.mrpack` 下载中真正使用 `NdDowItem.sha1`。
6. 修复 `CheckAllSha1` semaphore 异常和取消路径。

### P2：随后处理

1. 自有元数据改用 SHA-256；外部只提供 SHA1 时兼容保留并明确其能力边界。
2. Java 供应商下载接入官方 SHA-256 或签名验证。
3. 增加归档资源限制和暂存目录配额。
4. 对下载 URL 建立协议和来源策略：默认仅 HTTPS，对重定向后的最终 URI 重新检查策略。

---

## 7. 必须补充的安全测试

### 7.1 ZIP 解压测试

- `../escape.txt`、`../../escape.txt`；
- Windows 绝对路径、盘符路径、UNC 路径；
- Unix 绝对路径；
- 混合 `/` 与 `\` 分隔符；
- 路径规范化后共享前缀但不属于目标目录的路径，例如 `out-evil/file`；
- 目标目录中的 symlink/junction/reparse point；
- 重复条目、同名覆盖、文件与目录冲突；
- 超大条目数、超大展开体积和极端压缩比。

验收标准：任何非法归档不得在暂存根目录外创建或修改文件，失败后暂存内容被清理，原安装目录保持不变。

### 7.2 完整性测试

- 已存在、长度相同但内容不同的文件必须被识别并重下；
- 竞速源返回错误内容时不能成为正式文件；
- 下载内容与摘要不匹配时不得解压、加载或执行；
- `.mrpack` 中 SHA1 不匹配时导入失败；
- 摘要格式错误、大小写差异、空摘要分别按策略处理；
- 多个文件同时失配时任务能够快速失败，不死锁；
- 校验等待期间取消，所有任务及时退出；
- 下载或校验中断后，旧正式文件保持完好，临时文件可清理。

---

## 8. 最终判断

1. **ZIP 解压路径穿越：存在，且为高危。** 自定义解压函数缺少目录边界校验，并在 Java ZIP、Minecraft native 解压中实际使用。单纯依赖 HTTPS 或事后 SHA1 校验不足以消除风险。
2. **SHA1 校验问题：存在，且整体为高危。** 当前代码虽然实现了 SHA1 计算，但其覆盖集合、调用路径和执行时机不足以构成可靠安全控制；部分流程完全不校验，部分内容在校验前已经解压或执行。
3. 修复应以“**使用前验证**”为原则，把安全解压和经过验证的原子下载作为统一基础能力，而不是在现有流程末尾追加一次批量散列检查。
