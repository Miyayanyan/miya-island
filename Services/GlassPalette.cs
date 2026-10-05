using MiyaIsland.Models;

namespace MiyaIsland.Services;

/// <summary>Preset values from the design document. Opacity overrides are percentages.</summary>
public sealed record GlassPalette(string Name, byte R, byte G, byte B, double Opacity, bool Light, bool Clear)
{
    public static readonly string[] Names = ["Milk", "Rose", "Lavender", "Midnight", "Clear Glass"];
    public static GlassPalette Resolve(AppearanceSettings settings)
    {
        var palette = settings.GlassPreset switch
        {
            "Milk" => new GlassPalette("Milk",248,245,250,.76,true,false),
            "Rose" => new GlassPalette("Rose",247,218,230,.74,true,false),
            "Lavender" => new GlassPalette("Lavender",228,220,255,.74,true,false),
            "Clear Glass" => new GlassPalette("Clear Glass",255,255,255,.10,false,true),
            _ => new GlassPalette("Midnight",16,15,22,.74,false,false)
        };
        if (settings.GlassOpacity is double opacity && double.IsFinite(opacity))
            palette = palette with { Opacity = Math.Clamp(opacity,20,95)/100 };
        if (TryTint(settings.CustomTint, out var rgb))
            palette = palette with { R=rgb.R,G=rgb.G,B=rgb.B,Clear=false,Light=(Brightness(rgb.R,rgb.G,rgb.B)>=150 && palette.Opacity>=.45) };
        return palette;
    }
    /// <summary>背景模糊强度（0–100%）对应的半径倍数：50% = 原来的模糊程度，0% ≈ 0.3 倍，100% ≈ 1.7 倍。</summary>
    public static double BlurFactor(double strength) => .3 + (double.IsFinite(strength) ? Math.Clamp(strength,0,100) : 50) / 100 * 1.4;
    public static int Brightness(byte r,byte g,byte b)=>(r*299+g*587+b*114)/1000;
    public static bool TryTint(string? tint,out (byte R,byte G,byte B) rgb)
    {
        rgb=default;
        if (tint is null || tint.Length!=7 || tint[0]!='#' || !uint.TryParse(tint.AsSpan(1),System.Globalization.NumberStyles.HexNumber,null,out var value)) return false;
        rgb=((byte)(value>>16),(byte)(value>>8),(byte)value);return true;
    }
}
