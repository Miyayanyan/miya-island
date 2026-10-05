using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MiyaIsland;
public sealed class AvatarCropWindow : Window
{
    private readonly BitmapSource _source;
    private readonly Image _image;
    private readonly Canvas _preview;
    private readonly Slider _zoom;
    private Point? _press;
    private Point _origin;
    private double _x,_y,_scale;
    public Int32Rect CropRect {get;private set;}
    public AvatarCropWindow(BitmapSource source)
    {
        _source=source;Title="裁剪头像";Width=360;Height=440;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
        SetResourceReference(ForegroundProperty,"Brush.Ink");
        var panel=new StackPanel {Margin=new Thickness(24)};
        var shell=new Grid {Margin=new Thickness(8)};Content=shell;
        shell.Children.Add(MiyaIsland.Services.ThemeService.Dynamic(new Border {CornerRadius=new CornerRadius(24),Effect=new System.Windows.Media.Effects.DropShadowEffect {BlurRadius=24,ShadowDepth=4,Opacity=.3}},Border.BackgroundProperty,"Brush.PopupBase"));
        shell.Children.Add(MiyaIsland.Services.ThemeService.Dynamic(new Border {CornerRadius=new CornerRadius(24)},Border.BackgroundProperty,"Brush.GlassSheen"));shell.Children.Add(panel);
        panel.Children.Add(new TextBlock {Text="拖动调整头像",FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
        _preview=new Canvas {Width=240,Height=240,Background=(Brush)Application.Current.FindResource("Brush.Well"),Clip=new RectangleGeometry(new Rect(0,0,240,240))};
        _image=new Image {Source=source,Stretch=Stretch.Fill};_preview.Children.Add(_image);
        _preview.Children.Add(new System.Windows.Shapes.Path {Data=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,240,240)),new EllipseGeometry(new Point(120,120),120,120)),Fill=(Brush)Application.Current.FindResource("Brush.CropMask"),IsHitTestVisible=false});
        panel.Children.Add(_preview);
        _zoom=new Slider {Minimum=100,Maximum=300,Value=100,Margin=new Thickness(0,16,0,0)};panel.Children.Add(_zoom);
        panel.Children.Add(new TextBlock {Text="缩放 100%–300% · 滚轮也可以",FontSize=11,Margin=new Thickness(0,4,0,12)});
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(buttons);
        var cancel=new Button {Content="取消",Style=(Style)Application.Current.FindResource("Button.Ghost")};cancel.Click+=(_,_)=>DialogResult=false;buttons.Children.Add(cancel);
        var confirm=new Button {Content="确定",Style=(Style)Application.Current.FindResource("Button.Primary")};confirm.Click+=(_,_)=>{var size=Math.Min(Math.Min(_source.PixelWidth,_source.PixelHeight),(int)Math.Floor(240/_scale));CropRect=new Int32Rect(Math.Clamp((int)Math.Round(-_x/_scale),0,_source.PixelWidth-size),Math.Clamp((int)Math.Round(-_y/_scale),0,_source.PixelHeight-size),size,size);DialogResult=true;};buttons.Children.Add(confirm);
        panel.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is TextBlock && e.LeftButton==MouseButtonState.Pressed)DragMove();};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){DialogResult=false;e.Handled=true;}};
        _zoom.ValueChanged+=(_,_)=>LayoutImage(true);
        _preview.MouseLeftButtonDown+=(_,e)=>{_press=e.GetPosition(_preview);_origin=new Point(_x,_y);_preview.CaptureMouse();};
        _preview.MouseMove+=(_,e)=>{if(_press is not {} start)return;var delta=e.GetPosition(_preview)-start;_x=_origin.X+delta.X;_y=_origin.Y+delta.Y;Clamp();};
        _preview.MouseLeftButtonUp+=(_,_)=>{_press=null;_preview.ReleaseMouseCapture();};_preview.LostMouseCapture+=(_,_)=>_press=null;
        _preview.MouseWheel+=(_,e)=>{_zoom.Value+=e.Delta>0?10:-10;e.Handled=true;};LayoutImage(false);
    }
    private void LayoutImage(bool preserveCenter)
    {
        var next=Math.Max(240d/_source.PixelWidth,240d/_source.PixelHeight)*_zoom.Value/100;
        if(preserveCenter && _scale>0){_x=120-(120-_x)*next/_scale;_y=120-(120-_y)*next/_scale;}
        else{_x=(240-_source.PixelWidth*next)/2;_y=(240-_source.PixelHeight*next)/2;}
        _scale=next;_image.Width=_source.PixelWidth*_scale;_image.Height=_source.PixelHeight*_scale;Clamp();
    }
    private void Clamp(){_x=Math.Clamp(_x,240-_image.Width,0);_y=Math.Clamp(_y,240-_image.Height,0);Canvas.SetLeft(_image,_x);Canvas.SetTop(_image,_y);}
}
