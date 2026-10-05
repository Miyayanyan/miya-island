using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MiyaIsland.Services;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace MiyaIsland;

public partial class MainWindow
{
    private bool? _equalizerPlaying;
    private bool _pillHasReminder;
    private void UpdatePillReminderDot() => PillReminderDot.Visibility=_compact && !_expanded && !_greetingVisible && _activeReminder is null && EffectiveRestStyle=="Pill" && _pillHasReminder?Visibility.Visible:Visibility.Collapsed;
    private void HideIsland_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).SetIslandVisible(false);
    private void CompactMode_Click(object sender, RoutedEventArgs e)
    {
        Collapse();
    }
    private void UpdateCompactContent()
    {
        var summary=_scheduler.GetSummary(DateTime.Now);
        var overdue=summary.OverdueCount>0;
        var due=overdue || _reminderQueue.Count>0 || _activeReminder is not null || (summary.Next?.Time is DateTime at && at<=DateTime.Now.AddMinutes(30));
        _pillHasReminder=due;UpdatePillReminderDot();
        PillReminderDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,overdue?"Brush.Overdue":"Brush.Warning");
        Equalizer.Visibility=_lastMediaPlaying?Visibility.Visible:Visibility.Collapsed;
        CompactClock.Text=DateTime.Now.ToString("HH:mm");
        UpdateAvatarGeometry();
        if(_compact && EffectiveRestStyle=="Pill" && !_appearance.AvatarEnabled)StatusIndicator.Visibility=_lastMediaPlaying?Visibility.Hidden:Visibility.Visible;
        if (_equalizerPlaying == _lastMediaPlaying) return;
        _equalizerPlaying = _lastMediaPlaying;
        if (_lastMediaPlaying)
            foreach (var (bar, height) in new[] { (Bar1, 5d), (Bar2, 11d), (Bar3, 7d) })
                bar.BeginAnimation(HeightProperty, new DoubleAnimation(4, height, TimeSpan.FromMilliseconds(450 + height*20)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        else foreach (var bar in new[] { Bar1, Bar2, Bar3 }) bar.BeginAnimation(HeightProperty, null);
    }
    private void Compact_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_compact || _activeReminder is not null) return;
        _snapTimer?.Stop();
        _compactPress = PointToScreen(e.GetPosition(this)); _dragOrigin = new Point(Left,Top); _compactDragging = false;
        IslandHost.CaptureMouse(); e.Handled = true;
    }
    private void Compact_MouseMove(object sender, MouseEventArgs e)
    {
        if (_compactPress is not { } origin || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = PointToScreen(e.GetPosition(this)) - origin;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        delta = new Vector(delta.X / dpi.DpiScaleX, delta.Y / dpi.DpiScaleY);
        if (delta.Length > 4) _compactDragging = true;
        if (_compactDragging && !_appearance.IslandLocked)
        { Left = _dragOrigin.X+delta.X; Top = _dragOrigin.Y+delta.Y; _horizontalCenter = Left+Width/2; }
        e.Handled = true;
    }
    private void Compact_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_compactPress is null) return;
        _compactPress = null; IslandHost.ReleaseMouseCapture();
        if (!_compactDragging) Expand();
        else if (!_appearance.IslandLocked) SnapAfterDrag();
        _compactDragging = false;
        e.Handled = true;
    }
    private void RestorePosition()
    {
        var screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _appearance.IslandMonitor);
        if (screen is not null && _appearance.IslandLeft is { } left && _appearance.IslandTop is { } top &&
            PositionPolicy.CanRestore(left,top,_currentShape.Width*GetScale(),_currentShape.Height*GetScale(),WindowPosition.Area(this,screen)))
        { _horizontalCenter = _appearance.IslandCenter ?? left + _currentShape.Width*GetScale()/2; Left = _horizontalCenter - Width/2; Top = top - CanvasMargin*GetScale(); }
        else CenterAtTop();
    }
    private void SavePosition()
    {
        _appearance.IslandLeft = _horizontalCenter - _currentShape.Width*GetScale()/2;
        _appearance.IslandCenter = _horizontalCenter;
        _appearance.IslandTop = Top + CanvasMargin*GetScale();
        _appearance.IslandMonitor = WindowPosition.Screen(this).DeviceName;
        try { _appearanceService.Save(_appearance); } catch (Exception ex) { System.Diagnostics.Trace.TraceWarning(ex.Message); }
    }
    private void SnapAfterDrag()
    {
        var area = WindowPosition.Area(this, WindowPosition.Screen(this));
        var snapped = PositionPolicy.Snap(_horizontalCenter, Top+CanvasMargin*GetScale(), area, area.Top+8, Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        _snapTimer?.Stop();
        _snapTimer = WindowPosition.Slide(this, snapped.Center-Width/2, snapped.Top-CanvasMargin*GetScale(),
            (left,top) => { _horizontalCenter = left+Width/2; Left = _horizontalCenter-Width/2; Top = top; }, SavePosition);
    }
}
