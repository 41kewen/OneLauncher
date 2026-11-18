using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.Messaging;
using OneLauncher.Codes;
using OneLauncher.Core.DefaultComponent.Server;
using OneLauncher.Core.DefaultComponent.Downloader.DownloadMinecraftProviders;
using OneLauncher.Core.Global;
using OneLauncher.Core.Global.ModelDataMangers;
using OneLauncher.Core.Helper;
using OneLauncher.Core.Helper.Models;
using OneLauncher.Views.Panes;
using OneLauncher.Views.Panes.PaneViewModels;
using OneLauncher.Views.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using OneLauncher.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneLauncher.ViewModel.Page;
using OneLauncher.Core.Global.Updater;

namespace OneLauncher.Views.ViewModels;
internal sealed class IdentifierVisibilityChangedMessage { }

internal partial class SettingsPageViewModel : BaseViewModel, IPageHost, IListTemplate<NavigationVisibilityItem>
{
    // 将 manager 重命名为 _dbManger 以遵循常见的私有字段命名约定
    private readonly DBManager _dbManger;
    private JavaManager _javaManager => _unitedCore.JavaManager;
    private GameDataManager _gameDataManager => _unitedCore.GameDataManager;
    private readonly UnitedCore _unitedCore;
    public string Version => Init.ApplicationVersoin;

