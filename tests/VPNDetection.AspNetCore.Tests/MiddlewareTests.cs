using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using VPNDetection.Middleware;

using Xunit;

namespace VPNDetection.AspNetCore.Tests;

/// <summary>
/// The adapter through a real ASP.NET Core pipeline, plus the shared conformance corpus asserted
/// end to end rather than against the core directly.
/// </summary>
/// <remarks>
/// TestServer leaves <c>Connection.RemoteIpAddress</c> null, because nothing dialled a socket. A
/// terminal middleware sets it, which is also the only honest way to write "the socket peer said
/// X while the header claimed Y" - the case the default selector exists for.
/// </remarks>
public class MiddlewareTests
{
    private const string PublicIp = "45.83.91.1";

    [Fact]
    public async Task EnrichesTheRequestAndLeavesTheDecisionToTheApp()
    {
        var (client, handler) = Stub.Serving(Stub.Lookup(isVpn: true));
        using var host = await Host(o =>
        {
            o.Client = client;
            o.IpSelector = _ => PublicIp;
        });

        var body = await Json(host, "/");

        Assert.True(body.GetProperty("attached").GetBoolean());
        Assert.True(body.GetProperty("is_vpn").GetBoolean());
        Assert.Equal(PublicIp, body.GetProperty("ip").GetString());
        Assert.Equal(new[] { PublicIp }, handler.Asked);
    }

