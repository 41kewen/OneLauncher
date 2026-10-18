using OneLauncher.Core.Downloader.DownloadMinecraftProviders;
using OneLauncher.Core.Global;
using OneLauncher.Core.Helper.Models;
if (args.Length > 0 && args[0] == "--import-argv-child")
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(args.Skip(1).ToList(),
        OneLauncher.Core.Minecraft.JsonModels.MinecraftJsonContext.Default.ListString));
    return;
}
if (args.Contains("--pcl-import-regression-tests", StringComparer.Ordinal))
{
    await PclImportRegressionTests.RunAsync();
    return;
}
if (args.Length == 3 && args[0] == "--pcl-import-real-test")
{
    await PclImportRegressionTests.ImportRealAsync(args[1], args[2]);
    return;
}
if (args.Length > 0 && args[0] == "--processor-test-child")
{
    System.Console.OutputEncoding = System.Text.Encoding.UTF8;
    if (args[1] == "wait")
    {
        Console.WriteLine("child-ready");
        await Task.Delay(Timeout.Infinite);
    }
    Console.WriteLine(args.Length > 2 ? args[2] : "child-output");
    Console.Error.WriteLine("processor-final-error-中文");
    Environment.ExitCode = args[1] == "fail" ? 1 : 0;
    return;
}
if (args.Contains("--processor-regression-tests", StringComparer.Ordinal))
{
    await ForgeProcessorRegressionTests.RunAsync();
    return;
}
if (args.Contains("--download-regression-tests", StringComparer.Ordinal))
{
    await DownloadRegressionTests.RunAsync();
    return;
}
await Init.Initialize(); // 原本的代码还是需要执行一下的
var core = await UnitedCore.Initialize();
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
