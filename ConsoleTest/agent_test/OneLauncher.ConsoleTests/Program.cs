string output = Path.GetFullPath(args.FirstOrDefault() ?? "build/console-ui-parity-20261116");
Directory.CreateDirectory(output);
await ConsoleInstallChecks.RunAsync(output);
