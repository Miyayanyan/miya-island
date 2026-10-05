using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MiyaIsland.Models;
namespace MiyaIsland;
public partial class FloatingLyricsWindow
{
    private readonly AppearanceSettings _preferences;
    private Popup? _morePopup;
    private BitmapSource? _cover;
    public event Action? PreferencesChanged;
    public void ApplyPreferences()
    {
        _expanded=_preferences.LyricShowNext;
        var size=_preferences.LyricFontSize is 16 or 18 or 22?_preferences.LyricFontSize:18;
        CurrentLyricText.FontSize=size+(_expanded?2:0);NextLyricText.FontSize=size-4;
        NextLyricText.Visibility=_expanded?Visibility.Visible:Visibility.Collapsed;
        ClockText.Visibility=_preferences.LyricShowClock?Visibility.Visible:Visibility.Hidden;
        CurrentLyricText.HorizontalAlignment=NextLyricText.HorizontalAlignment=HorizontalAlignment.Stretch;
        CurrentLyricText.TextAlignment=NextLyricText.TextAlignment=_preferences.LyricAlignment=="Left"?TextAlignment.Left:TextAlignment.Center;
        var height=_expanded?ExpandedHeight:CollapsedHeight;
        LyricBorder.CornerRadius=ShadowPlate.CornerRadius=new CornerRadius(_expanded?24:22);
        GlassEdgeBlur.Radius=Math.Clamp(height*.2,6,22);
        
        var resize = new DoubleAnimation(height,TimeSpan.FromMilliseconds(240)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}};
        resize.Completed += (_, _) => ApplyBlur();LyricHost.BeginAnimation(HeightProperty,resize);
        SetCover(_cover);UpdateClip();
    }
    private void SetCover(BitmapSource? cover)
    {
        _cover=cover;AlbumCover.Visibility=_preferences.LyricShowCover?Visibility.Visible:Visibility.Collapsed;
        AlbumCover.Width=AlbumCover.Height=_expanded?56:28;AlbumCover.CornerRadius=new CornerRadius(_expanded?12:8);
        AlbumCover.Background=cover is null?(Brush)FindResource("Brush.Primary"):new ImageBrush(cover){Stretch=Stretch.UniformToFill};
    }
    private void UpdateHover()
    {
        var hover=LyricHost.IsMouseOver || _morePopup?.IsOpen==true;
        ClockText.BeginAnimation(OpacityProperty,new DoubleAnimation(hover?0:1,TimeSpan.FromMilliseconds(120)));
        ControlStrip.BeginAnimation(OpacityProperty,new DoubleAnimation(hover?1:0,TimeSpan.FromMilliseconds(120)));
    }
    private void More_Click(object sender,RoutedEventArgs e)
    {
        if(_morePopup?.IsOpen==true){_morePopup.IsOpen=false;return;}
        var panel=new StackPanel();
        void Label(string text)=>panel.Children.Add(MiyaIsland.Services.ThemeService.Dynamic(new TextBlock {Text=text,Margin=new Thickness(6,4,6,4),FontSize=11},TextBlock.ForegroundProperty,"Brush.Ink2"));
        void Check(string label,bool value,Action<bool> update)
        {
            var check=new CheckBox {Content=label,Style=(Style)FindResource("ToggleSwitch"),IsChecked=value,Margin=new Thickness(6,4,6,4)};
            check.Checked+=(_,_)=>{update(true);ApplyPreferences();PreferencesChanged?.Invoke();};check.Unchecked+=(_,_)=>{update(false);ApplyPreferences();PreferencesChanged?.Invoke();};panel.Children.Add(check);
        }
        Check(_isLocked?"解锁位置":"锁定位置",_isLocked,v=>{_isLocked=v;UpdateLockVisual();LockChangedByUser?.Invoke(v);});
        var center=new Button {Content="水平居中",Style=(Style)FindResource("Button.Ghost"),IsEnabled=!_isLocked};center.Click+=(_,_)=>{_morePopup!.IsOpen=false;CenterHorizontally();};panel.Children.Add(center);
        Check("显示下一句",_preferences.LyricShowNext,v=>_preferences.LyricShowNext=v);
        Label("字号");var sizes=new UniformGrid {Columns=3};panel.Children.Add(sizes);
        foreach(var (name,size) in new[]{("小",16),("中",18),("大",22)})
        {var choice=new RadioButton {Content=name,Style=(Style)FindResource("TaskSegment"),GroupName="LyricSize",IsChecked=_preferences.LyricFontSize==size};choice.Checked+=(_,_)=>{_preferences.LyricFontSize=size;ApplyPreferences();PreferencesChanged?.Invoke();};sizes.Children.Add(choice);}
        Label("对齐");var alignment=new UniformGrid {Columns=2};panel.Children.Add(alignment);
        foreach(var (label,key) in new[]{("居中","Center"),("左对齐","Left")})
        {var choice=new RadioButton {Content=label,Style=(Style)FindResource("TaskSegment"),GroupName="LyricAlign",IsChecked=_preferences.LyricAlignment==key};choice.Checked+=(_,_)=>{_preferences.LyricAlignment=key;ApplyPreferences();PreferencesChanged?.Invoke();};alignment.Children.Add(choice);}
        Check("显示时钟",_preferences.LyricShowClock,v=>_preferences.LyricShowClock=v);
        Check("显示专辑封面",_preferences.LyricShowCover,v=>_preferences.LyricShowCover=v);
        panel.Children.Add(new Separator());var close=new Button {Content="关闭悬浮歌词",Style=(Style)FindResource("Button.Ghost")};close.Click+=(_,_)=>{_morePopup!.IsOpen=false;HideRequested?.Invoke();};panel.Children.Add(close);
        var shell=new Grid {Margin=new Thickness(12)};
        shell.Children.Add(MiyaIsland.Services.ThemeService.Dynamic(new Border {CornerRadius=new CornerRadius(14),Effect=new System.Windows.Media.Effects.DropShadowEffect {BlurRadius=34,ShadowDepth=10,Opacity=.32}},Border.BackgroundProperty,"Brush.PopupBase"));
        shell.Children.Add(MiyaIsland.Services.ThemeService.Dynamic(MiyaIsland.Services.ThemeService.Dynamic(new Border {CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),Padding=new Thickness(6),Width=220,Child=panel},Border.BackgroundProperty,"Brush.GlassSheen"),Border.BorderBrushProperty,"Brush.Separator"));
        _morePopup=new Popup {AllowsTransparency=true,StaysOpen=false,PlacementTarget=LyricHost,Placement=PlacementMode.Custom,Child=shell};
        _morePopup.CustomPopupPlacementCallback=(popup,target,offset)=>new[]{new CustomPopupPlacement(new Point(target.Width-popup.Width-12,-popup.Height-20),PopupPrimaryAxis.None),new CustomPopupPlacement(new Point(target.Width-popup.Width-12,target.Height-4),PopupPrimaryAxis.None)};
        _morePopup.Opened+=(_,_)=>{UpdateHover();shell.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));var shift=new TranslateTransform(0,6);shell.RenderTransform=shift;shift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(6,0,TimeSpan.FromMilliseconds(160)));};
        _morePopup.Closed+=(_,_)=>UpdateHover();_morePopup.IsOpen=true;
    }
}

