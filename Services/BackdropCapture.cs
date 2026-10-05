using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MiyaIsland.Services;

/// <summary>Captures a fixed window canvas. Native buffers and bitmap are reused until its physical size changes.</summary>
public sealed class BackdropCapture : IDisposable
{
    private readonly Window _window;
    private readonly Image _image;
    private readonly BlurEffect _effect = new() { RenderingBias=RenderingBias.Performance };
    private readonly DispatcherTimer _timer;
    private readonly Action _failure;
    private readonly Func<int> _fps;
    private IntPtr _handle, _fullDc, _smallDc, _fullBitmap, _smallBitmap, _fullOld, _smallOld, _pixels;
    private int _width, _height, _smallWidth, _smallHeight;
    private bool _enabled, _hide, _disposed;
    private bool? _affinityApplied;
    private WriteableBitmap? _bitmap;
    public static bool IsSupported {get;private set;} = OperatingSystem.IsWindowsVersionAtLeast(10,0,19041);
    public long FrameCount {get;private set;}
    public bool IsRunning => _timer.IsEnabled;
    public bool Enabled => _enabled;
    public WriteableBitmap? Bitmap => _bitmap;
    public BackdropCapture(Window window, Image image, Func<int> fps, Action failure)
    {
        _window=window;_image=image;_fps=fps;_failure=failure;image.Effect=_effect;image.IsHitTestVisible=false;
        _timer=new DispatcherTimer(DispatcherPriority.Background,window.Dispatcher) {Interval=TimeSpan.FromMilliseconds(125)};
        _timer.Tick+=Tick;
        window.SourceInitialized+=SourceInitialized;window.IsVisibleChanged+=VisibilityChanged;
        window.StateChanged+=StateChanged;window.Closed+=Closed;
        _handle=new WindowInteropHelper(window).Handle;
    }
    /// <param name="enabled">实时背景模糊</param>
    /// <param name="hideFromCapture">即使不开模糊，也在截图和录屏里隐藏窗口</param>
    public void SetEnabled(bool enabled,bool hideFromCapture=false)
    {
        if (_disposed)return;
        _hide=hideFromCapture;
        if(enabled && !IsSupported){_enabled=false;_image.Visibility=Visibility.Collapsed;_timer.Stop();_failure();return;}
        _enabled=enabled;
        var exclude=_enabled||_hide;
        if(_handle!=IntPtr.Zero && _affinityApplied!=exclude && !SetWindowDisplayAffinity(_handle,exclude?0x11u:0u))
        {if(_enabled){IsSupported=false;Fail();return;}System.Diagnostics.Trace.TraceWarning("Unable to set capture affinity: "+Marshal.GetLastWin32Error());}
        if(_handle!=IntPtr.Zero)_affinityApplied=exclude;
        _image.Visibility=_enabled?Visibility.Visible:Visibility.Collapsed;
        UpdateRunning();
    }
    public void SetRadius(double islandHeight,double strength=50) => _effect.Radius=Math.Clamp(.07*islandHeight,3,16)*1.8*GlassPalette.BlurFactor(strength);
    private void SourceInitialized(object? sender,EventArgs e) {_handle=new WindowInteropHelper(_window).Handle;_affinityApplied=null;SetEnabled(_enabled,_hide);}
    private void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e)=>UpdateRunning();
    private void StateChanged(object? sender,EventArgs e)=>UpdateRunning();
    private void Closed(object? sender,EventArgs e)=>Dispose();
    private void UpdateRunning()
    {
        if(_enabled && _handle!=IntPtr.Zero && _window.IsVisible && _window.WindowState!=WindowState.Minimized)
        {_timer.Interval=TimeSpan.FromSeconds(1d/Math.Clamp(_fps(),1,30));_timer.Start();}
        else _timer.Stop();
    }
    private void Tick(object? sender,EventArgs e)
    {
        try {CaptureFrame();_timer.Interval=TimeSpan.FromSeconds(1d/Math.Clamp(_fps(),1,30));}
        catch(Exception ex) when(ex is Win32Exception or ExternalException or ArgumentException or InvalidOperationException or OverflowException)
        {System.Diagnostics.Trace.TraceWarning("Backdrop capture: "+ex.Message);Fail();}
    }
    public void CaptureFrame()
    {
        if(!_enabled || _handle==IntPtr.Zero || !_window.IsVisible || _window.WindowState==WindowState.Minimized)return;
        if(!GetWindowRect(_handle,out var rect))throw new Win32Exception();
        var width=rect.Right-rect.Left;var height=rect.Bottom-rect.Top;
        if(width<1 || height<1 || width>16000 || height>16000)throw new InvalidOperationException("Invalid capture dimensions");
        if(width!=_width || height!=_height)Allocate(width,height);
        var screen=GetDC(IntPtr.Zero);if(screen==IntPtr.Zero)throw new Win32Exception();
        try
        {
            // Coordinates are physical virtual-desktop pixels, including negative monitor origins.
            if(!BitBlt(_fullDc,0,0,width,height,screen,rect.Left,rect.Top,0x00CC0020))throw new Win32Exception();
            if(!StretchBlt(_smallDc,0,0,_smallWidth,_smallHeight,_fullDc,0,0,width,height,0x00CC0020))throw new Win32Exception();
            GdiFlush();
            _bitmap!.WritePixels(new Int32Rect(0,0,_smallWidth,_smallHeight),_pixels,checked(_smallWidth*_smallHeight*4),_smallWidth*4);
            FrameCount++;
        }
        finally {ReleaseDC(IntPtr.Zero,screen);}
    }
    private void Allocate(int width,int height)
    {
        ReleaseBuffers();_width=width;_height=height;_smallWidth=(width+1)/2;_smallHeight=(height+1)/2;
        _fullDc=CreateCompatibleDC(IntPtr.Zero);_smallDc=CreateCompatibleDC(IntPtr.Zero);
        if(_fullDc==IntPtr.Zero || _smallDc==IntPtr.Zero)throw new Win32Exception();
        var full=Info(width,height);_fullBitmap=CreateDIBSection(_fullDc,ref full,0,out _,IntPtr.Zero,0);
        var small=Info(_smallWidth,_smallHeight);_smallBitmap=CreateDIBSection(_smallDc,ref small,0,out _pixels,IntPtr.Zero,0);
        if(_fullBitmap==IntPtr.Zero || _smallBitmap==IntPtr.Zero)throw new Win32Exception();
        _fullOld=SelectObject(_fullDc,_fullBitmap);_smallOld=SelectObject(_smallDc,_smallBitmap);
        // Bgr32 ignores GDI's unused alpha byte. No per-frame ImageSource, array or bitmap allocation.
        _bitmap=new WriteableBitmap(_smallWidth,_smallHeight,96,96,PixelFormats.Bgr32,null);_image.Source=_bitmap;
    }
    private static BitmapInfo Info(int width,int height)=>new() {Header=new() {Size=40,Width=width,Height=-height,Planes=1,BitCount=32}};
    private void Fail()
    {
        _enabled=false;_timer.Stop();_image.Visibility=Visibility.Collapsed;
        if(_handle!=IntPtr.Zero){var hidden=_hide && SetWindowDisplayAffinity(_handle,0x11);if(!hidden)SetWindowDisplayAffinity(_handle,0);_affinityApplied=hidden;}
        ReleaseBuffers();_failure();
    }
    private void ReleaseBuffers()
    {
        if(_fullOld!=IntPtr.Zero && _fullDc!=IntPtr.Zero)SelectObject(_fullDc,_fullOld);
        if(_smallOld!=IntPtr.Zero && _smallDc!=IntPtr.Zero)SelectObject(_smallDc,_smallOld);
        if(_fullBitmap!=IntPtr.Zero)DeleteObject(_fullBitmap);if(_smallBitmap!=IntPtr.Zero)DeleteObject(_smallBitmap);
        if(_fullDc!=IntPtr.Zero)DeleteDC(_fullDc);if(_smallDc!=IntPtr.Zero)DeleteDC(_smallDc);
        _fullDc=_smallDc=_fullBitmap=_smallBitmap=_fullOld=_smallOld=_pixels=IntPtr.Zero;_width=_height=0;
        _bitmap=null;_image.Source=null;
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_timer.Stop();_timer.Tick-=Tick;
        if(_handle!=IntPtr.Zero)SetWindowDisplayAffinity(_handle,0);
        _window.SourceInitialized-=SourceInitialized;_window.IsVisibleChanged-=VisibilityChanged;_window.StateChanged-=StateChanged;_window.Closed-=Closed;
        ReleaseBuffers();
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct BitmapHeader {public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,SizeImage;public int XPels,YPels;public uint Used,Important;}
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo {public BitmapHeader Header;public uint Colors;}
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint affinity);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("gdi32.dll",SetLastError=true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll",SetLastError=true)] private static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll",SetLastError=true)] private static extern bool BitBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int sx,int sy,uint operation);
    [DllImport("gdi32.dll",SetLastError=true)] private static extern bool StretchBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int sx,int sy,int sw,int sh,uint operation);
}

