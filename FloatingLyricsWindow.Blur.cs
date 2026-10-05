using System.Windows;
using MiyaIsland.Services;
namespace MiyaIsland;
public partial class FloatingLyricsWindow
{
    private BackdropCapture? _backdrop;
    private void ApplyBlur()
    {
        _backdrop??=new BackdropCapture(this,BackdropImage,()=>_preferences.ReduceMotion?4:_expanded?15:8,()=>((App)Application.Current).BackgroundBlurFailed());
        _backdrop.SetEnabled(_preferences.BackgroundBlur,_preferences.HideFromCapture);_backdrop.SetRadius(LyricHost.ActualHeight>0?LyricHost.ActualHeight:44,_preferences.BlurStrength);
        BackdropLayer.Visibility=_backdrop.Enabled?Visibility.Visible:Visibility.Collapsed;
        ShadowPlate.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty,"Brush.IslandBase");
    }
}
