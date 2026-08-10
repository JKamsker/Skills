using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleCli.Runtime;

public sealed record HttpExchangeSnapshot(
    string? RequestMethod = null,
    string? RequestUri = null,
    string? RequestHeaders = null,
    string? RequestBody = null,
    int? ResponseStatusCode = null,
    string? ResponseReasonPhrase = null,
    string? ResponseHeaders = null,
    string? ResponseBody = null);

public sealed class HttpDiagnosticsContext
{
    private readonly object _gate = new();
    private HttpExchangeSnapshot? _snapshot;

    public HttpExchangeSnapshot? Snapshot
    {
        get
        {
            lock (_gate)
                return _snapshot;
        }
    }

    public async Task CaptureRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var snapshot = await HttpExchangeSnapshotFactory.FromRequestAsync(request, cancellationToken);
        lock (_gate)
        {
            _snapshot = (_snapshot ?? new HttpExchangeSnapshot()) with
            {
                RequestMethod = snapshot.RequestMethod,
                RequestUri = snapshot.RequestUri,
                RequestHeaders = snapshot.RequestHeaders,
                RequestBody = snapshot.RequestBody,
            };
        }
    }

    public async Task CaptureResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var snapshot = await HttpExchangeSnapshotFactory.FromResponseAsync(response, cancellationToken);
        lock (_gate)
        {
            _snapshot = (_snapshot ?? new HttpExchangeSnapshot()) with
            {
                ResponseStatusCode = snapshot.ResponseStatusCode,
                ResponseReasonPhrase = snapshot.ResponseReasonPhrase,
                ResponseHeaders = snapshot.ResponseHeaders,
                ResponseBody = snapshot.ResponseBody,
            };
        }
    }
}

internal static class HttpExchangeSnapshotFactory
{
    public static async Task<HttpExchangeSnapshot> FromRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return new HttpExchangeSnapshot(
            RequestMethod: request.Method.Method,
            RequestUri: DiagnosticLogger.SanitizeUriForDiagnostics(request.RequestUri) ?? "(unknown)",
            RequestHeaders: DiagnosticLogger.RedactHeadersForDiagnostics(request.Headers, request.Content?.Headers),
            RequestBody: await DiagnosticLogger.ReadContentPreviewAsync(request.Content, cancellationToken));
    }

    public static async Task<HttpExchangeSnapshot> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return new HttpExchangeSnapshot(
            ResponseStatusCode: (int)response.StatusCode,
            ResponseReasonPhrase: response.ReasonPhrase,
            ResponseHeaders: DiagnosticLogger.RedactHeadersForDiagnostics(response.Headers, response.Content?.Headers),
            ResponseBody: await DiagnosticLogger.ReadContentPreviewAsync(response.Content, cancellationToken));
    }
}
