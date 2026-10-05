using System.Windows;
namespace MiyaIsland;
public partial class MainWindow
{
    private void UpdateAvatarGeometry()
    {
        IslandAvatar.Visibility=_appearance.AvatarEnabled?Visibility.Visible:Visibility.Collapsed;
        ReminderBell.Visibility=_appearance.AvatarEnabled?Visibility.Hidden:Visibility.Visible;
        GreetingWords.Margin = new Thickness(_appearance.AvatarEnabled ? 68 : 38,0,18,0);
        if(!_appearance.AvatarEnabled){CompactContent.Margin=new Thickness(_lastMediaPlaying?11:26,0,11,0);return;}
        var size=_greetingVisible?40:_activeReminder is not null?34:_expanded?24:_compact?EffectiveRestStyle=="Orb"?20:20:26;
        IslandAvatar.SetOrb(_compact && !_expanded && !_greetingVisible && _activeReminder is null && EffectiveRestStyle=="Orb", _appearance);
        IslandAvatar.Width=IslandAvatar.Height=size;
        IslandAvatar.Margin=_greetingVisible?new Thickness(16,16,0,0):_activeReminder is not null?new Thickness(16,13,0,0):_expanded?new Thickness(15,15,0,0):_compact?EffectiveRestStyle=="Orb"?new Thickness(6):new Thickness(5):new Thickness(8);
        HeaderContent.Margin=_expanded?new Thickness(38,12,14,0):new Thickness(24,0,9,0);
        CompactContent.Margin=new Thickness(_lastMediaPlaying?31:31,0,11,0);
    }
}
