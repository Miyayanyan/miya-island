using MiyaIsland.Models;

namespace MiyaIsland.Services;

public readonly record struct LyricPosition(string Previous, string Current, string Next);

public static class LyricCursor
{
    public static LyricPosition Locate(IReadOnlyList<LyricLine> lines, TimeSpan position)
    {
        if (lines.Count == 0) return new("", "♪", "");
        var index = -1;
        for (var i = 0; i < lines.Count && lines[i].Time <= position; i++) index = i;
        return index < 0 ? new("", "♪", lines[0].Text) :
            new(index > 0 ? lines[index - 1].Text : "", lines[index].Text, index + 1 < lines.Count ? lines[index + 1].Text : "");
    }
}
