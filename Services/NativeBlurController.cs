using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace MiyaIsland.Services;
// Windows owns a region after SetWindowRgn succeeds; failures remain caller-owned.
public sealed class NativeBlurController
{
    private readonly Window _window;
    private IntPtr _handle;
    public static bool IsSupported {get;private set;}=OperatingSystem.IsWindows();
    public NativeBlurController(Window window){_window=window;window.SourceInitialized+=(_,_)=>_handle=new WindowInteropHelper(window).Handle;}
    public bool Apply(bool enabled,Rect bounds,double radius)
    {
        if(_handle==IntPtr.Zero)return IsSupported;
        if(!enabled){Suspend();return IsSupported;}
        if(!IsSupported)return false;
        try
        {
            if(!SetAccent(4) && !SetAccent(3)){IsSupported=false;Suspend();return false;}
            var dpi=VisualTreeHelper.GetDpi(_window);
            var region=CreateRoundRectRgn((int)Math.Round(bounds.Left*dpi.DpiScaleX),(int)Math.Round(bounds.Top*dpi.DpiScaleY),(int)Math.Ceiling(bounds.Right*dpi.DpiScaleX)+1,(int)Math.Ceiling(bounds.Bottom*dpi.DpiScaleY)+1,(int)Math.Round(radius*2*dpi.DpiScaleX),(int)Math.Round(radius*2*dpi.DpiScaleY));
            if(region==IntPtr.Zero){IsSupported=false;Suspend();return false;}
            if(SetWindowRgn(_handle,region,true)==0){DeleteObject(region);IsSupported=false;Suspend();return false;}
            return true;
        }
        catch(Exception ex) when(ex is EntryPointNotFoundException or DllNotFoundException){IsSupported=false;Suspend();return false;}
    }
    public void Suspend()
    {
        if(_handle==IntPtr.Zero)return;
        try{SetAccent(0);SetWindowRgn(_handle,IntPtr.Zero,true);}catch(Exception ex) when(ex is EntryPointNotFoundException or DllNotFoundException){IsSupported=false;}
    }
    private bool SetAccent(int state)
    {
        var policy=new AccentPolicy {State=state,Flags=2,GradientColor=0x68160f10};
        var pointer=Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
        try{Marshal.StructureToPtr(policy,pointer,false);var data=new AttributeData {Attribute=19,Data=pointer,Size=new IntPtr(Marshal.SizeOf<AccentPolicy>())};return SetWindowCompositionAttribute(_handle,ref data)!=0;}
        finally{Marshal.FreeHGlobal(pointer);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct AccentPolicy{public int State,Flags;public uint GradientColor;public int Animation;}
    [StructLayout(LayoutKind.Sequential)]private struct AttributeData{public int Attribute;public IntPtr Data;public IntPtr Size;}
    [DllImport("user32.dll")]private static extern int SetWindowCompositionAttribute(IntPtr hwnd,ref AttributeData data);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
    [DllImport("user32.dll")]private static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr handle);
}
