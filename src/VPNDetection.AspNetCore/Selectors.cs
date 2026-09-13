using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using VPNDetection.Middleware;

namespace VPNDetection.AspNetCore;

/// <summary>The shipped client-address selectors, bound to ASP.NET Core's request type.</summary>
/// <remarks>
/// Pass one as <c>IpSelector</c>. Anything with the same shape works, so an edge we have never
/// heard of is a lambda rather than a feature request.
/// </remarks>
public static class IpSelectors
{
    private static readonly Selectors<HttpContext> Bound = new(context => new RequestView(
        name => context.Request.Headers.TryGetValue(name, out var value) ? value.ToString() : null,
        () => Address(context)));

    /// <summary>
    /// <c>HttpContext.Connection.RemoteIpAddress</c>, which is the socket peer unless the app has
    /// <c>UseForwardedHeaders</c> in its pipeline.
    /// </summary>
    /// <remarks>
    /// Behind a reverse proxy without it, every visitor wears the proxy's address - a datacenter
    /// address, so a hosting rule would block all of them. Either add
    /// <c>app.UseForwardedHeaders()</c> ahead of this middleware, with the
    /// <c>KnownProxies</c>/<c>KnownNetworks</c> your topology needs, or name your edge's header
    /// with <see cref="Header"/>.
    /// </remarks>
    public static Func<HttpContext, string?> Default => Bound.Default;

    /// <summary>An address from <c>X-Forwarded-For</c>.</summary>
    /// <remarks>
    /// The LEFT-MOST entry (<paramref name="depth"/> 0) is whatever the caller sent, because
    /// proxies append to this header; it is only trustworthy when an edge you control overwrites
    /// it. When you know how many proxies sit in front, count from the right: depth 1 is the
    /// address your nearest proxy saw.
    /// </remarks>
    public static Func<HttpContext, string?> ForwardedFor(int depth = 0) => Bound.ForwardedFor(depth);

    /// <summary>
    /// An address from a single-value header your edge writes -
    /// <c>Header("CF-Connecting-IP")</c> behind Cloudflare. Falls back to the connection's
    /// address when the header is absent.
    /// </summary>
    public static Func<HttpContext, string?> Header(string name) => Bound.Header(name);

    // Kestrel reports a dual-stack listener's IPv4 peers as ::ffff:a.b.c.d. That is the same
    // address, but it is not the form the API keys answers by, so it would look like an IPv6
    // lookup of an address that does not route.
    private static string? Address(HttpContext context)
    {
        IPAddress? found = context.Connection.RemoteIpAddress;
        if (found is null)
        {
            return null;
        }
        return found.IsIPv4MappedToIPv6 ? found.MapToIPv4().ToString() : found.ToString();
    }
}
