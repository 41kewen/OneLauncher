using OneLauncher.Console.Commands.ImportPack;
using OneLauncher.Console.Commands.InstallGameVersion;
using OneLauncher.Console.Commands.JoinServer;
using OneLauncher.Console.Commands.QuicklyPlay;
using OneLauncher.Console.Commands.ReleaseMemory;
using OneLauncher.Console.Infrastructure;

namespace OneLauncher.Console;

public class Boot
{
    public static async Task RunBoot(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h")
            {
                PrintHelp();
                return;
            }
            var arguments = CommandArguments.Parse(args);
            switch (args[0])
            {
                case "--installGameVersion": await InstallGameVersionCommand.RunAsync(arguments); break;
                case "--importPack": await ImportPackCommand.RunAsync(arguments); break;
                case "--quicklyPlay": await QuicklyPlayCommand.RunAsync(arguments); break;
                case "--joinServer": await JoinServerCommand.RunAsync(arguments); break;
                case "--releaseMemory": await ReleaseMemoryCommand.RunAsync(arguments); break;
                default: throw new ArgumentException($"未知命令：{args[0]}。使用 --help 查看用法。");
            }
        }
        catch (Exception e)
        {
            System.Console.Error.WriteLine(e.ToString());
            Environment.ExitCode = e is OperationCanceledException ? 130 : 1;
        }
    }

    private static void PrintHelp() => System.Console.WriteLine("""
        OneLauncher 控制台（直接使用命令，无需 --console）
          --installGameVersion version:1.21.5 fabric:true [fabricVersion:0.18.3] [name:实例名称] [dataPath:启动器数据目录]
          --installGameVersion version:1.21.5 [fabric:true] [forge:true] [neoforge:true] [quilt:true]
            [fabricApi:true] [quiltQsl:true] [allowBetaNeoforge:false] [useRecommendedForge:true]
            [java:false] [recommendedMods:false]
          --importPack path:整合包.mrpack或.zip [dataPath:启动器数据目录]
          --quicklyPlay instance:实例ID [dataPath:启动器数据目录]
          --joinServer server:服务器ID或名称 [dataPath:启动器数据目录]
          --releaseMemory
        命令为首个参数，其余 key:value 参数顺序任意；带空格的参数需整体加引号。
        未指定加载器时安装原版，可选择多个加载器，分别创建实例并将首个设为该版本默认项。
        上方布尔选项展示 UI 默认值；交互下载失败可输入 y 重试单个文件。
        安装、导入使用当前账户及安装目录，Ctrl+C 取消，失败返回非零退出码。
        """);
}
