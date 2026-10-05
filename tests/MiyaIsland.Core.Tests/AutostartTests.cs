using MiyaIsland.Services;

namespace MiyaIsland.Core.Tests;

public sealed class AutostartTests
{
    private sealed class FakeRegistry : IAutostartRegistry
    {
        public string? Value { get; set; }
        public Exception? Failure { get; set; }
        public string? Read() { if (Failure is not null) throw Failure; return Value; }
        public void Write(string command) { if (Failure is not null) throw Failure; Value = command; }
        public void Delete() { if (Failure is not null) throw Failure; Value = null; }
    }

    [Fact]
    public void EnableQuotesPathAndDisableIsIdempotent()
    {
        var registry = new FakeRegistry();
        var service = new AutostartService(registry);
        Assert.False(service.IsEnabled());
        Assert.Null(service.GetRegisteredExePath());
        Assert.False(service.IsPathStale(@"C:\Apps\Miya.exe"));
        service.Enable(@"C:\My Apps\Miya Island.exe");
        Assert.Equal("\"C:\\My Apps\\Miya Island.exe\" --autostart", registry.Value);
        Assert.True(service.IsEnabled());
        Assert.Equal(@"C:\My Apps\Miya Island.exe", service.GetRegisteredExePath());
        Assert.False(service.IsPathStale(@"c:\my apps\MIYA ISLAND.EXE"));
        Assert.True(service.IsPathStale(@"D:\Miya.exe"));
        service.Disable();
        service.Disable();
        Assert.Null(registry.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative.exe")]
    [InlineData("C:relative.exe")]
    [InlineData("C:\\bad\"path.exe")]
    [InlineData("C:\\bad\npath.exe")]
    public void InvalidPathsCannotWrite(string path)
    {
        var registry = new FakeRegistry();
        Assert.Throws<ArgumentException>(() => new AutostartService(registry).Enable(path));
        Assert.Null(registry.Value);
    }

    [Theory]
    [InlineData("unquoted.exe --autostart")]
    [InlineData("\"C:\\Miya.exe\" --wrong")]
    [InlineData("\"relative.exe\" --autostart")]
    public void MalformedRegistrationIsStale(string command)
    {
        var service = new AutostartService(new FakeRegistry { Value = command });
        Assert.Null(service.GetRegisteredExePath());
        Assert.False(service.IsEnabled());
        Assert.True(service.IsPathStale(@"C:\Miya.exe"));
    }

    [Fact]
    public void RegistryFailuresHaveChineseContextAndInnerCause()
    {
        var cause = new UnauthorizedAccessException("denied");
        var registry = new FakeRegistry { Failure = cause };
        var service = new AutostartService(registry);
        var read = Assert.Throws<InvalidOperationException>(() => service.IsEnabled());
        Assert.Contains("读取开机自启动设置失败", read.Message);
        Assert.Same(cause, read.InnerException);
        Assert.Contains("启用开机自启动失败",
            Assert.Throws<InvalidOperationException>(() => service.Enable(@"C:\Miya.exe")).Message);
        Assert.Contains("关闭开机自启动失败",
            Assert.Throws<InvalidOperationException>(service.Disable).Message);
    }

    [Fact]
    public void UncPathRoundTrips()
    {
        var service = new AutostartService(new FakeRegistry());
        service.Enable(@"\\server\share\Miya Island.exe");
        Assert.Equal(@"\\server\share\Miya Island.exe", service.GetRegisteredExePath());
    }
}
