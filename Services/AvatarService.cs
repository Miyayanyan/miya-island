using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MiyaIsland.Services;
public sealed class AvatarService
{
    private readonly string _directory;
    public AvatarService(string? directory=null)=>_directory=directory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MiyaIsland");
    public BitmapImage LoadCurrent()
    {
        try {var path=Path.Combine(_directory,"avatar.png");if(File.Exists(path))return LoadFile(path);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidOperationException or NotSupportedException or ArgumentException){System.Diagnostics.Trace.TraceWarning(ex.Message);}
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri("pack://application:,,,/MiyaIsland;component/Assets/DefaultAvatar.png");image.EndInit();image.Freeze();return image;
    }
    public static BitmapImage LoadFile(string path)
    {
        if(new FileInfo(path).Length>20*1024*1024)throw new InvalidOperationException("图片超过 20MB，请选择小一些的图片。");
        if(!new[]{".png",".jpg",".jpeg",".bmp",".gif"}.Contains(Path.GetExtension(path).ToLowerInvariant()))throw new InvalidOperationException("请选择 PNG、JPG、BMP 或 GIF 图片。");
        using var stream=File.OpenRead(path);
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();
        if(image.PixelWidth>12000 || image.PixelHeight>12000)throw new InvalidOperationException("图片尺寸太大，请先缩小图片。");
        image.Freeze();return image;
    }
    public void SaveCustom(BitmapSource bitmap,Int32Rect cropRect)
    {
        if(cropRect.Width<=0 || cropRect.Height<=0 || cropRect.X<0 || cropRect.Y<0 || cropRect.X+cropRect.Width>bitmap.PixelWidth || cropRect.Y+cropRect.Height>bitmap.PixelHeight)throw new ArgumentException("裁剪范围超出图片。");
        var cropped=new CroppedBitmap(bitmap,cropRect);
        var scaled=new TransformedBitmap(cropped,new ScaleTransform(256d/cropRect.Width,256d/cropRect.Height));
        var encoded=new PngBitmapEncoder();encoded.Frames.Add(BitmapFrame.Create(scaled));Directory.CreateDirectory(_directory);
        var temporary=Path.Combine(_directory,"avatar-"+Guid.NewGuid()+".tmp");
        try{using(var stream=File.Create(temporary))encoded.Save(stream);File.Move(temporary,Path.Combine(_directory,"avatar.png"),true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public void ResetToDefault(){var path=Path.Combine(_directory,"avatar.png");if(File.Exists(path))File.Delete(path);}
}
