using MiyaIsland.Models;
using MiyaIsland.Services;
namespace MiyaIsland.Core.Tests;
public class GlassPaletteTests
{
    [Theory]
    [InlineData("Milk",248,245,250,.76,true)]
    [InlineData("Rose",247,218,230,.74,true)]
    [InlineData("Lavender",228,220,255,.74,true)]
    [InlineData("Midnight",16,15,22,.74,false)]
    [InlineData("Clear Glass",255,255,255,.10,false)]
    public void PresetMatchesDesign(string name,int r,int g,int b,double opacity,bool light)
    {var p=GlassPalette.Resolve(new(){GlassPreset=name});Assert.Equal((r,g,b),((int)p.R,(int)p.G,(int)p.B));Assert.Equal(opacity,p.Opacity);Assert.Equal(light,p.Light);}
    [Theory]
    [InlineData("#FFFFFF",44,false)]
    [InlineData("#FFFFFF",45,true)]
    [InlineData("#949494",95,false)]
    [InlineData("#969696",95,true)]
    public void CustomContrastUsesBrightnessAndOpacity(string color,double opacity,bool light)
    {Assert.Equal(light,GlassPalette.Resolve(new(){CustomTint=color,GlassOpacity=opacity}).Light);}
    [Fact]
    public void LegacyExperimentalBlurCannotReopenAndBadSettingsRecover()
    {var settings=AppearanceSettingsMigration.Deserialize("{\"ExperimentalBlur\":true,\"GlassPreset\":\"missing\",\"CustomTint\":\"bad\",\"GlassOpacity\":110}");Assert.False(settings.ExperimentalBlur);Assert.Equal("Midnight",settings.GlassPreset);Assert.Null(settings.CustomTint);Assert.Equal(95,settings.GlassOpacity);Assert.False(settings.BackgroundBlur);}
    [Fact] public void ExistingCustomTitleColorSurvivesWhileDefaultUsesPresetInk()
    {Assert.True(AppearanceSettingsMigration.Deserialize("{\"TitleColor\":\"#FFFF0000\"}").UseCustomTitleColor);Assert.False(AppearanceSettingsMigration.Deserialize("{}").UseCustomTitleColor);Assert.True(AppearanceSettingsMigration.Deserialize("{\"UseCustomTitleColor\":true,\"TitleColor\":\"#FFFFFFFF\"}").UseCustomTitleColor);}
    [Fact] public void BlurStrengthDefaultsToOriginalRadiusAndIsClamped()
    {
        Assert.Equal(1,GlassPalette.BlurFactor(50),3);Assert.Equal(.3,GlassPalette.BlurFactor(-20),3);Assert.Equal(1.7,GlassPalette.BlurFactor(400),3);
        Assert.Equal(50,AppearanceSettingsMigration.Deserialize("{}").BlurStrength);Assert.Equal(100,AppearanceSettingsMigration.Deserialize("{\"BlurStrength\":180}").BlurStrength);
    }
    [Fact] public void HidingFromCaptureFollowsBlur()
    {
        var settings=new MiyaIsland.Models.AppearanceSettings();Assert.False(settings.IsHiddenFromCapture());
        settings.BackgroundBlur=true;Assert.True(settings.IsHiddenFromCapture());
        settings.SetHiddenFromCapture(false);Assert.False(settings.BackgroundBlur);Assert.False(settings.IsHiddenFromCapture());
        settings.SetHiddenFromCapture(true);Assert.True(settings.IsHiddenFromCapture());Assert.False(settings.BackgroundBlur);
    }
    [Fact] public void LiveSnapLocksToCenterAndReportsGuides()
    {
        var area=new WorkArea(0,0,1920,1040);
        var snap=PositionPolicy.LiveSnap(950,500,area,926,false);Assert.Equal(960,snap.Center);Assert.True(snap.CenterGuide);Assert.False(snap.TopGuide);
        Assert.Equal((900d,500d,false,false),PositionPolicy.LiveSnap(900,500,area,926,false));
        Assert.Equal((950d,920d,false,false),PositionPolicy.LiveSnap(950,920,area,926,true));
        Assert.True(PositionPolicy.LiveSnap(1200,920,area,926,false).TopGuide);
    }
}
