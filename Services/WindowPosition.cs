using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace MiyaIsland.Services;

internal static class WindowPosition
{
    public static Forms.Screen Screen(Window window)
    {
        if (PresentationSource.FromVisual(window) is null) return Forms.Screen.PrimaryScreen!;
        var point = window.PointToScreen(new Point(window.ActualWidth / 2, 24));
        return Forms.Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y));
    }
    public static WorkArea Area(Window window, Forms.Screen screen)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        var area = screen.WorkingArea;
        return new(area.Left/dpi.DpiScaleX, area.Top/dpi.DpiScaleY, area.Width/dpi.DpiScaleX, area.Height/dpi.DpiScaleY);
    }
    public static DispatcherTimer Slide(Window window, double left, double top, Action<double,double> step, Action done)
    {
        var originLeft=window.Left; var originTop=window.Top;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_,_) =>
        {
            var progress=Math.Clamp(watch.Elapsed.TotalMilliseconds/160,0,1);
            var eased=1-Math.Pow(1-progress,3);
            step(originLeft+(left-originLeft)*eased, originTop+(top-originTop)*eased);
            if(progress>=1) { timer.Stop(); done(); }
        };
        timer.Start(); return timer;
    }
}
