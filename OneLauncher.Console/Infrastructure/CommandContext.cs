using Microsoft.Extensions.DependencyInjection;
using OneLauncher.Core.Global;

namespace OneLauncher.Console.Infrastructure;

internal sealed class CommandContext : IDisposable
{
    private readonly ServiceProvider _provider;
    public UnitedCore Core { get; }
    private CommandContext(ServiceProvider provider)
    {
        _provider = provider;
        Core = provider.GetRequiredService<UnitedCore>();
    }
    public static async Task<CommandContext> CreateAsync(CommandArguments arguments)
    {
        var services = await Init.Initialize(isCommandMode: true, basePath: arguments.Get("dataPath"));
        return new CommandContext(services.BuildServiceProvider());
    }
    public void Dispose() => _provider.Dispose();
}
