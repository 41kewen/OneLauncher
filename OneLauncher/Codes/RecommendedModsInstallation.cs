using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using OneLauncher.Core.Global;
using OneLauncher.Core.Helper.Models;
using OneLauncher.Core.Services;
using OneLauncher.Views;
using System;
using System.Threading.Tasks;

namespace OneLauncher.Codes;

// 多个实例入口共用任务、进度及错误提示，实际模组筛选和下载仍由 Core 负责。
internal static class RecommendedModsInstallation
{
    internal static bool Supports(ModEnum loader) =>
        loader is ModEnum.fabric or ModEnum.quilt or ModEnum.forge or ModEnum.neoforge;

    internal static async Task InstallAsync(
        GameData gameData, ResourceInstallerService installer, TaskManager taskManager)
    {
        if (!Supports(gameData.ModLoader))
            return;

        var task = taskManager.CreateTask($"安装推荐模组 {gameData.Name}");
        var token = task.Token!.Token;
        task.SetProgress("正在获取推荐模组信息...", 0);
        Notify($"已开始为“{gameData.Name}”安装推荐模组，可在任务列表查看进度");

        var progress = new Progress<(int current, string file)>(p =>
            Dispatcher.UIThread.Post(() =>
            {
                if (task.Status != TaskInfoStatus.Running) return;
                // 接口只报告文件序号，没有总数；成功返回后才标记 100%。
                task.SetProgress($"正在下载第 {p.current} 个文件：{p.file}", 0);
            }));

        try
        {
            await Task.Run(() => installer.InstallRecommendedMods(progress, gameData, token), token);
            token.ThrowIfCancellationRequested();
            task.SetProgress("推荐模组安装完成", 100);
            task.SetDone();
            Notify($"“{gameData.Name}”的推荐模组安装完成", NotificationType.Success);
        }
        catch (OperationCanceledException)
        {
            SetCanceled(task, gameData);
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            SetCanceled(task, gameData);
        }
        catch (OlanException ex)
        {
            task.SetFailed(ex);
            Notify($"“{gameData.Name}”的推荐模组安装失败，实例已保留", NotificationType.Error);
            await OlanExceptionWorker.ForOlanException(ex);
        }
        catch (Exception ex)
        {
            task.SetFailed(ex);
            Notify($"“{gameData.Name}”的推荐模组安装失败，实例已保留", NotificationType.Error);
            await OlanExceptionWorker.ForUnknowException(ex);
        }
    }

    private static void SetCanceled(TaskInstance task, GameData gameData)
    {
        task.Cancel();
        task.SetProgress("推荐模组安装已取消，实例已保留", task.Progress);
        Notify($"“{gameData.Name}”的推荐模组安装已取消，实例已保留", NotificationType.Warning);
    }

    private static void Notify(string message, NotificationType type = NotificationType.Information)
        => WeakReferenceMessenger.Default.Send(new MainWindowShowFlyoutMessage(message, type, openTaskCenterAfterClick: true));
}
