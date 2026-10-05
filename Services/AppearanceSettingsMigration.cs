using System.Text.Json;
using MiyaIsland.Models;

namespace MiyaIsland.Services;

public static class AppearanceSettingsMigration
{
    public static AppearanceSettings Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var settings = JsonSerializer.Deserialize<AppearanceSettings>(json) ?? new();
        if (!document.RootElement.TryGetProperty(nameof(AppearanceSettings.RestStyle), out _))
            settings.RestStyle = settings.StartCompact && settings.CompactStyle is "Pill" or "Orb"
                ? settings.CompactStyle : "Normal";
        if (settings.RestStyle is not ("Normal" or "Pill" or "Orb"))
            settings.RestStyle = "Normal";
        if (string.IsNullOrWhiteSpace(settings.Title)) settings.Title = "Miya Island";
        if (string.IsNullOrWhiteSpace(settings.FontFamily)) settings.FontFamily = "Segoe UI";
        settings.Nickname = (settings.Nickname ?? "").Trim();
        if (settings.Nickname.Length > 12) settings.Nickname = settings.Nickname[..12];
        settings.CustomGreetings = (settings.CustomGreetings ?? []).Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(50).Select(line => line.Length > 40 ? line[..40] : line).ToList();
        settings.RecentGreetings = (settings.RecentGreetings ?? []).Where(line => line is not null).TakeLast(8).ToList();
        if (settings.LyricFontSize is not (16 or 18 or 22)) settings.LyricFontSize = 18;
        if (settings.LyricAlignment is not ("Center" or "Left")) settings.LyricAlignment = "Center";
        if (settings.BreathingStyle is not ("Ring" or "Badge")) settings.BreathingStyle = "Ring";
        if (!TimeOnly.TryParseExact(settings.DefaultReminderTime, "HH:mm", out _)) settings.DefaultReminderTime = "09:00";
        settings.CompletedRetentionDays = Math.Clamp(settings.CompletedRetentionDays, 0, 3650);
        if(!document.RootElement.TryGetProperty(nameof(AppearanceSettings.UseCustomTitleColor),out _))settings.UseCustomTitleColor=settings.TitleColor!="#FFFFFFFF";
        settings.ExperimentalBlur = false;
        if (!GlassPalette.Names.Contains(settings.GlassPreset)) settings.GlassPreset = "Midnight";
        if (!GlassPalette.TryTint(settings.CustomTint, out _)) settings.CustomTint = null;
        settings.BlurStrength = double.IsFinite(settings.BlurStrength) ? Math.Clamp(settings.BlurStrength,0,100) : 50;
        if (settings.GlassOpacity is double opacity) settings.GlassOpacity = double.IsFinite(opacity) ? Math.Clamp(opacity,20,95) : null;
        return settings;
    }
}
