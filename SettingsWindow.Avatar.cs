using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MiyaIsland.Services;
namespace MiyaIsland;
public partial class SettingsWindow
{
    partial void BuildAvatarGreeting(StackPanel panel)
    {
        BuildAvatar(panel);BuildGreeting(panel);
    }
    private void BuildAvatar(StackPanel panel)
    {
        var service=new AvatarService();
        Toggle(panel,"显示头像",_settings.AvatarEnabled,v=>_settings.AvatarEnabled=v);
        var preview=new Image {Source=service.LoadCurrent(),Width=64,Height=64,Margin=new Thickness(0,8,0,0),Clip=new EllipseGeometry(new Point(32,32),32,32)};panel.Children.Add(preview);
        panel.Children.Add(ActionButton("更换图片",()=>
        {
            var picker=new Microsoft.Win32.OpenFileDialog {Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};if(picker.ShowDialog(this)!=true)return;
            try{var source=AvatarService.LoadFile(picker.FileName);var crop=new AvatarCropWindow(source){Owner=this};if(crop.ShowDialog()!=true)return;service.SaveCustom(source,crop.CropRect);Changed();Refresh();}
            catch(Exception ex){MessageBox.Show(this,"无法使用这张图片："+ex.Message,"头像",MessageBoxButton.OK,MessageBoxImage.Information);}
        }));
        panel.Children.Add(ActionButton("恢复默认头像",()=>{try{service.ResetToDefault();Changed();Refresh();}catch(Exception ex){MessageBox.Show(this,ex.Message,"头像");}}));
        Choice(panel,"呼吸灯样式",new[]{"光环","角标"},_settings.BreathingStyle=="Badge"?1:0,i=>_settings.BreathingStyle=i==1?"Badge":"Ring");
    }
    partial void BuildGreeting(StackPanel panel);
}
