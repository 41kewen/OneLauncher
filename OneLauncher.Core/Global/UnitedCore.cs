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
    internal static ResourceInstallerService _ResourceInstallerService;
    public ResourceInstallerService ResourceInstallerService { get; init; }
    internal static GameLaunchService _GameLaunchService;
    public GameLaunchService GameLaunchService { get; init; }
    internal static List<VersionBasicInfo> _MojangVersionList;
    public List<VersionBasicInfo> MojangVersionList { get; set; }
    public static async Task<UnitedCore> Initialize()
    {
        var taskManager = new TaskManager();
        return new UnitedCore()
        {
            TaskManager = _TaskManager = taskManager,
            VersionInstallerService = _VersionInstallerService = new VersionInstallerService(),
            ResourceInstallerService = _ResourceInstallerService = new ResourceInstallerService(),
            GameLaunchService = _GameLaunchService = new GameLaunchService(taskManager),
            MojangVersionList = _MojangVersionList = new (await VersionsList.GetOrRefreshVersionListAsync())
        };
    }
}
