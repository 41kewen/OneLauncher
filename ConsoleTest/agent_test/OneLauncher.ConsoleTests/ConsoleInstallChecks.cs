using System.Reflection;
using System.Runtime.CompilerServices;
using OneLauncher.Console.Commands.InstallGameVersion;
using OneLauncher.Console.Infrastructure;
using OneLauncher.Core.DefaultComponent.Downloader.DownloadMinecraftProviders.ModSources.SpecifiedVersionConfig;
using OneLauncher.Core.Global;
using OneLauncher.Core.Global.ModelDataMangers;
using OneLauncher.Core.Helper.Models;
using OneLauncher.Views;

internal static class ConsoleInstallChecks
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static InstallGameVersionOptions Parse(params string[] values)
        => InstallGameVersionOptions.Parse(CommandArguments.Parse(["--installGameVersion", .. values]));

    public static async Task RunAsync(string output)
    {
        var defaults = Parse("version:1.21.5");
        var uiType = typeof(MainWindow).Assembly.GetType("OneLauncher.Views.Panes.PaneViewModels.DownloadPaneViewModel", true)!;
        var ui = Activator.CreateInstance(uiType)!;
        bool Ui(string name) => (bool)uiType.GetProperty(name)!.GetValue(ui)!;
        Assert(defaults.FabricApi == Ui("IsDownloadFabricWithAPI") && defaults.QuiltQsl == Ui("IsDownloadQuiltWithQSL")
            && defaults.AllowBetaNeoforge == Ui("IsAllowToUseBetaNeoforge")
            && defaults.UseRecommendedForge == Ui("IsUseRecommendedToInstallForge")
            && defaults.Java == Ui("IsJava") && defaults.RecommendedMods == Ui("InstallRecommendedMods"), "console defaults match UI");

        string[] values = ["version:1.21.5", "fabric:true", "quilt:true", "forge:true", "neoforge:true",
            "fabricApi:false", "quiltQsl:false", "allowBetaNeoforge:true", "useRecommendedForge:false",
            "java:true", "recommendedMods:true", "fabricVersion:0.18.3", "name:多加载器实例", "dataPath:E:/folder with spaces"];
        for (int shift = 0; shift < values.Length; shift++)
        {
            var rotated = values.Skip(shift).Concat(values.Take(shift)).ToArray();
            foreach (var permutation in new[] { rotated, rotated.Reverse().ToArray() })
            {
                var args = CommandArguments.Parse(["--installGameVersion", .. permutation]);
                var item = InstallGameVersionOptions.Parse(args);
                Assert(item.Version == "1.21.5" && item.Loaders.IsFabric && item.Loaders.IsQuilt
                    && item.Loaders.IsForge && item.Loaders.IsNeoForge && !item.FabricApi && !item.QuiltQsl
                    && item.AllowBetaNeoforge && !item.UseRecommendedForge && item.Java && item.RecommendedMods
                    && item.FabricVersion == "0.18.3" && item.Name == "多加载器实例"
                    && args.Get("dataPath") == "E:/folder with spaces", "argument order independent");
            }
        }
        foreach (string[] invalid in new string[][] {
            ["fabric:true"], ["version:1.21.5", "java:yes"], ["version:1.21.5", "quiltQsl:1"],
            ["version:1.21.5", "recommendedMods:true"], ["version:1.21.5", "fabricVersion:0.18.3"],
            ["version:1.13", "fabric:true"], ["version:1.20.1", "neoforge:true"],
            ["version:1.1", "forge:true"], ["version:1.21.5", "VERSION:1.21"],
            ["version:../outside"], ["version:1.21.5", "typo:true"] })
        {
            bool rejected = false;
            try { Parse(invalid); } catch (ArgumentException) { rejected = true; }
            Assert(rejected, "invalid options rejected before initialization");
        }

        // 使用工作区内离线元数据，验证参数确实传到核心下载计划。
        string root = Path.Combine(output, "data", Guid.NewGuid().ToString("N"));
        Init.GameRootPath = Path.Combine(root, "minecraft");
        var config = new DBManager(Path.Combine(root, "config.json"));
        await config.InitializeAsync();
        typeof(Init).GetProperty("ConfigManager", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, config);
        var accounts = new AccountManager(Path.Combine(root, "accounts.json"));
        await accounts.InitializeAsync();
        var versions = new VersionListManager(Init.GameRootPath);
        await versions.InitializeAsync();
        var core = (UnitedCore)RuntimeHelpers.GetUninitializedObject(typeof(UnitedCore));
        typeof(UnitedCore).GetProperty(nameof(UnitedCore.AccountManager))!.SetValue(core, accounts);
        typeof(UnitedCore).GetProperty(nameof(UnitedCore.VersionListManager))!.SetValue(core, versions);
        typeof(UnitedCore).GetProperty(nameof(UnitedCore.MojangVersionList))!.SetValue(core,
            new List<VersionBasicInfo> { new("1.21.5", "release", DateTime.UtcNow, "https://fixture.invalid/version") });
        typeof(UnitedCore).GetProperty("Core", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, core);
        string metadataPath = Path.Combine(Init.GameRootPath, "versions", "1.21.5", "version.json");
        Directory.CreateDirectory(Path.GetDirectoryName(metadataPath)!);
        await File.WriteAllTextAsync(metadataPath, "{\"id\":\"1.21.5\"}");
        var specified = new SpecifiedFabricVersionConfig();
        var info = await Parse(values).CreateDownloadInfoAsync(specified);
        Assert(!info.IsDownloadFabricWithAPI && !info.ISDownloadQuiltWhitQSL && info.AndJava
            && info.IsAllowToUseBetaNeoforge && !info.IsUseRecommendedToInstallForge
            && ReferenceEquals(info.SpecifiedFabricVersion, specified), "core receives all installation flags");
        Assert(info.UserInfo.Select(item => item.ModLoader).SequenceEqual(new[] { ModEnum.fabric, ModEnum.forge, ModEnum.neoforge, ModEnum.quilt })
            && info.UserInfo.Select(item => item.InstanceId).Distinct().Count() == 4, "each selected loader gets its own instance");
        var vanilla = await defaults.CreateDownloadInfoAsync();
        Assert(vanilla.UserInfo.Count == 1 && vanilla.UserInfo[0].ModLoader == ModEnum.none && !vanilla.AndJava
            && vanilla.IsDownloadFabricWithAPI && vanilla.ISDownloadQuiltWhitQSL
            && vanilla.IsUseRecommendedToInstallForge && !vanilla.IsAllowToUseBetaNeoforge, "default vanilla plan matches UI settings");
        System.Console.WriteLine("PASS: UI defaults, 28 argument permutations, invalid options, core flag mapping, four loader instances and vanilla plan");
    }
}
