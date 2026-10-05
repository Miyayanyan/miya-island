using System.Net;
using System.Text;
using MiyaIsland.Services;
using Xunit.Abstractions;

namespace MiyaIsland.Core.Tests;

public sealed class LyricLookupTests : IDisposable
{
    private readonly string _cache = Path.Combine(Path.GetTempPath(), "miya-lyrics-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;
    public LyricLookupTests(ITestOutputHelper output) => _output = output;
    public void Dispose() { try { Directory.Delete(_cache, true); } catch { } }

    private const string Synced = "[00:01.00]第一句\n[00:05.50]第二句\n[00:09.00]第三句";

    [Theory]
    [InlineData("Here Comes The Sun - Remastered 2009", "Here Comes The Sun")]
    [InlineData("Stay (with Justin Bieber)", "Stay")]
    [InlineData("千里之外 (feat. 费玉清)", "千里之外")]
    [InlineData("晴天（Live）", "晴天")]
    [InlineData("光年之外 - 电影《太空旅客》中国区主题曲", "光年之外")]
    [InlineData("Song ft. Someone", "Song")]
    [InlineData("Lemon", "Lemon")]
    [InlineData("夜に駆ける", "夜に駆ける")]
    public void CleansSpotifyTitles(string raw, string expected) => Assert.Equal(expected, LyricQuery.CleanTitle(raw));

    [Fact]
    public void SplitsArtistsAndKeepsMainFirst()
    {
        Assert.Equal(["周杰伦", "费玉清"], LyricQuery.SplitArtists("周杰伦, 费玉清"));
        Assert.Equal(["The Kid LAROI", "Justin Bieber"], LyricQuery.SplitArtists("The Kid LAROI & Justin Bieber"));
        Assert.Equal(["A", "B", "C"], LyricQuery.SplitArtists("A feat. B、C"));
        Assert.Equal(["Official髭男dism"], LyricQuery.SplitArtists("Official髭男dism"));
    }

    [Fact]
    public void VariantsGoFromExactToLooseWithoutDuplicates()
    {
        var variants = LyricQuery.Variants("千里之外 (feat. 费玉清)", "周杰伦, 费玉清");
        Assert.Equal(("千里之外 (feat. 费玉清)", "周杰伦, 费玉清"), variants[0]);
        Assert.Equal(("千里之外", "周杰伦"), variants[1]);
        Assert.Equal(("千里之外", ""), variants[^1]);
        Assert.Equal(3, variants.Count);
        Assert.Single(LyricQuery.Variants("Lemon", ""));
    }

    [Fact]
    public void ScoringRejectsWrongSongsButAcceptsOtherLanguageArtistNames()
    {
        var length = TimeSpan.FromSeconds(269);
        Assert.Null(LyricQuery.Score("晴天", "周杰伦", length, "晴天", "周杰伦", 300));            // 时长差 31 秒
        Assert.Null(LyricQuery.Score("晴天", "周杰伦", length, "雨天", "周杰伦", 269));            // 歌名不对
        Assert.NotNull(LyricQuery.Score("晴天", "Jay Chou", length, "晴天", "周杰伦", 270));       // 艺人语言不同，但歌名和时长都对上
        Assert.Null(LyricQuery.Score("晴天", "Jay Chou", TimeSpan.Zero, "晴天", "周杰伦", 270));   // 不知道时长时不冒险
        Assert.True(LyricQuery.Score("晴天", "周杰伦", length, "晴天", "周杰伦", 269) > LyricQuery.Score("晴天", "周杰伦", length, "晴天", "周杰伦", 280));
        Assert.NotNull(LyricQuery.Score("Here Comes The Sun - Remastered 2009", "The Beatles", TimeSpan.Zero, "Here Comes the Sun", "The Beatles", 185));
    }

    [Fact]
    public async Task FallsBackToCleanedTitleOnLrclib()
    {
        var handler = new FakeHandler(url =>
            url.Contains("lrclib") && url.Contains("track_name=Here Comes The Sun") && !url.Contains("Remastered")
                ? Json($$"""[{"id":1,"trackName":"Here Comes The Sun","artistName":"The Beatles","duration":185,"syncedLyrics":{{Quote(Synced)}}}]""")
                : Json("[]"));
        var service = new LyricService(handler, _cache);
        var lines = await service.GetLyricsAsync("Here Comes The Sun - Remastered 2009", "The Beatles", TimeSpan.FromSeconds(186));
        Assert.Equal(3, lines.Count);
        Assert.Equal("lrclib", service.LastSource);
        Assert.DoesNotContain(handler.Urls, u => u.Contains("163.com"));
    }

    [Fact]
    public async Task UsesNetEaseWhenLrclibHasNothingAndPicksTheMatchingDuration()
    {
        var handler = new FakeHandler(url =>
        {
            if (url.Contains("lrclib")) return Json("[]");
            if (url.Contains("/search/get/web"))
                return Json("""{"code":200,"result":{"songs":[{"id":11,"name":"晴天","artists":[{"name":"别人"}],"duration":200000},{"id":22,"name":"晴天","artists":[{"name":"周杰伦"}],"duration":269000}]}}""");
            if (url.Contains("song/lyric?id=22"))
                return Json("{\"code\":200,\"lrc\":{\"lyric\":" + Quote("[00:00.00] 作词 : 周杰伦\n[00:00.50] 作曲 : 周杰伦\n" + Synced) + "}}");
            return Json("""{"code":200,"lrc":{"lyric":"[00:01.00]错的歌\n[00:02.00]错的歌"}}""");
        });
        var service = new LyricService(handler, _cache);
        var lines = await service.GetLyricsAsync("晴天", "周杰伦", TimeSpan.FromSeconds(269));
        Assert.Equal("netease", service.LastSource);
        Assert.Equal(["第一句", "第二句", "第三句"], lines.Select(l => l.Text));   // 作词作曲行被过滤
        Assert.Contains(handler.Urls, u => u.Contains("song/lyric?id=22"));
        Assert.DoesNotContain(handler.Urls, u => u.Contains("song/lyric?id=11"));

        handler.Urls.Clear();
        Assert.Equal(3, (await service.GetLyricsAsync("晴天", "周杰伦", TimeSpan.FromSeconds(269))).Count);
        Assert.Equal("cache", service.LastSource);
        Assert.Empty(handler.Urls);
    }

    [Fact]
    public async Task RemembersMissesForADayButManualRetryAsksAgain()
    {
        var handler = new FakeHandler(url => url.Contains("lrclib") ? Json("[]") : Json("""{"code":200,"result":{"songs":[]}}"""));
        var service = new LyricService(handler, _cache);
        Assert.Empty(await service.GetLyricsAsync("没有的歌", "某人", TimeSpan.FromSeconds(200)));
        Assert.InRange(handler.Urls.Count, 1, 12);

        handler.Urls.Clear();
        Assert.Empty(await service.GetLyricsAsync("没有的歌", "某人", TimeSpan.FromSeconds(200)));
        Assert.Empty(handler.Urls);

        Assert.Empty(await service.GetLyricsAsync("没有的歌", "某人", TimeSpan.FromSeconds(200), ignoreMissCache: true));
        Assert.NotEmpty(handler.Urls);
    }

    [Fact]
    public async Task NetworkFailureIsReportedAndNotRememberedAsMiss()
    {
        var down = new FakeHandler(_ => throw new HttpRequestException("offline"));
        var service = new LyricService(down, _cache);
        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetLyricsAsync("晴天", "周杰伦", TimeSpan.FromSeconds(269)));

        var up = new FakeHandler(url => url.Contains("lrclib")
            ? Json($$"""[{"id":1,"trackName":"晴天","artistName":"周杰伦","duration":269,"syncedLyrics":{{Quote(Synced)}}}]""")
            : Json("{}"));
        Assert.Equal(3, (await new LyricService(up, _cache).GetLyricsAsync("晴天", "周杰伦", TimeSpan.FromSeconds(269))).Count);
    }

