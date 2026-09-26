namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class CommerceApiOptions
{
    public const string SectionName = "Commerce";

    public string BaseAddress { get; set; } = "http://localhost:5104/";
}
