using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.SeedWork;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed partial class FfmpegTranscriptionAudioExtractor(
    IMediaStoragePort storage,
    IPlaybackPlaylistReader playlists,
    IVideoKeyProtector keyProtector,
    IOptions<VideoPreparationOptions> options) : ITranscriptionAudioExtractor
{
    public async Task ExtractAndUploadAsync(TranscriptionWorkItem work, string workDirectory, CancellationToken cancellationToken)
    {
        var directory = work.LocalDirectory(workDirectory);
        byte[]? key = null;
        try
        {
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var prefix = $"{work.Attempt.TenantId:D}/{work.Attempt.VideoId:D}/hls";
            using (MediaTelemetry.BeginTranscriptionStage("extract-audio", work.Video.VideoId, work.Attempt.TranscriptionId))
            {
                var master = await playlists.ReadPlaylistAsync(prefix + "/master.m3u8", cancellationToken);
                var variant = SelectVariant(master);
                var playlist = await playlists.ReadPlaylistAsync(prefix + "/" + variant, cancellationToken);
                var keyPath = Path.Combine(directory, "key.bin");
                key = keyProtector.Unprotect(work.Video.VideoId, work.Video.MasterKeyId!, work.Video.EncryptedVideoKey!);
                await WritePrivateKeyAsync(keyPath, key, cancellationToken);
                var localPlaylist = RewritePlaylist(playlist, work.Video.VideoId, keyPath);
                foreach (var resource in ResourceLines(playlist).Distinct(StringComparer.Ordinal))
                {
                    if (!SegmentPath().IsMatch(resource)) throw InvalidPlaylist();
                    var path = Path.Combine(directory, resource);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await storage.DownloadObjectAsync(prefix + "/" + resource, path, cancellationToken);
                }
                var playlistPath = Path.Combine(directory, "source.m3u8");
                await File.WriteAllTextAsync(playlistPath, localPlaylist, cancellationToken);
                var outputDirectory = Path.Combine(directory, "audio");
                Directory.CreateDirectory(outputDirectory);
                await RunFfmpegAsync(playlistPath, Path.Combine(outputDirectory, "audio.m4a"), cancellationToken);
            }
            using (MediaTelemetry.BeginTranscriptionStage("upload-audio", work.Video.VideoId, work.Attempt.TranscriptionId))
                await storage.UploadDirectoryAsync(Path.Combine(directory, "audio"), work.AudioObjectKey[..work.AudioObjectKey.LastIndexOf('/')], cancellationToken);
        }
        finally
        {
            if (key is not null) CryptographicOperations.ZeroMemory(key);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            var parent = Path.GetDirectoryName(directory)!;
            if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        }
    }

    private static string SelectVariant(string master)
    {
        var lines = master.Replace("\r", "", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var variants = new List<(long Bandwidth, string Path)>();
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
            {
                if (!lines[index].StartsWith('#')) throw InvalidPlaylist();
                continue;
            }
            var match = Bandwidth().Match(lines[index]);
            if (!match.Success || !long.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var bandwidth)
                || bandwidth <= 0 || index + 1 >= lines.Length || !VariantPath().IsMatch(lines[index + 1])) throw InvalidPlaylist();
            variants.Add((bandwidth, lines[++index]));
        }
        return variants.OrderBy(variant => variant.Bandwidth).ThenBy(variant => variant.Path, StringComparer.Ordinal)
            .FirstOrDefault().Path ?? throw InvalidPlaylist();
    }

    private static string RewritePlaylist(string playlist, Guid videoId, string keyPath)
    {
        var keySeen = false;
        var lines = playlist.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0) continue;
            if (!line.StartsWith('#'))
            {
                if (!SegmentPath().IsMatch(line)) throw InvalidPlaylist();
                lines[index] = line;
                continue;
            }
            // Resource-bearing tags other than the expected AES key cannot introduce a second input.
            if (line.Contains("URI=", StringComparison.Ordinal))
            {
                var match = KeyUri().Match(line);
                if (!line.StartsWith("#EXT-X-KEY:METHOD=AES-128,", StringComparison.Ordinal)
                    || !match.Success || match.Groups[1].Value != $"c4c-key:{videoId:D}" || keySeen) throw InvalidPlaylist();
                lines[index] = line.Replace(match.Value, "URI=\"" + keyPath + "\"", StringComparison.Ordinal);
                keySeen = true;
            }
        }
        if (!keySeen || !ResourceLines(playlist).Any()) throw InvalidPlaylist();
        return string.Join('\n', lines);
    }

    private static IEnumerable<string> ResourceLines(string playlist)
        => playlist.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(line => !line.StartsWith('#'));

    private static async Task WritePrivateKeyAsync(string path, byte[] key, CancellationToken cancellationToken)
    {
        var settings = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) settings.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var file = new FileStream(path, settings);
        await file.WriteAsync(key, cancellationToken);
    }

    private async Task RunFfmpegAsync(string playlistPath, string outputPath, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(options.Value.FfmpegPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-nostats", "-y", "-allowed_extensions", "ALL",
            "-protocol_whitelist", "file,crypto", "-i", playlistPath, "-vn", "-ac", "1", "-ar", "16000", "-c:a", "aac",
            "-b:a", "48k", "-movflags", "+faststart", "-threads", "1", outputPath }) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw;
        }
        // Discard tool output: it may contain resource paths or key data.
        await Task.WhenAll(stdout, stderr);
        if (process.ExitCode != 0) throw new EntityValidationException("Transcription audio extraction failed.");
    }

    private static EntityValidationException InvalidPlaylist() => new("The transcription source playlist is invalid.");
    [GeneratedRegex(@"^[A-Za-z0-9]+/segment_[0-9]+\.ts$")]
    private static partial Regex SegmentPath();
    [GeneratedRegex(@"^[A-Za-z0-9]+\.m3u8$")]
    private static partial Regex VariantPath();
    [GeneratedRegex(@"(?:[:,])BANDWIDTH=([0-9]+)(?:,|$)")]
    private static partial Regex Bandwidth();
    [GeneratedRegex("URI=\"([^\"]+)\"")]
    private static partial Regex KeyUri();
}
