using OneLauncher.Core.Global;
using OneLauncher.Core.Global.ModelDataMangers;
using OneLauncher.Views.ViewModels;
using System;

namespace OneLauncher.Views.Panes.PaneViewModels.Factories;

internal class InstallModPaneViewModelFactory
{
    private readonly GameDataManager _gameDataManager;
    private readonly UnitedCore _unitedCore;

    public InstallModPaneViewModelFactory(UnitedCore unitedCore)
    {
        _gameDataManager = unitedCore.GameDataManager;
        _unitedCore = unitedCore;
    }

    public InstallModPaneViewModel Create(ModItem item, Action onCloseCallback)
    {
        return new InstallModPaneViewModel(item, _gameDataManager, _unitedCore, onCloseCallback);
    }
}
