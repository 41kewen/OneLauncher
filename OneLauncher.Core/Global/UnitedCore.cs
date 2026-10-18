using OneLauncher.Core.Helper.Models;
using OneLauncher.Core.Minecraft;
using OneLauncher.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace OneLauncher.Core.Global;
public class UnitedCore
{
    internal static TaskManager _TaskManager;
    public TaskManager TaskManager { get; init; }
    internal static VersionInstallerService _VersionInstallerService;
    public VersionInstallerService VersionInstallerService { get; init; }
    internal static List<VersionBasicInfo> _MojangVersionList;
    public List<VersionBasicInfo> MojangVersionList { get; set; }
    public static async Task<UnitedCore> Initialize()
    {
        return new UnitedCore()
        {
            TaskManager = _TaskManager = new TaskManager(),
            VersionInstallerService = _VersionInstallerService = new VersionInstallerService(),
            MojangVersionList = _MojangVersionList = new (await VersionsList.GetOrRefreshVersionListAsync())
        };
    }
}
