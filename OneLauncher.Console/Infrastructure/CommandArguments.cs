namespace OneLauncher.Console.Infrastructure;

internal sealed class CommandArguments
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    public static CommandArguments Parse(string[] args)
    {
        var result = new CommandArguments();
        for (int i = 1; i < args.Length; i++)
        {
            int separator = args[i].IndexOf(':');
            // 保留既有快速启动、加入服务器的单个位置参数。
            if (separator < 0 && args.Length == 2 && args[0] is "--quicklyPlay" or "--joinServer")
            {
                result._values.Add(args[0] == "--quicklyPlay" ? "instance" : "server", args[i]);
                continue;
            }
            if (separator <= 0 || separator == args[i].Length - 1)
                throw new ArgumentException($"参数必须使用 key:value 格式：{args[i]}");
            string key = args[i][..separator];
            string value = args[i][(separator + 1)..];
            if (string.IsNullOrWhiteSpace(value) || !result._values.TryAdd(key, value))
                throw new ArgumentException($"参数为空或重复：{key}");
        }
        return result;
    }
    public string? Get(string key) => _values.GetValueOrDefault(key);
    public string Require(string key) => Get(key) ?? throw new ArgumentException($"缺少参数：{key}:<值>");
    public bool GetBoolean(string key, bool defaultValue = false)
    {
        string? value = Get(key);
        if (value == null) return defaultValue;
        return bool.TryParse(value, out bool flag) ? flag : throw new ArgumentException($"{key} 必须为 true 或 false。");
    }
    public void Allow(params string[] keys)
    {
        var unknown = _values.Keys.FirstOrDefault(key => !keys.Contains(key, StringComparer.OrdinalIgnoreCase));
        if (unknown != null) throw new ArgumentException($"未知参数：{unknown}");
    }
}
