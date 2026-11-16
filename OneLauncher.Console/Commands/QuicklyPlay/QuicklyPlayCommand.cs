using OneLauncher.Console.Infrastructure;
namespace OneLauncher.Console.Commands.QuicklyPlay;

internal static class QuicklyPlayCommand
{
    public static async Task RunAsync(CommandArguments arguments)
    {
        arguments.Allow("instance", "dataPath");
        string instance = arguments.Require("instance");
        using var context = await CommandContext.CreateAsync(arguments);
        using var launch = context.Core.GameLaunchService.CreateLaunchService();
        await launch.Play(instance);
    }
}
