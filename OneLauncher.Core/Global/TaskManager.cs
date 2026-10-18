using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
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
    public double Progress { get; private set; }
    public TaskInfoStatus Status { get; private set; }
    public CancellationTokenSource? Token { get; private set; }
    public Exception? Exception { get; set; }
    public void SetProgress(string current, double progress)
    {
        Current = current;
        Progress = progress;
    }
    public void SetFailed(Exception exception)
    {
        Status = TaskInfoStatus.Failed;
        Exception = exception;
        Debug.WriteLine(exception.ToString());
    }
    public void Cancel
        ()
    {
        if(Token != null)
        {
            Token.Cancel();
            Status = TaskInfoStatus.Canceled;
        }
    }
    public void SetDone()
    {
        Status = TaskInfoStatus.Finished;
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
