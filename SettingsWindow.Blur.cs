using System.Windows;
using System.Windows.Controls;
using MiyaIsland.Services;
namespace MiyaIsland;
public partial class SettingsWindow
{
    private BackdropCapture? _backdrop;
    public void ApplyBackdrop()
    {
        _backdrop??=new BackdropCapture(this,BackdropImage,()=>_settings.ReduceMotion?4:15,()=>((App)Application.Current).BackgroundBlurFailed());
        _backdrop.SetEnabled(_settings.BackgroundBlur,_settings.HideFromCapture);_backdrop.SetRadius(520,_settings.BlurStrength);
        BackdropLayer.Visibility=_backdrop.Enabled?Visibility.Visible:Visibility.Collapsed;
    }
    partial void BuildBlur(StackPanel panel)
    {
        var check=new CheckBox {Style=(Style)FindResource("ToggleSwitch"),IsChecked=_settings.BackgroundBlur,IsEnabled=BackdropCapture.IsSupported,VerticalAlignment=VerticalAlignment.Center};
        check.Checked+=(_,_)=>{_settings.BackgroundBlur=true;Changed();};check.Unchecked+=(_,_)=>{_settings.BackgroundBlur=false;Changed();};
        Row(panel,"背景模糊",check,BackdropCapture.IsSupported?"开启后截图和录屏时小岛会隐身，需要截图时可以临时关闭":"需要 Windows 10 2004 或更新版本");
        var group=new StackPanel {Orientation=Orientation.Horizontal,IsEnabled=_settings.BackgroundBlur && BackdropCapture.IsSupported};
        var value=new TextBlock {Text=$"{_settings.BlurStrength:0}%",Width=40,VerticalAlignment=VerticalAlignment.Center};value.SetResourceReference(TextBlock.ForegroundProperty,"Brush.Ink2");
        var slider=new Slider {Minimum=0,Maximum=100,Value=_settings.BlurStrength,Width=125};
        slider.ValueChanged+=(_,e)=>{_settings.BlurStrength=Math.Round(e.NewValue);value.Text=$"{_settings.BlurStrength:0}%";Changed();};
        check.Checked+=(_,_)=>group.IsEnabled=true;check.Unchecked+=(_,_)=>group.IsEnabled=false;
        group.Children.Add(slider);group.Children.Add(value);Row(panel,"模糊强度",group);
        var hide=new CheckBox {Style=(Style)FindResource("ToggleSwitch"),IsChecked=_settings.IsHiddenFromCapture(),IsEnabled=BackdropCapture.IsSupported,VerticalAlignment=VerticalAlignment.Center};
        hide.Checked+=(_,_)=>{_settings.SetHiddenFromCapture(true);Changed();};
        hide.Unchecked+=(_,_)=>{var hadBlur=_settings.BackgroundBlur;_settings.SetHiddenFromCapture(false);Changed();if(hadBlur)Dispatcher.BeginInvoke(new Action(Refresh));};
        check.Checked+=(_,_)=>hide.IsChecked=true;
        check.Unchecked+=(_,_)=>hide.IsChecked=_settings.HideFromCapture;
        Row(panel,"截图和录屏时隐藏小岛",hide,"背景模糊需要它开着；关掉的话模糊会暂停，小岛会出现在截图里");
    }
}
