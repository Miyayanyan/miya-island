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

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    }

    public async Task<MediaSnapshot> GetCurrentAsync()
    {
        var session = _manager?.GetCurrentSession();
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
        var session = _manager?.GetCurrentSession();
        if (session is null) return;
        var status = session.GetPlaybackInfo().PlaybackStatus;
        if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            await session.TryPauseAsync();
        else
            await session.TryPlayAsync();
    }

    public async Task PreviousAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (session is not null) await session.TrySkipPreviousAsync();
    }

    public async Task NextAsync()
    {
        var session = _manager?.GetCurrentSession();
        if (session is not null) await session.TrySkipNextAsync();
    }
}
