using System.Windows;
using System.Windows.Controls;
using MiyaIsland.Models;
using MiyaIsland.Services;
namespace MiyaIsland;
public partial class SettingsWindow
{
    partial void BuildGreeting(StackPanel panel)
    {
        Text(panel,"我的称呼",_settings.Nickname,12,v=>_settings.Nickname=v);
        if(_settings.QuietMode)Row(panel,"启动问候",Label("安静模式中","Brush.Ink3"));else Toggle(panel,"启动问候",_settings.GreetingEnabled,v=>_settings.GreetingEnabled=v,"最多每 4 小时一次");
        Choice(panel,"文案来源",new[]{"内置 + 自定义","只用自定义"},_settings.GreetingSource==GreetingSource.CustomOnly?1:0,i=>_settings.GreetingSource=i==1?GreetingSource.CustomOnly:GreetingSource.BuiltInAndCustom);
        var input=new TextBox {MaxLength=40,Margin=new Thickness(0,8,0,0)};panel.Children.Add(input);
        var placeholders=new WrapPanel();panel.Children.Add(placeholders);
        foreach(var value in new[]{"{name}","{count}","{next}","{nexttime}","{weekday}"})placeholders.Children.Add(ActionButton(value,()=>{var text=input.Text.Insert(input.CaretIndex,value);if(text.Length<=40){var cursor=input.CaretIndex+value.Length;input.Text=text;input.Focus();input.CaretIndex=cursor;}} ,"Button.Ghost"));
        var lines=new StackPanel();panel.Children.Add(lines);
        void Render(){lines.Children.Clear();foreach(var value in _settings.CustomGreetings.ToArray()){var row=new DockPanel();var remove=ActionButton("删除",()=>{_settings.CustomGreetings.Remove(value);Changed();Render();},"Button.Ghost");DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);row.Children.Add(Label(value,"Brush.Ink2"));lines.Children.Add(row);}}
        panel.Children.Add(ActionButton("添加问候",()=>{var text=input.Text.Trim();if(text.Length==0 || _settings.CustomGreetings.Count>=50)return;_settings.CustomGreetings.Add(text);input.Clear();Changed();Render();}));Render();
        var preview=Label("","Brush.Lyric");panel.Children.Add(ActionButton("试一下",()=>{var context=((App)Application.Current).CreateGreetingContext();context.Nickname=_settings.Nickname;context.Source=_settings.GreetingSource;context.CustomLines=_settings.CustomGreetings;var result=new GreetingService().Pick(context);preview.Text=result.Title+"\n"+result.Subtitle;}));panel.Children.Add(preview);panel.Children.Add(ActionButton("预览问候卡",()=>((App)Application.Current).PreviewGreetingCard()));
    }
}
