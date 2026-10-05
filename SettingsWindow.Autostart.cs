using System.Windows;
using System.Windows.Controls;
using MiyaIsland.Services;
namespace MiyaIsland;
public partial class SettingsWindow
{
    partial void BuildAutostart(StackPanel panel)
    {
        var service=new AutostartService();var path=Environment.ProcessPath;
        var status=Label("默认关闭","Brush.Ink3");
        try
        {
            var stale=path is not null && service.IsPathStale(path);
            var check=new CheckBox {Style=(Style)FindResource("ToggleSwitch"),IsChecked=service.IsEnabled(),VerticalAlignment=VerticalAlignment.Center};
            Row(panel,"开机自启动",check);
            void Update(bool enabled)
            {
                if(_loading)return;
                try{if(enabled){if(path is null)throw new InvalidOperationException("无法获取程序路径。");service.Enable(path);}else service.Disable();Refresh();}
                catch(Exception ex){MessageBox.Show(this,ex.Message,"开机自启动");Refresh();}
            }
            check.Checked+=(_,_)=>Update(true);check.Unchecked+=(_,_)=>Update(false);
            if(stale)panel.Children.Add(ActionButton("路径已变化，点击修复",()=>{try{service.Enable(path!);Refresh();}catch(Exception ex){MessageBox.Show(this,ex.Message,"开机自启动");}}));
            else status.Text=service.IsEnabled()?"已启用":"默认关闭";
        }
        catch(Exception ex){status.Text=ex.Message;}
        panel.Children.Add(status);
    }
}
