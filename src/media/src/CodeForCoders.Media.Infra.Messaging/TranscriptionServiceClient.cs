using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.SeedWork;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class TranscriptionServiceClient(HttpClient httpClient, IOptions<TranscriptionServiceOptions> options) : ITranscriptionServiceClient
{
    public async Task<string> SubmitAsync(TranscriptionSubmission submission, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, "transcriptions");
        request.Headers.Add("Idempotency-Key", $"c4c-{submission.TranscriptionId:D}-{submission.SubmissionNumber}");
        request.Content = JsonContent.Create(new { sourceUrl = submission.SourceUrl.AbsoluteUri, clientReference = submission.TranscriptionId.ToString("D") });
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await ReadBoundedAsync(response, cancellationToken));
        return document.RootElement.GetProperty("jobId").GetString() ?? throw new EntityValidationException("The transcription job identifier is absent.");
    }

    public async Task<string?> GetStatusAsync(string jobId, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, $"transcriptions/{Uri.EscapeDataString(jobId)}");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await ReadBoundedAsync(response, cancellationToken));
        return document.RootElement.GetProperty("status").GetString();
    }

    public async Task<byte[]?> GetResultAsync(string jobId, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, $"transcriptions/{Uri.EscapeDataString(jobId)}/result");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await ReadBoundedAsync(response, cancellationToken);
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(options.Value.BaseAddress.TrimEnd('/') + "/" + path));
        request.Headers.Add("X-API-Key", options.Value.ApiKey);
        return request;
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > TranscriptionResultValidation.MaximumJsonBytes)
            throw new EntityValidationException("The transcription response exceeds the size limit.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (destination.Length + count > TranscriptionResultValidation.MaximumJsonBytes)
                throw new EntityValidationException("The transcription response exceeds the size limit.");
            destination.Write(buffer, 0, count);
        }
        return destination.ToArray();
    }
}