    [Fact]
    public async Task InstrumentalTracksShowAFriendlyLine()
    {
        var handler = new FakeHandler(url => url.Contains("lrclib")
            ? Json("""[{"id":5,"trackName":"Interlude","artistName":"Band","duration":90,"instrumental":true,"syncedLyrics":null}]""")
            : Json("{}"));
        var service = new LyricService(handler, _cache);
        var lines = await service.GetLyricsAsync("Interlude", "Band", TimeSpan.FromSeconds(90));
        Assert.Equal("instrumental", service.LastSource);
        Assert.Contains("纯音乐", Assert.Single(lines).Text);
    }

    [Fact]
    public void ParsesColonFractionsAndMultipleTags()
    {
        var lines = new LyricService(new FakeHandler(_ => Json("[]")), _cache).Parse("[00:01:50][00:10.25]副歌\n[ti:标题]\n[00:03]句子");
        Assert.Equal([1.5, 3, 10.25], lines.Select(l => l.Time.TotalSeconds));
    }

    /// <summary>
    /// 真实网络探针：只在 CI 设置 LYRIC_PROBE=1 时运行，打印每首歌的命中来源，用来量命中率。
    /// 不设置时直接通过，所以本地和普通测试都不联网。
    /// </summary>
    [Fact]
    public async Task LiveProbe()
    {
        if (Environment.GetEnvironmentVariable("LYRIC_PROBE") != "1") return;
        var songs = new (string Title, string Artist, int Seconds)[]
        {
            ("晴天", "周杰伦", 269), ("稻香", "Jay Chou", 223), ("告白气球", "周杰伦", 215),
            ("千里之外 (feat. 费玉清)", "周杰伦, 费玉清", 255), ("光年之外", "G.E.M. 邓紫棋", 235),
            ("小幸运", "田馥甄", 265), ("演员", "薛之谦", 261), ("后来", "刘若英", 341), ("红豆", "王菲", 255),
            ("起风了", "买辣椒也用券", 325), ("孤勇者", "陈奕迅", 256), ("夜に駆ける", "YOASOBI", 261),
            ("Lemon", "米津玄師", 255), ("Pretender", "Official髭男dism", 327), ("Dynamite", "BTS", 199),
            ("Hype Boy", "NewJeans", 179), ("Shape of You", "Ed Sheeran", 233), ("Blinding Lights", "The Weeknd", 200),
            ("Here Comes The Sun - Remastered 2009", "The Beatles", 185), ("STAY (with Justin Bieber)", "The Kid LAROI, Justin Bieber", 141)
        };
        var service = new LyricService(cacheFolder: _cache);
        var hits = 0;
        foreach (var (title, artist, seconds) in songs)
        {
            string result;
            try
            {
                var lines = await service.GetLyricsAsync(title, artist, TimeSpan.FromSeconds(seconds));
                if (lines.Count > 0) hits++;
                result = lines.Count > 0 ? $"{service.LastSource,-12} {lines.Count,3} 行  首句：{lines.FirstOrDefault(l => l.Text != "♪")?.Text}" : "未找到";
            }
            catch (Exception ex) { result = "出错：" + ex.Message; }
            _output.WriteLine($"[LyricProbe] {title} - {artist}: {result}");
            Console.WriteLine($"[LyricProbe] {title} - {artist}: {result}");
        }
        Console.WriteLine($"[LyricProbe] 命中 {hits}/{songs.Length}");
        _output.WriteLine($"[LyricProbe] 命中 {hits}/{songs.Length}");
    }

    private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value);
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = Uri.UnescapeDataString(request.RequestUri!.ToString());
            lock (Urls) Urls.Add(url);
            return Task.FromResult(respond(url));
        }
    }
}
