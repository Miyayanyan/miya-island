using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MiyaIsland.Models;
using MiyaIsland.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;

namespace MiyaIsland;

public partial class SettingsWindow : Window
{
    private readonly AppearanceSettings _settings;
    private readonly Action _apply;
    private int _page;
    private bool _loading;
    private readonly List<Button> _navigation = [];
    public SettingsWindow(AppearanceSettings settings, Action apply)
    {
        InitializeComponent(); _settings = settings; _apply = apply;
        ApplyBackdrop();
        foreach (var (name, index) in new[] { ("常规",0), ("外观",1), ("头像与问候",2), ("悬浮歌词",3), ("任务",4), ("高级",5) })
        {
            var button = ActionButton(name, () => ShowPage(index));
            button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Height = 38;
            Navigation.Children.Add(button); _navigation.Add(button);
        }
        Activated += (_, _) => Refresh();
        Closing += (_, _) => { _settings.SettingsLeft = Left; _settings.SettingsTop = Top; _apply(); };
        ShowPage(0);
    }
    public void Refresh() => ShowPage(_page);
    public void ShowPage(int index)
    {
        _loading = true; _page = index; Page.Children.Clear();
        for (var i=0;i<_navigation.Count;i++) _navigation[i].SetResourceReference(Control.BackgroundProperty, i==index ? "Brush.Lens" : "Brush.ControlTransparent");
        var panel = new StackPanel();
        Page.Children.Add(ThemeService.Dynamic(new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(12), Child = panel },Border.BackgroundProperty,"Brush.Well"));
        switch(index)
        {
            case 0:
                BuildAutostart(panel);
                Toggle(panel,"启动时显示小岛",_settings.ShowIsland,v=>_settings.ShowIsland=v);
                Toggle(panel,"启动时显示歌词",_settings.ShowLyrics,v=>_settings.ShowLyrics=v);
                Toggle(panel,"自动收起",_settings.AutoCollapse,v=>_settings.AutoCollapse=v);
                Toggle(panel,"安静模式",_settings.QuietMode,v=>{_settings.QuietMode=v;Dispatcher.BeginInvoke(new Action(Refresh));});
                if(_settings.QuietMode)Row(panel,"收起样式",Label("安静模式中","Brush.Ink3"));else Choice(panel,"收起样式",new[]{"普通条","小胶囊","小圆"},_settings.RestStyle switch {"Pill"=>1,"Orb"=>2,_=>0},i=>_settings.RestStyle=new[]{"Normal","Pill","Orb"}[i]);
                panel.Children.Add(ActionButton("重新居中",()=>((App)Application.Current).CenterIsland()));
                panel.Children.Add(ActionButton("退出程序",()=>((App)Application.Current).ExitApplication(),"Button.Danger"));break;
            case 1:
                BuildGlass(panel);
                Text(panel,"待机标题",_settings.Title,80,v=>_settings.Title=v);
                var fonts=Fonts.SystemFontFamilies.Select(f=>f.Source).OrderBy(f=>f).ToArray();
                Choice(panel,"标题字体",fonts,Math.Max(0,Array.IndexOf(fonts,_settings.FontFamily)),i=>_settings.FontFamily=fonts[i]);
                panel.Children.Add(ActionButton("文字颜色",()=>PickColor(false)));
                Toggle(panel,"呼吸灯",_settings.BreathingEnabled,v=>_settings.BreathingEnabled=v);
                BuildIndicatorColor(panel);
                Choice(panel,"呼吸速度",new[]{"慢速","正常","快速"},_settings.BreathingSpeed=="慢速"?0:_settings.BreathingSpeed=="快速"?2:1,i=>_settings.BreathingSpeed=new[]{"慢速","正常","快速"}[i]);
                var scale=new Slider {Minimum=70,Maximum=160,Value=_settings.ScalePercent,Width=180};
                scale.ValueChanged+=(_,e)=>{_settings.ScalePercent=e.NewValue;Changed();};Row(panel,"小岛大小",scale);
                break;
            case 2: BuildAvatarGreeting(panel);break;
            case 3: BuildLyricSettings(panel);break;
            case 4:
                Text(panel,"默认提醒时间",_settings.DefaultReminderTime,5,v=>{if(TimeOnly.TryParseExact(v,"HH:mm",out _))_settings.DefaultReminderTime=v;},"HH:mm");
                Choice(panel,"已完成保留天数",new[]{"7 天","30 天","90 天","永久"},_settings.CompletedRetentionDays switch{7=>0,90=>2,0=>3,_=>1},i=>_settings.CompletedRetentionDays=new[]{7,30,90,0}[i]);break;
            case 5:
                panel.Children.Add(ActionButton("打开数据文件夹",()=>{var directory=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiyaIsland");System.IO.Directory.CreateDirectory(directory);Process.Start(new ProcessStartInfo(directory){UseShellExecute=true});}));
                panel.Children.Add(ActionButton("恢复所有默认",ResetAll));
                panel.Children.Add(Label("版本 "+typeof(App).Assembly.GetName().Version,"Brush.Ink3"));break;
        }
        _loading = false;
    }
    private Brush Ink(string key)=>(Brush)FindResource(key);
    private TextBlock Label(string text,string ink="Brush.Ink")=>ThemeService.Dynamic(new TextBlock {Text=text,FontSize=13,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap},TextBlock.ForegroundProperty,ink);
    private void Row(StackPanel panel,string label,UIElement control,string? hint=null)
    {
        var grid=new Grid {MinHeight=42};grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,6,10,6)};words.Children.Add(Label(label));if(hint is not null){var small=Label(hint,"Brush.Ink3");small.FontSize=11;words.Children.Add(small);}grid.Children.Add(words);
        Grid.SetColumn(control,1);grid.Children.Add(control);panel.Children.Add(ThemeService.Dynamic(new Border {BorderThickness=new Thickness(0,0,0,1),Child=grid},Border.BorderBrushProperty,"Brush.Separator"));
    }
    private void Toggle(StackPanel panel,string label,bool value,Action<bool> update,string? hint=null)
    {
        var check=new CheckBox {Style=(Style)FindResource("ToggleSwitch"),IsChecked=value,VerticalAlignment=VerticalAlignment.Center};
        check.Checked+=(_,_)=>{update(true);Changed();};check.Unchecked+=(_,_)=>{update(false);Changed();};Row(panel,label,check,hint);
    }
    private void Choice(StackPanel panel,string label,string[] values,int selected,Action<int> update)
    {
        var combo=new ComboBox {ItemsSource=values,SelectedIndex=selected,Width=160,VerticalAlignment=VerticalAlignment.Center};
        combo.SelectionChanged+=(_,_)=>{if(combo.SelectedIndex>=0){update(combo.SelectedIndex);Changed();}};Row(panel,label,combo);
    }
    private void Text(StackPanel panel,string label,string value,int limit,Action<string> update,string? hint=null)
    {
        var input=new TextBox {Text=value,MaxLength=limit,Width=180,VerticalAlignment=VerticalAlignment.Center};input.TextChanged+=(_,_)=>{update(input.Text);Changed();};Row(panel,label,input,hint);
    }
    private Button ActionButton(string label,Action action,string style="Button.Glass")
    {var button=new Button {Content=label,Style=(Style)FindResource(style),Margin=new Thickness(0,6,0,0)};button.Click+=(_,_)=>action();return button;}
    private void Changed(){if(!_loading)_apply();}
    private void ResetAll()
    {
        try
        {
            new AutostartService().Disable();new AvatarService().ResetToDefault();
            var defaults=new AppearanceSettings();
            foreach(var property in typeof(AppearanceSettings).GetProperties())property.SetValue(_settings,property.GetValue(defaults));
            Changed();((App)Application.Current).CenterIsland();Refresh();
        }
        catch(Exception ex){MessageBox.Show(this,"恢复默认失败："+ex.Message,"设置");Refresh();}
    }
    // 呼吸灯颜色：头像的光环和角标绑定在呼吸灯上，会一起变色
    private void BuildIndicatorColor(StackPanel panel)
    {
        var colors=new StackPanel {Orientation=Orientation.Horizontal};
        foreach(var tint in new[]{"#FF8B7CFF","#FFD884C0","#FFFF8FAE","#FFF0B878","#FF4FBF8E","#FF6FB7FF","#FFFFFFFF"})
        {
            var selected=string.Equals(_settings.IndicatorColor,tint,StringComparison.OrdinalIgnoreCase);
            var swatch=new Border {Width=14,Height=14,CornerRadius=new CornerRadius(7),Background=(Brush)new BrushConverter().ConvertFromString(tint)!,BorderThickness=new Thickness(selected?2:1)};
            swatch.SetResourceReference(Border.BorderBrushProperty,selected?"Brush.Ink":"Brush.Separator");
            var button=ActionButton("●",()=>{_settings.IndicatorColor=tint;Changed();Refresh();});button.Content=swatch;button.Padding=new Thickness(0);button.Width=28;button.ToolTip=selected?"当前颜色":null;colors.Children.Add(button);
        }
        colors.Children.Add(ActionButton("自定义…",()=>{PickColor(true);Refresh();}));
        Row(panel,"呼吸灯颜色",colors,"开启头像时，光环和角标也用这个颜色");
    }
    private void PickColor(bool indicator)
    {
        using var picker=new System.Windows.Forms.ColorDialog {FullOpen=true};
        var color=(Color)ColorConverter.ConvertFromString(indicator?_settings.IndicatorColor:_settings.TitleColor);
        picker.Color=System.Drawing.Color.FromArgb(color.A,color.R,color.G,color.B);
        if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;
        var result=$"#FF{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";if(indicator)_settings.IndicatorColor=result;else {_settings.TitleColor=result;_settings.UseCustomTitleColor=true;}Changed();
    }
    private void Window_Loaded(object sender,RoutedEventArgs e)
    {
        var area=SystemParameters.WorkArea;
        if(_settings.SettingsLeft is double left && _settings.SettingsTop is double top && System.Windows.Forms.Screen.AllScreens.Any(s=>PositionPolicy.CanRestore(left,top,Width,Height,WindowPosition.Area(this,s)))){Left=left;Top=top;}
        else {Left=area.Left+(area.Width-Width)/2;Top=area.Top+(area.Height-Height)/2;}
    }
    private void Header_MouseDown(object sender,MouseButtonEventArgs e){if(e.LeftButton==MouseButtonState.Pressed && e.OriginalSource is not Button)DragMove();}
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
    partial void BuildAvatarGreeting(StackPanel panel);
    partial void BuildAutostart(StackPanel panel);
    partial void BuildLyricSettings(StackPanel panel);
    partial void BuildBlur(StackPanel panel);
}
