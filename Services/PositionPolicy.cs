namespace MiyaIsland.Services;

public readonly record struct WorkArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public double Center => Left + Width / 2;
}
public static class PositionPolicy
{
    public static (double Center, double Top) Snap(double center, double top, WorkArea area, double defaultTop, bool alt)
        => alt ? (center,top) : (Math.Abs(center-area.Center) <= 16 ? area.Center : center, Math.Abs(top-defaultTop) <= 10 ? defaultTop : top);
    /// <summary>
    /// 拖动过程中的磁吸（像 PS 的参考线）：靠近屏幕水平中线或默认高度时吸过去，并告诉调用方要显示哪条参考线。
    /// 按住 Alt 暂时不吸附。
    /// </summary>
    public static (double Center, double Top, bool CenterGuide, bool TopGuide) LiveSnap(double center, double top, WorkArea area, double defaultTop, bool alt, double centerRange = 24, double topRange = 14)
    {
        if (alt) return (center, top, false, false);
        var snapCenter = Math.Abs(center - area.Center) <= centerRange;
        var snapTop = Math.Abs(top - defaultTop) <= topRange;
        return (snapCenter ? area.Center : center, snapTop ? defaultTop : top, snapCenter, snapTop);
    }
    public static bool CanRestore(double left, double top, double width, double height, WorkArea area)
        => double.IsFinite(left) && double.IsFinite(top) &&
        Math.Min(left+width,area.Right)-Math.Max(left,area.Left) >= Math.Min(80,width) &&
        Math.Min(top+height,area.Bottom)-Math.Max(top,area.Top) >= Math.Min(30,height);
}
