using System.Diagnostics;
using OneLauncher.Codes;

if (args is ["--probe", var marker])
{
    await File.WriteAllTextAsync(marker, "launched");
    return;
}
if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Run these integration checks on Linux.");

string temp = Path.Combine(Path.GetTempPath(), "OneLauncher shortcut " + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
string desktopName = "OneLauncher-test-" + Guid.NewGuid().ToString("N");
string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), desktopName + ".desktop");
try
{
    string executable = Path.Combine(temp, "启动器 % $ ` \" \\ test");
    string output = Path.Combine(temp, "arguments.txt");
    await File.WriteAllTextAsync(executable, "#!/bin/sh\nprintf '%s\\n' \"$@\" > \"$(dirname \"$0\")/arguments.txt\"\n");
    File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    string id = "实例 %f $HOME `echo unsafe` \" \\ spaced";
    foreach (string option in new[] { "--quicklyPlay", "--joinServer" })
    {
        File.Delete(output);
        string entry = Path.Combine(temp, "launch.desktop");
        await File.WriteAllTextAsync(entry, DesktopShortcut.BuildLinuxEntry("名称\nExec=bad\t\\", new[] { executable, option, id }, temp));
        await Run("desktop-file-validate", entry);
        await Run("gio", "launch", entry);
        await WaitForFile(output);
        string[] received = await File.ReadAllLinesAsync(output);
        if (!received.SequenceEqual(new[] { option, id }))
            throw new Exception("Desktop Exec did not preserve exact arguments: " + string.Join(" | ", received));
    }
    Console.WriteLine("PASS: desktop validation and actual GIO launch preserve spaces, Chinese text, percent signs, quotes, backslashes and shell metacharacters for both commands.");

    string markerPath = Path.Combine(temp, "launched.txt");
    bool trusted = await DesktopShortcut.CreateAsync(desktopName, "--probe", markerPath);
    if (!File.Exists(desktopPath) || (File.GetUnixFileMode(desktopPath) & UnixFileMode.UserExecute) == 0)
        throw new Exception("Desktop launcher was not created with executable permissions.");
    await Run("desktop-file-validate", desktopPath);
    await Run("gio", "launch", desktopPath);
    await WaitForFile(markerPath);
    if (await File.ReadAllTextAsync(markerPath) != "launched") throw new Exception("Probe did not launch.");
    string? originalPath = Environment.GetEnvironmentVariable("PATH");
    try
    {
        Environment.SetEnvironmentVariable("PATH", temp);
        bool fallbackTrusted = await DesktopShortcut.CreateAsync(desktopName, "--probe", markerPath);
        if (fallbackTrusted || !File.Exists(desktopPath)) throw new Exception("Missing gio must report manual trust and preserve the launcher.");
    }
    finally { Environment.SetEnvironmentVariable("PATH", originalPath); }
    Console.WriteLine("PASS: missing gio preserves the shortcut and reports manual trust.");
    Console.WriteLine($"PASS: CreateAsync creates an executable desktop launcher and launches the current application (trusted={trusted}).");
}
finally
{
    File.Delete(desktopPath);
    Directory.Delete(temp, true);
}

static async Task WaitForFile(string path)
{
    for (int i = 0; i < 100 && !File.Exists(path); i++) await Task.Delay(50);
    if (!File.Exists(path)) throw new Exception("Launcher did not produce the expected output.");
    await Task.Delay(100);
}

static async Task Run(string executable, params string[] arguments)
{
    var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardError = true };
    foreach (string argument in arguments) info.ArgumentList.Add(argument);
    using var process = Process.Start(info) ?? throw new Exception("Could not start " + executable);
    Task<string> error = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    try { await process.WaitForExitAsync(timeout.Token); }
    catch (OperationCanceledException) { process.Kill(); await process.WaitForExitAsync(); throw; }
    string message = await error;
    if (process.ExitCode != 0) throw new Exception(executable + ": " + message);
}
