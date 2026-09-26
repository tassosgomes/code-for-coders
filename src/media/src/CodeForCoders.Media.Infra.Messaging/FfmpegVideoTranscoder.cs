using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class FfmpegVideoTranscoder(IOptions<VideoPreparationOptions> options) : IVideoTranscoder
{
    public async Task<VideoTranscodeResult> TranscodeAsync(
        string sourcePath,
        string outputDirectory,
        string keyInfoPath,
        CancellationToken cancellationToken)
    {
        var probe = await ProbeAsync(sourcePath, cancellationToken);
        var qualities = VideoQualityLadder.Select(probe.Width, probe.Height);
        Directory.CreateDirectory(outputDirectory);
        foreach (var quality in qualities)
        {
            Directory.CreateDirectory(Path.Combine(outputDirectory, quality.Name));
        }

        using (var activity = StartActivity("media.video.transcode"))
        {
            activity?.SetTag("video.qualities", string.Join(',', qualities.Select(quality => quality.Name)));
            await RunFfmpegAsync(sourcePath, outputDirectory, keyInfoPath, qualities, cancellationToken);
        }

        await WriteMasterPlaylistAsync(outputDirectory, qualities, cancellationToken);
        var storedBytes = Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
        return new VideoTranscodeResult(probe.DurationSeconds, qualities, storedBytes);
    }

    private async Task<ProbeResult> ProbeAsync(string sourcePath, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("media.video.probe");
        var output = await RunProcessAsync(
            options.Value.FfprobePath,
            [
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "stream=width,height:format=duration",
                "-of", "json",
                sourcePath,
            ],
            cancellationToken);
        using var document = JsonDocument.Parse(output);
        if (!document.RootElement.TryGetProperty("streams", out var streams)
            || streams.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("The uploaded file does not contain a video stream.");
        }

        var stream = streams[0];
        var width = stream.GetProperty("width").GetInt32();
        var height = stream.GetProperty("height").GetInt32();
        var durationText = document.RootElement.GetProperty("format").GetProperty("duration").GetString();
        if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
        {
            throw new InvalidOperationException("The uploaded video duration is invalid.");
        }
        if (width < 1 || height < 1 || duration <= 0)
        {
            throw new InvalidOperationException("The uploaded video metadata is invalid.");
        }

        return new ProbeResult(width, height, checked((int)Math.Ceiling(duration)));
    }

    private async Task RunFfmpegAsync(
        string sourcePath,
        string outputDirectory,
        string keyInfoPath,
        IReadOnlyList<VideoQuality> qualities,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-nostats", "-y", "-i", sourcePath,
        };
        foreach (var quality in qualities)
        {
            var qualityDirectory = Path.Combine(outputDirectory, quality.Name);
            var segmentPath = Path.Combine(qualityDirectory, "segment_%05d.ts");
            var playlistPath = Path.Combine(outputDirectory, $"{quality.Name}.m3u8");
            arguments.AddRange(
            [
                "-map", "0:v:0",
                "-map", "0:a:0?",
                "-vf", $"scale=-2:{quality.Height}",
                "-c:v", "libx264",
                "-preset", "veryfast",
                "-pix_fmt", "yuv420p",
                "-b:v", $"{quality.Bandwidth / 1000}k",
                "-maxrate", $"{quality.Bandwidth / 1000}k",
                "-bufsize", $"{quality.Bandwidth / 500}k",
                "-c:a", "aac",
                "-b:a", "128k",
                "-ac", "2",
                "-hls_time", "6",
                "-hls_playlist_type", "vod",
                "-hls_flags", "independent_segments",
                "-hls_base_url", $"{quality.Name}/",
                "-hls_key_info_file", keyInfoPath,
                "-hls_segment_filename", segmentPath,
                "-f", "hls",
                playlistPath,
            ]);
        }

        await RunProcessAsync(options.Value.FfmpegPath, arguments, cancellationToken);
    }

    private static async Task WriteMasterPlaylistAsync(
        string outputDirectory,
        IReadOnlyList<VideoQuality> qualities,
        CancellationToken cancellationToken)
    {
        var contents = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n");
        foreach (var quality in qualities)
        {
            contents.Append("#EXT-X-STREAM-INF:BANDWIDTH=")
                .Append(quality.Bandwidth.ToString(CultureInfo.InvariantCulture))
                .Append(",RESOLUTION=")
                .Append(quality.Width.ToString(CultureInfo.InvariantCulture))
                .Append('x')
                .Append(quality.Height.ToString(CultureInfo.InvariantCulture))
                .AppendLine()
                .AppendLine($"{quality.Name}.m3u8");
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "master.m3u8"),
            contents.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The child process must be terminated and awaited if cancellation occurs.")]
    private static async Task<string> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        _ = await standardError;
        var output = await standardOutput;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Video preparation tool exited with code {process.ExitCode}.");
        }

        return output;
    }

    private static Activity? StartActivity(string name)
    {
        var activity = MediaTelemetry.ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("video.status", "preparing");
        return activity;
    }

    private sealed record ProbeResult(int Width, int Height, int DurationSeconds);
}
