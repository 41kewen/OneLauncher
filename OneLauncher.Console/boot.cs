using OneLauncher.Core.Global;
using OneLauncher.Core.Helper;
using OneLauncher.Core.Helper.Models;
using Microsoft.Extensions.DependencyInjection;
using OneLauncher.Core.Net.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OneLauncher.Console;
public class Boot
{
    public static async Task RunBoot(string[] args)
    {
        try
        {
            var services = await Init.Initialize();
            using var serviceProvider = services.BuildServiceProvider();
            var unitedCore = serviceProvider.GetRequiredService<UnitedCore>();
            switch (args[0])
            {
                case "--quicklyPlay":
                    using (var launchService = unitedCore.GameLaunchService.CreateLaunchService())
                        await launchService.Play(args[1]);
                    break;
                case "--joinServer":
                    if (args.Length < 2) break;

                    string serverIdentifier = args[1];
                    ServerEntry? server = Guid.TryParse(serverIdentifier, out Guid serverId)
                        ? Init.ConfigManger.Data.ServerList.FirstOrDefault(x => x.Id == serverId)
                        : Init.ConfigManger.Data.ServerList.FirstOrDefault(x =>
                            string.Equals(x.Name, serverIdentifier, StringComparison.OrdinalIgnoreCase));
                    if (server == null) break;

                    var game = Init.GameDataManger.GetInstanceFromId(server.InstanceId);
                    if (game == null) break;
                    using (var launchService = unitedCore.GameLaunchService.CreateLaunchService())
                        await launchService.Play(game, server.ServerInfo);
                    break;
                case "--releaseMemory":
                    await ReleaseMemory.OptimizeAsync();
                    break;
            }
        }
        catch (Exception e) { 
            Environment.FailFast(e.ToString());
        }
    }
}
