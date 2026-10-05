using System.Text.Json;
using MiyaIsland.Models;
using MiyaIsland.Services;

namespace MiyaIsland.Core.Tests;

public class AppearanceSettingsMigrationTests
{
    [Theory]
    [InlineData(true, "Pill", "Pill")]
    [InlineData(true, "Orb", "Orb")]
    [InlineData(false, "Pill", "Normal")]
    [InlineData(false, "Orb", "Normal")]
    [InlineData(true, "unknown", "Normal")]
    public void LegacySettingsMigrate(bool startCompact, string compactStyle, string expected)
    {
        var json = JsonSerializer.Serialize(new { StartCompact = startCompact, CompactStyle = compactStyle });
        Assert.Equal(expected, AppearanceSettingsMigration.Deserialize(json).RestStyle);
    }

    [Theory]
    [InlineData("Normal")]
    [InlineData("Pill")]
    [InlineData("Orb")]
    public void ExplicitRestStyleWinsOverLegacyFieldsAndSurvivesSave(string style)
    {
        var folder = Path.Combine(Path.GetTempPath(), "MiyaIsland-Settings-" + Guid.NewGuid());
        try
        {
            var service = new AppearanceSettingsService(folder);
            service.Save(new AppearanceSettings { RestStyle = style, StartCompact = true, CompactStyle = "Orb" });
            Assert.Equal(style, service.Load().RestStyle);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"RestStyle\":\"unknown\"}")]
    [InlineData("{\"RestStyle\":null,\"StartCompact\":true,\"CompactStyle\":\"Orb\"}")]
    public void MissingOrInvalidStyleFallsBackToNormal(string json) =>
        Assert.Equal("Normal", AppearanceSettingsMigration.Deserialize(json).RestStyle);
}
