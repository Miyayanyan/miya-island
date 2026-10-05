using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using MiyaIsland.Controls;
using MiyaIsland.Models;
using MiyaIsland.Services;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MiyaIsland;

public partial class MainWindow : Window
{
    private const double CollapsedWidth = 210;
    private const double CollapsedHeight = 42;
    private const double ExpandedWidth = 430;
    private const double ExpandedHeight = 352;
    private const double ExpandedRadius = 26;
    // 固定画布四周的阴影边距（未缩放的 DIP）
    private const double CanvasMargin = 24;
    private const double CanvasWidth = ExpandedWidth + CanvasMargin * 2;
    private const double CanvasHeight = ExpandedHeight + CanvasMargin * 2;
    private const double ExpandMilliseconds = 340;
    private const double CollapseMilliseconds = 260;

    public static readonly DependencyProperty MorphProgressProperty = DependencyProperty.Register(
        nameof(MorphProgress), typeof(double), typeof(MainWindow),
        new PropertyMetadata(1.0, (d, _) => ((MainWindow)d).ApplyMorph()));

    private readonly record struct IslandShape(double Width, double Height, double Radius);
    private static readonly IslandShape CollapsedShape = new(CollapsedWidth, CollapsedHeight, CollapsedHeight / 2);
    private static readonly IslandShape ExpandedShape = new(ExpandedWidth, ExpandedHeight, ExpandedRadius);
    private readonly IEasingFunction _expandEase = new SpringEase();
    private readonly IEasingFunction _collapseEase = new CubicEase { EasingMode = EasingMode.EaseInOut };
    private IslandShape _shapeFrom = CollapsedShape;
    private IslandShape _shapeTo = CollapsedShape;
    private IslandShape _currentShape = CollapsedShape;
    private bool _morphExpanding;
    private double _morphMilliseconds = ExpandMilliseconds;
    private double _contentOpacityFrom;
    private double _appliedScale = 1;

    private readonly MusicSession _music;
    private readonly VolumeService _volume = new();
    private readonly TaskStore _taskStore;
    private readonly List<TaskItem> _tasks = [];
    private readonly ReminderScheduler _scheduler;
    private readonly Queue<(TaskItem Task, DateTime Occurrence)> _reminderQueue = new();
    private (TaskItem Task, DateTime Occurrence)? _activeReminder;
    private bool _reminderWasHidden;
    private static readonly IslandShape RemindShape = new(380, 136, 26);
    private UIElement? _targetContent;
    private readonly Dictionary<UIElement, double> _layerOpacities = [];
    private readonly DownloadService _download = new();
    private readonly AppearanceSettingsService _appearanceService;
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private bool _compact;
    private Point? _compactPress;
    private Point _dragOrigin;
    private bool _compactDragging;
    private bool _positionRestored;
    private DispatcherTimer? _snapTimer;
    private readonly DispatcherTimer _compactTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private IslandShape CompactShape => EffectiveRestStyle == "Orb" ? new(32,32,16) : new(104,30,15);
    private bool _expanded;
    private bool _settingVolume = true;
    private string _currentTrackKey = string.Empty;
    private string _currentTrackTitle = string.Empty;
    private string _currentTrackArtist = string.Empty;
    private AppearanceSettings _appearance;
    private bool _appearanceReady;
    private DateTime _notificationUntil;
    private string _floatingCurrentText = "播放音乐后显示歌词";
    private string _floatingNextText = string.Empty;
    private bool _lastMediaPlaying;
    private double _horizontalCenter = double.NaN;

    public MainWindow(MusicSession music, AppearanceSettings appearance, string? dataDirectory = null, bool startTimers = true)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => UpdateBlur();
        _appearanceService = new AppearanceSettingsService(dataDirectory);
        _taskStore = new TaskStore(dataDirectory);
        InitializeAutoCollapse();
        _greetingExpiry.Tick += (_, _) => { _greetingExpiry.Stop(); if (_greetingVisible) Collapse(); };
        _appearance = appearance;
        _music = music;
        _music.StateChanged += RenderMusic;
        FontFamilyCombo.ItemsSource = Fonts.SystemFontFamilies.OrderBy(font => font.Source).ToList();
        LoadAppearanceControls();
        ApplyAppearance(resizeWindow: true);
        UpdateIslandLockVisual();
        _appearanceReady = true;

        _tasks.AddRange(_taskStore.Load());
        TaskRetention.Prune(_tasks, DateTime.Now, _appearance.CompletedRetentionDays);
        _scheduler = new ReminderScheduler(_tasks);
        var startup = _scheduler.CatchUpOnStartup(DateTime.Now);
        _taskStore.Save(_tasks);
        TasksPanel.DefaultReminderTime = _appearance.DefaultReminderTime;
        TasksPanel.Attach(_tasks, _scheduler);
        TasksPanel.Changed += () => _taskStore.Save(_tasks);
        if (startup.OverdueCount > 0) ShowCompact($"有 {startup.OverdueCount} 件过期任务");

