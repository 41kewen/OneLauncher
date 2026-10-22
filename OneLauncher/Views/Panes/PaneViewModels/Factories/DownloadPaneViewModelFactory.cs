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
    private readonly UnitedCore _unitedCore;

    public DownloadPaneViewModelFactory(UnitedCore unitedCore)
    {
        _unitedCore = unitedCore;
    }

    public DownloadPaneViewModel Create(VersionBasicInfo version,Action onCloseCallback)
    {
        return new DownloadPaneViewModel(version, _unitedCore.GameDataManager, _unitedCore, onCloseCallback);
    }
}
