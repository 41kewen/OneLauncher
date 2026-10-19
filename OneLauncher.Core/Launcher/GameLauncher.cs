using OneLauncher.Core.Global;
using OneLauncher.Core.Global.ModelDataMangers;
using OneLauncher.Core.Helper;
using OneLauncher.Core.Helper.Models;
using OneLauncher.Core.Mod.ModLoader.forgeseries;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OneLauncher.Core.Launcher;
public class GameLauncher : IGameLauncher, IDisposable
{
    public event Action? GameStartedEvent;
    public event Action<int>? GameClosedEvent;
    public event Action<string>? GameOutputEvent;
    public CancellationToken CancellationToken = CancellationToken.None; // 外部可以设置

    private Process? _gameProcess;
    private string? _accessToken;
    public string GameDirectory { get; private set; } = "";
    private readonly string gameRootPath = Init.GameRootPath;
    private readonly AccountManager accountManager = Init.AccountManager;
    private async Task BasicLaunch(GameData gameData,ServerInfo? serverInfo = null,bool useRootMode = false)
    {
        #region 准备工作
        if (gameData.HasImportedLaunchManifest && gameData.ModLoader == ModEnum.forge)
            ForgeRuntimeLayout.ValidateImportedFiles(gameRootPath, gameData.VersionId, gameData.ModLoaderVersion);
        // 先把令牌刷新了
        UserModel user = Init.AccountManager.GetUser(gameData.DefaultUserModelID) ?? Init.AccountManager.GetDefaultUser();
        GameDirectory = useRootMode ? gameRootPath : gameData.InstancePath;
        Task resh =
            user.IntelligentLogin(Init.MsalAuthenticator);
        #region 外置登入补丁
        var libPath = Path.Combine(Init.InstalledPath, "authlib.jar");
        if(!File.Exists(libPath))
            // 如果不存在authlib.jar，则尝试下载
            await Init.Download
                .DownloadFile(
                "https://authlib-injector.yushi.moe/artifact/53/authlib-injector-1.2.5.jar",
                libPath
                );
        
        #endregion
        // 帮用户设置一下语言
        var optionsPath = Path.Combine(GameDirectory, "options.txt");
        if (!File.Exists(optionsPath))
            await File.WriteAllTextAsync(optionsPath, "lang:zh_CN");
        LaunchCommandBuilder commandBuilder = (await LaunchCommandBuilder.CreateAsync(
                gameRootPath,
                gameData.VersionId,
                gameData.HasImportedLaunchManifest ? Path.Combine(gameData.InstancePath, "launch.json") : null
            ));
        commandBuilder
            .SetLoginUser(accountManager.GetUser(gameData.DefaultUserModelID) ?? throw new OlanException("启动失败","找不到你想要启动的用户"))
            .WithServerInfo(serverInfo)
            .SetGamePath(GameDirectory)
            .SetModType(gameData.ModLoader);
        // 设置额外参数
        JvmArguments jvmArgsToUse = gameData.CustomJvmOptimizationArguments ?? Init.ConfigManger.Data.DefaultSettings.MinecraftJvmArguments;
        List<string> ojr = new(1);
        if (jvmArgsToUse.mode != OptimizationMode.None)
            ojr.AddRange(jvmArgsToUse.GetArguments(commandBuilder.versionInfo.GetJavaVersion(), gameData));
        if (gameData.ExtraJvmArguments != null)
            ojr.Add(gameData.ExtraJvmArguments);
        commandBuilder.WithExtraJvmArgs(ojr);
        if(gameData.CustomGameArguments != null)
            commandBuilder.WithExtraGameArgs([gameData.CustomGameArguments]);
        await resh;
        _accessToken = user.AccessToken;
        #endregion
        // 配置并启动游戏进程
        try
        {
            
            using (var launchCommand = await commandBuilder.BuildCommand())
            {
                string finalCommand = await launchCommand.GetArguments();
                var processInfo = new ProcessStartInfo
                {
                    FileName = commandBuilder.GetJavaPath(),
                    WorkingDirectory = GameDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    Arguments = finalCommand
                };
                await File.WriteAllTextAsync(Path.Combine(Init.BasePath, "LastLaunchArgs.bat"),
                    LaunchDiagnostics.Redact(finalCommand, _accessToken));
                _gameProcess = new Process();
                _gameProcess.StartInfo = processInfo;
                _gameProcess.OutputDataReceived += OnOutputDataReceived;
                _gameProcess.ErrorDataReceived += OnErrorDataReceived;
                // Java 读取 @参数文件前不能释放命令对象；文件在进程结束后清理。
                await RunProcessAsync(_gameProcess, code => GameClosedEvent?.Invoke(code), CancellationToken);
            }
        }
        catch(Win32Exception)
        {
            throw new OlanException("启动失败","无法找到Java运行环境，请确保已正确安装Java并在设置中配置了Java路径",OlanExceptionAction.Error);
        }
    }
    public Task Play(string InstanceID)
    {
        GameData? gameData = Init.GameDataManager.GetInstanceFromId(InstanceID);
        if (gameData == null)
            throw new OlanException("启动失败",$"没有找到匹配的且已安装的实例{Environment.NewLine}请检查你的输入{InstanceID}是否正确");
        return BasicLaunch(gameData);
    }
    public async Task Play(GameData gameData, ServerInfo? serverInfo = null)
        => await BasicLaunch(gameData, serverInfo);
    
    public async Task Play(UserVersion userVersion, ServerInfo? serverInfo = null, bool useRootMode = false)
    {
        GameData finded = await Init.GameDataManager.GetOrCreateInstanceAsync(userVersion);
        await BasicLaunch(finded, serverInfo, useRootMode);
    }
    public Task Stop()
    {
        if (_gameProcess != null && !_gameProcess.HasExited)
        {
            _gameProcess.Kill(true); // 强制结束进程树
            return _gameProcess.WaitForExitAsync();
        }
        else return Task.CompletedTask;
    }

    #region 事件处理
    internal static async Task RunProcessAsync(Process process, Action<int>? closed, CancellationToken token)
    {
        bool started = false;
        try
        {
            token.ThrowIfCancellationRequested();
            started = process.Start();
            if (!started) throw new InvalidOperationException("无法创建游戏进程。");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (started && !process.HasExited) process.Kill(entireProcessTree: true);
            if (started) await process.WaitForExitAsync();
            throw;
        }
        finally
        {
            if (started && process.HasExited)
            {
                // 输出流全部到达 EOF 后再通知关闭，失败诊断才能包含最后的 stderr。
                process.WaitForExit();
                closed?.Invoke(process.ExitCode);
            }
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        string text = LaunchDiagnostics.Redact(e.Data, _accessToken);
        Debug.WriteLine(text);
        GameOutputEvent?.Invoke($"[STDOUT] {text}{Environment.NewLine}");
        // 咱就是，其实没有什么真正好的判断游戏已经启动的方法
        if (e.Data.Contains("Backend library: LWJGL version"))
            GameStartedEvent?.Invoke();
    }
    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;
        string text = LaunchDiagnostics.Redact(e.Data, _accessToken);
        Debug.WriteLine(text);
        GameOutputEvent?.Invoke($"[ERROR] {text}{Environment.NewLine}");
    }

    public void Dispose()
    {
        _gameProcess?.Dispose();
    }
    #endregion
}
