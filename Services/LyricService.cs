using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MiyaIsland.Models;

namespace MiyaIsland.Services;

/// <summary>
/// 在线歌词：本地缓存 → lrclib（多种写法）→ 网易云音乐（非官方接口，作为中文歌的后备）。
/// 命中一个就停；全部没找到的歌会记 24 小时，期间不再联网查。
/// </summary>
public sealed class LyricService
{
    private static readonly Regex TimeTag = new(@"\[(\d{1,3}):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
    private static readonly Regex CreditLine = new(@"^(?:作词|作曲|编曲|制作人|监制|词|曲|編曲|作詞|lyrics?\s+by|composed\s+by|arranged\s+by|producer)\s*[:：]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly TimeSpan MissLifetime = TimeSpan.FromHours(24);
    private const string InstrumentalLrc = "[00:00.00]纯音乐，请欣赏 ♪";
    private const string BrowserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";
    private readonly HttpClient _client;
    private readonly string _cacheFolder;

    public LyricService(HttpMessageHandler? handler = null, string? cacheFolder = null)
    {
        _client = handler is null ? new HttpClient() : new HttpClient(handler);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("MiyaIsland/0.7 (personal Windows lyric display)");
        _client.Timeout = TimeSpan.FromSeconds(12);
        _cacheFolder = cacheFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiyaIsland",
            "lyrics");
        Directory.CreateDirectory(_cacheFolder);
    }

    /// <summary>最近一次查询命中的来源（cache / lrclib / netease / instrumental），用于测试和排查。</summary>
    public string? LastSource { get; private set; }

    public string MakeTrackKey(string title, string artist) =>
        $"{Normalize(title)}|{Normalize(artist)}";

    public async Task<IReadOnlyList<LyricLine>> GetLyricsAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken token = default,
        bool ignoreMissCache = false)
    {
        LastSource = null;
        var key = MakeTrackKey(title, artist);
        var cached = await TryReadCacheAsync(key, token);
        if (cached.Count > 0) { LastSource = "cache"; return cached; }
        if (!ignoreMissCache && IsRecentMiss(key)) return [];

        var responded = false;
        Exception? lastError = null;
        var sources = new (string Name, Func<Task<string?>> Fetch)[]
        {
            ("lrclib", () => FromLrclibAsync(title, artist, duration, token)),
            ("netease", () => FromNetEaseAsync(title, artist, duration, token))
        };
        foreach (var (name, fetch) in sources)
        {
            try
            {
                var lrc = await fetch();
                responded = true;
                if (lrc is null) continue;
                var parsed = Parse(lrc);
                if (parsed.Count == 0) continue;
                await WriteCacheAsync(key, lrc, token);
                LastSource = lrc == InstrumentalLrc ? "instrumental" : name;
                return parsed;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                // 某个来源失败（超时、接口变化）就换下一个，不影响整体。
                lastError = ex;
            }
        }

        if (!responded && lastError is not null) throw new HttpRequestException("所有歌词来源都无法访问", lastError);
        RememberMiss(key);
        return [];
    }

    // ---------- lrclib ----------

