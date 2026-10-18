using OneLauncher.Core.Global;
using OneLauncher.Core.Global.ModelDataMangers;
using OneLauncher.Core.Helper.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneLauncher.Views.Panes.PaneViewModels.Factories;

internal class DownloadPaneViewModelFactory
{
    private readonly DBManager _dbManager;
    private readonly GameDataManager _gameDataManager;
    private readonly UnitedCore _unitedCore;

    public DownloadPaneViewModelFactory(
        
        DBManager dbManager,
        GameDataManager gameDataManager,
        UnitedCore unitedCore
        )
    {
        _dbManager = dbManager;
        _gameDataManager = gameDataManager;
        _unitedCore = unitedCore;
    }

    public DownloadPaneViewModel Create(VersionBasicInfo version,Action onCloseCallback)
    {
        return new DownloadPaneViewModel(version, _dbManager,_gameDataManager,_unitedCore,onCloseCallback);
    }
}
