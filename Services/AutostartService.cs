using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace MiyaIsland.Services;

// 测试可替换注册表存储，避免修改真实启动项。
public interface IAutostartRegistry
{
    string? Read();
    void Write(string command);
    void Delete();
}

public sealed class AutostartService
{
    public const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "MiyaIsland";
    private readonly IAutostartRegistry _registry;

    public AutostartService(IAutostartRegistry? registry = null)
    {
        if (registry is not null) _registry = registry;
        else if (OperatingSystem.IsWindows()) _registry = new CurrentUserRegistry();
        else throw new PlatformNotSupportedException("开机自启动仅支持 Windows。");
    }

    public bool IsEnabled() => GetRegisteredExePath() is not null;

    public string? GetRegisteredExePath() => Parse(Read());

    public void Enable(string exePath)
    {
        ValidatePath(exePath);
        Execute(() => _registry.Write($"\"{exePath}\" --autostart"), "启用开机自启动失败");
    }

    public void Disable() => Execute(_registry.Delete, "关闭开机自启动失败");

    public bool IsPathStale(string currentExePath)
    {
        ValidatePath(currentExePath);
        var command = Read();
        if (command is null) return false;
        return !string.Equals(Parse(command), currentExePath, StringComparison.OrdinalIgnoreCase);
    }

    private string? Read()
    {
        string? command = null;
        Execute(() => command = _registry.Read(), "读取开机自启动设置失败");
        return command;
    }

    private static string? Parse(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = command.Trim();
        if (!command.StartsWith('"')) return null;
        var end = command.IndexOf('"', 1);
        if (end <= 1 || !string.Equals(command[(end + 1)..].Trim(), "--autostart", StringComparison.Ordinal))
            return null;
        var path = command[1..end];
        return IsAbsoluteWindowsPath(path) && path.IndexOfAny(['\r', '\n', '\0']) < 0 ? path : null;
    }

    private static bool IsAbsoluteWindowsPath(string path) =>
        (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/') ||
        (path.StartsWith(@"\\", StringComparison.Ordinal) &&
         path[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries).Length >= 3);

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsAbsoluteWindowsPath(path) ||
            path.IndexOfAny(['"', '\r', '\n', '\0']) >= 0)
            throw new ArgumentException("请提供不含引号或换行的可执行文件完整路径。", nameof(path));
    }

    private static void Execute(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException
            or System.ComponentModel.Win32Exception or ObjectDisposedException)
        {
            throw new InvalidOperationException($"{message}，请检查注册表访问权限。", ex);
        }
    }

    [SupportedOSPlatform("windows")]
    private sealed class CurrentUserRegistry : IAutostartRegistry
    {
        public string? Read()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
            return key?.GetValue(ValueName) as string;
        }

        public void Write(string command)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, true)
                ?? throw new IOException("无法打开自启动注册表项。");
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }

        public void Delete()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            key?.DeleteValue(ValueName, false);
        }
    }
}
