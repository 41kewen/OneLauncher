using Microsoft.Extensions.DependencyInjection;
using OneLauncher.Core.DefaultComponent.Downloader;
using OneLauncher.Core.Global.ModelDataMangers;
using OneLauncher.Core.Helper;
using OneLauncher.Core.Helper.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OneLauncher.Core.Global;
/// <summary>
/// 此代码耦合过于严重，现职责已迁移到UnitedCore中，该类仅用于还没来得及重写的大量原始代码使用
/// </summary>
public static partial class Init
{
    public const string ApplicationID = "53740b20-7f24-46a3-82cc-ea0376b9f5b5";
    public const string PackageName = "com.onelauncher.lnetface";
    public const string ProjectWebsite = "https://github.com/lnetface/OneLauncher";
    public static Task<IServiceCollection> InitTask;
    //public static IServiceCollection Service { get; private set; }
    public static string BasePath { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneLauncher");
    public static string GameRootPath
#if DEBUG
    { get; set; }
#else
    { get; private set; }
#endif
    private static string _installedPath = null!;
    public static string InstalledPath
    {
        get => _installedPath;
        // 启动时解析并固定路径，配置变更在重启后生效。
        private set => _installedPath = Path.GetFullPath(Tools.ReplacePlaceholders(value,
            new Dictionary<string, string>
            {
                ["OLAN_BASE_PATH"] = BasePath,
                ["OLAN_APP_RUNNING_PATH"] = AppContext.BaseDirectory
            }).Replace('/', Path.DirectorySeparatorChar));
    }
    public static SystemType SystemType { get; private set; }
    //public static List<VersionBasicInfo> MojangVersionList { get; set; } = null;
    public static List<IDisposable> OnApplicationClosingReleaseSourcesList = new();
    // 必要的时候还是得耦合一下的，啥都得传递那他妈的写起来太难受了
    public static DBManager ConfigManger => ConfigManager;
    internal static DBManager ConfigManager { get; private set; }
    internal static Download Download { get; private set; }
    public static async Task<IServiceCollection> Initialize(bool isCommandMode = false, string? basePath = null)
    {
        if (basePath != null)
            BasePath = Path.GetFullPath(basePath);
        string diagnosticStep = "创建启动器数据目录";
        string diagnosticTarget = BasePath;
        try
        {
            Directory.CreateDirectory(BasePath);

            var services = new ServiceCollection();
            
            // 先把最重要的基本配置信息初始化了，然后初始化别的
            var configManger = new DBManager(Path.Combine(BasePath,"config.json"));
            diagnosticStep = "读取并初始化启动器配置";
            diagnosticTarget = Path.Combine(BasePath, "config.json");
            await configManger.InitializeAsync();
            services.AddSingleton<DBManager>(configManger);
            ConfigManager = configManger;

            // 持久化配置模型对旧版 null 或空安装路径的默认值补全。
            diagnosticStep = "保存默认安装路径配置";
            await configManger.Save();
            diagnosticStep = "解析游戏安装目录配置";
            InstalledPath = configManger.Data.OlanSettings.InstallPath;
            GameRootPath = Path.Combine(InstalledPath, "minecraft");
            diagnosticStep = "创建游戏安装目录";
            diagnosticTarget = InstalledPath;
            Directory.CreateDirectory(InstalledPath);
            // 初始化系统信息
            SystemType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? SystemType.windows :
                         RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? SystemType.linux :
                         RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? SystemType.osx : SystemType.linux;

            var downloadTool = new Download();
            services.AddSingleton<Download>(downloadTool);
            OnApplicationClosingReleaseSourcesList.Add(downloadTool);
            Download = downloadTool;

            diagnosticStep = "初始化核心组件";
            var unitedCore = await UnitedCore.Initialize(isCommandMode);
            services.AddSingleton<UnitedCore>(unitedCore);

            return services; 
        }
        #region
        catch (ArgumentException ex)
        {
            throw new OlanException("参数错误", $"初始化启动器时校验参数；当前阶段={diagnosticStep}；目标文件/目录={diagnosticTarget}。参数未被正常传递：{ex}", OlanExceptionAction.FatalError);
        }
        catch (PathTooLongException ex)
        {
            throw new OlanException("路径过长", $"初始化启动器时解析路径；当前阶段={diagnosticStep}；目标文件/目录={diagnosticTarget}。路径过长：{ex}", OlanExceptionAction.FatalError);
        }
        catch (NotSupportedException ex)
        {
            throw new OlanException("不支持的操作", $"初始化启动器时执行平台操作；当前阶段={diagnosticStep}；目标文件/目录={diagnosticTarget}。当前操作不被支持：{ex}", OlanExceptionAction.FatalError);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new OlanException("权限不足", $"初始化启动器时访问目录或配置；当前阶段={diagnosticStep}；目标文件/目录={diagnosticTarget}。当前用户没有足够的权限：{ex}", OlanExceptionAction.FatalError);
        }
        catch (FileNotFoundException ex)
        {
            throw new OlanException("文件未找到", $"初始化启动器时读取所需文件；当前阶段={diagnosticStep}；目标文件/目录={diagnosticTarget}。所需文件不存在：{ex}", OlanExceptionAction.FatalError);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new OlanException("目录未找到", $"初始化启动器时访问所需目录；当前阶段={diagnosticStep}；目标文件/目录={diagnosticTarget}。所需目录不存在：{ex}", OlanExceptionAction.FatalError);
        }
        catch (InvalidOperationException ex)
        {
            throw new OlanException("操作无效", $"初始化启动器时建立核心组件；当前阶段={diagnosticStep}；目标文件/目录={diagnosticTarget}。当前操作无效：{ex}", OlanExceptionAction.FatalError);
        }
        #endregion
    }
}
