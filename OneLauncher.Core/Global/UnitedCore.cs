using OneLauncher.Core.DefaultComponent.Account.Microsoft;
using OneLauncher.Core.Global.ModelDataMangers;
using OneLauncher.Core.Global.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneLauncher.Core.Helper.Models;
using OneLauncher.Core.Minecraft;
using OneLauncher.Core.Services;
using OneLauncher.Core.Global.Updater;

namespace OneLauncher.Core.Global;
public class UnitedCore
{
    private static readonly Lazy<LoggingDependencies> Logging = new(CreateLoggingDependencies);
    internal static UnitedCore Core { get; private set; } = null!;
    public ExceptionFileLogManager LogManager => Logging.Value.Manager;
    public required TaskManager TaskManager { get; init; }
    public required VersionInstallerService VersionInstallerService { get; init; }
    public required VersionListManager VersionListManager { get; init; }
    public required ResourceInstallerService ResourceInstallerService { get; init; }
    public required GameLaunchService GameLaunchService { get; init; }
    public List<VersionBasicInfo> MojangVersionList { get; set; }
    public required AccountManager AccountManager { get; init; }
    public required GameDataManager GameDataManager { get; init; }
    public required MsalAuthenticator MsalAuthenticator { get; init; }
    public required JavaManager JavaManager { get; init; }

    /// <summary>供 OlanException 构造函数调用；记录失败不得影响原异常。</summary>
    internal static void Log(OlanException exception)
    {
        try
        {
            Logging.Value.Logger.LogError(exception,
                "{Title}；Action={Action}；{Message}", exception.Title, exception.Action, exception.Message);
        }
        catch (Exception)
        {
            // 包括日志依赖创建失败；不能在此构造 OlanException 或递归记录。
        }
    }

    private static LoggingDependencies CreateLoggingDependencies()
    {
        // 仅管理日志依赖，不提前构建应用容器，也不依赖完整 Core 初始化。
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders().AddFilter(
            (category, level) => category == typeof(OlanException).FullName && level == LogLevel.Error));
        services.AddSingleton(_ => new ExceptionFileLogManager(Path.Combine(Init.BasePath, "logs")));
        services.AddSingleton<ILoggerProvider>(provider => provider.GetRequiredService<ExceptionFileLogManager>());
        var provider = services.BuildServiceProvider();
        try
        {
            var dependencies = new LoggingDependencies(
                provider.GetRequiredService<ExceptionFileLogManager>(),
                provider.GetRequiredService<ILogger<OlanException>>());
            Init.OnApplicationClosingReleaseSourcesList.Add(provider);
            return dependencies;
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    private sealed record LoggingDependencies(ExceptionFileLogManager Manager, ILogger<OlanException> Logger);

    public static async Task<UnitedCore> Initialize(bool isCommandMode = false)
    {
        _ = Logging.Value;
        if (!isCommandMode)
            _ = AutoUpdate.CheckManifestUpdate();
        var versionListManager = new VersionListManager(Init.GameRootPath);
        await versionListManager.InitializeAsync();

        var taskManager = new TaskManager();
        var accountManger = new AccountManager(Path.Combine(Init.BasePath, "playerdata", "account.json"));
        await accountManger.InitializeAsync();

        var gameDataManger = new GameDataManager(Path.Combine(Init.GameRootPath, "instance", "instance.json"));
        await gameDataManger.InitializeAsync();

        var msrl = await MsalAuthenticator.CreateAsync(Init.ApplicationID);
        Init.OnApplicationClosingReleaseSourcesList.Add(msrl);

        var javaManager = new JavaManager();
        var core = new UnitedCore()
        {
            TaskManager = taskManager,
            VersionListManager = versionListManager,
            VersionInstallerService = new VersionInstallerService(versionListManager),
            ResourceInstallerService = new ResourceInstallerService(),
            GameLaunchService = new GameLaunchService(taskManager),
            // 不得await，否则初始化阶段网络异常会直接导致整个启动器崩溃
            //MojangVersionList = new (await VersionsList.GetOrRefreshVersionListAsync()),
            AccountManager = accountManger,
            GameDataManager = gameDataManger,
            MsalAuthenticator = msrl,
            JavaManager = javaManager
        };
        core.MojangVersionList = new();
        Core = core;
        //if (!isCommandMode)
        //    _ = Task.Run(async () =>
        //    {
        //        core.MojangVersionList = new(await VersionsList.GetOrRefreshVersionListAsync());
        //    });
        return core;
    }
}
