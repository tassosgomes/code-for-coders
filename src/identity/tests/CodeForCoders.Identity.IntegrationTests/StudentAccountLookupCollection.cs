using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class StudentAccountLookupCollection : ICollectionFixture<StudentAccountLookupFixture>
{
    public const string Name = "student-account-lookup";
}
