using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MiyaIsland.Services;

/// <summary>
/// 歌词查询用的纯逻辑：清洗 Spotify 歌名、拆分艺人、生成查询顺序，以及给候选打分。
/// 不依赖 WPF 和网络，方便单元测试。
/// </summary>
public static class LyricQuery
{
    // “ - Remastered 2011”“ - Live”“ - 电视剧《…》插曲”这类破折号后缀
    private static readonly Regex DashSuffix = new(
        @"\s+[-–—]\s+(?:.*\b(?:remaster(?:ed)?|live|version|ver\.?|edit|mix|remix|mono|stereo|demo|acoustic|instrumental|from|bonus|deluxe|single|radio)\b.*|.*(?:版|伴奏|插曲|主题曲|片尾曲|片头曲|现场|原声带).*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // 括号里的 feat./with/版本说明，半角和全角括号都处理
    private static readonly Regex BracketNote = new(
        @"\s*[\(\[（【「]\s*(?:feat\.?|ft\.?|with|prod\.?|remaster(?:ed)?|live|version|ver\.?|edit|remix|mix|acoustic|demo|instrumental|from|taken from|bonus|deluxe|explicit|clean|radio|伴奏|现场|版|电视剧|电影|插曲|主题曲|片尾曲|片头曲|原声)[^\)\]）】」]*[\)\]）】」]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AnyBracket = new(@"\s*[\(\[（【「][^\)\]）】」]*[\)\]）】」]", RegexOptions.Compiled);
    private static readonly Regex InlineFeat = new(@"\s+(?:feat\.?|ft\.?)\s+.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ArtistSeparator = new(@"\s*(?:,|，|、|&|/|;|\s+feat\.?\s+|\s+ft\.?\s+|\s+with\s+|\s+x\s+|\s*×\s*)\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string CleanTitle(string title)
    {
        var value = (title ?? "").Trim();
        value = DashSuffix.Replace(value, "");
        value = BracketNote.Replace(value, "");
        value = InlineFeat.Replace(value, "");
        value = value.Trim(' ', '-', '–', '—', '·', '.', ',', '，');
        return value.Length > 0 ? value : (title ?? "").Trim();
    }

    /// <summary>去掉所有括号内容，作为最后一种写法，例如“晴天 (Live)”中没有识别出的说明。</summary>
    public static string StripAllBrackets(string title)
    {
        var value = AnyBracket.Replace(CleanTitle(title), "").Trim();
        return value.Length > 0 ? value : CleanTitle(title);
    }

    public static IReadOnlyList<string> SplitArtists(string artist) =>
        ArtistSeparator.Split(artist ?? "")
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>按顺序尝试的 (歌名, 艺人) 组合，已去重。艺人为空表示只按歌名搜。</summary>
    public static IReadOnlyList<(string Title, string Artist)> Variants(string title, string artist)
    {
        var artists = SplitArtists(artist);
        var main = artists.FirstOrDefault() ?? "";
        var cleaned = CleanTitle(title);
        var bare = StripAllBrackets(title);
        var list = new List<(string, string)>
        {
            ((title ?? "").Trim(), (artist ?? "").Trim()),
            (cleaned, main),
            (bare, main),
            (cleaned, "")
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return list.Where(item => item.Item1.Length > 0 && seen.Add(Normalize(item.Item1) + "|" + Normalize(item.Item2))).ToList();
    }

    /// <summary>用于比较的规范化：全角转半角、小写、去掉空白和常见标点。简繁不转换。</summary>
    public static string Normalize(string value)
    {
        var text = (value ?? "").Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var category = char.GetUnicodeCategory(ch);
            if (char.IsWhiteSpace(ch) || category is UnicodeCategory.OtherPunctuation or UnicodeCategory.DashPunctuation
                or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation
                or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.ConnectorPunctuation) continue;
            builder.Append(ch);
        }
        return builder.ToString();
    }

    /// <summary>
    /// 给候选打分；返回 null 表示排除。
    /// 时长差 > 15 秒直接排除；歌名至少要包含关系；艺人语言不同时，需要歌名相等且时长对得上。
    /// </summary>
    public static double? Score(string wantedTitle, string wantedArtist, TimeSpan wantedDuration,
        string candidateTitle, string candidateArtist, double candidateSeconds)
    {
        var score = 0d;
        var hasDuration = wantedDuration.TotalSeconds > 1 && candidateSeconds > 1;
        var durationClose = false;
        if (hasDuration)
        {
            var diff = Math.Abs(wantedDuration.TotalSeconds - candidateSeconds);
            if (diff > 15) return null;
            durationClose = diff <= 3;
            score += diff <= 3 ? 40 : 40 * (1 - (diff - 3) / 12);
        }

        var wanted = new[] { Normalize(CleanTitle(wantedTitle)), Normalize(StripAllBrackets(wantedTitle)) };
        var candidate = new[] { Normalize(CleanTitle(candidateTitle)), Normalize(StripAllBrackets(candidateTitle)) };
        var titleEqual = wanted.Any(w => w.Length > 0 && candidate.Contains(w));
        var titleContains = !titleEqual && wanted.Any(w => w.Length > 0 && candidate.Any(c => c.Length > 0 && (c.Contains(w) || w.Contains(c))));
        if (titleEqual) score += 40;
        else if (titleContains) score += 20;
        else return null;

        var wantedArtists = SplitArtists(wantedArtist).Select(Normalize).Where(a => a.Length > 0).ToHashSet();
        var candidateArtists = SplitArtists(candidateArtist).Select(Normalize).Where(a => a.Length > 0).ToList();
        var artistMatch = wantedArtists.Count == 0 || candidateArtists.Any(c => wantedArtists.Any(w => c == w || c.Contains(w) || w.Contains(c)));
        if (artistMatch) score += 30;
        else if (!(titleEqual && durationClose)) return null; // 例如 Jay Chou ↔ 周杰伦：只有歌名和时长都对上才接受

        return score;
    }

    /// <summary>LRC 内容里至少有两行带时间标签的歌词，才算同步歌词。</summary>
    public static bool IsSynced(string? lrc) =>
        !string.IsNullOrWhiteSpace(lrc) && Regex.Matches(lrc, @"^\s*\[\d{1,3}:\d{2}(?:[.:]\d{1,3})?\]", RegexOptions.Multiline).Count >= 2;
}
