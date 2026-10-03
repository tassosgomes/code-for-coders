using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CodeForCoders.Media.EndToEndTests;

internal sealed class MediaRoleFactory(
    string role,
    string connectionString,
    string rabbitMqHost,
    int rabbitMqPort,
    bool omitRole = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("EndToEndTest");
        builder.UseSetting("Playback:Delivery:SharedSecret", "development-test-secret");
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        if (!omitRole)
        {
            builder.UseSetting("Media:Role", role);
        }

        builder.UseSetting("RabbitMq:Host", rabbitMqHost);
        builder.UseSetting("RabbitMq:Port", rabbitMqPort.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.UseSetting("Preparation:MasterKey", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("Preparation:MasterKeyId", "media-e2e-test");
    }
}
