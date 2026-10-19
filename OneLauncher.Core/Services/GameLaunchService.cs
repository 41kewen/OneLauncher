using OneLauncher.Core.Global;
using OneLauncher.Core.Launcher;

namespace OneLauncher.Core.Services;

/// <summary>
/// 创建彼此独立的游戏启动会话。
/// </summary>
public sealed class GameLaunchService
{
    private readonly TaskManager _taskManager;

    internal GameLaunchService(TaskManager taskManager)
    {
        _taskManager = taskManager;
    }

    public LaunchService CreateLaunchService(string taskName = "启动游戏")
    {
        TaskInstance task = _taskManager.CreateTask(taskName);
        task.SetProgress("等待启动...", 0);
        return new LaunchService(task);
    }
}
