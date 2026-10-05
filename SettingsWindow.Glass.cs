using System.Windows;
using System.Windows.Controls;
using MiyaIsland.Services;
namespace MiyaIsland;
public partial class SettingsWindow
{
    private void BuildGlass(StackPanel panel)
    {
        Row(panel,"玻璃风格",ThemeService.PresetPicker(_settings,()=>{Changed();Refresh();}));
        var colors=new StackPanel {Orientation=Orientation.Horizontal};
        foreach(var tint in new[]{"#F8F5FA","#F7DAE6","#E4DCFF","#100F16"})
        {var button=ActionButton("●",()=>{_settings.CustomTint=tint;Changed();Refresh();});button.Content=new Border {Width=12,Height=12,CornerRadius=new CornerRadius(3),Background=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(tint)!,BorderBrush=System.Windows.Media.Brushes.Gray,BorderThickness=new Thickness(1)};button.Padding=new Thickness(0);button.Width=28;colors.Children.Add(button);}
        colors.Children.Add(ActionButton("自定义…",()=>{using var picker=new System.Windows.Forms.ColorDialog {FullOpen=true};if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;_settings.CustomTint=$"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";Changed();Refresh();}));
        Row(panel,"玻璃颜色",colors,_settings.CustomTint);
        var group=new StackPanel {Orientation=Orientation.Horizontal};
        var value=new TextBlock {Text=$"{GlassPalette.Resolve(_settings).Opacity:P0}",Width=40,VerticalAlignment=VerticalAlignment.Center};value.SetResourceReference(TextBlock.ForegroundProperty,"Brush.Ink2");
        var slider=new Slider {Minimum=20,Maximum=95,Value=GlassPalette.Resolve(_settings).Opacity*100,Width=125};
        slider.PreviewMouseLeftButtonDown+=(_,_)=>{if(_settings.GlassOpacity is null && GlassPalette.Resolve(_settings).Opacity<.2){_settings.GlassOpacity=slider.Value;value.Text=$"{slider.Value:0}%";Changed();}};
        slider.ValueChanged+=(_,e)=>{_settings.GlassOpacity=e.NewValue;value.Text=$"{e.NewValue:0}%";Changed();};group.Children.Add(slider);group.Children.Add(value);Row(panel,"浓度",group);
        panel.Children.Add(ActionButton("恢复预设默认",()=>{_settings.CustomTint=null;_settings.GlassOpacity=null;Changed();Refresh();}));
        BuildBlur(panel);
        Toggle(panel,"减少动态效果",_settings.ReduceMotion,v=>_settings.ReduceMotion=v);
    }
}
