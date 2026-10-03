using System.Text.Json;
namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface ICourtesyGrantsClient
{
    Task<CourtesyGrantResponse> SendAsync(CourtesyGrantRequest request, CancellationToken cancellationToken);
}
