using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace MiyaIsland;
public partial class MainWindow
{
    private readonly DispatcherTimer _headerTimer=new() {Interval=TimeSpan.FromSeconds(3)};
    private bool _headerGreeting,_headerTransition;
    private int _headerGeneration;
    private void BeginHeaderGreeting()
    {
        StopHeaderGreeting();
        _headerGreeting=!_appearance.QuietMode && !string.IsNullOrWhiteSpace(_appearance.Nickname);
        if(!_headerGreeting)return;
        _headerTimer.Tick-=HeaderGreetingExpired;_headerTimer.Tick+=HeaderGreetingExpired;_headerTimer.Start();
    }
    private void HeaderGreetingExpired(object? sender,EventArgs e)
    {
        _headerTimer.Stop();if(!_expanded)return;
        _headerGreeting=false;_headerTransition=true;var generation=_headerGeneration;
        HeaderTitleNext.Text=_appearance.Title;
        CompactText.BeginAnimation(OpacityProperty,new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(300)));
        var fade=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(300));
        fade.Completed+=(_,_)=>{if(generation!=_headerGeneration)return;StopHeaderGreeting();UpdateHeaderText();};
        HeaderTitleNext.BeginAnimation(OpacityProperty,fade);
    }
    private void StopHeaderGreeting()
    {
        _headerTimer.Stop();_headerGeneration++;_headerGreeting=false;_headerTransition=false;
        CompactText.BeginAnimation(OpacityProperty,null);CompactText.Opacity=1;
        HeaderTitleNext.BeginAnimation(OpacityProperty,null);HeaderTitleNext.Opacity=0;
    }
}
