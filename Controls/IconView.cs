using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MiyaIsland.Controls;

/// <summary>
/// 绘制 Themes/Icons.xaml 中的 24×24 描边图标。
/// 图标随控件尺寸缩放，但描边粗细保持 StrokeThickness（DIP），小尺寸也不会变粗或变细。
/// Foreground 会从父级继承，放进按钮里自动跟随按钮文字颜色。
/// </summary>
public sealed class IconView : FrameworkElement
{
    private const double GridSize = 24;

    public static readonly DependencyProperty FilledProperty=DependencyProperty.Register(nameof(Filled),typeof(bool),typeof(IconView),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public bool Filled {get=>(bool)GetValue(FilledProperty);set=>SetValue(FilledProperty,value);}
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(Geometry),
        typeof(IconView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(IconView),
        new FrameworkPropertyMetadata(
            Brushes.White,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(IconView),
        new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

    public IconView()
    {
        Width = 16;
        Height = 16;
        IsHitTestVisible = false;
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public Brush? Foreground
    {
        get => (Brush?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var data = Data;
        var size = Math.Min(ActualWidth, ActualHeight);
        if (data is null || Foreground is null || size <= 0) return;

        var scale = size / GridSize;
        var offsetX = (ActualWidth - size) / 2;
        var offsetY = (ActualHeight - size) / 2;
        var pen = new Pen(Foreground, StrokeThickness / scale)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        drawingContext.PushTransform(new MatrixTransform(scale, 0, 0, scale, offsetX, offsetY));
        drawingContext.DrawGeometry(Filled?Foreground:null, Filled?null:pen, data);
        drawingContext.Pop();
    }
}
