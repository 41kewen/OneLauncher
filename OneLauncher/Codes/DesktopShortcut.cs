using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OneLauncher.Codes;

internal static class DesktopShortcut
{
    // false means the launcher exists, but the desktop requires manual trust.
    public static async Task<bool> CreateAsync(string name, string option, string id)
    {
        string processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法获取启动器程序路径。");
        var arguments = new List<string> { processPath };
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string? entryPath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(entryPath))
                throw new InvalidOperationException("无法获取启动器程序集路径。");
            arguments.Add(entryPath);
        }
        arguments.Add(option);
        arguments.Add(id);

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop))
            throw new DirectoryNotFoundException("无法获取桌面目录。");
        Directory.CreateDirectory(desktop);
        string safeName = new(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || char.IsControl(c) ? '_' : c).ToArray());
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "OneLauncher";
        string extension = OperatingSystem.IsLinux() ? ".desktop" : OperatingSystem.IsWindows() ? ".bat" : ".sh";
        string path = Path.Combine(desktop, safeName + extension);
        string content;
        if (OperatingSystem.IsLinux())
            content = BuildLinuxEntry(name, arguments, AppContext.BaseDirectory);
        else if (OperatingSystem.IsWindows())
            content = "@echo off\r\n" + string.Join(" ", arguments.Select(a => "\"" + a.Replace("%", "%%") + "\"")) + "\r\n";
        else
            content = "#!/bin/sh\nexec " + string.Join(" ", arguments.Select(a => "'" + a.Replace("'", "'\"'\"'") + "'")) + "\n";

        await File.WriteAllTextAsync(path, content);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return !OperatingSystem.IsLinux() || await TryTrustAsync(path);
    }

    // Use env so GLib does not resolve an executable containing %% before field-code expansion.
    internal static string BuildLinuxEntry(string name, IEnumerable<string> arguments, string workingDirectory)
        => "[Desktop Entry]\nType=Application\nVersion=1.0\n"
            + $"Name={EscapeValue(name)}\n"
            + $"Exec=/usr/bin/env -- {string.Join(" ", arguments.Select(QuoteExecArgument))}\n"
            + $"Path={EscapeValue(workingDirectory)}\n"
            + "Icon=applications-games\nTerminal=false\nCategories=Game;\n";

    private static string EscapeValue(string value)
        => value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace(" ", "\\s");

    private static string QuoteExecArgument(string value)
    {
        // Exec is parsed by the desktop, not a shell. Escape quoting first, then
        // the desktop-entry string layer; literal percent signs are field codes.
        string quoted = value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("`", "\\`").Replace("$", "\\$").Replace("%", "%%");
        return "\"" + EscapeValue(quoted) + "\"";
    }

    private static async Task<bool> TryTrustAsync(string path)
    {
        var startInfo = new ProcessStartInfo("gio") { UseShellExecute = false, RedirectStandardError = true };
        foreach (string argument in new[] { "set", path, "metadata::trusted", "true" })
            startInfo.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(startInfo);
            if (process == null) return false;
            Task<string> error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                await process.WaitForExitAsync();
                await error;
                return false;
            }
            string errorMessage = await error;
            if (process.ExitCode != 0)
                Debug.WriteLine($"Desktop shortcut requires manual trust: {errorMessage.Trim()}");
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // gio/GVfs is optional; preserve the usable launcher and tell the user.
            return false;
        }
    }
}
