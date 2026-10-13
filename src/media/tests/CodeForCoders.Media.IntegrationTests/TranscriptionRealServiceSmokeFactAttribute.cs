using System.Runtime.CompilerServices;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

/// <summary>MTP class filters do not automatically enable xUnit explicit tests.</summary>
public sealed class TranscriptionRealServiceSmokeFactAttribute : FactAttribute
{
    public TranscriptionRealServiceSmokeFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        var arguments = Environment.GetCommandLineArgs();
        var className = typeof(TranscriptionRealServiceSmokeTests).FullName;
        Explicit = !arguments.Zip(arguments.Skip(1)).Any(pair =>
            pair.First == "--filter-class" && pair.Second == className);
    }
}
