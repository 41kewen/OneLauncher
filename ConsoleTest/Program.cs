using OneLauncher.Core.DefaultComponent.Downloader.DownloadMinecraftProviders;
using OneLauncher.Core.Global;
using OneLauncher.Core.Helper.Models;
using Microsoft.Extensions.DependencyInjection;
var services = await Init.Initialize();
using var serviceProvider = services.BuildServiceProvider();
var core = serviceProvider.GetRequiredService<UnitedCore>();
var task = core.TaskManager.CreateTask("下载MC");
_=Task.Run(async () =>
{
    await Task.Delay(2000);
    task.Cancel();
});
await core.VersionInstallerService.InstallNewVersion(
    await DownloadInfo.Create(
        "1.20.1",
        new ModType() { IsFabric = true }
        ),
    new Progress<(DownProgress Title, int AllFiles, int DownedFiles, string DowingFileName)>(p =>
    {
        Console.WriteLine($"进度：{p.Title} {p.DownedFiles}/{p.AllFiles} 下载中：{p.DowingFileName}");
    }),
    task!.Token!.Token
    );
