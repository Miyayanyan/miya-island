global using System.IO;
global using System.Net.Http;

namespace MiyaIsland.Services;

public sealed record DownloadProgressInfo(string FileName, double Percent, string Status);

public sealed class DownloadService
{
    private readonly HttpClient _client = new();

    public async Task DownloadAsync(string url, string targetFile, IProgress<DownloadProgressInfo> progress, CancellationToken token)
    {
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        var fileName = Path.GetFileName(targetFile);

        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            received += read;
            var percent = total is > 0 ? received * 100d / total.Value : 0;
            var status = total is > 0 ? $"{percent:0}%" : $"{received / 1024d / 1024d:0.0} MB";
            var speed = received / Math.Max(.01, watch.Elapsed.TotalSeconds) / 1024 / 1024;
            progress.Report(new DownloadProgressInfo(fileName, percent, $"{status} · {speed:0.0} MB/s"));
        }

        progress.Report(new DownloadProgressInfo(fileName, 100, "下载完成"));
    }
}
