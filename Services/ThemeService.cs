using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using MiyaIsland.Models;

namespace MiyaIsland.Services;

public static class ThemeService
{
    public static GlassPalette Current { get; private set; } = GlassPalette.Resolve(new());
    public static void Apply(AppearanceSettings settings)
    {
        var p=Current=GlassPalette.Resolve(settings);var resources=Application.Current.Resources;
        void Brush(string key,byte r,byte g,byte b,double a=1) {var brush=new SolidColorBrush(Color.FromArgb((byte)Math.Round(a*255),r,g,b));brush.Freeze();resources[key]=brush;}
        void White(string key,double a)=>Brush(key,255,255,255,a);
        Brush("Brush.IslandBase",p.R,p.G,p.B,p.Opacity*(settings.BackgroundBlur?.6:1));
        resources["IslandBackground"]=resources["Brush.IslandBase"];
        Brush("Brush.SettingsBase",p.Clear?(byte)22:p.R,p.Clear?(byte)20:p.G,p.Clear?(byte)30:p.B,Math.Max(.9,p.Opacity)*(settings.BackgroundBlur?.6:1));
        Brush("Brush.PopupBase",p.Clear?(byte)22:p.R,p.Clear?(byte)20:p.G,p.Clear?(byte)30:p.B,Math.Max(.9,p.Opacity));
        var r=p.Light?(byte)42:(byte)255;var g=p.Light?(byte)34:(byte)255;var b=p.Light?(byte)48:(byte)255;
        Brush("Brush.Ink",r,g,b);Brush("Brush.Ink2",r,g,b,p.Light?.66:.68);Brush("Brush.Ink3",r,g,b,.44);
        resources["MutedText"]=resources["Brush.Ink2"];
        Brush("Brush.Lyric",p.Light?(byte)91:(byte)199,p.Light?(byte)71:(byte)190,p.Light?(byte)201:(byte)255);
        White("Brush.Control",p.Light?.50:p.Clear?.14:.08);White("Brush.ControlHover",p.Light?.68:p.Clear?.22:.13);
        White("Brush.ControlPressed",p.Light?.40:.05);White("Brush.Well",p.Light?.40:p.Clear?.10:.06);
        White("Brush.Lens",p.Light?.55:p.Clear?.22:.14);
        Brush("Brush.Separator",p.Light?(byte)40:(byte)255,p.Light?(byte)20:(byte)255,p.Light?(byte)60:(byte)255,p.Clear?.18:.10);
        resources["Brush.IslandBorder"]=resources["Brush.Separator"];
        Brush("Brush.Overdue",p.Light?(byte)181:(byte)255,p.Light?(byte)55:(byte)143,p.Light?(byte)92:(byte)174);
        Brush("Brush.Warning",p.Light?(byte)154:(byte)240,p.Light?(byte)91:(byte)184,p.Light?(byte)18:(byte)120);
        Brush("Brush.GlassInnerDark",0,0,0,p.Light?.06:p.Clear?.12:.30);
        White("Brush.GlassInnerLight",p.Light?.55:p.Clear?.40:.22);
        White("Brush.GlassEdge",p.Light?.55:p.Clear?.30:.14);
        var sheen=new LinearGradientBrush {StartPoint=new(0,0),EndPoint=new(0,1)};
        sheen.GradientStops.Add(new(Color.FromArgb((byte)Math.Round(255*(p.Light?.32:p.Clear?.18:.10)),255,255,255),0));
        sheen.GradientStops.Add(new(Colors.Transparent,.46));sheen.GradientStops.Add(new(Color.FromArgb((byte)Math.Round(255*(p.Light?.05:p.Clear?.10:.18)),0,0,0),1));sheen.Freeze();resources["Brush.GlassSheen"]=sheen;
        foreach(var (key,x,y,alpha) in new[]{("Brush.GlassRimTopLeft",.06,0d,.95),("Brush.GlassRimBottomRight",.96,1d,.70)})
        {var rim=new RadialGradientBrush {GradientOrigin=new(x,y),Center=new(x,y),RadiusX=.7,RadiusY=1.3,Opacity=p.Light?.72:p.Clear?.62:.45};rim.GradientStops.Add(new(Color.FromArgb((byte)Math.Round(255*alpha),255,255,255),0));rim.GradientStops.Add(new(Colors.Transparent,.6));rim.Freeze();resources[key]=rim;}
        resources["Color.GlassShadow"]=p.Light?Color.FromRgb(90,60,120):Colors.Black;
        resources["Glass.ShadowOpacity"]=p.Light?.18:p.Clear?.22:.32;
        var textShadow=p.Clear?new DropShadowEffect {Color=Colors.Black,ShadowDepth=1,Direction=270,BlurRadius=2,Opacity=.45}:null;
        resources["Effect.GlassText"]=textShadow ?? new DropShadowEffect {Opacity=0,BlurRadius=0,ShadowDepth=0};
    }
    public static T Dynamic<T>(T element,DependencyProperty property,string key) where T:FrameworkElement
    {element.SetResourceReference(property,key);return element;}
    public static ComboBox PresetPicker(AppearanceSettings settings,Action changed)
    {
        var combo=new ComboBox {Width=160,VerticalAlignment=VerticalAlignment.Center};
        foreach(var name in GlassPalette.Names)
        {var p=GlassPalette.Resolve(new(){GlassPreset=name});var row=new StackPanel {Orientation=Orientation.Horizontal};row.Children.Add(new Border {Width=12,Height=12,CornerRadius=new(3),Background=new SolidColorBrush(Color.FromRgb(p.R,p.G,p.B)),BorderBrush=Brushes.Gray,BorderThickness=new(1),Margin=new(0,0,7,0)});row.Children.Add(new TextBlock {Text=name});combo.Items.Add(new ComboBoxItem {Content=row,Tag=name});}
        combo.SelectedIndex=Math.Max(0,Array.IndexOf(GlassPalette.Names,settings.GlassPreset));
        combo.SelectionChanged+=(_,_)=>{if(combo.SelectedItem is ComboBoxItem {Tag:string name}){settings.GlassPreset=name;settings.CustomTint=null;settings.GlassOpacity=null;changed();}};return combo;
    }
}
