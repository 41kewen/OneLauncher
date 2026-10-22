using Microsoft.Extensions.DependencyInjection;
using OneLauncher.Core.Global;

try
{
    if (args.Contains("--protocol")) return await ForgeProtocolChecks.RunAsync();
    var services = await Init.Initialize();
    using var serviceProvider = services.BuildServiceProvider();
    var core = serviceProvider.GetRequiredService<UnitedCore>();
    return await ForgeIntegrationTests.RunAsync(core, args);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception is OlanException olan ? $"FAIL {olan.Title}: {olan.Message}" : "FAIL " + exception);
    return 1;
}
