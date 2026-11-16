using System.IO.Compression;
using OneLauncher.Console.Infrastructure;
using OneLauncher.Core.DefaultComponent.Resource.Pack;
using OneLauncher.Core.Global;
using OneLauncher.Core.Minecraft;

namespace OneLauncher.Console.Commands.ImportPack;

internal static class ImportPackCommand
{
    public static async Task RunAsync(CommandArguments arguments)
    {
        arguments.Allow("path", "dataPath");
        string path = Path.GetFullPath(arguments.Require("path"));
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (!File.Exists(path) || extension is not (".mrpack" or ".zip"))
            throw new ArgumentException("请指定存在的 .mrpack 或包含一个 .mrpack 的 .zip 文件。");
        using var context = await CommandContext.CreateAsync(arguments);
        using var task = new InstallationTask(context.Core, "导入整合包");
        await task.RunAsync(async () =>
        {
            string? extractedPack = null;
            try
            {
                if (extension == ".zip")
                {
                    using var zip = ZipFile.OpenRead(path);
                    var packs = zip.Entries.Where(entry => entry.FullName.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (packs.Length != 1) throw new ArgumentException("ZIP 中必须包含且仅包含一个 .mrpack 整合包。");
                    extractedPack = Path.Combine(Path.GetTempPath(), $"olan-console-{Guid.NewGuid():N}.mrpack");
                    await using (var source = packs[0].Open())
                    await using (var destination = new FileStream(extractedPack, FileMode.CreateNew))
                        await source.CopyToAsync(destination, task.Token);
                    path = extractedPack;
                }
                System.Console.WriteLine($"游戏目录：{Init.GameRootPath}");
                context.Core.MojangVersionList = new(await VersionsList.GetOrRefreshVersionListAsync());
                task.Token.ThrowIfCancellationRequested();
                var game = await ModpackImporter.ImportFromMrpackAsync(path, Init.GameRootPath, task, task.Token);
                System.Console.WriteLine($"导入成功：{game.Name}；实例ID：{game.InstanceId}；目录：{game.InstancePath}");
            }
            finally
            {
                if (extractedPack != null && File.Exists(extractedPack)) File.Delete(extractedPack);
            }
        });
    }
}
