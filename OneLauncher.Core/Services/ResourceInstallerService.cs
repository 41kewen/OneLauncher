using OneLauncher.Core.Global;
using OneLauncher.Core.Helper.Models;

namespace OneLauncher.Core.Services;

public sealed class ResourceInstallerService
{
    internal ResourceInstallerService()
    {
    }

    public Task InstallMod(
        IProgress<(long AllSizes, long DownedSizes, string DowingFileName)> progress,
        string modId,
        string modPath,
        string version,
        ModEnum modType,
        bool includeDependencies = true,
        int maxConcurrentDownloads = 8,
        int maxConcurrentSha1 = 4,
        bool verifySha1 = true,
        CancellationToken? cancellationToken = null)
    {
        return Init.Download.StartDownloadMod(
            progress,
            modId,
            modPath,
            version,
            modType,
            includeDependencies,
            maxConcurrentDownloads,
            maxConcurrentSha1,
            verifySha1,
            cancellationToken);
    }
}