    private async Task<string?> FromLrclibAsync(string title, string artist, TimeSpan duration, CancellationToken token)
    {
        var candidates = new List<(double Score, string? Synced, bool Instrumental)>();
        var seen = new HashSet<long>();
        var responded = false;
        Exception? lastError = null;

        async Task Collect(string url)
        {
            try
            {
                using var response = await _client.GetAsync(url, token);
                responded = true;
                if (response.StatusCode == HttpStatusCode.NotFound) return;
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
                if (document.RootElement.ValueKind != JsonValueKind.Array) return;
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id) && id.TryGetInt64(out var value) && !seen.Add(value)) continue;
                    var score = LyricQuery.Score(title, artist, duration,
                        Text(item, "trackName") ?? Text(item, "name") ?? "", Text(item, "artistName") ?? "",
                        item.TryGetProperty("duration", out var d) && d.TryGetDouble(out var seconds) ? seconds : 0);
                    if (score is null) continue;
                    var synced = Text(item, "syncedLyrics");
                    var instrumental = item.TryGetProperty("instrumental", out var flag) && flag.ValueKind == JsonValueKind.True;
                    candidates.Add((score.Value, LyricQuery.IsSynced(synced) ? synced : null, instrumental));
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                lastError = ex;
            }
        }

        string? Best() =>
            candidates.Where(c => c.Synced is not null).OrderByDescending(c => c.Score).Select(c => c.Synced).FirstOrDefault();

        foreach (var (variantTitle, variantArtist) in LyricQuery.Variants(title, artist))
        {
            var url = "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(variantTitle) +
                      (variantArtist.Length > 0 ? "&artist_name=" + Uri.EscapeDataString(variantArtist) : "");
            await Collect(url);
            if (Best() is { } hit) return hit;
        }

        var main = LyricQuery.SplitArtists(artist).FirstOrDefault() ?? "";
        await Collect("https://lrclib.net/api/search?q=" + Uri.EscapeDataString((LyricQuery.CleanTitle(title) + " " + main).Trim()));
        if (Best() is { } free) return free;

        // 只有“纯音乐”结果，并且它是最匹配的那一个时，才显示纯音乐提示
        if (candidates.Count > 0 && candidates.MaxBy(c => c.Score).Instrumental) return InstrumentalLrc;

        if (!responded && lastError is not null) throw new HttpRequestException("lrclib 无法访问", lastError);
        return null;
    }

    // ---------- 网易云音乐（非官方接口，随时可能变化；失败就静默跳过） ----------

    private async Task<string?> FromNetEaseAsync(string title, string artist, TimeSpan duration, CancellationToken token)
    {
        var main = LyricQuery.SplitArtists(artist).FirstOrDefault() ?? "";
        var cleaned = LyricQuery.CleanTitle(title);
        var queries = new[] { (cleaned + " " + main).Trim(), cleaned }.Distinct().ToList();
        var candidates = new List<(double Score, long Id)>();
        var seen = new HashSet<long>();
        var endpoints = new[]
        {
            "https://music.163.com/api/search/get/web?type=1&offset=0&limit=10&s=",
            "https://music.163.com/api/cloudsearch/pc?type=1&offset=0&limit=10&s="
        };

        foreach (var query in queries)
        {
            foreach (var endpoint in endpoints)
            {
                using var document = await NetEaseJsonAsync(endpoint + Uri.EscapeDataString(query), token);
                if (document is null || !document.RootElement.TryGetProperty("result", out var result) ||
                    result.ValueKind != JsonValueKind.Object ||
                    !result.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array) continue;
                foreach (var song in songs.EnumerateArray())
                {
                    if (!song.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out var id) || !seen.Add(id)) continue;
                    var artists = song.TryGetProperty("artists", out var a) ? a : song.TryGetProperty("ar", out var ar) ? ar : default;
                    var artistNames = artists.ValueKind == JsonValueKind.Array
                        ? string.Join(", ", artists.EnumerateArray().Select(x => Text(x, "name")).Where(x => !string.IsNullOrWhiteSpace(x)))
                        : "";
                    var milliseconds = song.TryGetProperty("duration", out var d) && d.TryGetDouble(out var ms) ? ms
                        : song.TryGetProperty("dt", out var dt) && dt.TryGetDouble(out var ms2) ? ms2 : 0;
                    var score = LyricQuery.Score(title, artist, duration, Text(song, "name") ?? "", artistNames, milliseconds / 1000);
                    if (score is not null) candidates.Add((score.Value, id));
                }
                if (candidates.Count > 0) break;
            }
            if (candidates.Count > 0) break;
        }

        foreach (var (_, id) in candidates.OrderByDescending(c => c.Score).Take(2))
        {
            using var document = await NetEaseJsonAsync($"https://music.163.com/api/song/lyric?id={id}&lv=1&kv=1&tv=-1", token);
            if (document is null || !document.RootElement.TryGetProperty("lrc", out var lrc)) continue;
            var lyric = Text(lrc, "lyric");
            if (LyricQuery.IsSynced(lyric)) return lyric;
        }
        return null;
    }

    private async Task<JsonDocument?> NetEaseJsonAsync(string url, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Referrer = new Uri("https://music.163.com/");
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.ParseAdd(BrowserAgent);
        using var response = await _client.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode) return null;
        var text = (await response.Content.ReadAsStringAsync(timeout.Token)).TrimStart();
        if (text.Length == 0 || text[0] != '{') return null;
        var document = JsonDocument.Parse(text);
        if (document.RootElement.TryGetProperty("code", out var code) && code.TryGetInt32(out var value) && value != 200)
        {
            document.Dispose();
            return null;
        }
        return document;
    }

    // ---------- 导入、解析、缓存 ----------

    public async Task<IReadOnlyList<LyricLine>> ImportAsync(
        string title,
        string artist,
        string sourceFile,
        CancellationToken token = default)
    {
        var content = await File.ReadAllTextAsync(sourceFile, token);
        var parsed = Parse(content);
        if (parsed.Count == 0) return [];
        await WriteCacheAsync(MakeTrackKey(title, artist), content, token);
        return parsed;
    }

    public IReadOnlyList<LyricLine> Parse(string lrc)
    {
        var result = new List<LyricLine>();
        foreach (var rawLine in lrc.Replace("\r", string.Empty).Split('\n'))
        {
            var matches = TimeTag.Matches(rawLine);
            if (matches.Count == 0) continue;
            var text = rawLine[(matches[^1].Index + matches[^1].Length)..].Trim();
            if (CreditLine.IsMatch(text)) continue;
            if (string.IsNullOrWhiteSpace(text)) text = "♪";

            foreach (Match match in matches)
            {
                if (!int.TryParse(match.Groups[1].Value, out var minutes)) continue;
                if (!int.TryParse(match.Groups[2].Value, out var seconds)) continue;
                var fraction = match.Groups[3].Success
                    ? double.Parse("0." + match.Groups[3].Value, CultureInfo.InvariantCulture)
                    : 0;
                result.Add(new LyricLine(TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds + fraction), text));
            }
        }
        return result.OrderBy(line => line.Time).ToList();
    }

    private async Task<IReadOnlyList<LyricLine>> TryReadCacheAsync(string key, CancellationToken token)
    {
        var path = GetCachePath(key);
        if (!File.Exists(path)) return [];
        try
        {
            return Parse(await File.ReadAllTextAsync(path, token));
        }
        catch
        {
            return [];
        }
    }

    private async Task WriteCacheAsync(string key, string content, CancellationToken token)
    {
        await File.WriteAllTextAsync(GetCachePath(key), content, Encoding.UTF8, token);
        ForgetMiss(key);
    }

    private bool IsRecentMiss(string key)
    {
        var path = GetCachePath(key) + ".miss";
        return File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < MissLifetime;
    }

    private void RememberMiss(string key)
    {
        try { File.WriteAllText(GetCachePath(key) + ".miss", DateTime.UtcNow.ToString("O")); } catch { }
    }

    private void ForgetMiss(string key)
    {
        try { File.Delete(GetCachePath(key) + ".miss"); } catch { }
    }

    private string GetCachePath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(_cacheFolder, hash + ".lrc");
    }

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Normalize(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
}
