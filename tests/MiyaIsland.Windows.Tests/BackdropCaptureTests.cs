using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MiyaIsland.Services;
namespace MiyaIsland.Windows.Tests;
internal static class BackdropCaptureTests
{
    public static void VerifyLifecycle()
    {
        Window? window=null,background=null;
        try
        {
                var area=SystemParameters.WorkArea;
                background=new Window {Width=260,Height=180,Left=area.Left+30,Top=area.Top+30,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Background=Brushes.LimeGreen};background.Show();
                var image=new Image();window=new Window {Width=180,Height=100,Left=area.Left+60,Top=area.Top+60,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,Content=new Grid {Background=Brushes.Red,Children={image}}};
                var failed=false;using var capture=new BackdropCapture(window,image,()=>8,()=>failed=true);window.Show();capture.SetEnabled(true);Pump(350);
                if(!capture.Enabled){Assert.True(failed || !BackdropCapture.IsSupported);return;}
                var hwnd=new WindowInteropHelper(window).Handle;Assert.True(GetWindowDisplayAffinity(hwnd,out var affinity));Assert.Equal(0x11u,affinity);
                capture.CaptureFrame();var bitmap=capture.Bitmap;Assert.NotNull(bitmap);Assert.True(capture.FrameCount>0);capture.CaptureFrame();Assert.Same(bitmap,capture.Bitmap);
                Assert.True(GetWindowRect(hwnd,out var rect));Assert.Equal((rect.Right-rect.Left+1)/2,bitmap.PixelWidth);Assert.Equal((rect.Bottom-rect.Top+1)/2,bitmap.PixelHeight);
                window.Hide();Assert.False(capture.IsRunning);var frames=capture.FrameCount;Pump(180);Assert.Equal(frames,capture.FrameCount);
                window.Show();Assert.True(capture.IsRunning);capture.SetEnabled(false);Assert.False(capture.IsRunning);Assert.True(GetWindowDisplayAffinity(hwnd,out affinity));Assert.Equal(0u,affinity);
        }
        finally {window?.Close();background?.Close();}
    }
    private static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    [StructLayout(LayoutKind.Sequential)]private struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool GetWindowDisplayAffinity(IntPtr hwnd,out uint affinity);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
}