    [Fact]
    public async Task BlocksWhenTheConditionMatchesAndPassesWhenItDoesNot()
    {
        var (vpn, _) = Stub.Serving(Stub.Lookup(isVpn: true, extra: ""","vpn":{"provider":"nordvpn"}"""));
        using var blocking = await Host(o =>
        {
            o.Client = vpn;
            o.IpSelector = _ => PublicIp;
            o.BlockCondition = new[] { new Condition { ["is_vpn"] = true } };
        });

        var refused = await blocking.GetTestClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("""{"error":"access denied"}""", await refused.Content.ReadAsStringAsync());

        var (clean, _) = Stub.Serving(Stub.Lookup(isVpn: false, extra: ""","vpn":{}"""));
        using var passing = await Host(o =>
        {
            o.Client = clean;
            o.IpSelector = _ => PublicIp;
            o.BlockCondition = new[] { new Condition { ["is_vpn"] = true } };
        });
        Assert.Equal(HttpStatusCode.OK,
            (await passing.GetTestClient().GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task AConditionReachesTheEvidenceFields()
    {
        var (nord, _) = Stub.Serving(Stub.Lookup(isVpn: true, extra: ""","vpn":{"provider":"nordvpn"}"""));
        using var other = await Host(o =>
        {
            o.Client = nord;
            o.IpSelector = _ => PublicIp;
            o.BlockCondition = new[]
            {
                new Condition { ["vpn"] = new Condition { ["provider"] = "mullvad" } },
            };
        });
        Assert.Equal(HttpStatusCode.OK, (await other.GetTestClient().GetAsync("/")).StatusCode);

        var (mullvad, _) = Stub.Serving(Stub.Lookup(isVpn: true, extra: ""","vpn":{"provider":"MULLVAD"}"""));
        using var cased = await Host(o =>
        {
            o.Client = mullvad;
            o.IpSelector = _ => PublicIp;
            o.BlockCondition = new[]
            {
                new Condition { ["vpn"] = new Condition { ["provider"] = "mullvad" } },
            };
        });
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cased.GetTestClient().GetAsync("/")).StatusCode);
    }

    // A condition on a member from VPNDetection 6.2.1 on: the carrier behind an address, through
    // the pipeline, matched and refused, and one naming another carrier passed.
    [Fact]
    public async Task AConditionReachesTheNetworkMembers()
    {
        const string carrier = ""","is_carrier":true,"carrier":{"name":"T-Mobile US","mcc":"310"}""";
        var (tmobile, _) = Stub.Serving(Stub.Lookup(isVpn: false, extra: carrier));
        using var blocked = await Host(o =>
        {
            o.Client = tmobile;
            o.IpSelector = _ => PublicIp;
            o.BlockCondition = new[]
            {
                new Condition { ["is_carrier"] = true, ["carrier"] = new Condition { ["mcc"] = "310" } },
            };
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await blocked.GetTestClient().GetAsync("/")).StatusCode);

        var (again, _) = Stub.Serving(Stub.Lookup(isVpn: false, extra: carrier));
        using var passed = await Host(o =>
        {
            o.Client = again;
            o.IpSelector = _ => PublicIp;
            o.BlockCondition = new[] { new Condition { ["carrier"] = new Condition { ["mcc"] = "234" } } };
        });
        Assert.Equal(HttpStatusCode.OK, (await passed.GetTestClient().GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task OnBlockedReplacesTheRefusal()
    {
        var (client, _) = Stub.Serving(Stub.Lookup(isVpn: true, extra: ""","vpn":{"provider":"nordvpn"}"""));
        using var host = await Host(o =>
        {
            o.Client = client;
            o.IpSelector = _ => PublicIp;
            o.BlockCondition = new[] { new Condition { ["is_vpn"] = true } };
            o.OnBlocked = (context, lookup) =>
            {
                context.Response.StatusCode = 451;
                return context.Response.WriteAsync(lookup.Result!.Vpn!.Provider!);
            };
        });

        var answer = await host.GetTestClient().GetAsync("/");
        Assert.Equal(451, (int)answer.StatusCode);
        Assert.Equal("nordvpn", await answer.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SkipLeavesTheRequestUntouched()
    {
        var (client, handler) = Stub.Serving(Stub.Lookup(isVpn: true));
        using var host = await Host(o =>
        {
            o.Client = client;
            o.IpSelector = _ => PublicIp;
            o.BlockCondition = new[] { new Condition { ["is_vpn"] = true } };
            o.Skip = context => context.Request.Path.StartsWithSegments("/healthz");
        });

        var body = await Json(host, "/healthz");
        Assert.False(body.GetProperty("attached").GetBoolean());
        Assert.Empty(handler.Asked);
    }

    [Fact]
    public async Task AFailingLookupLetsTheVisitorThrough()
    {
        var (failing, _) = Stub.Serving("""{"error":"boom"}""", 500);
        using var host = await Host(o =>
        {
            o.Client = failing;
            o.IpSelector = _ => PublicIp;
            o.BlockCondition = new[] { new Condition { ["is_vpn"] = true } };
        });

        var body = await Json(host, "/");
        Assert.Equal("VpnDetectionException", body.GetProperty("error").GetString());
    }

    // The test that matters. Every other assertion here would pass whether or not the selector is
    // right, because a direct connection has nothing to confuse.
    [Fact]
    public async Task AForgedXForwardedForIsIgnoredByDefault()
    {
        var (client, handler) = Stub.Serving(Stub.Lookup(isVpn: true));
        using var host = await Host(o => o.Client = client, peer: IPAddress.Parse("10.0.0.7"));

        var body = await Json(host, "/", ("X-Forwarded-For", PublicIp));
        Assert.Equal("10.0.0.7", body.GetProperty("ip").GetString());
        Assert.True(body.GetProperty("is_bogon").GetBoolean());
        Assert.Empty(handler.Asked);

        var (explicitly, explicitHandler) = Stub.Serving(Stub.Lookup(isVpn: true));
        using var trusting = await Host(
            o =>
            {
                o.Client = explicitly;
                o.IpSelector = IpSelectors.ForwardedFor();
            },
            peer: IPAddress.Parse("10.0.0.7"));

        var forwarded = await Json(trusting, "/", ("X-Forwarded-For", PublicIp));
        Assert.Equal(PublicIp, forwarded.GetProperty("ip").GetString());
        Assert.Equal(new[] { PublicIp }, explicitHandler.Asked);
    }

    [Fact]
    public async Task DepthCountsTrustedHopsFromTheRight()
    {
        var (client, handler) = Stub.Serving(Stub.Lookup(isVpn: true));
        using var host = await Host(
            o =>
            {
                o.Client = client;
                o.IpSelector = IpSelectors.ForwardedFor(1);
            },
            peer: IPAddress.Parse("10.0.0.7"));

        await Json(host, "/", ("X-Forwarded-For", $"{PublicIp}, 70.41.3.18, 150.172.238.178"));
        Assert.Equal(new[] { "150.172.238.178" }, handler.Asked);
    }

    [Fact]
    public async Task AHeaderSelectorReadsTheEdgeThatWritesIt()
    {
        var (client, handler) = Stub.Serving(Stub.Lookup(isVpn: true));
        using var host = await Host(
            o =>
            {
                o.Client = client;
                o.IpSelector = IpSelectors.Header("CF-Connecting-IP");
            },
            peer: IPAddress.Parse("10.0.0.7"));

        await Json(host, "/", ("CF-Connecting-IP", "45.83.91.9"));
        Assert.Equal(new[] { "45.83.91.9" }, handler.Asked);
    }

    // Kestrel hands a dual-stack listener's IPv4 peers over in their mapped form, and the API keys
    // its answers by the dotted form. Without the unmapping this looks up an address that does not
    // route, so every visitor comes back an error and the middleware fails open on all of them.
    [Fact]
    public async Task AnIPv4PeerArrivingMappedIntoIPv6IsLookedUpAsIPv4()
    {
        var (client, handler) = Stub.Serving(Stub.Lookup(isVpn: true));
        using var host = await Host(
            o => o.Client = client, peer: IPAddress.Parse("::ffff:45.83.91.1"));

        var body = await Json(host, "/");
        Assert.Equal(PublicIp, body.GetProperty("ip").GetString());
        Assert.Equal(new[] { PublicIp }, handler.Asked);
    }

    [Fact]
    public async Task APrivateClientAddressIsAnsweredLocallyAndNeverBlocks()
    {
        var (client, handler) = Stub.Serving(Stub.Lookup(isVpn: true));
        using var host = await Host(
            o =>
            {
                o.Client = client;
                o.BlockCondition = new[] { new Condition { ["is_vpn"] = true } };
            },
            peer: IPAddress.Loopback);

        var answer = await host.GetTestClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Empty(handler.Asked);
    }

    // Built exactly as the README builds it, the key from configuration and nothing else, set and
    // unset: every other test hands the middleware a client. The loopback peer is a bogon, so the
    // answer is the middleware's own and nothing leaves the machine.
    [Theory]
    [InlineData("vd_example_key")]
    [InlineData(null)]
    public async Task TheReadmesSetupStartsAndAttachesAnAnswer(string? key)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["VPNDETECTION_API_KEY"] = key;
        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            return next();
        });

        app.UseVPNDetection(o => o.ApiKey = builder.Configuration["VPNDETECTION_API_KEY"]);

        app.MapGet("/", (HttpContext context) =>
        {
            var found = context.GetVPNDetection();
            return found?.Result?.IsBogon == true ? "bogon" : "nothing attached";
        });
        await app.StartAsync();

        Assert.Equal("bogon", await app.GetTestClient().GetStringAsync("/"));
    }

    // The README's setup again, from a PUBLIC peer with a forged X-Forwarded-For, the lookup
    // answered by a recording API: a middleware that dropped the key, or read the header by
    // default, would pass the test above, since a loopback peer is answered with no request.
    [Theory]
    [InlineData("vd_example_key", "Bearer vd_example_key")]
    [InlineData(null, null)]
    public async Task TheReadmesKeyReachesTheApiForAPublicPeer(string? key, string? sent)
    {
        await using var api = await RecordingApi.StartAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["VPNDETECTION_API_KEY"] = key;
        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(PublicIp);
            return next();
        });

        app.UseVPNDetection(o =>
        {
            o.ApiKey = builder.Configuration["VPNDETECTION_API_KEY"];
            o.BaseUrl = api.BaseUrl;
        });

        app.MapGet("/", (HttpContext context) =>
            context.GetVPNDetection()?.Result?.Ip ?? "nothing attached");
        await app.StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "1.1.1.1");
        var answer = await app.GetTestClient().SendAsync(request);

        Assert.Equal(PublicIp, await answer.Content.ReadAsStringAsync());
        Assert.Equal(new[] { $"/{PublicIp}" }, api.Asked);
        Assert.Equal(new[] { sent }, api.Authorizations);
    }

