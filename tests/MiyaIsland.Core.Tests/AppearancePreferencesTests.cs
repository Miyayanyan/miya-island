using System.Text.Json;
using MiyaIsland.Services;
namespace MiyaIsland.Core.Tests;
public class AppearancePreferencesTests
{
    [Fact]public void NullListsAndInvalidPreferencesRecoverWithoutBreakingStartup()
    {
        var settings=AppearanceSettingsMigration.Deserialize("{\"Nickname\":null,\"CustomGreetings\":null,\"RecentGreetings\":null,\"LyricFontSize\":100,\"BreathingStyle\":\"bad\",\"DefaultReminderTime\":\"25:00\"}");
        Assert.Empty(settings.CustomGreetings);Assert.Empty(settings.RecentGreetings);Assert.Equal("",settings.Nickname);Assert.Equal(18,settings.LyricFontSize);Assert.Equal("Ring",settings.BreathingStyle);Assert.Equal("09:00",settings.DefaultReminderTime);
    }
    [Fact]public void UserTextAndHistoryStayWithinLimits()
    {
        var settings=AppearanceSettingsMigration.Deserialize(JsonSerializer.Serialize(new{Nickname=new string('x',30),CustomGreetings=Enumerable.Repeat(new string('a',80),70),RecentGreetings=Enumerable.Range(0,20).Select(i=>i.ToString())}));
        Assert.Equal(12,settings.Nickname.Length);Assert.Equal(50,settings.CustomGreetings.Count);Assert.All(settings.CustomGreetings,s=>Assert.Equal(40,s.Length));Assert.Equal(Enumerable.Range(12,8).Select(i=>i.ToString()),settings.RecentGreetings);
    }
}
