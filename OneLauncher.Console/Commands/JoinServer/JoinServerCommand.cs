using OneLauncher.Console.Infrastructure;
using OneLauncher.Core.Global;
namespace OneLauncher.Console.Commands.JoinServer;

internal static class JoinServerCommand
{
    public static async Task RunAsync(CommandArguments arguments)
    {
        arguments.Allow("server", "dataPath");
        string identifier = arguments.Require("server");
        using var context = await CommandContext.CreateAsync(arguments);
        var servers = Init.ConfigManger.Data.ServerList;
        var server = Guid.TryParse(identifier, out Guid id)
            ? servers.FirstOrDefault(entry => entry.Id == id)
            : servers.FirstOrDefault(entry => string.Equals(entry.Name, identifier, StringComparison.OrdinalIgnoreCase));
        if (server == null) throw new ArgumentException($"找不到服务器：{identifier}");
        var game = context.Core.GameDataManager.GetInstanceFromId(server.InstanceId)
            ?? throw new ArgumentException($"找不到服务器关联的实例：{server.InstanceId}");
        using var launch = context.Core.GameLaunchService.CreateLaunchService();
        await launch.Play(game, server.ServerInfo);
    }
}
