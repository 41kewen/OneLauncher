namespace OneLauncher.Console.Infrastructure;

internal sealed class DownloadRetryPrompt(CancellationToken token)
{
    private readonly object _gate = new();
    private bool _gaveUp;

    public bool Ask(string detail)
    {
        lock (_gate)
        {
            if (_gaveUp || token.IsCancellationRequested) return false;
            System.Console.Error.WriteLine($"单个文件下载失败：{Environment.NewLine}{detail}");
            if (System.Console.IsInputRedirected)
            {
                System.Console.Error.WriteLine("标准输入已重定向，结束本次安装。");
                _gaveUp = true;
                return false;
            }
            System.Console.Error.Write("只重试该文件？[y/N] ");
            string? answer = System.Console.In.ReadLineAsync(token).AsTask().GetAwaiter().GetResult();
            bool retry = answer?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
            _gaveUp = !retry;
            return retry;
        }
    }
}