    [Fact]
    public async Task AConditionThatConstrainsNothingIsRefusedWhenTheAppStarts()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Host(o =>
            o.BlockCondition = new[] { new Condition { ["is_vpn"] = false } }));
        Assert.Contains("constrains nothing", error.Message);
    }

    [Theory]
    [MemberData(nameof(CorpusConditions))]
    public async Task CorpusThroughThePipeline(string name, string why, JsonElement c)
    {
        _ = name;
        var ip = c.TryGetProperty("bogon", out var bogon)
            ? bogon.GetString()!
            : c.GetProperty("body").GetProperty("ip").GetString()!;
        var body = c.TryGetProperty("body", out var served)
            ? served.GetRawText()
            : Stub.Lookup();
        var (client, _) = Stub.Serving(body);
        var warnings = new List<string>();

        using var host = await Host(o =>
        {
            o.Client = client;
            o.IpSelector = _ => ip;
            o.BlockCondition = Corpus.Conditions(c.GetProperty("condition"));
            o.OnWarn = warnings.Add;
        });

        var answer = await host.GetTestClient().GetAsync("/");
        var expect = c.GetProperty("expect");
        Assert.Equal(
            expect.GetProperty("blocked").GetBoolean() ? HttpStatusCode.Forbidden : HttpStatusCode.OK,
            answer.StatusCode);

        var missing = expect.GetProperty("missing").EnumerateArray().Select(m => m.GetString()!).ToList();
        var reported = warnings.Where(w => w.Contains("does not include", StringComparison.Ordinal)).ToList();
        Assert.Equal(missing.Count == 0 ? 0 : 1, reported.Count);
        foreach (var member in missing)
        {
            Assert.Contains(member, reported[0], StringComparison.Ordinal);
        }
        _ = why;
    }

    public static TheoryData<string, string, JsonElement> CorpusConditions()
    {
        var data = new TheoryData<string, string, JsonElement>();
        foreach (var c in Corpus.Data.GetProperty("middleware").GetProperty("conditions").EnumerateArray())
        {
            data.Add(c.GetProperty("name").GetString()!, c.GetProperty("why").GetString()!, c);
        }
        return data;
    }

    /// <summary>An app whose one endpoint reports back what the middleware attached.</summary>
    private static async Task<IHost> Host(
        Action<VPNDetectionOptions> configure, IPAddress? peer = null)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddRouting())
                .Configure(app =>
                {
                    if (peer is not null)
                    {
                        app.Use((context, next) =>
                        {
                            context.Connection.RemoteIpAddress = peer;
                            return next();
                        });
                    }
                    app.UseVPNDetection(configure);
                    // Serialized and written, rather than WriteAsJsonAsync: System.Text.Json
                    // 10 demands PipeWriter.UnflushedBytes, which ASP.NET Core 8's TestServer
                    // writer does not implement, so every test throws on the `latest` leg of
                    // the matrix while passing on the floor.
                    app.Run(async context =>
                    {
                        Lookup? found = context.GetVPNDetection();
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            attached = found is not null,
                            ip = found?.Ip,
                            is_vpn = found?.Result?.IsVpn,
                            is_bogon = found?.Result?.IsBogon,
                            error = found?.Error?.GetType().Name,
                        }));
                    });
                }))
            .StartAsync();
        return host;
    }

    private static async Task<JsonElement> Json(
        IHost host, string path, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }
        var answer = await host.GetTestClient().SendAsync(request);
        return JsonDocument.Parse(await answer.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    /// <summary>
    /// A stand-in API on a real socket, answering every lookup and recording the path and the
    /// Authorization header of each request.
    /// </summary>
    private sealed class RecordingApi : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly List<string> asked = new();
        private readonly List<string?> authorizations = new();

        private RecordingApi(WebApplication app) => this.app = app;

        internal string BaseUrl => app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();

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

        internal IReadOnlyList<string?> Authorizations
        {
            get
            {
                lock (asked)
                {
                    return authorizations.ToArray();
                }
            }
        }

        internal static async Task<RecordingApi> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            var api = new RecordingApi(app);
            app.Run(async context =>
            {
                var path = context.Request.Path.Value!;
                lock (api.asked)
                {
                    api.asked.Add(path);
                    api.authorizations.Add(context.Request.Headers.Authorization.FirstOrDefault());
                }
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    Stub.Lookup().Replace("$IP", path.TrimStart('/'), StringComparison.Ordinal));
            });
            await app.StartAsync();
            return api;
        }

        public ValueTask DisposeAsync() => app.DisposeAsync();
    }
}
