using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace OneLauncher.Core.Global.Updater;
public class AutoUpdate
{
    const string CheckUpdateUrl = "https://raw.githubusercontent.com/lnetface/OneLauncher/master/OneLauncher.Core/Global/Version.cs";
    const string Manifest = "https://raw.githubusercontent.com/lnetface/OneLauncher/master/resource_manifest.json";
    /// <summary>
    /// 请求资源清单，过滤后写入 DefaultSettings；失败时只记录警告，保留现有配置。
    /// </summary>
    public static async Task CheckManifestUpdate()
    {
        try
        {
            // 1. 请求并解析清单
            ResourceManifest? manifest;
            await using (var stream = await Init.Download.HttpStreamGetter(Manifest, CancellationToken.None))
                manifest = await JsonSerializer.DeserializeAsync(stream, ResourceManifestJsonContext.Default.ResourceManifest);
            if (manifest == null)
                return;

            // 2. 过滤：清单记录的最新版本低于当前版本时忽略整份清单
            if (!Version.TryParse(manifest.LatestLauncherVersion, out var latestVersion))
                throw new OlanException("资源清单无效", $"解析资源清单中的启动器版本；请求URL={Manifest}；LatestLauncherVersion={manifest.LatestLauncherVersion}。无法识别的版本号", OlanExceptionAction.Warning);
            if (latestVersion < new Version(Init.ApplicationVersoin))
                return;

            // 3. 写入配置：存在值则覆盖，null、缺失或空列表则忽略
            var settings = Init.ConfigManager.Data.DefaultSettings;
            var resources = manifest.Resources;
            if (CleanList(resources?.WindowsUpdater) is { } updater)
                settings.Resources.Updater = updater;
            if (CleanList(resources?.ConnectCore) is { } connectCore)
                settings.Resources.ConnectCore = connectCore;
            // 配置中皮肤地址为直接拼接 UUID 的前缀，只接受以 ${UUID} 结尾的模板
            if (CleanList(resources?.SkinBodyRender?
                    .Where(url => url.EndsWith(UuidPlaceholder, StringComparison.Ordinal))
                    .Select(url => url[..^UuidPlaceholder.Length])) is { } skin)
                settings.Resources.SkinGetterWithUUID = skin;

            // 两个开关合并为 AllowNonMicrosoftLogin：任一为 false 即禁用；都为 null 则忽略
            var function = manifest.Function;
            if (function?.AllowOfflineLogin == false || function?.AllowYggdrasilLogin == false)
                settings.AllowNonMicrosoftLogin = false;
            else if (function?.AllowOfflineLogin == true || function?.AllowYggdrasilLogin == true)
                settings.AllowNonMicrosoftLogin = true;

            if (CleanList(manifest.RecommendedMods) is { } recommendedMods)
                settings.RecommandedMods = recommendedMods;
            // 只保留正整数版本号并去重
            var javaVersions = manifest.JavaVersions?.Where(version => version > 0).Distinct().ToList();
            if (javaVersions is { Count: > 0 })
                settings.AvailableJavaVersions = javaVersions;

            await Init.ConfigManager.Save();
        }
        catch (Exception ex)
        {
            if (ex is not OlanException)
                _ = new OlanException("同步资源清单失败", $"请求、解析资源清单并写入配置；请求URL={Manifest}；当前版本={Init.ApplicationVersoin}。原因：{ex.Message}", OlanExceptionAction.Warning, ex);
            /*保留现有配置即可*/
        }
    }
    const string UuidPlaceholder = "${UUID}";
    // 去除空白项与重复项；结果为空时返回 null 表示忽略
    private static List<string>? CleanList(IEnumerable<string?>? items)
    {
        var result = items?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct()
            .ToList();
        return result is { Count: > 0 } ? result : null;
    }
    /// <returns>需要更新返回true，否则返回false</returns>
    public static async Task<bool> CheckUpdate()
    {
        try
        {
            using var httpClient = new HttpClient();
            string response = await httpClient.GetStringAsync(CheckUpdateUrl);
            int start = -1, end = -1;
            for(int i  = 0; i < response.Length; i++)
            {
                if (response[i] == '\"')
                {
                    start = i+1;
                    break;
                }
            }
            if(start == -1) throw new Exception();
            for (int i = start; i < response.Length; i++)
            {
                if (response[i] == '\"')
                {
                    end = i;
                    break;
                }
            }
            if(end == -1) throw new Exception();
            var latestVersion = new Version(response[start..end]);
            if (latestVersion <= new Version(Init.ApplicationVersoin))
                return false;
            else
                return true;
        }
        catch (Exception ex)
        {
            if (ex is not OlanException)
                _ = new OlanException("检查更新失败", $"请求并解析启动器最新版本；请求URL={CheckUpdateUrl}；当前版本={Init.ApplicationVersoin}。原因：{ex.Message}", OlanExceptionAction.Warning, ex);
            /*无关紧要，忽略就好*/
            return false;
        }
    }
    public static async Task Update()
    {
        var updaterSources = Init.ConfigManager.Data.DefaultSettings.Resources.Updater;
        try
        {
#if ONELAUNCHER_TARGET_WINDOWS
            var path = Path.Combine(Init.BasePath, "install.ps1");
            await Init.Download.DownloadFile(updaterSources, path);
            string executablePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("无法确定当前启动器路径。");
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            process.StartInfo.ArgumentList.Add("Bypass");
            process.StartInfo.ArgumentList.Add("-File");
            process.StartInfo.ArgumentList.Add(path);
            process.StartInfo.ArgumentList.Add("update");
            process.StartInfo.ArgumentList.Add(executablePath);
            process.Start();
#endif
        }
        catch (Exception ex)
        {
            if (ex is not OlanException)
                _ = new OlanException("更新启动器失败", $"下载并启动更新脚本；候选URL={string.Join(", ", updaterSources)}；脚本文件={Init.BasePath}/install.ps1。原因：{ex.Message}", OlanExceptionAction.Warning, ex);
            Debug.WriteLine(ex);
        }
    }
}
