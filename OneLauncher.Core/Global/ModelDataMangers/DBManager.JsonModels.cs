using OneLauncher.Core.DefaultComponent.Downloader.DownloadMinecraftProviders;
using OneLauncher.Core.Helper.Models;
using OneLauncher.Core.DefaultComponent.Server;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using OneLauncher.Core.DefaultComponent.Resource.ResourceGetterService.AutoInformation.RecommededMods;
using System.Collections.Generic;

namespace OneLauncher.Core.Global.ModelDataMangers;
[JsonSerializable(typeof(JvmArguments))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(OlanUISettings))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(DefaultConfig))]
[JsonSerializable(typeof(ResourceCollection))]
[JsonSerializable(typeof(UserModel))]
[JsonSerializable(typeof(UserVersion))]
[JsonSerializable(typeof(ServerEntry))]
[JsonSerializable(typeof(ModType))]
[JsonSerializable(typeof(ModEnum))]
[JsonSerializable(typeof(HomePageStyle))]
public partial class OneLauncherAppConfigsJsonContext : JsonSerializerContext { }
public enum HomePageStyle
{
    Classic = 0,
    MagicButton = 1
}
public class DefaultConfig
{
    public JvmArguments MinecraftJvmArguments { get; set; } = JvmArguments.CreateFromMode();
    public string? ApiKey { get; set; } = null;
    // 资源下载地址，初始值即启动器默认值，网络资源同步时覆盖
    public ResourceCollection Resources { get; set; } = new();
    // 是否允许离线、外置等非微软账户登入
    public bool AllowNonMicrosoftLogin { get; set; } = true;
    // Java 安装面板中可选择下载的 Java 版本
    public List<int> AvailableJavaVersions { get; set; } = [8, 11, 16, 17, 21, 24, 25];
    #region 推荐模组
    [JsonConverter(typeof(RecommendedModListConverter))]
    public List<string> RecommandedMods { get; set; } =
    [
        // Animatica Refabricated — animaticarefabricated
        "xEyZuswh",
        // Better Block Entities — better-block-entities
        "ONZm0H7Y",
        // Better Mount HUD — better-mount-hud
        "kqJFAPU9",
        // Cloth Config API — cloth-config
        "9s6osm5g",
        // Config Manager — configmanager
        "jlNms3Jp",
        // Continuity — continuity
        "1IjD5062",
        // Controlify (Controller support) — controlify
        "DOUdJVEm",
        // Crash Assistant — crash-assistant
        "ix1qq8Ux",
        // Debugify — debugify
        "QwxR6Gcd",
        // Dynamic FPS — dynamic-fps
        "LQ3K71Q1",
        // Dynamic Surroundings — dynamicsurroundingsfabric
        "H7fshfpD",
        // e4mc — e4mc
        "qANg5Jrr",
        // Embeddium — embeddium
        "sk9rgfiA",
        // [EMF] Entity Model Features — entity-model-features
        "4I1XuqiY",
        // Entity Culling — entityculling
        "NNAgCjsB",
        // [ETF] Entity Texture Features — entitytexturefeatures
        "BVzZfTc1",
        // Fabric API — fabric-api
        "P7dR8mSH",
        // Fabric Language Kotlin — fabric-language-kotlin
        "Ha28R6CL",
        // FastQuit — fastquit
        "x1hIzbuY",
        // FerriteCore — ferrite-core
        "uXXizFIs",
        // Forge Config API Port — forge-config-api-port
        "ohNO6lps",
        // IMBlocker — imblocker-original
        "WMDesFsZ",
        // ImmediatelyFast — immediatelyfast
        "5ZwdcRci",
        // Iris Shaders — iris
        "YL57xq9U",
        // Ixeris — ixeris
        "p8RJPJIC",
        // Just Enough Items (JEI) — jei
        "u6dRKJwZ",
        // LambDynamicLights - Dynamic Lights — lambdynamiclights
        "yBW8D80W",
        // Language Reload — language-reload
        "uLbm7CG6",
        // Lithium — lithium
        "gvQqBUqZ",
        // Main Menu Credits — main-menu-credits
        "qJDfP7WN",
        // MixinTrace Reborn — mixintrace-reborn
        "Ok6FyXJ9",
        // ModernFix — modernfix
        "nmDcB62a",
        // ModernFix-mVUS — modernfix-mvus
        "TjSm1wrD",
        // Mod Menu — modmenu
        "mOgUt4GM",
        // More Chat History — morechathistory
        "8qkXwOnk",
        // More Culling — moreculling
        "51shyZVL",
        // No Chat Reports — no-chat-reports
        "qQyHxfxd",
        // OptiGUI — optigui
        "JuksLGBQ",
        // Paginated Advancements & Custom Frames — paginatedadvancements
        "pJogNFap",
        // Text Placeholder API — placeholder-api
        "eXts2L7r",
        // Polytone — polytone
        "3qAYkBMB",
        // Puzzle — puzzle
        "3IuO68q1",
        // Quilted Fabric API (QFAPI) / Quilt Standard Libraries (QSL) — qsl
        "qvIfYCYJ",
        // Reese's Sodium Options — reeses-sodium-options
        "Bh37bMuy",
        // Renice Shot — renice-shot
        "ZDCwiIc3",
        // Remove Reloading Screen — rrls
        "ZP7xHXtw",
        // Skyboxify — skyboxify
        "DWuwk8aA",
        // Sodium — sodium
        "AANobbMI",
        // Sodium Extra — sodium-extra
        "PtjYWJkn",
        // Sodium Shadowy Path Blocks (SSPB) — sodium-shadowy-path-blocks
        "EIa1eiMm",
        // Sound Physics Remastered — sound-physics-remastered
        "qyVF9oeo",
        // YetAnotherConfigLib (YACL) — yacl
        "1eAoo2KR",
        // Zoomify (Zoom) — zoomify
        "w7ThoJFB",
    ];

