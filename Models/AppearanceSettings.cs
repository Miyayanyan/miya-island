namespace MiyaIsland.Models;

public sealed class AppearanceSettings
{
    public string GlassPreset { get; set; } = "Midnight";
    public string? CustomTint { get; set; }
    public double? GlassOpacity { get; set; }
    public bool BackgroundBlur { get; set; }
    public double BlurStrength { get; set; } = 50;
    public bool HideFromCapture { get; set; }

    // 背景模糊需要小岛在截图里隐身（否则会把自己截进去），所以开着模糊时一定是隐身的。
    public bool IsHiddenFromCapture() => HideFromCapture || BackgroundBlur;
    public void SetHiddenFromCapture(bool hide) { HideFromCapture = hide; if (!hide) BackgroundBlur = false; }
    public bool QuietMode { get; set; }
    public bool ReduceMotion { get; set; }
    public bool ExperimentalBlur { get; set; }
    public int LyricFontSize { get; set; } = 18;
    public bool LyricShowNext { get; set; }
    public bool LyricShowClock { get; set; } = true;
    public string LyricAlignment { get; set; } = "Center";
    public bool LyricShowCover { get; set; } = true;
    public string Nickname { get; set; } = "";
    public bool GreetingEnabled { get; set; } = true;
    public GreetingSource GreetingSource { get; set; }
    public List<string> CustomGreetings { get; set; } = [];
    public DateTime? LastGreetingAt { get; set; }
    public List<string> RecentGreetings { get; set; } = [];
    public DateTime? LastLaunchAt { get; set; }
    public bool IsFirstRun { get; set; } = true;
    public bool AvatarEnabled { get; set; }
    public string BreathingStyle { get; set; } = "Ring";
    public double? SettingsLeft { get; set; }
    public double? SettingsTop { get; set; }
    public string DefaultReminderTime { get; set; } = "09:00";
    public int CompletedRetentionDays { get; set; } = 30;
    public bool ShowIsland { get; set; } = true;
    public bool ShowLyrics { get; set; }
    public string RestStyle { get; set; } = "Normal";
    // Legacy fields retained for reading older settings only.
    public string CompactStyle { get; set; } = "Pill";
    public bool StartCompact { get; set; }
    public double? IslandLeft { get; set; }
    public double? IslandCenter { get; set; }
    public double? IslandTop { get; set; }
    public string? IslandMonitor { get; set; }
    public string? FloatingLyricsMonitor { get; set; }
    public string Title { get; set; } = "Miya Island";
    public string FontFamily { get; set; } = "Segoe UI";
    public bool UseCustomTitleColor { get; set; }
    public string TitleColor { get; set; } = "#FFFFFFFF";
    public string IndicatorColor { get; set; } = "#FF8B7CFF";
    public bool BreathingEnabled { get; set; } = true;
    public string BreathingSpeed { get; set; } = "正常";
    public double ScalePercent { get; set; } = 100;
    public bool AutoCollapse { get; set; } = true;
    public bool IslandLocked { get; set; }
    public bool FloatingLyricsLocked { get; set; }
    public double? FloatingLyricsLeft { get; set; }
    public double? FloatingLyricsTop { get; set; }
}
