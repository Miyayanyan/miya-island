using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using MiyaIsland.Models;
using MiyaIsland.Services;
using Application = System.Windows.Application;

namespace MiyaIsland;

public partial class App : Application
{
    private readonly AppearanceSettingsService _settings = new();
    private AppearanceSettings _appearance = new();
    private MusicSession? _music;
    private MainWindow? _island;
    private SettingsWindow? _settingsWindow;
    private FloatingLyricsWindow? _lyrics;
    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Drawing.Icon? _trayIcon;
    public bool IsExiting { get; private set; }
    public void ExitApplication() { IsExiting = true; Shutdown(); }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MiyaIsland.Controls.InputModality.Initialize();
        _appearance = _settings.Load();
        ThemeService.Apply(_appearance);
        // 上次把小岛和歌词都隐藏了的话，启动时仍显示小岛，避免看起来像没打开
        if (!e.Args.Contains("--autostart", StringComparer.OrdinalIgnoreCase) && !_appearance.ShowIsland && !_appearance.ShowLyrics) _appearance.ShowIsland = true;
        _music = new MusicSession();
        _island = new MainWindow(_music, _appearance);
        MainWindow = _island;
        CreateTray();
        if (_appearance.ShowIsland) _island.Show();
        if (_appearance.ShowLyrics) SetLyricsVisible(true);
        _island.BeginStartupGreeting();
        await _music.StartAsync();
    }

    public void SaveSettings() { try { _settings.Save(_appearance); } catch (Exception ex) { System.Diagnostics.Trace.TraceWarning(ex.Message); } }
    public void SetIslandVisible(bool visible)
    {
        if (_island is null) return;
        _appearance.ShowIsland = visible;
        if (visible) { _island.Show(); _island.Activate(); } else _island.Hide();
        SaveSettings();
    }
    public void SetLyricsVisible(bool visible)
    {
        if (_music is null) return;
        if (visible && _lyrics is null)
        {
            _lyrics = new FloatingLyricsWindow(_appearance.FloatingLyricsLeft, _appearance.FloatingLyricsTop, _appearance.FloatingLyricsLocked, _music, _appearance.FloatingLyricsMonitor, _appearance);
            _lyrics.PreferencesChanged += () => { ApplySharedSettings(); _settingsWindow?.Refresh(); };
            _lyrics.HideRequested += () => SetLyricsVisible(false);
            _lyrics.PositionChangedByUser += (left, top) => { _appearance.FloatingLyricsLeft = left; _appearance.FloatingLyricsTop = top; _appearance.FloatingLyricsMonitor = WindowPosition.Screen(_lyrics).DeviceName; SaveSettings(); };
            _lyrics.LockChangedByUser += locked => { _appearance.FloatingLyricsLocked = locked; SaveSettings(); };
            UpdateLyricsFont(new System.Windows.Media.FontFamily(_appearance.FontFamily));
        }
        _appearance.ShowLyrics = visible;
        if (visible) _lyrics!.Show(); else _lyrics?.Hide();
        _island?.UpdateLyricsVisibility();
        SaveSettings();
    }
    public void UpdateLyricsFont(System.Windows.Media.FontFamily font) => _lyrics?.SetLyricFont(font);

    public void ShowSettings()
    {
        if (_settingsWindow is null)
        { _settingsWindow = new SettingsWindow(_appearance, ApplySharedSettings); _settingsWindow.Closed += (_, _) => _settingsWindow = null; }
        _settingsWindow.Show(); _settingsWindow.Activate();
    }
    public void ApplySharedSettings() { ThemeService.Apply(_appearance); _island?.RefreshAppearance(); ApplyLyricsSettings(); _settingsWindow?.ApplyBackdrop(); SaveSettings(); }
    public void SettingsChangedFromIsland() { _settingsWindow?.Refresh(); _settingsWindow?.ApplyBackdrop(); ApplyLyricsSettings(); }
    public void BackgroundBlurFailed()
    {
        _appearance.BackgroundBlur=false;ApplySharedSettings();_island?.NotifyBackdropFailure();
        Dispatcher.BeginInvoke(new Action(()=>_settingsWindow?.Refresh()));
    }
    public GreetingContext CreateGreetingContext() => _island?.GreetingContext() ?? new GreetingContext();
    public void PreviewGreetingCard()=>_island?.PreviewGreetingCard();
    public void CenterIsland() => _island?.CenterAtTop();
    partial void ApplyLyricsSettings();

    private void CreateTray()
    {
        // Render WPF artwork, then clone the icon before releasing the native bitmap handle.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle((Brush)FindResource("Brush.IslandBase"), new Pen((Brush)FindResource("Brush.Ink2"), 1), new Rect(1,7,30,18),9,9);
            dc.DrawEllipse((Brush)FindResource("Brush.Accent"),null,new Point(11,16),4,4);
        }
        var bitmap = new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream(); encoder.Save(stream); stream.Position = 0;
        using var drawing = new System.Drawing.Bitmap(stream);
        var handle = drawing.GetHicon();
        try { using var temporary = System.Drawing.Icon.FromHandle(handle); _trayIcon = (System.Drawing.Icon)temporary.Clone(); }
        finally { DestroyIcon(handle); }
        _tray = new System.Windows.Forms.NotifyIcon { Icon = _trayIcon, Text = "Miya Island", Visible = true };
        _tray.ContextMenuStrip = CreateTrayMenu();
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => SetIslandVisible(true));
    }
    private System.Windows.Forms.ContextMenuStrip CreateTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        var island = menu.Items.Add("隐藏灵动岛"); island.Click += (_, _) => Dispatcher.Invoke(() => SetIslandVisible(!_appearance.ShowIsland));
        var lyrics = menu.Items.Add("显示悬浮歌词"); lyrics.Click += (_, _) => Dispatcher.Invoke(() => SetLyricsVisible(!_appearance.ShowLyrics));
        menu.Items.Add("截图").Click += async (_, _) => await _island!.TakeScreenshotAsync();
        var quiet=new System.Windows.Forms.ToolStripMenuItem("安静模式");menu.Items.Add(quiet);quiet.Click+=(_,_)=>Dispatcher.Invoke(()=>{_appearance.QuietMode=!_appearance.QuietMode;ApplySharedSettings();_settingsWindow?.Refresh();});
        var blur=new System.Windows.Forms.ToolStripMenuItem("背景模糊") {CheckOnClick=false};menu.Items.Add(blur);
        blur.Click+=(_,_)=>Dispatcher.Invoke(()=>{_appearance.BackgroundBlur=!_appearance.BackgroundBlur;ApplySharedSettings();_settingsWindow?.Refresh();});
        var hide=new System.Windows.Forms.ToolStripMenuItem("截图/录屏时隐藏小岛") {CheckOnClick=false};menu.Items.Add(hide);
        hide.Click+=(_,_)=>Dispatcher.Invoke(()=>{_appearance.SetHiddenFromCapture(!_appearance.IsHiddenFromCapture());ApplySharedSettings();_settingsWindow?.Refresh();});
        menu.Items.Add("设置").Click += (_, _) => Dispatcher.Invoke(ShowSettings);
        menu.Items.Add("重新居中").Click += (_, _) => Dispatcher.Invoke(() => _island?.CenterAtTop());
        menu.Items.Add("退出").Click += (_, _) => Dispatcher.Invoke(ExitApplication);
        menu.Opening += (_, _) => { quiet.Checked=_appearance.QuietMode;hide.Checked=_appearance.IsHiddenFromCapture();hide.Enabled=BackdropCapture.IsSupported;blur.Checked=_appearance.BackgroundBlur;blur.Enabled=BackdropCapture.IsSupported;blur.ToolTipText=BackdropCapture.IsSupported?"截图时可临时关闭":"需要 Windows 10 2004 或更新版本";island.Text = _appearance.ShowIsland ? "隐藏灵动岛" : "显示灵动岛"; lyrics.Text = _appearance.ShowLyrics ? "隐藏悬浮歌词" : "显示悬浮歌词"; };
        return menu;
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        _music?.Dispose();
        _settingsWindow?.Close(); _island?.Close(); _lyrics?.Close();
        if (_tray is not null) { _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Dispose(); }
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