    #endregion
}
// 资源下载地址集合，每项为按顺序尝试的候选地址列表
public class ResourceCollection
{
    // Windows 更新脚本 install.ps1
    public List<string> Updater { get; set; } =
    [
        "https://raw.githubusercontent.com/lnetface/OneLauncher/master/build/install.ps1"
    ];
    // MCT 联机核心
    public List<string> ConnectCore { get; set; } =
    [
        "https://api.mct.mczlf.loft.games/mainnew.exe"
    ];
    // 皮肤全身渲染图，地址后直接拼接玩家 UUID
    public List<string> SkinGetterWithUUID { get; set; } =
    [
        "https://crafatar-pub.neodium.fr/renders/body/"
    ];
}
public class AppSettings : IJsonOnDeserialized
{
    // 创建或加入联机房间时自动启动游戏；旧配置保持原有行为。
    public bool AutoLaunchGameOnPowerPlay { get; set; } = true;
    // 下载
    public int MaximumDownloadThreads { get; set; } = 24;
    public int MaximumSha1Threads { get; set; } = 24;
    public bool IsSha1Enabled { get; set; } = true;
    public DownloadSourceStrategy DownloadMinecraftSourceStrategy { get; set; } = DownloadSourceStrategy.OfficialOnly;
    private string _installPath = "${OLAN_BASE_PATH}/installed";
    [AllowNull]
    public string InstallPath
    {
        get => _installPath;
        set => _installPath = string.IsNullOrWhiteSpace(value) ? "${OLAN_BASE_PATH}/installed" : value;
    }
    public string? DefaultInstanceID { get; set; }
    public Guid? DefaultServerID { get; set; }
    // 主页默认启动项为服务器收藏时记录其 Id；为 null 时主页使用 DefaultInstanceID 对应的游戏数据。
    public Guid? HomeDefaultServerID { get; set; }
    // 每天一更新
    public DateTimeOffset LastVersionManifestRefreshTime { get; set; } = DateTimeOffset.UtcNow;
    private OlanUISettings _olanUISettings = new();
    private bool _hasUISettings;
    [AllowNull]
    public OlanUISettings OlanUISettings
    {
        get => _olanUISettings;
        set
        {
            _olanUISettings = value ?? new();
            _hasUISettings = value != null;
        }
    }

    // 读取旧版平铺 UI 设置；保存时只写入新的嵌套对象。
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? LegacyUISettings { get; set; }

    void IJsonOnDeserialized.OnDeserialized()
    {
        if (!_hasUISettings && LegacyUISettings is { Count: > 0 })
        {
            var context = OneLauncherAppConfigsJsonContext.Default;
            var legacy = JsonSerializer.SerializeToElement(LegacyUISettings, context.DictionaryStringJsonElement);
            _olanUISettings = legacy.Deserialize(context.OlanUISettings) ?? new();
        }
        LegacyUISettings = null;
    }
}
public class OlanUISettings
{
    public int LeftButtonListSpacing { get; set; } = 8;
    public bool ShowIdentifiers { get; set; } = true;
    // 主页和设置始终显示，不提供可关闭的配置。
    public bool IsVersionPageVisible { get; set; } = false;
    public bool IsGameDataPageVisible { get; set; } = true;
    public bool IsServerPageVisible { get; set; } = true;
    public bool IsDownloadPageVisible { get; set; } = true;
    public bool IsModsBrowserPageVisible { get; set; } = true;
    public bool IsAccountPageVisible { get; set; } = true;
    // 主页样式；板块可见性只在魔法按钮样式下生效，默认样式始终显示全部板块。
    public HomePageStyle HomePageStyle { get; set; } = HomePageStyle.Classic;
    public bool IsHomeNewsVisible { get; set; } = true;
    public bool IsHomeTaskCenterVisible { get; set; } = true;
    public bool IsHomePowerPlayVisible { get; set; } = true;
    public bool IsHomeImportVisible { get; set; } = true;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public class AppConfig
{
    // 除了系统自带的Java以外启动器安装的所有Java版本列表
    public Dictionary<int,string?> AvailableJavas { get; set; } = new();
    // 玩家收藏的所有服务器列表，内部对应GameData
    public List<ServerEntry> ServerList { get; set; } = new();
    public AppSettings OlanSettings { get; set; } = new AppSettings();
    public DefaultConfig DefaultSettings { get; set; } = new DefaultConfig();
}
