using System.Windows.Threading;
using MiyaIsland.Models;

namespace MiyaIsland.Services;

public sealed record MusicState
{
    public System.Windows.Media.Imaging.BitmapSource? Cover { get; init; }
    public bool HasSession { get; init; }
    public string Title { get; init; } = "没有正在播放的音乐";
    public string Artist { get; init; } = "打开音乐软件后会显示在这里";
    public bool IsPlaying { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
    public string PreviousLine { get; init; } = "";
    public string CurrentLine { get; init; } = "播放音乐后自动匹配歌词";
    public string NextLine { get; init; } = "";
    public bool HasLyrics { get; init; }
    public string TrackKey { get; init; } = "";
}

public sealed class MusicSession : IDisposable
{
    private readonly MediaService _media = new();
    private readonly LyricService _lyrics = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private IReadOnlyList<LyricLine> _lines = [];
    private bool _refreshing;
    private bool _disposed;
    private string _status = "播放音乐后自动匹配歌词";
    private int _lyricRevision;
    private CancellationTokenSource? _lyricCancel;
    public MusicState State { get; private set; } = new();
    public event Action<MusicState>? StateChanged;
    public event Action<MusicState>? TrackChanged;
    public MusicSession() { _timer.Tick += async (_, _) => await RefreshAsync(); }
    public async Task StartAsync()
    {
        try { await _media.InitializeAsync(); await RefreshAsync(); }
        catch { _status = "播放音乐后自动匹配歌词"; Publish(); }
        if (!_disposed) _timer.Start();
    }
    private void Publish() { if (!_disposed) StateChanged?.Invoke(State); }
    private async Task RefreshAsync()
    {
        if (_refreshing || _disposed) return;
        _refreshing = true;
        try
        {
            var snapshot = await _media.GetCurrentAsync();
            if (_disposed) return;
            var key = snapshot.HasSession ? _lyrics.MakeTrackKey(snapshot.Title, snapshot.Artist) : "";
            var changed = !string.Equals(key, State.TrackKey, StringComparison.Ordinal);
            State = State with { Cover = snapshot.Cover, HasSession = snapshot.HasSession, Title = snapshot.Title, Artist = snapshot.Artist,
                IsPlaying = snapshot.IsPlaying, Position = snapshot.Position, Duration = snapshot.Duration, TrackKey = key };
            if (!snapshot.HasSession)
            {
                _lines = []; _status = "播放音乐后自动匹配歌词";
            }
            else if (changed)
            {
                TrackChanged?.Invoke(State);
                await LoadLyricsAsync(snapshot.Title, snapshot.Artist, snapshot.Duration, ignoreMissCache: false);
                if (_disposed) return;
            }
            UpdateCursor(); Publish();
        }
        finally { _refreshing = false; }
    }
    // 切歌时取消上一首还没查完的歌词；revision 保证晚到的结果不会盖掉新歌
    private async Task LoadLyricsAsync(string title, string artist, TimeSpan duration, bool ignoreMissCache)
    {
        _lyricCancel?.Cancel();
        var cancel = _lyricCancel = new CancellationTokenSource();
        _lines = []; _status = "正在匹配歌词…"; UpdateCursor(); Publish();
        var revision = ++_lyricRevision;
        try
        {
            var lines = await _lyrics.GetLyricsAsync(title, artist, duration, cancel.Token, ignoreMissCache);
            if (_disposed || revision != _lyricRevision) return;
            _lines = lines; _status = "暂未找到同步歌词，可导入 LRC";
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        catch { if (revision == _lyricRevision) _status = "歌词获取失败，可稍后重试或导入 LRC"; }
    }

    public bool CanRetryLyrics => State.HasSession && _lines.Count == 0 && _status != "正在匹配歌词…";

    /// <summary>手动重新查找：忽略“24 小时内没找到”的记录。</summary>
    public async Task RetryLyricsAsync()
    {
        if (!CanRetryLyrics) return;
        var state = State;
        await LoadLyricsAsync(state.Title, state.Artist, state.Duration, ignoreMissCache: true);
        if (_disposed || State.TrackKey != state.TrackKey) return;
        UpdateCursor(); Publish();
    }

    private void UpdateCursor()
    {
        var cursor = LyricCursor.Locate(_lines, State.Position);
        State = State with { HasLyrics = _lines.Count > 0, PreviousLine = cursor.Previous,
            CurrentLine = _lines.Count > 0 ? cursor.Current : _status, NextLine = cursor.Next };
    }
    public async Task TogglePlayAsync() { await _media.ToggleAsync(); await RefreshAsync(); }
    public async Task NextAsync() { await _media.NextAsync(); await RefreshAsync(); }
    public async Task PreviousAsync() { await _media.PreviousAsync(); await RefreshAsync(); }
    public async Task ImportLyricsAsync(string path)
    {
        if (!State.HasSession) throw new InvalidOperationException("请先播放一首歌再导入歌词");
        var state = State; var revision = ++_lyricRevision;
        var imported = await _lyrics.ImportAsync(state.Title, state.Artist, path);
        if (_disposed || State.TrackKey != state.TrackKey || revision != _lyricRevision) return;
        if (imported.Count == 0) throw new InvalidDataException("文件里没有识别到时间标签");
        _lines = imported; UpdateCursor(); Publish();
    }
    public void Dispose() { _disposed = true; _timer.Stop(); _lyricRevision++; _lyricCancel?.Cancel(); }
}
