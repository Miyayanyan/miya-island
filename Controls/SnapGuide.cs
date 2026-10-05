using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MiyaIsland.Controls;

/// <summary>拖动对齐时显示的细参考线：一个不抢焦点、不挡鼠标的透明置顶小窗口。</summary>
public sealed class SnapGuide : Window
{
    public SnapGuide(Window owner)
    {
        Owner = owner;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false; Focusable = false;
        var line = new Border { Opacity = .85 };
        line.SetResourceReference(Border.BackgroundProperty, "Brush.Accent");
        Content = line;
    }

    public void ShowAt(Rect rect)
    {
        Left = rect.Left; Top = rect.Top; Width = Math.Max(1, rect.Width); Height = Math.Max(1, rect.Height);
        if (!IsVisible) Show();
    }
}
