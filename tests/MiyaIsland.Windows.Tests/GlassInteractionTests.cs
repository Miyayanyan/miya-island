using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MiyaIsland;
using MiyaIsland.Controls;
using MiyaIsland.Models;
using MiyaIsland.Services;
[assembly: CollectionBehavior(DisableTestParallelization=true)]
namespace MiyaIsland.Windows.Tests;
public class GlassInteractionTests
{
    private sealed class TestApp:App {protected override void OnStartup(StartupEventArgs e){}}
    [Fact]
    public void ThemeQuietModeCenteredLyricsHeaderAndQueuedGreetingWorkTogether()
    {
        Exception? error=null;
        var thread=new Thread(()=>
        {
            var data=Path.Combine(Path.GetTempPath(),"Miya-Windows-Test-"+Guid.NewGuid());
            TestApp? app=null;MainWindow? island=null;FloatingLyricsWindow? lyrics=null;
            try
            {
                app=new TestApp();foreach(var name in new[]{"Tokens","Icons","Controls"})app.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/MiyaIsland;component/Themes/"+name+".xaml",UriKind.Relative)});
                using var music=new MusicSession();var prefs=new AppearanceSettings {Title="Home",Nickname="Miya",AutoCollapse=false,BreathingEnabled=false};
                SetApp(app,"_appearance",prefs);SetApp(app,"_settings",new AppearanceSettingsService(data));
                island=new MainWindow(music,prefs,data,false);SetApp(app,"_island",island);island.Show();island.Left=island.Top=-20000;
                lyrics=new FloatingLyricsWindow(null,null,false,music,null,prefs);SetApp(app,"_lyrics",lyrics);lyrics.Show();lyrics.Left=lyrics.Top=-20000;
                foreach(var name in GlassPalette.Names)
                {prefs.GlassPreset=name;app.ApplySharedSettings();Pump(40);Assert.Equal(((SolidColorBrush)app.FindResource("Brush.Ink")).Color,((SolidColorBrush)((TextBlock)lyrics.FindName("CurrentLyricText")).Foreground).Color);}
                prefs.QuietMode=true;island.RefreshAppearance();Pump(400);Assert.Equal("Normal",prefs.RestStyle);Assert.True(prefs.GreetingEnabled);Assert.Equal(32,((Grid)island.FindName("IslandHost")).Width);
                prefs.QuietMode=false;island.RefreshAppearance();Pump(400);Assert.Equal(210,((Grid)island.FindName("IslandHost")).Width);
                foreach(var next in new[]{false,true})foreach(var cover in new[]{false,true})
                {prefs.LyricShowNext=next;prefs.LyricShowCover=cover;lyrics.ApplyPreferences();Pump(300);var area=(StackPanel)lyrics.FindName("LyricTextArea");Assert.InRange(area.TranslatePoint(new Point(area.ActualWidth/2,0),(Grid)lyrics.FindName("LyricHost")).X,279.5,280.5);}
                Call(island,"Expand");Pump(400);Assert.Contains("Miya",((TextBlock)island.FindName("CompactText")).Text);Pump(3300);Assert.Equal("Home",((TextBlock)island.FindName("CompactText")).Text);
                foreach(var name in new[]{"Music","Tasks","Download","Screenshot","Settings"})
                {Call(island,"ShowPanel",(Grid)island.FindName(name=="Tasks"?"ReminderPanel":name+"Panel"));Assert.True(((IconView)island.FindName("Nav"+name+"Icon")).Filled);}
                Call(island,"Collapse");Pump(400);
                var task=new TaskItem {Title="Reminder first"};((List<TaskItem>)Field(island,"_tasks")!).Add(task);((Queue<(TaskItem Task,DateTime Occurrence)>)Field(island,"_reminderQueue")!).Enqueue((task,DateTime.Now));Call(island,"ShowNextReminder");island.BeginStartupGreeting();Pump(1800);Assert.False((bool)Field(island,"_greetingVisible")!);Call(island,"ResolveReminder","已完成",null!);Pump(1800);Assert.True((bool)Field(island,"_greetingVisible")!);Assert.NotNull(prefs.LastGreetingAt);
                BackdropCaptureTests.VerifyLifecycle();
            }
            catch(Exception ex){error=ex;}
            finally
            {
                if(app is not null){typeof(App).GetField("<IsExiting>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(app,true);lyrics?.Close();island?.Close();app.Shutdown();}
                if(Directory.Exists(data))Directory.Delete(data,true);Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)));if(error is not null)ExceptionDispatchInfo.Capture(error).Throw();
    }
    private static object? Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target);
    private static void SetApp(App app,string name,object value)=>typeof(App).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,value);
    private static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
}
