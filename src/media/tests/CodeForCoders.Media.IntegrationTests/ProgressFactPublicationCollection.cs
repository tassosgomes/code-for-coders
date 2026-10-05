using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class ProgressFactPublicationCollection : ICollectionFixture<VideoFactReplayFixture>
{
    public const string Name = "progress-fact-publication";
}
