using System.Windows;
using MiyaIsland.Services;
namespace MiyaIsland;
public partial class MainWindow
{
    private BackdropCapture? _backdrop;
    private void UpdateBlur()
    {
        if(BackdropImage is null || _appearance is null)return;
        _backdrop??=new BackdropCapture(this,BackdropImage,()=>_appearance.ReduceMotion?4:_expanded||_greetingVisible||_activeReminder is not null?15:8,()=>((App)Application.Current).BackgroundBlurFailed());
        _backdrop.SetEnabled(_appearance.BackgroundBlur,_appearance.HideFromCapture);
        BackdropLayer.Visibility=_backdrop.Enabled?Visibility.Visible:Visibility.Collapsed;
        BackdropImage.Margin=new Thickness(-(CanvasWidth-_currentShape.Width)/2,-CanvasMargin,0,0);
        _backdrop.SetRadius(_currentShape.Height,_appearance.BlurStrength);
        ShadowPlate.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty,"Brush.IslandBase");
    }
    public void NotifyBackdropFailure() => ShowCompact("背景模糊暂时不可用");
}
