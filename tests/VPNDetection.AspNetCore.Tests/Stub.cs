using System.Net;
using System.Text;

namespace VPNDetection.AspNetCore.Tests;

/// <summary>
/// A transport that answers every lookup from one body and records what it was asked about, so
/// "never touched the network" is asserted rather than assumed.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly string body;
    private readonly int status;
    private readonly List<string> asked = new();

    internal StubHandler(string body, int status = 200)
    {
        this.body = body;
        this.status = status;
    }

    /// <summary>Every address this handler was asked about, in arrival order.</summary>
    internal IReadOnlyList<string> Asked
    {
        get
        {
            lock (asked)
            {
                return asked.ToArray();
            }
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ip = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.TrimStart('/'));
        lock (asked)
        {
            asked.Add(ip);
        }
        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(
                body.Replace("$IP", ip, StringComparison.Ordinal), Encoding.UTF8,
                "application/json"),
            RequestMessage = request,
        });
    }
}

internal static class Stub
{
    /// <summary>A client whose every answer is <paramref name="body"/>.</summary>
    internal static (VpnDetectionClient Client, StubHandler Handler) Serving(
        string body, int status = 200)
    {
        var handler = new StubHandler(body, status);
        var client = new VpnDetectionClient(new VpnDetectionClientOptions
        {
            CacheEnabled = false,
            Retries = 0,
            HttpClient = new HttpClient(handler),
        });
        return (client, handler);
    }

    internal static string Lookup(bool isVpn = false, string extra = "")
        => $$"""{"ip":"$IP","is_vpn":{{(isVpn ? "true" : "false")}}{{extra}}}""";
}
