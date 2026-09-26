using System.Diagnostics;

namespace CodeForCoders.Media.IntegrationTests;

internal static class FfmpegTools
{
    private const int VersionTimeoutSeconds = 30;
    private const int InstallTimeoutMinutes = 10;

    private static readonly SemaphoreSlim ProvisioningLock = new(1, 1);
    private static (string Ffmpeg, string Ffprobe)? resolved;

    public static async Task<(string Ffmpeg, string Ffprobe)> EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        if (resolved.HasValue)
        {
            return resolved.Value;
        }

        await ProvisioningLock.WaitAsync(cancellationToken);
        try
        {
            if (resolved.HasValue)
            {
                return resolved.Value;
            }

            var ffmpeg = FindOnPath("ffmpeg");
            var ffprobe = FindOnPath("ffprobe");
            if (ffmpeg is null || ffprobe is null)
            {
                await InstallWithAptAsync(cancellationToken);
                ffmpeg = FindOnPath("ffmpeg");
                ffprobe = FindOnPath("ffprobe");
            }

            if (ffmpeg is null || ffprobe is null)
            {
                throw new InvalidOperationException(
                    "ffmpeg and ffprobe are required by the media integration tests but were not found. " +
                    "Install ffmpeg with the system package manager (for example: sudo apt-get install -y ffmpeg).");
            }

            resolved = (ffmpeg, ffprobe);
            return resolved.Value;
        }
        finally
        {
            ProvisioningLock.Release();
        }
    }

    public static async Task<string> ReadVersionAsync(string executable, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(executable, "-version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(VersionTimeoutSeconds), cancellationToken);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException(
                $"The '{executable} -version' probe failed with exit code {process.ExitCode}: {error}");
        }

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            try
            {
                var candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                continue;
            }
        }

        return null;
    }

    private static async Task InstallWithAptAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(InstallTimeoutMinutes));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var update = await RunAsync("sudo", "-n apt-get update", linked.Token);
        if (update.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ffmpeg is missing and the automatic install failed (apt-get update exit code {update.ExitCode}): {update.Output}");
        }

        var install = await RunAsync("sudo", "-n apt-get install -y ffmpeg", linked.Token);
        if (install.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ffmpeg is missing and the automatic install failed (apt-get install exit code {install.ExitCode}): {install.Output}");
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, $"{output}{error}".Trim());
    }
}
