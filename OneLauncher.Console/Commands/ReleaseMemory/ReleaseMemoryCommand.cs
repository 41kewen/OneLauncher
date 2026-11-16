using OneLauncher.Console.Infrastructure;
namespace OneLauncher.Console.Commands.ReleaseMemory;

internal static class ReleaseMemoryCommand
{
    public static async Task RunAsync(CommandArguments arguments)
    {
        arguments.Allow();
        await OneLauncher.Core.Helper.ReleaseMemory.OptimizeAsync();
    }
}
