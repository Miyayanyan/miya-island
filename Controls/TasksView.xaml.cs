using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MiyaIsland.Models;
using MiyaIsland.Services;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;

namespace MiyaIsland.Controls;

public partial class TasksView : System.Windows.Controls.UserControl
{
    private IList<TaskItem> _tasks = new List<TaskItem>();
    private ReminderScheduler? _scheduler;
    private TaskReminder? _selection;
    private TaskItem? _editing;
    private readonly HashSet<ContextMenu> _openMenus = [];
    private readonly Dictionary<Guid,string> _titleDrafts = [];
    public event Action? PopupActivityChanged;
    public bool HasOpenMenu => _openMenus.Count > 0;
    public event Action? Changed;
    public string DefaultReminderTime { get; set; } = "09:00";
    public bool IsEditorOpen => TimePopup.IsOpen;
    public TasksView() { InitializeComponent(); TimePopup.Opened += (_, _) => PopupActivityChanged?.Invoke(); TimePopup.Closed += (_, _) => PopupActivityChanged?.Invoke(); }
    public void Attach(IList<TaskItem> tasks, ReminderScheduler scheduler) { _tasks = tasks; _scheduler = scheduler; Refresh(); }
    private Brush Ink(string key) => (Brush)FindResource(key);
    public void Refresh()
    {
        ActiveRows.Children.Clear(); CompletedRows.Children.Clear();
        foreach (var task in _tasks.OrderBy(t => t.CreatedAt))
            (TaskPresentation.IsCompleted(task, DateTime.Now) ? CompletedRows : ActiveRows).Children.Add(Row(task));
        EmptyHint.Visibility = _tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CompletedHeader.Visibility = CompletedRows.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        CompletedToggle.Content = $"已完成 ({CompletedRows.Children.Count})";
    }
    private Grid Row(TaskItem task)
    {
        var row = new Grid { Height = 36 };
        foreach (var width in new[] { new GridLength(22), new GridLength(1, GridUnitType.Star), GridLength.Auto, new GridLength(24) }) row.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        var separator = new Border { BorderThickness = new Thickness(0,0,0,1), IsHitTestVisible = false }; separator.SetResourceReference(Border.BorderBrushProperty,"Brush.Separator"); Grid.SetColumnSpan(separator,4); row.Children.Add(separator);
        var check = new CheckBox { Style = (Style)FindResource("TaskCheck"), IsChecked = TaskPresentation.IsCompleted(task, DateTime.Now), VerticalAlignment = VerticalAlignment.Center };
        check.Click += (_, _) => { if (check.IsChecked == true) _scheduler!.Complete(task, DateTime.Now); else _scheduler!.Uncomplete(task); Commit(); };
        row.Children.Add(check);
        var title = new TextBlock { Text = task.Title, FontSize = 13, Margin = new Thickness(8,0,8,0), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = task.Title };
        title.SetResourceReference(TextBlock.ForegroundProperty,"Brush.Ink"); Grid.SetColumn(title, 1); row.Children.Add(title);
        if (task.Remind is { } rule)
        {
            var text = TaskPresentation.Label(task, DateTime.Now);
            var badge = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center };
            badge.SetResourceReference(Border.BackgroundProperty,"Brush.Well");
            var contents = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            contents.Children.Add(ThemeService.Dynamic(new IconView { Width = 11, Height = 11, Data = (Geometry)FindResource(rule.Repeat == RepeatKind.None ? "Icon.Bell" : "Icon.Repeat"), Margin = new Thickness(0,0,3,0) },IconView.ForegroundProperty,"Brush.Ink2"));
            var next = _scheduler!.NextOccurrence(task, DateTime.Now);
            var color = text.StartsWith("已过期") ? "Brush.Overdue" : next?.Date == DateTime.Today && !TaskPresentation.IsCompleted(task,DateTime.Now) ? "Brush.Warning" : "Brush.Ink2";
            contents.Children.Add(ThemeService.Dynamic(new TextBlock { Text = text, FontSize = 11, MaxWidth = 165, TextTrimming = TextTrimming.CharacterEllipsis,Typography={NumeralAlignment=FontNumeralAlignment.Tabular} },TextBlock.ForegroundProperty,color));
            badge.Child = contents; Grid.SetColumn(badge, 2); row.Children.Add(badge);
        }
        var more = new Button { Style = (Style)FindResource("Button.Icon"), Width = 24, Height = 24, Opacity = 0, Content = new IconView { Width = 15, Height = 15, Data = (Geometry)FindResource("Icon.More") }, ToolTip = "更多" };
        Grid.SetColumn(more, 3); row.Children.Add(more);
        row.MouseEnter += (_, _) => more.Opacity = 1; row.MouseLeave += (_, _) => more.Opacity = more.IsKeyboardFocusWithin ? 1 : 0;
        more.GotKeyboardFocus += (_, _) => more.Opacity = 1;
        var menu = new ContextMenu();
        menu.Opened += (_, _) => { _openMenus.Add(menu); PopupActivityChanged?.Invoke(); };
        menu.Closed += (_, _) => { _openMenus.Remove(menu); PopupActivityChanged?.Invoke(); };
        var edit = new MenuItem { Header = "编辑" }; edit.Click += (_, _) =>
        {
            BeginTitleEdit(row, title, task, true);
            _editing = task; SetEditor(task.Remind); TimePopup.PlacementTarget = TimeButton; TimePopup.IsOpen = true;
        };
        var delete = new MenuItem { Header = "删除" }; delete.Click += (_, _) => { _tasks.Remove(task); Commit(); };
        menu.Items.Add(edit); menu.Items.Add(delete); more.Click += (_, _) => { menu.PlacementTarget = more; menu.IsOpen = true; };
        if (_titleDrafts.ContainsKey(task.Id)) BeginTitleEdit(row, title, task, false);
        return row;
    }
    private void BeginTitleEdit(Grid row, TextBlock title, TaskItem task, bool focus)
    {
        var input = new TextBox { Text = _titleDrafts.GetValueOrDefault(task.Id,task.Title), VerticalAlignment = VerticalAlignment.Center, FontSize = 13, Margin = new Thickness(8,0,8,0) };
        _titleDrafts[task.Id] = input.Text;
        input.TextChanged += (_, _) => _titleDrafts[task.Id] = input.Text;
        Grid.SetColumn(input,1);row.Children.Remove(title);row.Children.Add(input);
        input.KeyDown += (_, e) =>
        {
            if(e.Key!=Key.Enter || string.IsNullOrWhiteSpace(input.Text))return;
            task.Title=input.Text.Trim();_titleDrafts.Remove(task.Id);Commit();e.Handled=true;
        };
        if(focus){input.Focus();input.SelectAll();}
    }
    private void Commit() { Changed?.Invoke(); Refresh(); }
    private void Add_Click(object sender, RoutedEventArgs e) => Add();
    private void Title_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter) { Add(); e.Handled = true; } }
    private void Add() { if (string.IsNullOrWhiteSpace(TitleInput.Text)) return; _tasks.Add(new TaskItem { Title = TitleInput.Text.Trim(), CreatedAt = DateTime.Now, Remind = _selection }); TitleInput.Clear(); _selection = null; TimeButton.Content = "不提醒"; Commit(); }
    private void Time_Click(object sender, RoutedEventArgs e) { _editing = null; SetEditor(_selection); TimePopup.PlacementTarget = TimeButton; TimePopup.IsOpen = true; }
    private void SetEditor(TaskReminder? rule)
    {
        Preset.SelectedIndex = rule is null ? 0 : 3;
        DateInput.Text = rule?.Date ?? DateTime.Today.ToString("yyyy-MM-dd"); TimeInput.Text = rule?.Time ?? DefaultReminderTime;
        RepeatNone.IsChecked = rule?.Repeat is null or RepeatKind.None; RepeatDaily.IsChecked = rule?.Repeat == RepeatKind.Daily; RepeatWeekly.IsChecked = rule?.Repeat == RepeatKind.Weekly;
        foreach (ToggleButton day in Weekdays.Children) day.IsChecked = rule?.Weekdays.Contains((DayOfWeek)int.Parse((string)day.Tag)) == true;
        ErrorText.Text = string.Empty;
    }
    private void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DateInput is null) return;
        var date = Preset.SelectedIndex == 2 ? DateTime.Today.AddDays(1).AddHours(9) : DateTime.Now.Date.AddMinutes((Math.Floor(DateTime.Now.TimeOfDay.TotalMinutes / 30) + 1) * 30);
        if (Preset.SelectedIndex is 1 or 2) { DateInput.Text = date.ToString("yyyy-MM-dd"); TimeInput.Text = date.ToString("HH:mm"); }
    }
    private void Repeat_Changed(object sender, RoutedEventArgs e) { if (Weekdays is not null) Weekdays.Visibility = RepeatWeekly.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; }
    private void ApplyTime_Click(object sender, RoutedEventArgs e)
    {
        TaskReminder? reminder = null;
        var repeat = RepeatDaily.IsChecked == true ? RepeatKind.Daily : RepeatWeekly.IsChecked == true ? RepeatKind.Weekly : RepeatKind.None;
        var days = Weekdays.Children.OfType<ToggleButton>().Where(b => b.IsChecked == true).Select(b => (DayOfWeek)int.Parse((string)b.Tag));
        if (Preset.SelectedIndex != 0 && !TaskPresentation.TryCreateReminder(DateInput.Text.Trim(), TimeInput.Text.Trim(), repeat, days, out reminder, out var error))
        { ErrorText.Text = error; DateInput.BorderBrush = TimeInput.BorderBrush = Ink("Brush.Overdue"); return; }
        DateInput.ClearValue(BorderBrushProperty); TimeInput.ClearValue(BorderBrushProperty);
        if (_editing is { } task) { task.Remind = reminder; task.LastFiredFor = task.LastCompletedFor = task.SnoozedUntil = null; Commit(); }
        else { _selection = reminder; TimeButton.Content = reminder is null ? "不提醒" : TaskPresentation.Label(new TaskItem { Remind = reminder }, DateTime.Now); }
        TimePopup.IsOpen = false;
    }
    private void Completed_Click(object sender, RoutedEventArgs e) => CompletedRows.Visibility = CompletedRows.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void Clear_Click(object sender, RoutedEventArgs e) { foreach (var task in _tasks.Where(t => TaskPresentation.IsCompleted(t, DateTime.Now)).ToList()) _tasks.Remove(task); Commit(); }
}
