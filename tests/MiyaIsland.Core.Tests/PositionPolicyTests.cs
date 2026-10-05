using System.Text.Json;
using MiyaIsland.Models;
using MiyaIsland.Services;
namespace MiyaIsland.Core.Tests;
public class PositionPolicyTests
{
    private static readonly WorkArea Area = new(-1920,0,1920,1040);
    [Theory]
    [InlineData(-944,18,false,-960,8)]
    [InlineData(-943,19,false,-943,19)]
    [InlineData(-944,18,true,-944,18)]
    public void SnapsOnlyWithinThresholdUnlessAlt(double center,double top,bool alt,double expectedCenter,double expectedTop)
        => Assert.Equal((expectedCenter,expectedTop),PositionPolicy.Snap(center,top,Area,8,alt));
    [Fact] public void OffscreenAndNonFiniteCannotRestore()
    {
        Assert.False(PositionPolicy.CanRestore(100,10,210,42,Area));
        Assert.False(PositionPolicy.CanRestore(double.NaN,10,210,42,Area));
        Assert.True(PositionPolicy.CanRestore(-1800,8,210,42,Area));
        Assert.True(PositionPolicy.CanRestore(-1000,8,32,32,Area));
    }
    [Fact] public void LegacySettingsUseSafeDefaults()
    {
        var settings=JsonSerializer.Deserialize<AppearanceSettings>("{\"Title\":\"旧小岛\"}")!;
        Assert.True(settings.ShowIsland);Assert.False(settings.ShowLyrics);Assert.False(settings.StartCompact);
        Assert.Equal("Pill",settings.CompactStyle);Assert.Null(settings.IslandLeft);
    }
}
