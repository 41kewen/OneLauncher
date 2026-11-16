using OneLauncher.Console.Infrastructure;
using OneLauncher.Core.DefaultComponent.Downloader.DownloadMinecraftProviders;
using OneLauncher.Core.DefaultComponent.Downloader.DownloadMinecraftProviders.ModSources.SpecifiedVersionConfig;
using OneLauncher.Core.Helper.Models;

namespace OneLauncher.Console.Commands.InstallGameVersion;

internal sealed class InstallGameVersionOptions
{
    public required string Version { get; init; }
    public required ModType Loaders { get; init; }
    public string? FabricVersion { get; init; }
    public string? Name { get; init; }
    public bool FabricApi { get; init; }
    public bool QuiltQsl { get; init; }
    public bool AllowBetaNeoforge { get; init; }
    public bool UseRecommendedForge { get; init; }
    public bool Java { get; init; }
    public bool RecommendedMods { get; init; }

    public static InstallGameVersionOptions Parse(CommandArguments arguments)
    {
        arguments.Allow("version", "fabric", "fabricVersion", "forge", "neoforge", "quilt", "name", "dataPath",
            "fabricApi", "quiltQsl", "allowBetaNeoforge", "useRecommendedForge", "java", "recommendedMods");
        string version = arguments.Require("version");
        if (version is "." or ".." || version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || version.IndexOfAny(['/', '\\', ':']) >= 0 || version.EndsWith('.') || version.EndsWith(' '))
            throw new ArgumentException("无效的 Minecraft 版本号。");
        var mods = new ModType
        {
            IsFabric = arguments.GetBoolean("fabric"), IsForge = arguments.GetBoolean("forge"),
            IsNeoForge = arguments.GetBoolean("neoforge"), IsQuilt = arguments.GetBoolean("quilt")
        };
        string? fabricVersion = arguments.Get("fabricVersion");
        if (fabricVersion != null && !mods.IsFabric)
            throw new ArgumentException("fabricVersion 需要同时指定 fabric:true。");
        if (System.Version.TryParse(version, out var numericVersion))
        {
            if ((mods.IsFabric || mods.IsQuilt) && numericVersion < new System.Version(1, 14))
                throw new ArgumentException("Fabric 和 Quilt 需要 Minecraft 1.14 或更新版本。");
            if (mods.IsForge && numericVersion < new System.Version(1, 2))
                throw new ArgumentException("Forge 需要 Minecraft 1.2 或更新版本。");
            if (mods.IsNeoForge && numericVersion < new System.Version(1, 20, 2))
                throw new ArgumentException("NeoForge 需要 Minecraft 1.20.2 或更新版本。");
        }
        bool recommended = arguments.GetBoolean("recommendedMods");
        if (recommended && !(mods.IsFabric || mods.IsForge || mods.IsNeoForge || mods.IsQuilt))
            throw new ArgumentException("安装推荐模组需要选择模组加载器。");
        return new()
        {
            Version = version, Loaders = mods, FabricVersion = fabricVersion, Name = arguments.Get("name"),
            FabricApi = arguments.GetBoolean("fabricApi", true), QuiltQsl = arguments.GetBoolean("quiltQsl", true),
            AllowBetaNeoforge = arguments.GetBoolean("allowBetaNeoforge"),
            UseRecommendedForge = arguments.GetBoolean("useRecommendedForge", true),
            Java = arguments.GetBoolean("java"), RecommendedMods = recommended
        };
    }

    public Task<DownloadInfo> CreateDownloadInfoAsync(SpecifiedFabricVersionConfig? fabricConfig = null)
        => DownloadInfo.Create(Version, Loaders,
            isAllowToUseBetaNeoforge: AllowBetaNeoforge,
            isUseRecommendedToInstallForge: UseRecommendedForge,
            isDownloadFabricWithAPI: FabricApi,
            isDownloadWithJavaRuntime: Java,
            isDownloadQuiltWhitQSL: QuiltQsl,
            fabricConfig: fabricConfig);
}
