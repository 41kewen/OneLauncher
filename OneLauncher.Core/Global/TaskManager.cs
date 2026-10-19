using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace OneLauncher.Core.Global;
public enum TaskInfoStatus
{
    Running,
    Finished,
    Failed,
    Canceled,
}
public class TaskInstance
{
    public string Name { get; init; }
    public string? Current { get; private set; }
    public TaskInfoStatus Status { get; private set; }
    public CancellationTokenSource? Token { get; private set; }
    public Exception? Exception { get; set; }
    public double Progress { get; private set; } = 0;
    public event Action<double>? ProgressChanged;
    public event Action? Finished;
    public event Action? Failed;
    public event Action? Canceled;
    public void SetProgress(string current, double progress)
    {
        Current = current;
        Progress = progress;
        ProgressChanged?.Invoke(progress);
    }
    public void SetFailed(Exception exception)
    {
        Status = TaskInfoStatus.Failed;
        Exception = exception;
        Failed?.Invoke();
        Debug.WriteLine(exception.ToString());
    }
    public void Cancel()
    {
        if (Token != null && Status == TaskInfoStatus.Running)
        {
            Status = TaskInfoStatus.Canceled;
            try
            {
                Token.Cancel();
            }
            catch(System.ObjectDisposedException)
            {

            }
            finally
            {
                Canceled?.Invoke();
            }
        }
    }
    public void SetDone()
    {
        Status = TaskInfoStatus.Finished;
        Finished?.Invoke();
    }
    public TaskInstance(string name,CancellationTokenSource? cts = null)
    {
        Name = name;
        Token = cts;
        Status = TaskInfoStatus.Running;
    }
}
public class TaskManager
{
    public ConcurrentQueue<TaskInstance> TaskQueue { get; private set; } = new ConcurrentQueue<TaskInstance>();
    public TaskInstance CreateTask(string name)
    {
        var cts = new CancellationTokenSource();
        var task = new TaskInstance(name, cts);
        TaskQueue.Enqueue(task);
        return task;
    }
}
