using OneLauncher.Core.Downloader.DownloadMinecraftProviders;
using System;
using System.Collections.Generic;
using System.Text;

namespace OneLauncher.Core.Services;
public sealed class VersionInstallerService
{
    internal VersionInstallerService()
    {
    }

    public Task InstallNewVersion(
        DownloadInfo info,
        IProgress<(DownProgress Title, int AllFiles, int DownedFiles, string DowingFileName)> progress,
        CancellationToken? cancelToken = null
        )
    {
        return new DownloadMinecraft(info, progress, cancelToken).MinecraftBasic();
    }
}
