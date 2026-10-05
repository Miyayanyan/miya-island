using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MiyaIsland.Models;
using MiyaIsland.Services;
namespace MiyaIsland;
public partial class MainWindow
{
    private static readonly IslandShape GreetShape=new(400,72,24);
    private bool _greetingVisible;
    private readonly DispatcherTimer _greetingDelay=new(){Interval=TimeSpan.FromSeconds(1.5)};
    private readonly DispatcherTimer _greetingExpiry=new(){Interval=TimeSpan.FromSeconds(6)};
    private readonly GreetingService _greetings=new();
    private string _restDisplayText="Miya Island";
    private string? _restIconKey;
    public GreetingContext GreetingContext()=>new()
    {
        Now=DateTime.Now,Nickname=_appearance.Nickname,Enabled=_appearance.GreetingEnabled && !_appearance.QuietMode,Source=_appearance.GreetingSource,
        CustomLines=_appearance.CustomGreetings,RecentTemplates=_appearance.RecentGreetings,IsFirstRun=_appearance.IsFirstRun,
        LastLaunchAt=_appearance.LastLaunchAt,LastGreetingAt=_appearance.LastGreetingAt,
        Summary=GreetingSummaryFromTasks()
    };
    private GreetingSummary GreetingSummaryFromTasks()
    {
        var summary=_scheduler.GetSummary(DateTime.Now);
        return new(){TodayCount=summary.TodayCount,OverdueCount=summary.OverdueCount,DoneTodayCount=summary.DoneTodayCount,NextTitle=summary.Next?.Title,NextTime=summary.Next?.Time};
    }
    private readonly StartupGreetingQueue _startupGreetingQueue=new();
    private GreetingContext? _startupGreetingContext;
    private GreetingResult? _previewGreeting;
    public void BeginStartupGreeting()
    {
        _startupGreetingContext=GreetingContext();_appearance.LastLaunchAt=DateTime.Now;_appearance.IsFirstRun=false;
        _appearanceService.Save(_appearance);_startupGreetingQueue.Start();
        _greetingDelay.Interval=TimeSpan.FromMilliseconds(250);
        _greetingDelay.Tick-=TryQueuedGreeting;_greetingDelay.Tick+=TryQueuedGreeting;_greetingDelay.Start();
    }
    private void TryQueuedGreeting(object? sender,EventArgs e)
    {
        var blocked=!IsVisible || _activeReminder is not null || _reminderQueue.Count>0 || _expanded || _greetingVisible || _morphing;
        if(_previewGreeting is not null && !blocked)
        {var preview=_previewGreeting;_previewGreeting=null;ShowGreeting(preview,false);return;}
        if(_startupGreetingQueue.Poll(blocked || _appearance.QuietMode || !_appearance.GreetingEnabled) && _startupGreetingContext is {} context)
        {
            context.Enabled=true;context.Summary=GreetingSummaryFromTasks();context.Now=DateTime.Now;
            var greeting=_greetings.TryGetStartupGreeting(context);if(greeting is not null)ShowGreeting(greeting);
        }
        if(!_startupGreetingQueue.Pending && _previewGreeting is null)_greetingDelay.Stop();
    }
    public void PreviewGreetingCard()
    {
        if(!IsVisible)((App)Application.Current).SetIslandVisible(true);
        var context=GreetingContext();context.Enabled=true;_previewGreeting=_greetings.Pick(context);
        if(_activeReminder is null)Collapse();
        _greetingDelay.Interval=TimeSpan.FromMilliseconds(250);_greetingDelay.Tick-=TryQueuedGreeting;_greetingDelay.Tick+=TryQueuedGreeting;_greetingDelay.Start();
    }
    private void ShowGreeting(GreetingResult greeting,bool record=true)
    {
        _greetingVisible=true;_expanded=false;_compact=false;
        GreetingTitle.Text=greeting.Title;GreetingSubtitle.Text=greeting.Subtitle;
        if(record)
        {
        _appearance.LastGreetingAt=DateTime.Now;
        _appearance.RecentGreetings.Add(greeting.TemplateUsed);_appearance.RecentGreetings=_appearance.RecentGreetings.TakeLast(8).ToList();
        _appearanceService.Save(_appearance);
        }
        MorphTo(GreetShape,true);
        GreetingProgress.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(1,0,TimeSpan.FromSeconds(6)));
        _greetingExpiry.Start();
    }
    private string ExpandedTitle
    {
        get
        {
            if(string.IsNullOrWhiteSpace(_appearance.Nickname))return _appearance.Title;
            var hour=DateTime.Now.Hour;
            var time=hour is >=5 and <11?"早上好":hour is >=11 and <14?"中午好":hour is >=14 and <18?"下午好":hour is >=18 and <23?"晚上好":"夜深了";
            return time+"，"+_appearance.Nickname;
        }
    }
    private void UpdateHeaderText()
    {
        if(!_headerTransition)CompactText.Text=_expanded?(_headerGreeting?ExpandedTitle:_appearance.Title):_restDisplayText;
        CompactIcon.Visibility=!_expanded && _restIconKey is not null && !_appearance.AvatarEnabled?Visibility.Visible:Visibility.Collapsed;
        if(_restIconKey is not null)CompactIcon.Data=IconGeometry(_restIconKey);
    }
    private void Greeting_Click(object sender,System.Windows.Input.MouseButtonEventArgs e){Collapse();e.Handled=true;}
}


