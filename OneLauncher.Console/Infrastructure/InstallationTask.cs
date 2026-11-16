using OneLauncher.Core.DefaultComponent.Downloader.DownloadMinecraftProviders;
using OneLauncher.Core.Global;

namespace OneLauncher.Console.Infrastructure;

internal sealed class InstallationTask : IDisposable, IProgress<(DownProgress Title, int AllFiles, int DownedFiles, string DowingFileName)>
{
    private readonly TaskInstance _task;
    private readonly ConsoleCancelEventHandler _cancelHandler;
    private DateTime _lastReport;
    private DownProgress? _lastStage;
    public CancellationToken Token => _task.Token!.Token;
    public InstallationTask(UnitedCore core, string name)
    {
        _task = core.TaskManager.CreateTask(name);
        _cancelHandler = (_, e) => { e.Cancel = true; _task.Cancel(); };
        System.Console.CancelKeyPress += _cancelHandler;
    }
    public void Report((DownProgress Title, int AllFiles, int DownedFiles, string DowingFileName) value)
    {
        lock (this)
        {
            if (_task.Status != TaskInfoStatus.Running) return;
            _task.SetProgress(value.DowingFileName, value.AllFiles > 0 ? 100d * value.DownedFiles / value.AllFiles : 0);
            if (_lastStage != value.Title || value.Title is DownProgress.Meta or DownProgress.Done
                || DateTime.UtcNow - _lastReport >= TimeSpan.FromSeconds(2))
            {
                System.Console.WriteLine($"[{value.Title}] {value.DownedFiles}/{value.AllFiles} {value.DowingFileName}");
                _lastReport = DateTime.UtcNow;
                _lastStage = value.Title;
            }
        }
    }
    public async Task RunAsync(Func<Task> operation)
    {
        try { await operation(); _task.SetDone(); }
        catch (OperationCanceledException) { _task.Cancel(); throw; }
        catch (Exception) when (Token.IsCancellationRequested)
        {
            _task.Cancel();
            throw new OperationCanceledException(Token);
        }
        catch (Exception ex) { _task.SetFailed(ex); throw; }
    }
    public void Dispose()
    {
        System.Console.CancelKeyPress -= _cancelHandler;
        _task.Token!.Dispose();
    }
}