    #region 设置筛选
    public string[] SettingsCategories { get; } = ["全部设置", "界面", "游戏启动", "下载", "运行环境"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInterfaceSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsGameLaunchSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsDownloadSectionVisible))]
    [NotifyPropertyChangedFor(nameof(IsRuntimeSectionVisible))]
    private string selectedSettingsCategory = "全部设置";

    public bool IsInterfaceSectionVisible => SelectedSettingsCategory is "全部设置" or "界面";
    public bool IsGameLaunchSectionVisible => SelectedSettingsCategory is "全部设置" or "游戏启动";
    public bool IsDownloadSectionVisible => SelectedSettingsCategory is "全部设置" or "下载";
    public bool IsRuntimeSectionVisible => SelectedSettingsCategory is "全部设置" or "运行环境";
    #endregion

    [ObservableProperty]
    private bool autoLaunchGameOnPowerPlay = true;

    [ObservableProperty]
    private bool isSavingPowerPlaySetting;

    partial void OnAutoLaunchGameOnPowerPlayChanged(bool value)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        if (!IsSavingPowerPlaySetting)
            _ = SavePowerPlaySettingAsync(value);
    }

    private async Task SavePowerPlaySettingAsync(bool value)
    {
        var settings = _dbManger.Data.OlanSettings;
        var previousValue = settings.AutoLaunchGameOnPowerPlay;
        IsSavingPowerPlaySetting = true;
        try
        {
            settings.AutoLaunchGameOnPowerPlay = value;
            await _dbManger.Save();
            WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage("联机自动启动游戏设置已保存", NotificationType.Success));
        }
        catch (Exception ex)
        {
            settings.AutoLaunchGameOnPowerPlay = previousValue;
            AutoLaunchGameOnPowerPlay = previousValue;
            await ShowSettingsException(ex);
        }
        finally
        {
            IsSavingPowerPlaySetting = false;
        }
    }

    #region 启动参数优化选项
    [ObservableProperty]
    public bool isM1, isM2, isM3;
    private readonly System.Threading.SemaphoreSlim _jvmSelectionLock = new(1, 1);
    private bool _loadingJvmSelection;
    partial void OnIsM1Changed(bool value)
    {
        if (!value) return; // 避免在取消选中时也执行
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        // 使用注入的实例
        if (!_loadingJvmSelection) _ = SaveJvmModeAsync(OptimizationMode.Conservative);
    }
    partial void OnIsM2Changed(bool value)
    {
        if (!value) return;
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        // 使用注入的实例
        if (!_loadingJvmSelection) _ = SaveJvmModeAsync(OptimizationMode.Standard);
    }
    partial void OnIsM3Changed(bool value)
    {
        if (!value) return;
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        // 使用注入的实例
        if (!_loadingJvmSelection) _ = SaveJvmModeAsync(OptimizationMode.Aggressive);
    }

    private async Task SaveJvmModeAsync(OptimizationMode mode)
    {
        await _jvmSelectionLock.WaitAsync();
        try
        {
            await ReplaceJvmPresetAsync(_dbManger.Data.DefaultSettings, mode, () => _dbManger.Save());
        }
        catch (Exception ex)
        {
            await OlanExceptionWorker.ForOlanException(ex as OlanException ?? new OlanException("无法保存 JVM 配置", ($"保存 JVM 优化模式到配置文件；配置文件={Path.Combine(Init.BasePath, "config.json")}。" + ex.Message), OlanExceptionAction.Error, ex));
        }
        finally
        {
            LoadJvmSelection(_dbManger.Data.DefaultSettings.MinecraftJvmArguments.mode);
            _jvmSelectionLock.Release();
        }
    }

    /// <summary>用户切换时整份替换，保存失败恢复原对象，不合并手工编辑字段。</summary>
    internal static async Task ReplaceJvmPresetAsync(DefaultConfig settings, OptimizationMode mode, Func<Task> save)
    {
        var previous = settings.MinecraftJvmArguments;
        settings.MinecraftJvmArguments = JvmArguments.CreateFromMode(mode);
        try { await save(); }
        catch
        {
            settings.MinecraftJvmArguments = previous;
            throw;
        }
    }

    private void LoadJvmSelection(OptimizationMode mode)
    {
        _loadingJvmSelection = true;
        try
        {
            IsM1 = mode == OptimizationMode.Conservative;
            IsM2 = mode == OptimizationMode.Standard;
            IsM3 = mode == OptimizationMode.Aggressive;
        }
        finally { _loadingJvmSelection = false; }
    }
    [RelayCommand]
    private void ReleaseMemory()
    {
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = Environment.ProcessPath,
            Arguments = "--releaseMemory"
#if ONELAUNCHER_TARGET_WINDOWS
            ,
            Verb = "runas" // 请求管理员权限
#endif
        });
    }
    #endregion

    #region UI选项
    [ObservableProperty]
    private bool showIdentifiers = true;

    [ObservableProperty]
    private bool isSavingIdentifiers;

    partial void OnShowIdentifiersChanged(bool value)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        _ = SaveIdentifierVisibilityAsync(value);
    }

    private async Task SaveIdentifierVisibilityAsync(bool value)
    {
        var settings = _dbManger.Data.OlanSettings;
        bool previous = settings.OlanUISettings.ShowIdentifiers;
        IsSavingIdentifiers = true;
        try
        {
            settings.OlanUISettings.ShowIdentifiers = value;
            await _dbManger.Save();
            WeakReferenceMessenger.Default.Send(new IdentifierVisibilityChangedMessage());
            WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage(
                "标识符显示设置已保存并应用", NotificationType.Success));
        }
        catch (Exception ex)
        {
            settings.OlanUISettings.ShowIdentifiers = previous;
            SetProperty(ref showIdentifiers, previous, nameof(ShowIdentifiers));
            if (ex is OlanException olanException)
                await OlanExceptionWorker.ForOlanException(olanException);
            else
                await OlanExceptionWorker.ForUnknowException(ex);
        }
        finally
        {
            IsSavingIdentifiers = false;
        }
    }

    [ObservableProperty]
    public int _leftButtonListSpacing;
    partial void OnLeftButtonListSpacingChanged(int value)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        _dbManger.Data.OlanSettings.OlanUISettings.LeftButtonListSpacing = Math.Clamp(value, 0, 30);
        _dbManger.Save();
    }

    [ObservableProperty]
    private ObservableCollection<NavigationVisibilityItem> items = new();

    [ObservableProperty]
    private bool isNavigationEditing;

    [ObservableProperty]
    private bool isSavingNavigation;

    partial void OnIsNavigationEditingChanged(bool value)
    {
        if (value && !IsSavingNavigation)
            _ = RefList();
    }

    public Task RefList()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RebuildList();
            return Task.CompletedTask;
        }
        return Dispatcher.UIThread.InvokeAsync(RebuildList).GetTask();
    }

    private void RebuildList()
    {
#if DEBUG
        var settings = Design.IsDesignMode ? new AppSettings() : _dbManger.Data.OlanSettings;
#else
        var settings = _dbManger.Data.OlanSettings;
#endif
        Items = new ObservableCollection<NavigationVisibilityItem>(CreateNavigationItems(settings));
    }

    private static List<NavigationVisibilityItem> CreateNavigationItems(AppSettings settings) =>
    [
        new(NavigationPage.Version, "版本管理", settings.OlanUISettings.IsVersionPageVisible),
        new(NavigationPage.GameData, "游戏数据", settings.OlanUISettings.IsGameDataPageVisible),
        new(NavigationPage.Server, "服务器收藏", settings.OlanUISettings.IsServerPageVisible),
        new(NavigationPage.Download, "下载管理", settings.OlanUISettings.IsDownloadPageVisible),
        new(NavigationPage.ModsBrowser, "资源检索", settings.OlanUISettings.IsModsBrowserPageVisible),
        new(NavigationPage.Account, "账户管理", settings.OlanUISettings.IsAccountPageVisible)
    ];

    private static void ApplyNavigationItems(AppSettings settings, IEnumerable<NavigationVisibilityItem> items)
    {
        foreach (var item in items)
        {
            switch (item.Page)
            {
                case NavigationPage.Version: settings.OlanUISettings.IsVersionPageVisible = item.IsVisible; break;
                case NavigationPage.GameData: settings.OlanUISettings.IsGameDataPageVisible = item.IsVisible; break;
                case NavigationPage.Server: settings.OlanUISettings.IsServerPageVisible = item.IsVisible; break;
                case NavigationPage.Download: settings.OlanUISettings.IsDownloadPageVisible = item.IsVisible; break;
                case NavigationPage.ModsBrowser: settings.OlanUISettings.IsModsBrowserPageVisible = item.IsVisible; break;
                case NavigationPage.Account: settings.OlanUISettings.IsAccountPageVisible = item.IsVisible; break;
            }
        }
    }

    [RelayCommand]
    private async Task SaveNavigation()
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        var settings = _dbManger.Data.OlanSettings;
        var previousItems = CreateNavigationItems(settings);
        ServerEntry? addedServer = null;
        IsSavingNavigation = true;
        WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage("正在保存导航栏设置"));
        try
        {
            ApplyNavigationItems(settings, Items);
            // 默认服务器仅在保存时添加；没有游戏实例时保持服务器列表为空。
            if (settings.OlanUISettings.IsServerPageVisible && _dbManger.Data.ServerList.Count == 0 &&
                _gameDataManager.GetDefaultInstance() is { } defaultInstance)
            {
                addedServer = new(Guid.NewGuid(), defaultInstance.InstanceId,
                    new() { Ip = "mc.hypixel.net", Port = 25565 }, "Hypixel",
                    "为避免空旷为您添加了一个默认的受欢迎服务器");
                _dbManger.Data.ServerList.Add(addedServer);
            }

            await _dbManger.Save();
            IsNavigationEditing = false;
            WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage(
                "导航栏设置已保存并应用", NotificationType.Success));
        }
        catch (Exception ex)
        {
            ApplyNavigationItems(settings, previousItems);
            if (addedServer != null)
                _dbManger.Data.ServerList.Remove(addedServer);
            if (ex is OlanException olanException)
                await OlanExceptionWorker.ForOlanException(olanException);
            else
                await OlanExceptionWorker.ForUnknowException(ex);
        }
        finally
        {
            IsSavingNavigation = false;
        }
    }
    #endregion

    #region 主页样式
    public string[] HomePageStyles { get; } = ["默认", "魔法按钮"];
    private bool _isLoadingHomeSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMagicHomeOptionsVisible))]
    private string selectedHomePageStyle = "默认";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHomeBackground))]
    private string? currentHomeBackgroundPath;

    [ObservableProperty]
    private bool isSavingHomeAppearance;

    [ObservableProperty]
    private ObservableCollection<HomeSectionVisibilityItem> homeSectionItems = new();

    [ObservableProperty]
    private bool isHomeSectionEditing;

    [ObservableProperty]
    private bool isSavingHomeSections;

    [ObservableProperty]
    private ObservableCollection<IQuicklyLaunchItem> homeLaunchItems = new();

    [ObservableProperty]
    private IQuicklyLaunchItem? selectedHomeLaunchItem;

    [ObservableProperty]
    private bool isSavingHomeLaunchItem;

    private string? _homeLaunchItemsSignature;

    public bool IsMagicHomeOptionsVisible => SelectedHomePageStyle == "魔法按钮";
    public bool HasHomeBackground => CurrentHomeBackgroundPath != null;

    partial void OnSelectedHomePageStyleChanged(string value)
    {
        if (_isLoadingHomeSettings)
            return;
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        var style = value == "魔法按钮" ? HomePageStyle.MagicButton : HomePageStyle.Classic;
        _ = SaveHomePageStyleAsync(style);
    }

    partial void OnIsHomeSectionEditingChanged(bool value)
    {
        if (value && !IsSavingHomeSections)
            RebuildHomeSectionItems();
    }

    partial void OnSelectedHomeLaunchItemChanged(IQuicklyLaunchItem? value)
    {
        if (_isLoadingHomeSettings || value == null)
            return;
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        _ = SaveHomeLaunchItemAsync(value);
    }

    private async Task SaveHomeLaunchItemAsync(IQuicklyLaunchItem item)
    {
        var settings = _dbManger.Data.OlanSettings;
        var previousInstance = settings.DefaultInstanceID;
        var previousServer = settings.HomeDefaultServerID;
        IsSavingHomeLaunchItem = true;
        try
        {
            QuicklyLaunchItems.SaveAsDefault(settings, item);
            await _dbManger.Save();
            WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage(
                $"主页默认启动项已设为：{item.Name}", NotificationType.Success));
        }
        catch (Exception ex)
        {
            settings.DefaultInstanceID = previousInstance;
            settings.HomeDefaultServerID = previousServer;
            RefreshHomeLaunchItems(force: true);
            await ShowSettingsException(ex);
        }
        finally
        {
            IsSavingHomeLaunchItem = false;
        }
    }

    // 游戏数据或服务器收藏变化后同步下拉列表，并重新对齐当前默认项。
    private void RefreshHomeLaunchItems(bool force = false)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RefreshHomeLaunchItems(force));
            return;
        }
        _isLoadingHomeSettings = true;
        try
        {
            var signature = QuicklyLaunchItems.Signature(_unitedCore, _dbManger);
            if (force || signature != _homeLaunchItemsSignature)
            {
                _homeLaunchItemsSignature = signature;
                HomeLaunchItems = new ObservableCollection<IQuicklyLaunchItem>(
                    QuicklyLaunchItems.Create(_unitedCore, _dbManger));
            }
            SelectedHomeLaunchItem = QuicklyLaunchItems.ResolveDefault(HomeLaunchItems, _dbManger.Data.OlanSettings);
        }
        finally
        {
            _isLoadingHomeSettings = false;
        }
    }

    private void LoadHomeSettings(AppSettings settings)
    {
        _isLoadingHomeSettings = true;
        try
        {
            SelectedHomePageStyle = settings.OlanUISettings.HomePageStyle == HomePageStyle.MagicButton ? "魔法按钮" : "默认";
        }
        finally
        {
            _isLoadingHomeSettings = false;
        }
        RefreshHomeBackgroundPath();
        RebuildHomeSectionItems();
    }

    private void RefreshHomeBackgroundPath() => CurrentHomeBackgroundPath = HomeBackgroundStore.FindCurrent();

    private void RebuildHomeSectionItems()
    {
#if DEBUG
        var settings = Design.IsDesignMode ? new AppSettings() : _dbManger.Data.OlanSettings;
#else
        var settings = _dbManger.Data.OlanSettings;
#endif
        HomeSectionItems = new ObservableCollection<HomeSectionVisibilityItem>(CreateHomeSectionItems(settings));
    }

    private static List<HomeSectionVisibilityItem> CreateHomeSectionItems(AppSettings settings) =>
    [
        new(HomeSection.News, "MC新闻", settings.OlanUISettings.IsHomeNewsVisible),
        new(HomeSection.TaskCenter, "任务中心", settings.OlanUISettings.IsHomeTaskCenterVisible),
        new(HomeSection.PowerPlay, "联机面板", settings.OlanUISettings.IsHomePowerPlayVisible),
        new(HomeSection.Import, "导入游戏实例", settings.OlanUISettings.IsHomeImportVisible)
    ];

    private static void ApplyHomeSectionItems(AppSettings settings, IEnumerable<HomeSectionVisibilityItem> items)
    {
        foreach (var item in items)
        {
            switch (item.Section)
            {
                case HomeSection.News: settings.OlanUISettings.IsHomeNewsVisible = item.IsVisible; break;
                case HomeSection.TaskCenter: settings.OlanUISettings.IsHomeTaskCenterVisible = item.IsVisible; break;
                case HomeSection.PowerPlay: settings.OlanUISettings.IsHomePowerPlayVisible = item.IsVisible; break;
                case HomeSection.Import: settings.OlanUISettings.IsHomeImportVisible = item.IsVisible; break;
            }
        }
    }

    [RelayCommand]
    private async Task SaveHomeSections()
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        var settings = _dbManger.Data.OlanSettings;
        var previousItems = CreateHomeSectionItems(settings);
        IsSavingHomeSections = true;
        try
        {
            ApplyHomeSectionItems(settings, HomeSectionItems);
            await _dbManger.Save();
            IsHomeSectionEditing = false;
            WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage(
                "主页板块设置已保存并应用", NotificationType.Success));
        }
        catch (Exception ex)
        {
            ApplyHomeSectionItems(settings, previousItems);
            await ShowSettingsException(ex);
        }
        finally
        {
            IsSavingHomeSections = false;
        }
    }

    [RelayCommand]
    private async Task ChooseHomeBackground()
    {
        if (IsSavingHomeAppearance)
            return;
        try
        {
            var topLevel = TopLevel.GetTopLevel(MainWindow.mainwindow);
            if (topLevel?.StorageProvider is not { CanOpen: true } storageProvider)
                return;
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择主页背景图片",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("图片") { Patterns = HomeBackgroundStore.ImagePatterns }]
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path)
                return;
            IsSavingHomeAppearance = true;
            // 复制到 customdata 后主页直接从目录读取，原图被移动或删除也不影响背景。
            await HomeBackgroundStore.ImportAsync(path);
            RefreshHomeBackgroundPath();
            WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage("主页背景已保存并应用", NotificationType.Success));
        }
        catch (Exception ex)
        {
            RefreshHomeBackgroundPath();
            await ShowSettingsException(ex);
        }
        finally
        {
            IsSavingHomeAppearance = false;
        }
    }

    [RelayCommand]
    private void ClearHomeBackground()
    {
        HomeBackgroundStore.Clear();
        RefreshHomeBackgroundPath();
        WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage(
            HasHomeBackground ? "主页背景文件正在被占用，请稍后重试" : "已移除主页背景",
            HasHomeBackground ? NotificationType.Warning : NotificationType.Success));
    }

    private async Task SaveHomePageStyleAsync(HomePageStyle style)
    {
        var settings = _dbManger.Data.OlanSettings;
        var previousStyle = settings.OlanUISettings.HomePageStyle;
        IsSavingHomeAppearance = true;
        try
        {
            settings.OlanUISettings.HomePageStyle = style;
            await _dbManger.Save();
            RefreshHomeBackgroundPath();
            WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage("主页样式已保存并应用", NotificationType.Success));
        }
        catch (Exception ex)
        {
            settings.OlanUISettings.HomePageStyle = previousStyle;
            LoadHomeSettings(settings);
            await ShowSettingsException(ex);
        }
        finally
        {
            IsSavingHomeAppearance = false;
        }
    }

    private static Task ShowSettingsException(Exception ex)
        => ex is OlanException olanException
            ? OlanExceptionWorker.ForOlanException(olanException)
            : OlanExceptionWorker.ForUnknowException(ex);
    #endregion

    #region 安装路径
    public string[] InstallPathModes { get; } = ["基本路径", "运行路径", "自定义"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomInstallPathEnabled))]
    private string selectedInstallPathMode = "基本路径";

    [ObservableProperty]
    private string customInstallPath = string.Empty;

    [ObservableProperty]
    private string currentInstallPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomInstallPathEnabled))]
    private bool isSavingInstallPath;

    public bool IsCustomInstallPathEnabled => SelectedInstallPathMode == "自定义" && !IsSavingInstallPath;
    private bool _loadingInstallPath;

    private void LoadInstallPathSetting(AppSettings settings)
    {
        _loadingInstallPath = true;
        try
        {
            SelectedInstallPathMode = settings.InstallPath.Replace('\\', '/') switch
            {
                "${OLAN_BASE_PATH}/installed" => "基本路径",
                "${OLAN_APP_RUNNING_PATH}/installed" => "运行路径",
                _ => "自定义"
            };
            CurrentInstallPath = Init.InstalledPath ?? Path.Combine(Init.BasePath, "installed");
            CustomInstallPath = SelectedInstallPathMode == "自定义" ? settings.InstallPath : CurrentInstallPath;
        }
        finally
        {
            _loadingInstallPath = false;
        }
    }

    partial void OnSelectedInstallPathModeChanged(string value)
    {
        if (_loadingInstallPath || IsSavingInstallPath)
            return;
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        if (value == "基本路径")
            _ = SaveInstallPathAsync("${OLAN_BASE_PATH}/installed");
        else if (value == "运行路径")
            _ = SaveInstallPathAsync("${OLAN_APP_RUNNING_PATH}/installed");
    }

    private async Task SaveInstallPathAsync(string template)
    {
        var settings = _dbManger.Data.OlanSettings;
        var previousPath = settings.InstallPath;
        if (IsSavingInstallPath)
            return;

        IsSavingInstallPath = true;
        try
        {
            var path = Path.GetFullPath(Tools.ReplacePlaceholders(template, new Dictionary<string, string>
            {
                ["OLAN_BASE_PATH"] = Init.BasePath,
                ["OLAN_APP_RUNNING_PATH"] = AppContext.BaseDirectory
            }).Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(path);
            if (!template.Contains("${OLAN_BASE_PATH}", StringComparison.Ordinal) &&
                !template.Contains("${OLAN_APP_RUNNING_PATH}", StringComparison.Ordinal))
                template = path;
            if (template == previousPath)
                return;
            settings.InstallPath = template;
            await _dbManger.Save();
        }
        catch (Exception ex)
        {
            settings.InstallPath = previousPath;
            LoadInstallPathSetting(settings);
            await ShowSettingsException(ex);
            return;
        }
        finally
        {
            IsSavingInstallPath = false;
        }

        await OlanExceptionWorker.ForOlanException(new OlanException("安装路径已更改",
            $"保存安装路径后的重启提醒；文件/目录 previousPath={previousPath}；配置文件={Path.Combine(Init.BasePath, "config.json")}。更改游戏安装路径不会复制原有的游戏文件与数据，请自行迁移；新路径将在重启启动器后生效。",
            OlanExceptionAction.FatalError));
    }

    [RelayCommand]
    private async Task PickInstallPath()
    {
        if (!IsCustomInstallPathEnabled)
            return;
        try
        {
            var topLevel = TopLevel.GetTopLevel(MainWindow.mainwindow);
            if (topLevel?.StorageProvider is not { CanPickFolder: true } storageProvider)
                throw new OlanException("无法选择安装路径", $"选择自定义安装目录时检查系统文件选择器；配置文件={Path.Combine(Init.BasePath, "config.json")}。当前环境无法打开文件夹选择器，请手动输入路径后点击应用。");

            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择游戏安装路径",
                AllowMultiple = false
            });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } path)
                return;

            CustomInstallPath = Path.GetFullPath(path);
            await SaveInstallPathAsync(CustomInstallPath);
        }
        catch (Exception ex)
        {
            await ShowSettingsException(ex);
        }
    }

    [RelayCommand]
    private async Task ApplyInstallPath()
    {
        if (!IsCustomInstallPathEnabled)
            return;
        try
        {
            if (string.IsNullOrWhiteSpace(CustomInstallPath))
                throw new OlanException("安装路径不能为空", $"应用自定义安装路径前校验输入；配置文件={Path.Combine(Init.BasePath, "config.json")}。请输入游戏安装路径，或点击选择路径选择文件夹。");

            await SaveInstallPathAsync(CustomInstallPath.Trim());
        }
        catch (Exception ex)
        {
            await ShowSettingsException(ex);
        }
    }
    #endregion

    #region 下载选项
    [ObservableProperty]
    public int _MaxDownloadThreadsValue;
    partial void OnMaxDownloadThreadsValueChanged(int value)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        // 使用注入的实例
        _dbManger.Data.OlanSettings.MaximumDownloadThreads = value;
        _dbManger.Save();
    }

    [ObservableProperty]
    public int _MaxSha1ThreadsValue;
    partial void OnMaxSha1ThreadsValueChanged(int value)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        // 使用注入的实例
        _dbManger.Data.OlanSettings.MaximumSha1Threads = value;
        _dbManger.Save();
    }

    [ObservableProperty]
    public bool _IsSha1Enabled;
    partial void OnIsSha1EnabledChanged(bool value)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        // 使用注入的实例
        _dbManger.Data.OlanSettings.IsSha1Enabled = value;
        _dbManger.Save();
    }

    [ObservableProperty]
    public DownloadSourceStrategy[] downloadSources =
        [
        DownloadSourceStrategy.OfficialOnly,
        DownloadSourceStrategy.RaceWithBmcl,
        DownloadSourceStrategy.RaceWithOlan,
        DownloadSourceStrategy.RaceWithSkyCrystal
        ];
    [ObservableProperty]
    public DownloadSourceStrategy _SelectedDownloadSource = DownloadSourceStrategy.OfficialOnly;
    partial void OnSelectedDownloadSourceChanged(DownloadSourceStrategy value)
    {
#if DEBUG
        if (Design.IsDesignMode)
            return;
#endif
        _dbManger.Data.OlanSettings.DownloadMinecraftSourceStrategy = value;
        _dbManger.Save();
    }

    #endregion
    [RelayCommand]
    private async Task UpdateLauncher()
    {
        WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage("正在准备启动器更新"));
        try
        {
            await Task.Run(AutoUpdate.Update);
            WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage("更新操作已结束"));
        }
        catch (Exception ex)
        {
            await ShowSettingsException(ex);
        }
    }

    [RelayCommand]
    public void OpenGithub()
    {
        Tools.OpenWebsite(Init.ProjectWebsite);
    }
    [ObservableProperty]
    UserControl? _paneContent;
    [ObservableProperty] bool _isPaneShow;

    [RelayCommand]
    private void OpenJavaInstallPane()
    {
        PaneContent = new JavaInstallPane
        {
            DataContext = new JavaInstallPaneViewModel(_javaManager, _unitedCore, () => IsPaneShow = false)
        };
        IsPaneShow = true;
    }

    // 构造函数接收正确的 DBManger 类型
    public SettingsPageViewModel(DBManager configManager,UnitedCore unitedCore)
    {
        this._dbManger = configManager;
        this._unitedCore = unitedCore;
#if DEBUG
        if (Design.IsDesignMode)
        {
            LeftButtonListSpacing = 8;
            _ = RefList();
            LoadHomeSettings(new AppSettings());
            LoadInstallPathSetting(new AppSettings());
            MaxDownloadThreadsValue = 24;
            MaxSha1ThreadsValue = 24;
            IsSha1Enabled = true;
            return; // 在设计模式下提前返回
        }
#endif
        // else 块不再需要，因为非 DEBUG 模式下总会执行下面的代码

        try
        {
            // 使用注入的实例来初始化属性
            showIdentifiers = _dbManger.Data.OlanSettings.OlanUISettings.ShowIdentifiers;
            autoLaunchGameOnPowerPlay = _dbManger.Data.OlanSettings.AutoLaunchGameOnPowerPlay;
            LeftButtonListSpacing = _dbManger.Data.OlanSettings.OlanUISettings.LeftButtonListSpacing;
            _ = RefList();
            LoadHomeSettings(_dbManger.Data.OlanSettings);
            RefreshHomeLaunchItems();
            _dbManger.OnDataChanged += () => RefreshHomeLaunchItems();
            _gameDataManager.OnDataChanged += () => RefreshHomeLaunchItems();
            LoadJvmSelection(_dbManger.Data.DefaultSettings.MinecraftJvmArguments.mode);
            MaxDownloadThreadsValue = _dbManger.Data.OlanSettings.MaximumDownloadThreads;
            MaxSha1ThreadsValue = _dbManger.Data.OlanSettings.MaximumSha1Threads;
            IsSha1Enabled = _dbManger.Data.OlanSettings.IsSha1Enabled;
            SelectedDownloadSource = _dbManger.Data.OlanSettings.DownloadMinecraftSourceStrategy;
            LoadInstallPathSetting(_dbManger.Data.OlanSettings);
        }
        catch (NullReferenceException ex)
        {
            // 异常处理中的静态调用暂时保留，因为这是更深层次的重构问题
            throw new OlanException(
                "内部异常",
                $"初始化设置页面并读取配置字段；配置文件={Path.Combine(Init.BasePath, "config.json")}。配置文件特定部分设置部分为空，这可能是新版和旧版配置文件不兼容导致的",
                OlanExceptionAction.FatalError,
                ex,
               () =>
               {
                   File.Delete(Path.Combine(Init.BasePath, "config.json"));
                   Init.Initialize(); // 注意：这个静态调用最终也应被移除
               }
            );
        }
    }
}
