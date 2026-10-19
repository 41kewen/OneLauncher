using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneLauncher.Core.Global;
using OneLauncher.Views.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneLauncher.Views.Panes.PaneViewModels;

internal sealed partial class TaskCenterPaneViewModel : BaseViewModel
{
    private readonly TaskManager _taskManager;
    private readonly Action _onCloseCallback;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Dictionary<TaskInstance, TaskInstanceItemViewModel> _taskItems = new();

    public ObservableCollection<TaskInstanceItemViewModel> Tasks { get; } = new();

    [ObservableProperty]
    private bool hasTasks;

    public TaskCenterPaneViewModel(UnitedCore unitedCore, Action onCloseCallback)
    {
        _taskManager = unitedCore.TaskManager;
        _onCloseCallback = onCloseCallback;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _refreshTimer.Tick += (_, _) => RefreshTasks();

        RefreshTasks();
        _refreshTimer.Start();
    }

    private void RefreshTasks()
    {
        var snapshot = _taskManager.TaskQueue.ToArray();
        var currentTasks = new HashSet<TaskInstance>();

        foreach (var task in snapshot)
        {
            currentTasks.Add(task);
            if (!_taskItems.TryGetValue(task, out var item))
            {
                item = new TaskInstanceItemViewModel(task);
                _taskItems.Add(task, item);
                // 最新创建的任务始终显示在顶部，避免用户向下滚动查找正在运行的任务。
                Tasks.Insert(0, item);
            }

            item.RefreshStatus();
        }

        for (var index = Tasks.Count - 1; index >= 0; index--)
        {
            var item = Tasks[index];
            if (currentTasks.Contains(item.Task))
                continue;

            Tasks.RemoveAt(index);
            _taskItems.Remove(item.Task);
            item.Dispose();
        }

        HasTasks = Tasks.Count > 0;
    }

    [RelayCommand]
    private void ClosePane() => _onCloseCallback();
}

internal sealed partial class TaskInstanceItemViewModel : ObservableObject
{
    private static readonly IBrush RunningBrush = new SolidColorBrush(Color.Parse("#FF007AFF"));
    private static readonly IBrush CanceledBrush = new SolidColorBrush(Color.Parse("#FF8A5A00"));
    private static readonly IBrush FailedBrush = new SolidColorBrush(Color.Parse("#FFC42B1C"));
    private static readonly IBrush FinishedBrush = new SolidColorBrush(Color.Parse("#FF107C10"));
    private static readonly IBrush RunningSurfaceBrush = new SolidColorBrush(Color.Parse("#14007AFF"));
    private static readonly IBrush CanceledSurfaceBrush = new SolidColorBrush(Color.Parse("#1FD18B00"));
    private static readonly IBrush FailedSurfaceBrush = new SolidColorBrush(Color.Parse("#18C42B1C"));
    private static readonly IBrush FinishedSurfaceBrush = new SolidColorBrush(Color.Parse("#19107C10"));

    private readonly TaskInstance _task;
    private string _statusText = string.Empty;
    private string _currentText = string.Empty;
    private double _progress;
    private IBrush _progressBrush = RunningBrush;

    public TaskInstance Task => _task;
    public string Name => _task.Name;
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }
    public string CurrentText
    {
        get => _currentText;
        private set => SetProperty(ref _currentText, value);
    }
    public bool IsRunning => _task.Status == TaskInfoStatus.Running;
    public bool IsCanceled => _task.Status == TaskInfoStatus.Canceled;
    public bool IsFailed => _task.Status == TaskInfoStatus.Failed;
    public bool IsFinished => _task.Status == TaskInfoStatus.Finished;
    public bool CanCancel => _task.Status == TaskInfoStatus.Running;
    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }
    public IBrush ProgressBrush
    {
        get => _progressBrush;
        private set => SetProperty(ref _progressBrush, value);
    }
    public IBrush ProgressSurfaceBrush => _task.Status switch
    {
        TaskInfoStatus.Canceled => CanceledSurfaceBrush,
        TaskInfoStatus.Failed => FailedSurfaceBrush,
        TaskInfoStatus.Finished => FinishedSurfaceBrush,
        _ => RunningSurfaceBrush
    };

    public TaskInstanceItemViewModel(TaskInstance task)
    {
        _task = task;
        _task.ProgressChanged += OnProgressChanged;
        _task.Finished += OnFinished;
        _task.Failed += OnFailed;
        _task.Canceled += OnCanceled;
        RefreshStatus();
    }

    private void OnProgressChanged(double _)
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshStatus();
        else
            Dispatcher.UIThread.Post(RefreshStatus);
    }

    private void OnFinished() => RefreshStatusFromEvent();
    private void OnFailed() => RefreshStatusFromEvent();
    private void OnCanceled() => RefreshStatusFromEvent();

    private void RefreshStatusFromEvent()
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshStatus();
        else
            Dispatcher.UIThread.Post(RefreshStatus);
    }

    public void RefreshStatus()
    {
        var status = _task.Status switch
        {
            TaskInfoStatus.Running => "运行中",
            TaskInfoStatus.Canceled => "已取消",
            TaskInfoStatus.Failed => "异常",
            TaskInfoStatus.Finished => "已完成",
            _ => "未知状态"
        };

        StatusText = status;
        CurrentText = _task.Exception?.Message
            ?? (string.IsNullOrWhiteSpace(_task.Current) ? "等待任务进度..." : _task.Current);
        Progress = Math.Clamp(_task.Progress, 0, 100);
        ProgressBrush = _task.Status switch
        {
            TaskInfoStatus.Canceled => CanceledBrush,
            TaskInfoStatus.Failed => FailedBrush,
            TaskInfoStatus.Finished => FinishedBrush,
            _ => RunningBrush
        };
        OnPropertyChanged(nameof(ProgressSurfaceBrush));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsCanceled));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(CanCancel));
    }

    [RelayCommand]
    private void CancelTask()
    {
        if (CanCancel)
            _task.Cancel();
    }

    public void Dispose()
    {
        _task.ProgressChanged -= OnProgressChanged;
        _task.Finished -= OnFinished;
        _task.Failed -= OnFailed;
        _task.Canceled -= OnCanceled;
    }
}
