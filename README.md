# [<img src="https://s3.vpndetection.io/vpndetection-public/brand/mark.svg" alt="VPNDetection" width="24"/>](https://vpndetection.io/) VPNDetection ASP.NET Core Middleware

[![NuGet](https://img.shields.io/nuget/v/VPNDetection.AspNetCore.svg)](https://www.nuget.org/packages/VPNDetection.AspNetCore)
[![license](https://img.shields.io/github/license/vpndetection-io/sdk-dotnet-aspnetcore.svg)](LICENSE)

The official ASP.NET Core middleware for the [VPNDetection](https://vpndetection.io) API.

It classifies the visitor behind each request — VPN, residential proxy, Tor, hosting, CDN, relay — and puts the answer on `HttpContext`. Blocking is opt-in.

MVC, minimal APIs, Razor Pages and Blazor Server all sit on the same pipeline, so this covers all of them.

## Getting Started

```bash
dotnet add package VPNDetection.AspNetCore
```

Targets `net8.0`, so it loads on .NET 8 and every later runtime.

You need an API key. Create one in the [console](https://app.vpndetection.io); the free tier's allowance is counted per source address, and a server is a single source address, so a key is what makes this usable in production rather than optional.

```csharp
using VPNDetection.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseVPNDetection(o => o.ApiKey = builder.Configuration["VPNDETECTION_API_KEY"]);

app.MapGet("/", (HttpContext context) =>
{
    var found = context.GetVPNDetection();
    return found?.Result?.IsVpn == true ? "Hello, VPN user" : "Hello";
});

app.Run();
```

By default nothing is blocked. Every request carries a `Lookup` and your own code decides what that means — which is usually what you want, because whether a VPN visitor is a problem depends entirely on what they are doing.

## Blocking

Set a `BlockCondition` and a matching request is answered with `403` and never reaches your endpoints.

```csharp
app.UseVPNDetection(o =>
{
    o.ApiKey = builder.Configuration["VPNDETECTION_API_KEY"];
    o.BlockCondition = new[] { new Condition { ["is_vpn"] = true } };
});
```

A condition is written in the shape of a result, keyed by the same names the API uses, and only the members you name are considered. That lets it reach the evidence, not just the flags:

```csharp
// one provider
new Condition { ["is_vpn"] = true, ["vpn"] = new Condition { ["provider"] = "nordvpn" } }

// a numeric threshold
new Condition { ["resproxy"] = new Condition { ["hits"] = Bound.Gte(5) } }

// any of these
new Condition { ["vpn"] = new Condition { ["confidence"] = new[] { "high", "medium" } } }

// a list is OR
new[]
{
    new Condition { ["is_tor"] = true },
    new Condition { ["is_resproxy"] = true },
}
```

Values are matched by equality, strings without regard to case. An array means any-of. `Bound.Gte`, `Gt`, `Lte` and `Lt` compare numbers and chain into a range (`Bound.Gte(5).AndLt(100)`); every bound you give must hold. Members set to `false` or `null` are ignored, so a condition states the signals you act on; one that constrains nothing would match every request, and is refused when the app starts rather than silently blocking all your traffic.

Replace the refusal with `OnBlocked`:

```csharp
o.OnBlocked = (context, lookup) =>
{
    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    return context.Response.WriteAsync("VPN not allowed");
};
```

## Where the client address comes from

This is the setting that decides whether any of the above works, and it is the one thing only you can get right.

By default the middleware uses `HttpContext.Connection.RemoteIpAddress`. **That is the socket peer** unless your pipeline has `UseForwardedHeaders`. If your app sits behind nginx, a load balancer, or a CDN, every visitor arrives wearing your proxy's address — which is a datacenter address, so a hosting rule would block all of them.

Either give ASP.NET Core the topology and let it resolve the address for you:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor,
    KnownProxies = { IPAddress.Parse("10.0.0.1") },
});
app.UseVPNDetection(/* ... */);   // after it, so the connection address is the visitor's
```

…or name the header your edge writes:

```csharp
o.IpSelector = IpSelectors.Header("CF-Connecting-IP");   // or True-Client-IP
```

`IpSelectors.ForwardedFor()` reads the left-most `X-Forwarded-For` entry. Be aware that the left-most entry is whatever the caller sent, because proxies append to that header — it is only trustworthy when an edge you control overwrites it. If you know how many proxies sit in front, count from the right instead: `IpSelectors.ForwardedFor(1)` is the address your nearest proxy saw.

Anything else, pass your own function. It receives the `HttpContext` and returns an address:

```csharp
o.IpSelector = context => context.Request.Headers["X-Real-IP"].ToString();
```

If the address resolves to a private one, the middleware says so once through `OnWarn`. That is expected on localhost and is the signal to fix your configuration anywhere else.

## When a lookup fails

The request is let through, and the reason is on `Lookup.Error`. Our outage should not become yours, so a network failure, an exhausted quota or a rejected key all fail open.

```csharp
var found = context.GetVPNDetection();
if (found?.Error is not null)
{
    logger.LogWarning(found.Error, "vpndetection unavailable");
}
```

Set `FailClosed = true` to block instead. Private addresses are answered locally and never fail, so this will not lock you out in development.

## Cost and latency

Answers are cached per middleware for an hour, so a returning visitor costs nothing, and private addresses never leave the process. A cache miss is one request to our API, bounded at 2500 ms by default and not retried — on a request path, failing open quickly beats holding a visitor while we try again. Both are adjustable, and so is the cache, through a client you build yourself and pass as `Client`.

Mount it on the branch that matters rather than the whole app, or skip what you do not care about:

```csharp
o.Skip = context => context.Request.Path.StartsWithSegments("/healthz");
```

If you already hold a `VpnDetectionClient`, pass it as `Client` and the middleware will share it rather than building a second cache.

Beyond a few million distinct visitors a day, stop calling the API per request: [download the dataset](https://vpndetection.io/databases) and look addresses up locally instead.

## Absent is not false

Only `Ip` and `IsVpn` come back on every plan. A field your plan does not include is `null`, which means "not in your plan" rather than "checked, and no".

```csharp
found.Result.IsHosting == true   // when you only want the flag
```

A `BlockCondition` naming a member your plan does not serve can never match, so the middleware warns once instead of failing silently. Set `OnMissingField = MiddlewareOptions<HttpContext>.MissingField.Throw` to make it an error.

## Other Libraries

There are official VPNDetection client libraries available for many languages including PHP, Python, Go, Java, Ruby, and many popular frameworks such as Django, Rails, and Laravel. See our GitHub at https://github.com/vpndetection-io for more.

## About VPNDetection

VPN Detection API: Accurate anonymity detection identifying VPNs, residential proxies, hosting servers, Tor nodes, CDNs, relays and more.

[<img src="https://s3.vpndetection.io/vpndetection-public/brand/mark.svg" alt="VPNDetection" width="96"/>](https://vpndetection.io/)

## License

This project is licensed under the [MIT License](LICENSE).
