using Windows.Media.Control;
using System.Windows.Media.Imaging;

namespace MiyaIsland.Services;

public sealed class MediaSnapshot
{
    public BitmapSource? Cover { get; init; }
    public string Title { get; init; } = "没有正在播放的音乐";
    public string Artist { get; init; } = "打开音乐软件后会显示在这里";
    public bool IsPlaying { get; init; }
    public bool HasSession { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed class MediaService
{
    private string _thumbnailKey = "";
    private BitmapSource? _thumbnail;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string? _chosenApp;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    }

    /// <summary>
    /// 选一个会话：Windows 的“当前会话”不一定是正在放歌的那个（比如浏览器里暂停的视频），
    /// 所以优先选正在播放的；上一次选中的如果还在播放就继续用它，避免来回跳。
    /// </summary>
    private GlobalSystemMediaTransportControlsSession? PickSession()
    {
        if (_manager is null) return null;
        var current = _manager.GetCurrentSession();
        IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions;
        try { sessions = _manager.GetSessions(); } catch { return current; }
        if (sessions.Count == 0) { _chosenApp = null; return current; }
        static bool Playing(GlobalSystemMediaTransportControlsSession s)
        {
            try { return s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }
            catch { return false; }
        }
        var playing = sessions.Where(Playing).ToList();
        var chosen = playing.FirstOrDefault(s => s.SourceAppUserModelId == _chosenApp)
            ?? (current is not null && playing.Any(s => s.SourceAppUserModelId == current.SourceAppUserModelId) ? current : null)
            ?? playing.FirstOrDefault()
            ?? sessions.FirstOrDefault(s => s.SourceAppUserModelId == _chosenApp)
            ?? current
            ?? sessions[0];
        _chosenApp = chosen.SourceAppUserModelId;
        return chosen;
    }

    public async Task<MediaSnapshot> GetCurrentAsync()
    {
        var session = PickSession();
        if (session is null) { _thumbnailKey = ""; _thumbnail = null; return new MediaSnapshot(); }

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            var thumbnailKey = session.SourceAppUserModelId + "\n" + properties.Title + "\n" + properties.Artist + "\n" + properties.AlbumTitle;
            if (_thumbnailKey != thumbnailKey)
            {
                _thumbnailKey = thumbnailKey; _thumbnail = null;
                if (properties.Thumbnail is not null)
                    try
                    {
                        using var random = await properties.Thumbnail.OpenReadAsync();
                        using var stream = random.AsStreamForRead();
                        var cover = new BitmapImage(); cover.BeginInit(); cover.CacheOption = BitmapCacheOption.OnLoad;
                        cover.DecodePixelWidth = 256; cover.StreamSource = stream; cover.EndInit(); cover.Freeze(); _thumbnail = cover;
                    }
                    catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("专辑封面不可用：" + ex.Message); }
            }
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var isPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var position = timeline.Position;
            if (isPlaying && timeline.LastUpdatedTime != default)
                position += DateTimeOffset.Now - timeline.LastUpdatedTime;
            var duration = timeline.EndTime > timeline.StartTime
                ? timeline.EndTime - timeline.StartTime
                : timeline.EndTime;
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            if (duration > TimeSpan.Zero && position > duration) position = duration;
            return new MediaSnapshot
            {
                Cover = _thumbnail,
                Title = string.IsNullOrWhiteSpace(properties.Title) ? "未知歌曲" : properties.Title,
                Artist = string.IsNullOrWhiteSpace(properties.Artist) ? session.SourceAppUserModelId : properties.Artist,
                IsPlaying = isPlaying,
                HasSession = true,
                Position = position,
                Duration = duration
            };
        }
        catch
        {
            return new MediaSnapshot();
        }
    }

    public async Task ToggleAsync()
    {
        var session = PickSession();
        if (session is null) return;
        var status = session.GetPlaybackInfo().PlaybackStatus;
        if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            await session.TryPauseAsync();
        else
            await session.TryPlayAsync();
    }

    public async Task PreviousAsync()
    {
        var session = PickSession();
        if (session is not null) await session.TrySkipPreviousAsync();
    }

    public async Task NextAsync()
    {
        var session = PickSession();
        if (session is not null) await session.TrySkipNextAsync();
    }
}
