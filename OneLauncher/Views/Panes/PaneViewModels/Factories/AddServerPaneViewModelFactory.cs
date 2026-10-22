using OneLauncher.Core.Global.ModelDataMangers;
using OneLauncher.Core.Global;
using System;

namespace OneLauncher.Views.Panes.PaneViewModels.Factories;

internal sealed class AddServerPaneViewModelFactory
{
    private readonly DBManager _dbManager;
    private readonly GameDataManager _gameDataManager;

    public AddServerPaneViewModelFactory(
        DBManager dbManager,
        UnitedCore unitedCore)
    {
        _dbManager = dbManager;
        _gameDataManager = unitedCore.GameDataManager;
    }

    public AddServerPaneViewModel Create(
        Action onCloseCallback,
        Action<Guid>? onServerInfoUpdated = null)
    {
        return new AddServerPaneViewModel(
            _dbManager,
            _gameDataManager,
            onCloseCallback,
            onServerInfoUpdated);
    }
}
