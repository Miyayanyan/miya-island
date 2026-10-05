using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MiyaIsland;

public partial class FloatingLyricsWindow : Window
{
    // 胶囊尺寸（单行 / 双行）和画布四周的阴影边距。保存的位置是胶囊本身的位置，与 v0.6.1 兼容。
    private const double LyricWidth = 560;
    private const double CollapsedHeight = 44;
    private const double ExpandedHeight = 76;
    private const double CanvasMargin = 16;
    private readonly double? _savedLeft;
    private readonly double? _savedTop;
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly MiyaIsland.Services.MusicSession _music;
    public event Action? HideRequested;
    private System.Windows.Threading.DispatcherTimer? _snapTimer;
    private readonly string? _savedMonitor;
    private bool _positionRestored;
    private bool _expanded;
    private string _currentText = string.Empty;
    private bool _isLocked;

    public event Action<double, double>? PositionChangedByUser;
    public event Action<bool>? LockChangedByUser;

    public FloatingLyricsWindow(double? savedLeft, double? savedTop, bool isLocked, MiyaIsland.Services.MusicSession music, string? savedMonitor = null, MiyaIsland.Models.AppearanceSettings? preferences = null)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyBlur();
        _preferences = preferences ?? new MiyaIsland.Models.AppearanceSettings();
        ApplyPreferences();
        _music = music;
        _music.StateChanged += RenderMusic;
        RenderMusic(_music.State);
        _savedMonitor = savedMonitor;
        _savedLeft = savedLeft;
        _savedTop = savedTop;
        _isLocked = isLocked;
        _clockTimer.Tick += (_, _) => UpdateClock();
        // 按圆角裁剪玻璃层，模糊的边缘光晕不会溢出到四角
        LyricHost.MouseEnter+=(_,_)=>UpdateHover();LyricHost.MouseLeave+=(_,_)=>UpdateHover();
        IsVisibleChanged+=(_,_)=>{if(!IsVisible && _morePopup is not null)_morePopup.IsOpen=false;};
        LyricBorder.MouseMove += LyricBorder_MouseMove;
        LyricBorder.MouseLeftButtonUp += LyricBorder_MouseLeftButtonUp;
        LyricBorder.LostMouseCapture += (_, _) => { if (_dragPress is not null) EndDrag(); };
        LyricBorder.SizeChanged += (_, _) => { UpdateClip(); ApplyBlur(); };
    }

    private void UpdateClip()
    {
        var radius = LyricBorder.CornerRadius.TopLeft;
        LyricBorder.Clip = new RectangleGeometry(
            new Rect(0, 0, LyricBorder.ActualWidth, LyricBorder.ActualHeight), radius, radius);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateClock();
        UpdateLockVisual();
        _clockTimer.Start();
        if (_positionRestored) return;
        _positionRestored = true;
        var screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _savedMonitor);
        var area = screen is null ? new MiyaIsland.Services.WorkArea(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height) : MiyaIsland.Services.WindowPosition.Area(this, screen);
        if ((_savedMonitor is null || screen is not null) && _savedLeft is double left && _savedTop is double top && MiyaIsland.Services.PositionPolicy.CanRestore(left,top,LyricWidth,CollapsedHeight,area))
        { Left = left - CanvasMargin; Top = top - CanvasMargin; }
        else { Left = area.Center-LyricWidth/2-CanvasMargin; Top = area.Bottom-CollapsedHeight-70-CanvasMargin; }

    }

    // 与小岛收起态使用同一个“标题字体”
    public void SetLyricFont(FontFamily fontFamily)
    {
        CurrentLyricText.FontFamily = fontFamily;
        NextLyricText.FontFamily = fontFamily;
    }

    private void UpdateClock() =>
        ClockText.Text = DateTime.Now.ToString("tt h:mm", CultureInfo.GetCultureInfo("zh-CN"));

    public void UpdateLyrics(string current, string next)
    {
        current = string.IsNullOrWhiteSpace(current) ? "♪" : current;
        if (string.Equals(current, _currentText, StringComparison.Ordinal))
        {
            NextLyricText.Text = next;
            return;
        }

        _currentText = current;
        CurrentLyricText.Text = current;
        NextLyricText.Text = next;
        // 换句：新句从下方 6px 处淡入
        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        CurrentLyricText.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        CurrentLyricShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, duration) { EasingFunction = ease });
    }

    private void LyricBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null)
            return;

        if (e.ClickCount == 2)
        {
            ToggleExpanded();
            return;
        }

        if (_isLocked || e.LeftButton != MouseButtonState.Pressed) return;
        // 自己处理拖动（不用 DragMove），这样拖的过程中就能磁吸到中线并显示参考线
        _snapTimer?.Stop();
        _dragPress = PointToScreen(e.GetPosition(this)); _dragOrigin = new Point(Left, Top); _dragging = false;
        LyricBorder.CaptureMouse();
        e.Handled = true;
    }

    private Point? _dragPress;
    private Point _dragOrigin;
    private bool _dragging;
    private MiyaIsland.Controls.SnapGuide? _centerGuide, _topGuide;

    private void LyricBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragPress is not { } press || e.LeftButton != MouseButtonState.Pressed) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var delta = PointToScreen(e.GetPosition(this)) - press;
        delta = new Vector(delta.X / dpi.DpiScaleX, delta.Y / dpi.DpiScaleY);
        if (!_dragging && delta.Length < 3) return;
        _dragging = true;
        var area = MiyaIsland.Services.WindowPosition.Area(this, MiyaIsland.Services.WindowPosition.Screen(this));
        var defaultTop = area.Bottom - CollapsedHeight - 70;
        var snap = MiyaIsland.Services.PositionPolicy.LiveSnap(_dragOrigin.X + delta.X + Width / 2, _dragOrigin.Y + delta.Y + CanvasMargin,
            area, defaultTop, Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        Left = snap.Center - Width / 2; Top = snap.Top - CanvasMargin;
        ShowGuides(snap.CenterGuide, snap.TopGuide, area);
        e.Handled = true;
    }

    private void LyricBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragPress is null) return;
        var moved = _dragging;
        EndDrag();
        if (moved) PositionChangedByUser?.Invoke(Left + CanvasMargin, Top + CanvasMargin);
        e.Handled = true;
    }

    private void EndDrag()
    {
        _dragPress = null; _dragging = false;
        if (LyricBorder.IsMouseCaptured) LyricBorder.ReleaseMouseCapture();
        ShowGuides(false, false, default);
    }

    private void ShowGuides(bool center, bool top, MiyaIsland.Services.WorkArea area)
    {
        if (center) (_centerGuide ??= new MiyaIsland.Controls.SnapGuide(this)).ShowAt(new Rect(area.Center - 0.5, area.Top, 1, area.Height));
        else _centerGuide?.Hide();
        if (top) (_topGuide ??= new MiyaIsland.Controls.SnapGuide(this)).ShowAt(new Rect(area.Left, Top + CanvasMargin + LyricHost.ActualHeight / 2 - 0.5, area.Width, 1));
        else _topGuide?.Hide();
    }

    /// <summary>菜单里的“水平居中”：保持高度，移到当前屏幕正中。</summary>
    public void CenterHorizontally()
    {
        var area = MiyaIsland.Services.WindowPosition.Area(this, MiyaIsland.Services.WindowPosition.Screen(this));
        _snapTimer?.Stop();
        _snapTimer = MiyaIsland.Services.WindowPosition.Slide(this, area.Center - Width / 2, Top,
            (left, top) => { Left = left; Top = top; }, () => PositionChangedByUser?.Invoke(Left + CanvasMargin, Top + CanvasMargin));
    }

    private static T? FindVisualParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match) return match;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => ToggleExpanded();

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        _isLocked = !_isLocked;
        UpdateLockVisual();
        LockChangedByUser?.Invoke(_isLocked);
    }

    private void UpdateLockVisual()
    {
        LyricBorder.Cursor = _isLocked ? Cursors.Arrow : Cursors.SizeAll;
    }

    private void ToggleExpanded()
    {
        _preferences.LyricShowNext = !_preferences.LyricShowNext;
        ApplyPreferences(); PreferencesChanged?.Invoke();
    }

    private Geometry IconGeometry(string key) => (Geometry)FindResource(key);

    private void RenderMusic(MiyaIsland.Services.MusicState state)
    {
        var current = !state.HasSession ? "没有正在播放的音乐" : state.CurrentLine switch
        {
            "暂未找到同步歌词，可导入 LRC" => "暂未找到同步歌词",
            "歌词获取失败，可稍后重试或导入 LRC" => "歌词获取失败",
            _ => state.CurrentLine
        };
        var next = !state.HasSession ? "打开 Spotify 后会自动显示歌词" : state.CurrentLine switch
        {
            "正在匹配歌词…" => state.Title,
            "暂未找到同步歌词，可导入 LRC" => "可以在小岛中导入 LRC",
            "歌词获取失败，可稍后重试或导入 LRC" => "可稍后重试或导入 LRC",
            _ => state.NextLine
        };
        UpdateLyrics(current, next);
        SetCover(state.Cover);
    }
    private void Close_Click(object sender, RoutedEventArgs e) => HideRequested?.Invoke();
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App app && !app.IsExiting) { e.Cancel = true; HideRequested?.Invoke(); }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _clockTimer.Stop(); _snapTimer?.Stop(); _centerGuide?.Close(); _topGuide?.Close();
        _music.StateChanged -= RenderMusic;
        if(_morePopup is not null)_morePopup.IsOpen=false;
        base.OnClosed(e);
    }
}
