using OneLauncher.Core.Global.ModelDataMangers;
using OneLauncher.Core.Helper.Models;
using OneLauncher.Core.Global;
using OneLauncher.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneLauncher.Views.Panes.PaneViewModels;

namespace OneLauncher.ViewModel.PaneViewModels.Factories;

internal class EditGameDataPaneViewModelFactory
{
    private readonly GameDataManager gameDataManager;
    private readonly AccountManager accountManager;
    private readonly ResourceInstallerService resourceInstaller;
    private readonly TaskManager taskManager;
    public EditGameDataPaneViewModelFactory(UnitedCore unitedCore)
    {
        gameDataManager = unitedCore.GameDataManager;
        accountManager = unitedCore.AccountManager;
        resourceInstaller = unitedCore.ResourceInstallerService;
        taskManager = unitedCore.TaskManager;
    }
    public EditGameDataPaneViewModel Create(GameData gameData,Action onCloseCallback)
    {
        return new EditGameDataPaneViewModel(gameData,gameDataManager,accountManager,resourceInstaller,taskManager,onCloseCallback);
    }
}
