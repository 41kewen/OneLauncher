using OneLauncher.Console.Infrastructure;
using OneLauncher.Core.DefaultComponent.Downloader.DownloadMinecraftProviders;
using OneLauncher.Core.DefaultComponent.Downloader.DownloadMinecraftProviders.ModSources.SpecifiedVersionConfig;
using OneLauncher.Core.Global;
using OneLauncher.Core.Helper.Models;
using OneLauncher.Core.Minecraft;

namespace OneLauncher.Console.Commands.InstallGameVersion;

internal static class InstallGameVersionCommand
{
    public static async Task RunAsync(CommandArguments arguments)
    {
        var options = InstallGameVersionOptions.Parse(arguments);
        string version = options.Version;
        using var context = await CommandContext.CreateAsync(arguments);
        var core = context.Core;
        var instances = new List<GameData>();
        using var task = new InstallationTask(core, $"安装 Minecraft {version}");
        await task.RunAsync(async () =>
        {
            System.Console.WriteLine($"游戏目录：{Init.GameRootPath}");
            core.MojangVersionList = new(await VersionsList.GetOrRefreshVersionListAsync());
            task.Token.ThrowIfCancellationRequested();
            if (!core.MojangVersionList.Any(item => item.ID == version))
                throw new ArgumentException($"找不到可安装的 Minecraft 版本：{version}");
            SpecifiedFabricVersionConfig? fabric = null;
            if (options.FabricVersion != null)
            {
                fabric = new();
                if (!(await fabric.GetVersions(version)).Contains(options.FabricVersion))
                    throw new ArgumentException($"Minecraft {version} 不支持 Fabric {options.FabricVersion}。");
                fabric.SetLoaderVersion(options.FabricVersion);
            }
            var info = await options.CreateDownloadInfoAsync(fabric);
            if (options.Name != null)
                foreach (var game in info.UserInfo)
                    game.Name = info.UserInfo.Count == 1 ? options.Name : $"{options.Name} - {game.ModLoader}";
            var retry = new DownloadRetryPrompt(task.Token);
            await core.VersionInstallerService.InstallNewVersion(info, task, task.Token, retry.Ask);
            task.Token.ThrowIfCancellationRequested();
            foreach (var game in info.UserInfo)
            {
                task.Token.ThrowIfCancellationRequested();
                await core.GameDataManager.AddGameDataAsync(game);
                instances.Add(game);
                System.Console.WriteLine($"安装成功：{game.Name}；实例ID：{game.InstanceId}；目录：{game.InstancePath}");
            }
            task.Token.ThrowIfCancellationRequested();
            await core.GameDataManager.SetDefaultInstanceAsync(info.UserInfo[0].InstanceId);
            System.Console.WriteLine($"已设为该版本默认实例：{info.UserInfo[0].InstanceId}");
        });
        if (options.RecommendedMods)
        {
            // 控制台等待附加任务结束，避免主进程退出使推荐模组下载中断。
            foreach (var game in instances)
            {
                using var recommended = new InstallationTask(core, $"安装推荐模组 {game.Name}");
                var progress = new Progress<(int current, string file)>(value =>
                    recommended.Report((DownProgress.DownAndInstModFiles, 0, value.current, value.file)));
                try
                {
                    await recommended.RunAsync(() => core.ResourceInstallerService.InstallRecommendedMods(progress, game, recommended.Token));
                    System.Console.WriteLine($"推荐模组安装完成：{game.Name}");
                }
                catch
                {
                    System.Console.Error.WriteLine($"推荐模组安装未完成，游戏实例已保留：{game.InstanceId}");
                    throw;
                }
            }
        }
    }
}
