using System.Windows;
using System.Windows.Media.Animation;
namespace MiyaIsland;
public partial class MainWindow
{
    private bool _glassMenuOpen;
    private string EffectiveRestStyle => _appearance.QuietMode?"Orb":_appearance.RestStyle;
    private void QuietMode_Changed(object sender,RoutedEventArgs e)
    {
        if(!_appearanceReady)return;_appearance.QuietMode=QuietModeCheck.IsChecked==true;RefreshAppearance();
        ApplyAndSaveAppearance(true);
    }
    private void FlashDispersion()
    {
        Dispersion.BeginAnimation(OpacityProperty,null);Dispersion.Opacity=0;
        if(_appearance.ReduceMotion || _appearance.QuietMode)return;
        var flash=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromMilliseconds(400)};
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(.55,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
        Dispersion.BeginAnimation(OpacityProperty,flash);
    }
}