        _reminderTimer.Tick += (_, _) => CheckReminders();
        if (startTimers) _reminderTimer.Start();
        _compactTimer.Tick += (_, _) => UpdateCompactContent();
        if (startTimers) _compactTimer.Start();
        _compact = EffectiveRestStyle != "Normal";
        UpdateCompactContent(); UpdateNavigation(MusicPanel);
        SnapIslandShape();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyAppearance(resizeWindow: true);
        if (!_positionRestored) { RestorePosition(); _positionRestored = true; }
        RenderMusic(_music.State);

        _settingVolume = true;
        var volume = _volume.GetVolume() * 100;
        VolumeSlider.Value = volume;
        VolumeText.Text = $"{volume:0}%";
        _settingVolume = false;
    }

    public double MorphProgress
    {
        get => (double)GetValue(MorphProgressProperty);
        set => SetValue(MorphProgressProperty, value);
    }

    public void CenterAtTop()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        // 画布顶部有阴影边距，让岛本身仍然距离屏幕顶部 8 DIP
        Top = area.Top + 8 - CanvasMargin * GetScale();
        _horizontalCenter = Left + Width / 2;
        if (_positionRestored) SavePosition();
    }

    private void Island_MouseEnter(object sender, MouseEventArgs e) { if (!_compact) Expand(); }

    private void Island_MouseLeave(object sender, MouseEventArgs e) => QueueAutoCollapse();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null)
            return;

        if (e.ClickCount == 1)
        {
            if (!_expanded) Expand();
            else DragExpandedIsland(e);
        }
    }

    // 展开后，面板里的空白处也能拖动；按钮、输入框、滑块、下拉框、列表项和滚动条照常响应。
    private void ExpandedContent_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_expanded || e.ClickCount != 1 || IsInteractive(e.OriginalSource as DependencyObject)) return;
        DragExpandedIsland(e);
    }

    private void DragExpandedIsland(MouseButtonEventArgs e)
    {
        if (_appearance.IslandLocked || e.LeftButton != MouseButtonState.Pressed) return;
        e.Handled = true;
        try
        {
            _snapTimer?.Stop();
            DragMove();
            _horizontalCenter = Left + Width / 2;
            SnapAfterDrag();
        }
        catch (InvalidOperationException)
        {
            // 鼠标在拖动开始前就已松开，忽略即可。
        }
    }

    private bool IsInteractive(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.TextBoxBase
                or System.Windows.Controls.PasswordBox or System.Windows.Controls.Slider or System.Windows.Controls.ComboBox
                or System.Windows.Controls.ListBoxItem or System.Windows.Controls.Primitives.ScrollBar
                or System.Windows.Controls.Primitives.Thumb or System.Windows.Documents.Hyperlink)
                return true;
            if (element == LyricWell && _music.CanRetryLyrics) return true; // 没歌词时歌词框可以点击重新查找
            if (element == ExpandedContent) return false;
            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }
        return false;
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

    private void IslandLock_Click(object sender, RoutedEventArgs e)
    {
        _appearance.IslandLocked = !_appearance.IslandLocked;
        UpdateIslandLockVisual();
        try { _appearanceService.Save(_appearance); ((App)Application.Current).SettingsChangedFromIsland(); } catch { }
        ShowCompact(_appearance.IslandLocked ? "灵动岛位置已锁定" : "灵动岛位置已解锁");
    }

    private void UpdateIslandLockVisual()
    {
        if (IslandLockButton is null) return;
        IslandLockIcon.Data = IconGeometry(_appearance.IslandLocked ? "Icon.Lock" : "Icon.Unlock");
        RestLockIcon.Visibility = !_expanded && _appearance.IslandLocked ? Visibility.Visible : Visibility.Collapsed;
        IslandLockButton.ToolTip = _appearance.IslandLocked ? "解锁灵动岛位置" : "锁定灵动岛位置";
    }

    private void Expand()
    {
        if (_expanded || _activeReminder is not null || _greetingVisible) return;
        _compact = false;
        _expanded = true;
        BeginHeaderGreeting();
        MorphTo(ExpandedShape, expanding: true);
    }

    private void Collapse()
    {
        if (_activeReminder is not null) return;
        StopHeaderGreeting();
        _greetingVisible = false; _greetingExpiry.Stop();
        _expanded = false;
        _compact = EffectiveRestStyle != "Normal";
        MorphTo(_compact ? CompactShape : CollapsedShape, expanding: false);
    }

    // 窗口大小不变，只让岛变形。一个进度值同时驱动尺寸、圆角、阴影和内容透明度，
    // 打断时从当前形状继续，不会跳。
    private bool _morphing;
    private void MorphTo(IslandShape target, bool expanding)
    {
        if(expanding)FlashDispersion();
        _shapeFrom = _currentShape;
        _shapeTo = target;
        _morphExpanding = expanding;
        _morphMilliseconds = target == CompactShape || _shapeFrom == CompactShape ? 300 : expanding ? ExpandMilliseconds : CollapseMilliseconds;
        _targetContent = _activeReminder is not null ? ReminderContent : _greetingVisible ? GreetingContent : _expanded ? ExpandedContent : null;
        _layerOpacities[GreetingContent] = GreetingContent.Opacity;
        _layerOpacities[ExpandedContent] = ExpandedContent.Opacity;
        _layerOpacities[ReminderContent] = ReminderContent.Opacity;
        _contentOpacityFrom = _targetContent?.Opacity ?? 0;
        BeginAnimation(MorphProgressProperty, null);
        MorphProgress = 0;
        _morphing=true;
        BeginAnimation(MorphProgressProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(_morphMilliseconds)));
    }

    private void ApplyMorph()
    {
        if (IslandHost is null) return;
        var progress = MorphProgress;
        var eased = (_morphMilliseconds == 300 || _morphExpanding ? _expandEase : _collapseEase).Ease(progress);
        SetIslandShape(new IslandShape(
            Lerp(_shapeFrom.Width, _shapeTo.Width, eased),
            Lerp(_shapeFrom.Height, _shapeTo.Height, eased),
            Lerp(_shapeFrom.Radius, _shapeTo.Radius, eased)));

        // 展开：120ms 后用 180ms 淡入并上移 4px；收起：前 100ms 淡出
        var elapsed = progress * _morphMilliseconds;
        double opacity;
        if (_morphExpanding)
        {
            var t = Math.Clamp((elapsed - 120) / 180, 0, 1);
            opacity = Lerp(_contentOpacityFrom, 1, t);
            ExpandedContentShift.Y = Math.Round((1 - t) * 4);
        }
        else
        {
            opacity = Lerp(_contentOpacityFrom, 0, Math.Clamp(elapsed / 100, 0, 1));
            ExpandedContentShift.Y = 0;
        }
        foreach (var layer in new UIElement[] { ExpandedContent, ReminderContent, GreetingContent })
        {
            layer.Opacity = layer == _targetContent ? opacity : Lerp(_layerOpacities.GetValueOrDefault(layer), 0, Math.Clamp(elapsed / 100, 0, 1));
            layer.IsHitTestVisible = layer == _targetContent;
        }
        if (progress >= 1) { _morphing=false; UpdateBlur(); }
    }

    private void SetIslandShape(IslandShape shape)
    {
        _currentShape = shape;
        IslandHost.Width = shape.Width;
        IslandHost.Height = shape.Height;
        var radius = new CornerRadius(shape.Radius);
        IslandBorder.CornerRadius = radius;
        ShadowPlate.CornerRadius = radius;
        IslandBorder.Clip = new RectangleGeometry(new Rect(0, 0, shape.Width, shape.Height), shape.Radius, shape.Radius);

        // 阴影从 E2（收起）过渡到 E3（展开）
        var expandedness = Math.Clamp((shape.Height - CollapsedHeight) / (ExpandedHeight - CollapsedHeight), 0, 1);
        IslandShadow.BlurRadius = Lerp(18, 34, expandedness);
        IslandShadow.ShadowDepth = Lerp(4, 10, expandedness);
        IslandShadow.Opacity = ThemeService.Current.Light ? .18 : ThemeService.Current.Clear ? .22 : Lerp(.32,.45,expandedness);
        Dispersion.CornerRadius = radius;

        // GlassPolicy：边缘带 = clamp(0.2h, 6, 22)
        var band = Math.Clamp(shape.Height * 0.2, 6, 22);
        GlassEdge.BorderThickness = new Thickness(Math.Max(2, band * 0.3));
        GlassEdgeBlur.Radius = band;
        var compactness = Math.Clamp((CollapsedWidth - shape.Width) / (CollapsedWidth - CompactShape.Width), 0, 1);
        var indicatorX = Lerp(_greetingVisible || _expanded ? 18 : 15, EffectiveRestStyle == "Orb" ? 11.5 : 11, compactness);
        var indicatorY = Lerp(_greetingVisible ? 31.5 : _expanded ? 19.5 : 16.5, (CompactShape.Height - 9) / 2, compactness);
        StatusIndicator.Margin = new Thickness(indicatorX, indicatorY,0,0);
        StatusIndicator.Visibility = !_appearance.AvatarEnabled && !(_compact && EffectiveRestStyle=="Pill" && _lastMediaPlaying) && _activeReminder is null && !_greetingVisible ? Visibility.Visible : Visibility.Hidden;
        HeaderContent.Opacity = 1 - compactness;
        HeaderContent.IsHitTestVisible = !_compact && _activeReminder is null;
        HeaderContent.Visibility = _activeReminder is null && !_greetingVisible ? Visibility.Visible : Visibility.Hidden;
        CompactContent.Opacity = EffectiveRestStyle == "Orb" ? 0 : compactness;
        HeaderContent.Height = _expanded ? 30 : 42;
        HeaderContent.Margin = _expanded ? new Thickness(18,12,14,0) : new Thickness(15,0,9,0);
        IslandLockButton.Visibility = HeaderCollapseButton.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        RestLockIcon.Visibility = !_expanded && _appearance.IslandLocked ? Visibility.Visible : Visibility.Collapsed;
        CompactIcon.Visibility = _expanded ? Visibility.Collapsed : CompactIcon.Visibility;
        CompactText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,_expanded?"Brush.Ink2":"Brush.Ink");
        if(!_expanded && _appearance.UseCustomTitleColor)CompactText.Foreground=BrushFromHex(_appearance.TitleColor,Colors.White);
        UpdateHeaderText();
        UpdateAvatarGeometry();
        UpdatePillReminderDot();
        UpdateBlur();
    }

    private void SnapIslandShape()
    {
        _morphing=false;
        BeginAnimation(MorphProgressProperty, null);
        if (!_expanded && _activeReminder is null) _compact = EffectiveRestStyle != "Normal";
        SetIslandShape(_activeReminder is not null ? RemindShape : _greetingVisible ? GreetShape : _expanded ? ExpandedShape : _compact ? CompactShape : CollapsedShape);
        ExpandedContent.Opacity = _expanded && _activeReminder is null ? 1 : 0;
        GreetingContent.Opacity = _greetingVisible ? 1 : 0;
        GreetingContent.IsHitTestVisible = _greetingVisible;
        ReminderContent.Opacity = _activeReminder is not null ? 1 : 0;
        ReminderContent.IsHitTestVisible = _activeReminder is not null;
        ExpandedContentShift.Y = 0;
        ExpandedContent.IsHitTestVisible = _expanded;
        UpdateBlur();
    }

    private static double Lerp(double from, double to, double t) => from + (to - from) * t;

    private void RenderMusic(MusicState state)
    {
        _lastMediaPlaying = state.IsPlaying;
        _currentTrackKey = state.TrackKey; _currentTrackTitle = state.Title; _currentTrackArtist = state.Artist;
        SongTitle.Text = state.Title; SongArtist.Text = state.Artist;
        PlayIcon.Data = IconGeometry(state.IsPlaying ? "Icon.Pause" : "Icon.Play");
        PlaybackTimeText.Text = $"{FormatTime(state.Position)} / {FormatTime(state.Duration)}";
        PreviousLyricText.Text = state.PreviousLine; CurrentLyricText.Text = state.CurrentLine; NextLyricText.Text = state.NextLine;
        var canRetry = _music.CanRetryLyrics;
        if (canRetry) NextLyricText.Text = "点这里重新查找，或导入 LRC";
        LyricWell.Cursor = canRetry ? Cursors.Hand : null;
        LyricWell.ToolTip = canRetry ? "重新查找歌词" : null;
        _floatingCurrentText = state.CurrentLine; _floatingNextText = state.NextLine;
        if (DateTime.Now >= _notificationUntil)
            SetCompactMusicDisplay(!state.HasSession ? _appearance.Title : state.HasLyrics && state.CurrentLine != "♪" ? state.CurrentLine : $"♫  {state.Title}");
    }

    private void SetCompactMusicDisplay(string musicText)
    {
        _renderingMusic = true;
        try
        {
        if (_appearance.ShowLyrics)
            ShowCompact(_appearance.Title);
        else if (!_expanded)
            ShowCompact(musicText);
        }
        finally { _renderingMusic = false; }
    }

    private void RestoreCompactMusicDisplay()
    {
        if (string.IsNullOrWhiteSpace(_currentTrackKey))
        {
            ShowCompact(_appearance.Title);
            return;
        }

        ShowCompact(_music.State.HasLyrics && !string.IsNullOrWhiteSpace(_floatingCurrentText)
            ? _floatingCurrentText
            : $"♫  {_currentTrackTitle}");
    }

    private Geometry IconGeometry(string key) => (Geometry)FindResource(key);

    // 收起态文字左侧的独立图标位。旧代码在文字前拼 ♫ / ⏰ / ⇩ 字符，
    // 这里把它们换成矢量图标，调用处的文字保持不变。
    private bool _renderingMusic;
    private void ShowCompact(string text)
    {
        if (!_renderingMusic) _notificationUntil = DateTime.Now.AddSeconds(6);
        string? iconKey = null;
        foreach (var (prefix, key) in CompactPrefixes)
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) continue;
            iconKey = key;
            text = text[prefix.Length..].TrimStart();
            break;
        }

        _restDisplayText = text; _restIconKey = iconKey; UpdateHeaderText();
    }

    private static readonly (string Prefix, string IconKey)[] CompactPrefixes =
    [
        ("♫", "Icon.Music"),
        ("⏰", "Icon.Bell"),
        ("⇩", "Icon.Download")
    ];

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    private async void Previous_Click(object sender, RoutedEventArgs e) { await _music.PreviousAsync(); }
    private async void Play_Click(object sender, RoutedEventArgs e) { await _music.TogglePlayAsync(); }
    private async void Next_Click(object sender, RoutedEventArgs e) { await _music.NextAsync(); }

    private async void LyricWell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_music.CanRetryLyrics) return;
        e.Handled = true;
        await _music.RetryLyricsAsync();
    }

    private async void ImportLyrics_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentTrackKey))
        {
            CurrentLyricText.Text = "请先播放一首歌再导入歌词";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "选择同步歌词文件",
            Filter = "LRC 同步歌词 (*.lrc)|*.lrc|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            await _music.ImportLyricsAsync(dialog.FileName);
            ShowCompact("歌词导入成功");
        }
        catch (Exception ex)
        {
            CurrentLyricText.Text = $"导入失败：{ex.Message}";
        }
    }

    private void FloatingLyrics_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).SetLyricsVisible(!_appearance.ShowLyrics);
    public void UpdateLyricsVisibility()
    {
        FloatingLyricsButton.Content = _appearance.ShowLyrics ? "关闭悬浮" : "悬浮歌词";
        RenderMusic(_music.State);
    }
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeText is null) return;
        VolumeText.Text = $"{e.NewValue:0}%";
        if (!_settingVolume) _volume.SetVolume((float)(e.NewValue / 100));
    }

    private void ShowPanel(UIElement panel)
    {
        MusicPanel.Visibility = Visibility.Collapsed;
        ReminderPanel.Visibility = Visibility.Collapsed;
        DownloadPanel.Visibility = Visibility.Collapsed;
        ScreenshotPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
        UpdateNavigation(panel);
    }

    private void MusicTab_Click(object sender, RoutedEventArgs e) => ShowPanel(MusicPanel);
    private void ReminderTab_Click(object sender, RoutedEventArgs e) => ShowPanel(ReminderPanel);
    private void DownloadTab_Click(object sender, RoutedEventArgs e) => ShowPanel(DownloadPanel);
    private void ScreenshotTab_Click(object sender, RoutedEventArgs e) => ShowPanel(ScreenshotPanel);
    private void SettingsTab_Click(object sender, RoutedEventArgs e) => ShowPanel(SettingsPanel);

    public void RefreshAppearance()
    {
        TasksPanel.DefaultReminderTime = _appearance.DefaultReminderTime;
        if (TaskRetention.Prune(_tasks, DateTime.Now, _appearance.CompletedRetentionDays) > 0) { _taskStore.Save(_tasks); TasksPanel.Refresh(); }
        _appearanceReady = false; LoadAppearanceControls(); _appearanceReady = true;
        var previous = _currentShape;
        ApplyAppearance(resizeWindow: true);
        if (!_expanded && _activeReminder is null && previous != _currentShape)
        { SetIslandShape(previous); Collapse(); }
    }
    private void FullSettings_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).ShowSettings();
    private void Island_RightClick(object sender, MouseButtonEventArgs e) { ((App)Application.Current).ShowSettings(); e.Handled = true; }

    private void LoadAppearanceControls()
    {
        TitleInput.Text = string.IsNullOrWhiteSpace(_appearance.Title) ? "Miya Island" : _appearance.Title;

        var selectedFont = FontFamilyCombo.Items
            .OfType<FontFamily>()
            .FirstOrDefault(font => string.Equals(font.Source, _appearance.FontFamily, StringComparison.OrdinalIgnoreCase));
        FontFamilyCombo.SelectedItem = selectedFont ?? FontFamilyCombo.Items.OfType<FontFamily>().FirstOrDefault();

        BreathingCheck.IsChecked = _appearance.BreathingEnabled;
        BreathingSpeedCombo.SelectedIndex = _appearance.BreathingSpeed switch
        {
            "慢速" => 0,
            "快速" => 2,
            _ => 1
        };
        ScaleSlider.Value = Math.Clamp(_appearance.ScalePercent, 70, 160);
        ScaleValueText.Text = $"{ScaleSlider.Value:0}%";
        AutoCollapseCheck.IsChecked = _appearance.AutoCollapse;
        QuietModeCheck.IsChecked=_appearance.QuietMode;
        RestStyleCombo.Visibility=_appearance.QuietMode?Visibility.Collapsed:Visibility.Visible;QuietRestHint.Visibility=_appearance.QuietMode?Visibility.Visible:Visibility.Collapsed;
        RestStyleCombo.IsEnabled=!_appearance.QuietMode;RestStyleCombo.ToolTip=_appearance.QuietMode?"安静模式中":null;
        RestStyleCombo.SelectedIndex = EffectiveRestStyle switch { "Pill" => 1, "Orb" => 2, _ => 0 };
        var picker = ThemeService.PresetPicker(_appearance, () => ApplyAndSaveAppearance(true));
        picker.DropDownOpened+=(_,_)=>{_glassMenuOpen=true;};picker.DropDownClosed+=(_,_)=>{_glassMenuOpen=false;QueueAutoCollapse();};GlassPickerHost.Content=picker;
        UpdateColorButtonPreviews();
    }

    private void ReadAppearanceControls()
    {
        _appearance.Title = string.IsNullOrWhiteSpace(TitleInput.Text) ? "Miya Island" : TitleInput.Text.Trim();
        if (FontFamilyCombo.SelectedItem is FontFamily font)
            _appearance.FontFamily = font.Source;

        _appearance.BreathingEnabled = BreathingCheck.IsChecked == true;
        _appearance.BreathingSpeed = (BreathingSpeedCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "正常";
        _appearance.ScalePercent = Math.Clamp(ScaleSlider.Value, 70, 160);
        _appearance.AutoCollapse = AutoCollapseCheck.IsChecked == true;
        if(!_appearance.QuietMode) _appearance.RestStyle = RestStyleCombo.SelectedIndex switch { 1 => "Pill", 2 => "Orb", _ => "Normal" };
    }

    private void ApplyAppearance(bool resizeWindow)
    {
        var fontName = string.IsNullOrWhiteSpace(_appearance.FontFamily) ? "Segoe UI" : _appearance.FontFamily;
        CompactText.FontFamily = new FontFamily(fontName);
        ((App)Application.Current).UpdateLyricsFont(CompactText.FontFamily);
        ThemeService.Apply(_appearance);
        CompactText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,"Brush.Ink");
        if(_appearance.UseCustomTitleColor)CompactText.Foreground=BrushFromHex(_appearance.TitleColor,Colors.White);
        if (_appearance.ShowLyrics)
            ShowCompact(_appearance.Title);
        StatusIndicator.Fill = BrushFromHex(_appearance.IndicatorColor, Color.FromRgb(139, 124, 255));

        var scale = GetScale();
        IslandScaleTransform.ScaleX = scale;
        IslandScaleTransform.ScaleY = scale;
        StartBreathingAnimation();
        IslandAvatar.Configure(new AvatarService().LoadCurrent(), _appearance, StatusIndicator);
        UpdateAvatarGeometry();
        var picker = ThemeService.PresetPicker(_appearance, () => ApplyAndSaveAppearance(true));
        picker.DropDownOpened+=(_,_)=>{_glassMenuOpen=true;};picker.DropDownClosed+=(_,_)=>{_glassMenuOpen=false;QueueAutoCollapse();};GlassPickerHost.Content=picker;
        UpdateColorButtonPreviews();

        if (!resizeWindow) return;
        if (IsLoaded && double.IsNaN(_horizontalCenter))
            _horizontalCenter = Left + Width / 2;
        // 缩放变化时，保持岛的顶部和水平中心不动
        var islandTop = Top + CanvasMargin * _appliedScale;
        Width = CanvasWidth * scale;
        Height = CanvasHeight * scale;
        _appliedScale = scale;
        SnapIslandShape();
        if (IsLoaded)
        {
            Left = _horizontalCenter - Width / 2;
            Top = islandTop - CanvasMargin * scale;
        }
    }

    private double GetScale() => Math.Clamp(_appearance.ScalePercent, 70, 160) / 100.0;

    private void StartBreathingAnimation()
    {
        StatusIndicator.BeginAnimation(OpacityProperty, null);
        IndicatorScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        IndicatorScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        StatusIndicator.Opacity = 1;
        IndicatorScaleTransform.ScaleX = 1;
        IndicatorScaleTransform.ScaleY = 1;

        if (!_appearance.BreathingEnabled) return;

        var seconds = _appearance.BreathingSpeed switch
        {
            "慢速" => 2.4,
            "快速" => 1.0,
            _ => 1.7
        };
        var duration = TimeSpan.FromSeconds(seconds);
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        var opacityAnimation = new DoubleAnimation(0.42, 1, duration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        var scaleAnimation = new DoubleAnimation(0.92, 1.18, duration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        StatusIndicator.BeginAnimation(OpacityProperty, opacityAnimation);
        IndicatorScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        IndicatorScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation.Clone());
    }

    private static SolidColorBrush BrushFromHex(string value, Color fallback)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }
        catch
        {
            return new SolidColorBrush(fallback);
        }
    }

    private void UpdateColorButtonPreviews()
    {
        if (TitleColorButton is null || IndicatorColorButton is null) return;
        TitleColorButton.Background = BrushFromHex(_appearance.TitleColor, Colors.White);
        TitleColorButton.Foreground = ContrastBrush(((SolidColorBrush)TitleColorButton.Background).Color);
        IndicatorColorButton.Background = BrushFromHex(_appearance.IndicatorColor, Color.FromRgb(139, 124, 255));
        IndicatorColorButton.Foreground = ContrastBrush(((SolidColorBrush)IndicatorColorButton.Background).Color);
    }

    private static SolidColorBrush ContrastBrush(Color color)
    {
        var brightness = GlassPalette.Brightness(color.R,color.G,color.B);
        return brightness >= 150 ? Brushes.Black : Brushes.White;
    }

    private static string ToHex(System.Drawing.Color color) =>
        $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static System.Drawing.Color ToDrawingColor(SolidColorBrush brush) =>
        System.Drawing.Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);

    private void ChooseTitleColor_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = ToDrawingColor(BrushFromHex(_appearance.TitleColor, Colors.White))
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        _appearance.TitleColor = ToHex(dialog.Color);_appearance.UseCustomTitleColor=true;
        ApplyAndSaveAppearance(resizeWindow: false);
    }

    private void ChooseIndicatorColor_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = ToDrawingColor(BrushFromHex(_appearance.IndicatorColor, Color.FromRgb(139, 124, 255)))
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        _appearance.IndicatorColor = ToHex(dialog.Color);
        ApplyAndSaveAppearance(resizeWindow: false);
    }

    private void AppearanceControl_Changed(object sender, RoutedEventArgs e)
    {
        if (!_appearanceReady) return;
        var oldRestStyle = EffectiveRestStyle;
        ReadAppearanceControls();
        ApplyAndSaveAppearance(resizeWindow: false);
        if (oldRestStyle != EffectiveRestStyle && !_expanded && _activeReminder is null)
            Collapse();
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleValueText is null) return;
        ScaleValueText.Text = $"{e.NewValue:0}%";
        if (!_appearanceReady) return;
        ReadAppearanceControls();
        ApplyAndSaveAppearance(resizeWindow: true);
    }

    private void ApplyAndSaveAppearance(bool resizeWindow)
    {
        ApplyAppearance(resizeWindow);
        try { _appearanceService.Save(_appearance); ((App)Application.Current).SettingsChangedFromIsland(); } catch { }
    }

    private void SaveAppearance_Click(object sender, RoutedEventArgs e)
    {
        ReadAppearanceControls();
        ApplyAndSaveAppearance(resizeWindow: true);
        ShowCompact("个性化设置已保存");
    }

    private void ResetAppearance_Click(object sender, RoutedEventArgs e)
    {
        _appearanceReady = false;
        var defaults = new AppearanceSettings();
        foreach (var property in typeof(AppearanceSettings).GetProperties())
            if (property.Name is not ("ShowIsland" or "ShowLyrics" or "IslandLeft" or "IslandCenter" or "IslandTop" or "IslandMonitor" or "FloatingLyricsLeft" or "FloatingLyricsTop" or "FloatingLyricsMonitor")) property.SetValue(_appearance, property.GetValue(defaults));
        LoadAppearanceControls();
        UpdateIslandLockVisual();
        _appearanceReady = true;
        ApplyAndSaveAppearance(resizeWindow: true);
        ShowCompact("已恢复默认外观");
    }

    private void CheckReminders()
    {
        foreach (var due in _scheduler.GetDue(DateTime.Now))
        {
            _scheduler.MarkFired(due.Task, due.Occurrence);
            _reminderQueue.Enqueue(due);
        }
        _taskStore.Save(_tasks);
        TasksPanel.Refresh();
        ShowNextReminder();
    }

    private void ShowNextReminder()
    {
        if (_activeReminder is not null || _reminderQueue.Count == 0) return;
        while (_reminderQueue.Count > 0)
        {
            var candidate = _reminderQueue.Peek();
            if (_tasks.Contains(candidate.Task) && !candidate.Task.Done &&
                !(candidate.Task.LastCompletedFor is { } completed && completed >= candidate.Occurrence)) break;
            _reminderQueue.Dequeue();
        }
        if (_reminderQueue.Count == 0) return;
        if (!IsVisible) _reminderWasHidden = true;
        _greetingVisible = false; _greetingExpiry.Stop();
        _activeReminder = _reminderQueue.Dequeue();
        ReminderCardTitle.Text = _activeReminder.Value.Task.Title;
        var at = _activeReminder.Value.Occurrence;
        var day = at.Date == DateTime.Today ? "今天" : at.Date == DateTime.Today.AddDays(-1) ? "昨天" : at.ToString("MM-dd");
        var repeat = _activeReminder.Value.Task.Remind?.Repeat;
        ReminderCardTime.Text = $"{day} {at:HH:mm}" + (repeat == RepeatKind.Daily ? " · 每天重复" : repeat == RepeatKind.Weekly ? " · 每周重复" : "");
        MorphTo(RemindShape, expanding: true);
        if (!IsVisible) ((App)Application.Current).SetIslandVisible(true);
    }

    private void ResolveReminder(string action, DateTime? snooze = null)
    {
        if (_activeReminder is not { } active) return;
        if (action == "已完成") _scheduler.Complete(active.Task, DateTime.Now);
        else if (snooze.HasValue) _scheduler.Snooze(active.Task, snooze.Value);
        else _scheduler.Dismiss(active.Task, active.Occurrence);
        _taskStore.Save(_tasks); TasksPanel.Refresh();
        _activeReminder = null;
        ShowCompact($"{action}：{active.Task.Title}");
        StopHeaderGreeting();
        _expanded = false;
        _compact = EffectiveRestStyle != "Normal";
        MorphTo(_compact ? CompactShape : CollapsedShape, expanding: false);
        ShowNextReminder();
        if (_activeReminder is null && _reminderWasHidden)
        { _reminderWasHidden = false; ((App)Application.Current).SetIslandVisible(false); }
    }
    private void CompleteReminder_Click(object sender, RoutedEventArgs e) => ResolveReminder("已完成");
    private void DismissReminder_Click(object sender, RoutedEventArgs e) => ResolveReminder("已忽略");
    private void SnoozeReminder_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        foreach (var (title, delay) in new[] { ("10 分钟", TimeSpan.FromMinutes(10)), ("30 分钟", TimeSpan.FromMinutes(30)), ("1 小时", TimeSpan.FromHours(1)), ("明天同一时间", TimeSpan.FromDays(1)) })
        {
            var item = new System.Windows.Controls.MenuItem { Header = title };
            item.Click += (_, _) => ResolveReminder("已延后", DateTime.Now + delay);
            menu.Items.Add(item);
        }
        TrackAutoCollapseMenu(menu);
        menu.PlacementTarget = (UIElement)sender; menu.IsOpen = true;
    }

    private async void StartDownload_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(DownloadUrlInput.Text.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ShowCompact("请粘贴 http 或 https 下载链接");
            return;
        }

        var suggestedName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(suggestedName)) suggestedName = "download.bin";
        var dialog = new SaveFileDialog { FileName = suggestedName, Title = "选择保存位置" };
        if (dialog.ShowDialog(this) != true) return;

        var progress = new Progress<DownloadProgressInfo>(info =>
        {
            DownloadProgress.Value = info.Percent;
            DownloadStatus.Text = info.FileName;
            DownloadDetail.Text = info.Status;
            ShowCompact($"⇩ {info.FileName} · {info.Status}");
        });

        try
        {
            await _download.DownloadAsync(uri.ToString(), dialog.FileName, progress, CancellationToken.None);
        }
        catch (Exception ex)
        {
            DownloadStatus.Text = $"下载失败：{ex.Message}";
            ShowCompact("下载失败");
        }
    }

    public async Task TakeScreenshotAsync()
    {
        if (_activeReminder is null) Collapse();
        await Task.Delay(300);
        try { Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); }
        catch
        {
            try { Process.Start(new ProcessStartInfo("snippingtool.exe", "/clip") { UseShellExecute = true }); }
            catch { ShowCompact("无法打开系统截图工具"); }
        }
    }
    private async void Screenshot_Click(object sender, RoutedEventArgs e) => await TakeScreenshotAsync();

    private void CenterWindow_Click(object sender, RoutedEventArgs e) => CenterAtTop();

    private void Exit_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).ExitApplication();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (Application.Current is App app && !app.IsExiting) { e.Cancel = true; app.SetIslandVisible(false); }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _music.StateChanged -= RenderMusic;
        _reminderTimer.Stop();
        _volume.Dispose();
        _autoCollapseDelay.Stop();
        _greetingDelay.Stop(); _greetingExpiry.Stop();
        _compactTimer.Stop(); _snapTimer?.Stop();
        StopHeaderGreeting();
        base.OnClosed(e);
    }
}
