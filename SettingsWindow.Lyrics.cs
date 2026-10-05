using System.Windows.Controls;
namespace MiyaIsland;
public partial class SettingsWindow
{
    partial void BuildLyricSettings(StackPanel panel)
    {
        Choice(panel,"字号",new[]{"小 16","中 18","大 22"},_settings.LyricFontSize==16?0:_settings.LyricFontSize==22?2:1,i=>_settings.LyricFontSize=new[]{16,18,22}[i]);
        Toggle(panel,"显示下一句",_settings.LyricShowNext,v=>_settings.LyricShowNext=v);
        Toggle(panel,"显示时钟",_settings.LyricShowClock,v=>_settings.LyricShowClock=v);
        Choice(panel,"对齐",new[]{"居中","左对齐"},_settings.LyricAlignment=="Left"?1:0,i=>_settings.LyricAlignment=i==1?"Left":"Center");
        Toggle(panel,"显示专辑封面",_settings.LyricShowCover,v=>_settings.LyricShowCover=v);
    }
}
