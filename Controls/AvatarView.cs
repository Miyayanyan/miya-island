using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using MiyaIsland.Models;
namespace MiyaIsland.Controls;
public sealed class AvatarView : Grid
{
    private readonly Ellipse _photo=new();
    private readonly Ellipse _ring=new(){StrokeThickness=2,Margin=new Thickness(-3),RenderTransformOrigin=new Point(.5,.5)};
    private readonly Ellipse _badge=new(){Width=9,Height=9,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,-2,-2),RenderTransformOrigin=new Point(.5,.5)};
    public AvatarView(){IsHitTestVisible=false;Children.Add(_photo);Children.Add(_ring);Children.Add(_badge);}
    public void Configure(ImageSource image,AppearanceSettings settings,Ellipse indicator)
    {
        _photo.Fill=new ImageBrush(image){Stretch=Stretch.UniformToFill};
        _ring.Visibility=settings.BreathingStyle=="Badge"?Visibility.Collapsed:Visibility.Visible;
        _badge.Visibility=settings.BreathingStyle=="Badge"?Visibility.Visible:Visibility.Collapsed;
        _ring.SetBinding(Shape.StrokeProperty,new Binding("Fill"){Source=indicator});
        _badge.SetBinding(Shape.FillProperty,new Binding("Fill"){Source=indicator});
        foreach(var element in new[]{_ring,_badge})element.SetBinding(OpacityProperty,new Binding("Opacity"){Source=indicator});
        var original=(ScaleTransform)indicator.RenderTransform;
        var ringScale=new ScaleTransform();_ring.RenderTransform=ringScale;
        BindingOperations.SetBinding(ringScale,ScaleTransform.ScaleXProperty,new Binding("ScaleX"){Source=original,Converter=new RingScale(settings.BreathingEnabled)});
        BindingOperations.SetBinding(ringScale,ScaleTransform.ScaleYProperty,new Binding("ScaleY"){Source=original,Converter=new RingScale(settings.BreathingEnabled)});
        var badgeScale=new ScaleTransform();_badge.RenderTransform=badgeScale;
        BindingOperations.SetBinding(badgeScale,ScaleTransform.ScaleXProperty,new Binding("ScaleX"){Source=original});BindingOperations.SetBinding(badgeScale,ScaleTransform.ScaleYProperty,new Binding("ScaleY"){Source=original});
    }
    public void SetOrb(bool orb,AppearanceSettings settings)
    {
        var badge=settings.BreathingStyle=="Badge" && !orb;
        _badge.Visibility=badge?Visibility.Visible:Visibility.Collapsed;
        _ring.Visibility=badge?Visibility.Collapsed:Visibility.Visible;
    }
    private sealed class RingScale(bool enabled):IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>enabled?1+Math.Clamp(((double)value-.92)/.26,0,1)*.08:1;
        public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
    }
}
