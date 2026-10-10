# OneLauncher

轻量、快速、少配置的 Minecraft 启动器。

## Windows 一键安装

在 Windows PowerShell 中运行：

```powershell
Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/zhweaa/OneLauncher/master/OneLauncher.Desktop/install.ps1'))
```

安装脚本可选择仅下载启动器，或同时安装 .NET 10 Desktop Runtime 与 Java 21。

## Linux x64

从 [Releases](https://github.com/zhweaa/OneLauncher/releases) 下载 `OneLauncher-linux-x64.tar.gz` 和同名 `.sha256` 文件，放在同一目录后运行：

```bash
sha256sum -c OneLauncher-linux-x64.tar.gz.sha256
tar -xzf OneLauncher-linux-x64.tar.gz
cd OneLauncher-linux-x64
./OneLauncher.Desktop
```

Linux 包自带 .NET 10 运行时，适用于使用 glibc 的 x86-64 桌面系统。需要 X11 桌面会话；Wayland 桌面请启用 XWayland。建议解压到用户可写目录，保留目录内所有文件。

Ubuntu 22.04 / 24.04 如缺少桌面依赖，可安装：

```bash
sudo apt update
sudo apt install libx11-6 libice6 libsm6 libfontconfig1 libxrandr2 libxi6 libxcursor1 libglib2.0-0 libgl1 libegl1 libsecret-1-0 xdg-utils
```

系统还需提供 .NET 的原生依赖（ICU、OpenSSL、zlib 等，常规桌面安装通常已具备）。Minecraft 使用的 Java 可由启动器管理。Linux 上 Microsoft 登录使用系统浏览器；Windows WAM 登录与 MCT 联机不可用。

## 开发

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022、Rider 或其他支持 .NET 10 的 IDE
- Java 可由启动器自动安装，也可自行准备

### 构建和运行

```powershell
git clone https://github.com/zhweaa/OneLauncher.git
cd OneLauncher
dotnet restore OneLauncher.sln
dotnet run --project OneLauncher.Desktop
```

发布 Windows x64：

```powershell
dotnet publish OneLauncher.Desktop -c Release -r win-x64 --self-contained false
```

发布 Linux x64（在 Linux 或 WSL 中执行，使用 `global.json` 指定的 .NET 10 SDK）：

```bash
bash scripts/publish-linux.sh
# 可选：指定产物输出目录
bash scripts/publish-linux.sh /tmp/onelauncher-linux
```

默认产物为 `artifacts/linux-x64/OneLauncher-linux-x64.tar.gz` 和对应的 `.sha256` 校验文件。脚本发布自包含程序，不启用 AOT 或裁剪，并保留 Linux 可执行权限。项目的平台条件编译依赖构建宿主系统，因此 Linux 包应在 Linux 上构建。

GitHub Actions 的 **Desktop builds** 工作流使用同一 SDK，在 Windows 与 Ubuntu 上分别构建；推送到 `master`、提交 PR 或手动触发后，可从运行页面下载 `OneLauncher-linux-x64` 产物。工作流仅上传构建产物，Release 文件由维护者发布。

### 命令行入口

```text
--quicklyPlay <instance-id>       快速启动实例
--joinServer <server-id-or-name>  启动收藏服务器
--releaseMemory                   释放游戏进程内存
```

## 核心功能

- **Microsoft 正版登录**：Windows 直接调用系统 Web Account Manager（WAM）完成 Microsoft 账户授权，无需打开浏览器、无需输入验证码；随后自动完成 Xbox Live、Minecraft Services 和正版权益验证。
- **版本与实例**：Mojang、BMCL、OneLauncher 多源竞速下载，支持 SHA-1 校验、PCL2 版本导入和 `.mrpack` 整合包导入。
- **模组与资源**：支持 Fabric、Quilt、NeoForge，Forge 为预览支持；通过 Modrinth 检索并安装模组、资源包和光影。
- **Java 管理**：按 Minecraft 版本自动下载匹配的 Java，支持多版本运行时。
- **账户与服务器**：支持 Microsoft、Yggdrasil 外置和离线账户；收藏服务器并关联游戏实例。
- **Windows 联机**：与 [MinecraftConnectTool（MCT）](https://github.com/MCZLF/MinecraftConnectTool) 合作提供 P2P 联机能力。
- **跨平台桌面**：基于 Avalonia，支持 Windows、Linux 和 macOS；MCT 联机及 WAM 登录为 Windows 专属能力。

## 界面

![OneLauncher 首页](docs/assets/screenshots/home.png)

![Microsoft 系统登录](docs/assets/screenshots/account-login.png)

![游戏数据编辑](docs/assets/screenshots/game-data.png)

![Modrinth 资源检索](docs/assets/screenshots/modrinth.png)

![Minecraft 下载](docs/assets/screenshots/download.png)

> 项目仍在持续开发中。界面、下载源和联机服务可能随版本更新而变化。


## 项目结构

```text
OneLauncher/          # Avalonia UI
OneLauncher.Core/     # 下载、启动、实例和模组管理
OneLauncher.Core.Net/ # 账户、Java、Modrinth、服务器和 MCT
OneLauncher.Desktop/  # 桌面入口
OneLauncher.Console/  # 命令行入口
```

UI 规范见 [`docs/UI_FI.md`](docs/UI_FI.md)，架构说明见 [`Welcome.md`](Welcome.md)。

## 开源与致谢

本项目使用 [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0) 开源，使用或参考 [Avalonia](https://github.com/AvaloniaUI/Avalonia)、[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)、[AsyncImageLoader.Avalonia](https://github.com/AvaloniaUtils/AsyncImageLoader.Avalonia)、[ProjBobcat](https://github.com/Corona-Studio/ProjBobcat/)、[Modrinth](https://modrinth.com/) 和 [MinecraftConnectTool](https://github.com/MCZLF/MinecraftConnectTool)。

OneLauncher 与 Mojang、Microsoft 或 Minecraft 官方没有从属关系。更多法律信息见 [`Terms_of_Service.md`](Terms_of_Service.md) 和 [`Privacy_Policy.md`](Privacy_Policy.md)。

欢迎联系作者讨论问题或提交 Issue：QQ `1826379500`。
