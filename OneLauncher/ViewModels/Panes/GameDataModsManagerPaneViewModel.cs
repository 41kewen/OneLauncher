using OneLauncher.Interfaces;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneLauncher.Codes;
using OneLauncher.Core.DefaultComponent.Resource.Mod.Manager;
using OneLauncher.Core.Global;
using OneLauncher.Core.Helper;
using OneLauncher.Core.Helper.Models;
using OneLauncher.ViewModel.Page;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OneLauncher.Views.Panes.PaneViewModels;
internal partial class InstanceModItem : BaseViewModel
{
    private readonly InstanceModService _instanceModService;
    [ObservableProperty]
    public ModInfo info;
    [ObservableProperty]
    public Bitmap icon;
    public string Description => Info.Description;
    public bool HasVersion => !info.IsUnknown && !string.IsNullOrWhiteSpace(Info.Version);
    public bool ModEnabledManager
    {
        get => Info.IsEnabled;
        set
        {
            try
            {
                Info.IsEnabled = value;
                if (value)
                    _instanceModService.EnableModAsync(Info.fileName);
                else
                    _instanceModService.DisableModAsync(Info.fileName);
            }
            catch(OlanException e)
            {
                OlanExceptionWorker.ForOlanException(e);
            }
            catch(Exception e)
            {
                OlanExceptionWorker.ForUnknowException(e);
            }
        }
    }
    public InstanceModItem(ModInfo info,InstanceModService modService)
    {
        _instanceModService = modService;
        Info = info;
        if(info.Icon != null)
            using (var stream = new MemoryStream(info.Icon))
                Icon = new Bitmap(stream);
        else
            Icon = new Bitmap(AssetLoader.Open(new Uri("avares://OneLauncher/Assets/Imgs/basic.png"))); // 默认图标
    }
}
internal partial class GameDataModsManagerPaneViewModel : BaseViewModel, IPaneHost
{
    // 完整列表保留所有实际文件，筛选仅更新显示列表。
    [ObservableProperty]
    private List<InstanceModItem> allMods = new();

    [ObservableProperty]
    private List<InstanceModItem> mods = new();

    [ObservableProperty]
    private string? searchText = string.Empty;

    private readonly GameData _gameData;
    private readonly InstanceModService _modService;
    public Action OnPaneClose { get; init; } = () => { };

    public GameDataModsManagerPaneViewModel(GameData gameData,Action onCloseCallback)
    {
#if DEBUG
        if(Design.IsDesignMode)
        {
            // 设计时数据
            AllMods = new List<InstanceModItem>
            {
                //new InstanceModItem(new ModInfo { Id = "mod1", Name = "Mod 1", Version = "1.0", Description = "这是一个测试Mod", IsEnabled = true }),
                //new InstanceModItem(new ModInfo { Id = "mod2", Name = "Mod 2", Version = "1.1", Description = "这是另一个测试Mod", IsEnabled = false }),
            };
        }
        else
#endif
        {
            _gameData = gameData;
            _modService = new InstanceModService(gameData);
            OnPaneClose = onCloseCallback ?? (() => { });
            _ = RefreshModsAsync(); // 初始化时加载
        }
    }

    [RelayCommand]
    private async Task RefreshModsAsync()
    {
        AllMods = (await _modService.GetModsAsync()).Select(x => new InstanceModItem(x,_modService)).ToList();
    }

    partial void OnAllModsChanged(List<InstanceModItem> value) => FilterMods();

    partial void OnSearchTextChanged(string? value) => FilterMods();

    private void FilterMods()
    {
        var keyword = SearchText?.Trim();
        Mods = string.IsNullOrEmpty(keyword)
            ? AllMods.ToList()
            : AllMods.Where(mod =>
                ContainsKeyword(mod.Info.Name, keyword) ||
                ContainsKeyword(mod.Info.Id, keyword) ||
                ContainsKeyword(mod.Description, keyword) ||
                ContainsKeyword(mod.Info.fileName, keyword)).ToList();
    }

    private static bool ContainsKeyword(string? text, string keyword) =>
        text?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;

    [RelayCommand]
    private void OpenModsFolder()
    {
        Tools.OpenFolder(Path.Combine(_gameData.InstancePath, "mods"));
    }

    [RelayCommand]
    private void ClosePane() => OnPaneClose();
}
