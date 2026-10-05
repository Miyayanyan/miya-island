using MiyaIsland.Models;
using MiyaIsland.Services;
namespace MiyaIsland.Core.Tests;
public class LyricCursorTests
{
    private static readonly LyricLine[] Lines = [new(TimeSpan.FromSeconds(10), "一"), new(TimeSpan.FromSeconds(20), "二"), new(TimeSpan.FromSeconds(30), "三")];
    [Theory]
    [InlineData(0, "", "♪", "一")]
    [InlineData(10, "", "一", "二")]
    [InlineData(19, "", "一", "二")]
    [InlineData(20, "一", "二", "三")]
    [InlineData(40, "二", "三", "")]
    public void LocatesBoundaries(int seconds, string previous, string current, string next)
        => Assert.Equal(new LyricPosition(previous,current,next), LyricCursor.Locate(Lines, TimeSpan.FromSeconds(seconds)));
    [Fact] public void EmptyHasMusicSymbol() => Assert.Equal(new LyricPosition("", "♪", ""), LyricCursor.Locate([], TimeSpan.Zero));
    [Fact] public void EqualTimestampsUseLastLine() => Assert.Equal("二", LyricCursor.Locate([Lines[0],new(Lines[0].Time,"二")],Lines[0].Time).Current);
}
