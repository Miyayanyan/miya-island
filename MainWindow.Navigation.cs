using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MiyaIsland;

public partial class MainWindow
{
    private int _selectedTab;
    private void UpdateNavigation(UIElement panel)
    {
        var panels = new UIElement[] { MusicPanel, ReminderPanel, DownloadPanel, ScreenshotPanel, SettingsPanel };
        _selectedTab = Array.IndexOf(panels, panel);
        var buttons = new[] { NavMusic, NavTasks, NavDownload, NavScreenshot, NavSettings };
        var icons=new[]{NavMusicIcon,NavTasksIcon,NavDownloadIcon,NavScreenshotIcon,NavSettingsIcon};
        var keys=new[]{"Music","Tasks","Download","Screenshot","Settings"};
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].SetResourceReference(Control.ForegroundProperty, i == _selectedTab ? "Brush.Ink" : "Brush.Ink2");
            icons[i].Filled=i==_selectedTab;icons[i].Data=(Geometry)FindResource("Icon."+keys[i]+(i==_selectedTab?"Filled":""));
        }
        var width = ExpandedContent.Width / 5 - .8;
        NavLens.BeginAnimation(WidthProperty, new DoubleAnimation(width, TimeSpan.FromMilliseconds(240)));
        NavLensPosition.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(_selectedTab * width, TimeSpan.FromMilliseconds(240))
            { EasingFunction = new BackEase { Amplitude = .18, EasingMode = EasingMode.EaseOut } });
    }

    private void NavTrack_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right)) return;
        var panels = new UIElement[] { MusicPanel, ReminderPanel, DownloadPanel, ScreenshotPanel, SettingsPanel };
        var buttons = new[] { NavMusic, NavTasks, NavDownload, NavScreenshot, NavSettings };
        var index = (_selectedTab + (e.Key == Key.Right ? 1 : 4)) % 5;
        ShowPanel(panels[index]); buttons[index].Focus(); e.Handled = true;
    }
}
