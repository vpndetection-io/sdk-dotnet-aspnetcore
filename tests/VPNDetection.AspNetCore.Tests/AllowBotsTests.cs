using System.Net;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using VPNDetection.Middleware;

using Xunit;

namespace VPNDetection.AspNetCore.Tests;

/// <summary>
/// <c>AllowBots</c> built the way the README builds it, from a PUBLIC peer, against a recording API
/// that answers the peer as a VPN with the verdict it names for the agent that arrived: the agent
/// it records is the request's own, so a middleware sending none, or another, fails.
/// </summary>
public class AllowBotsTests
{
    private const string PublicIp = "45.83.91.1";
    private const string Googlebot = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

    [Theory]
    [InlineData("verified", HttpStatusCode.OK)]
    [InlineData("spoofed", HttpStatusCode.Forbidden)]
    [InlineData("not_bot", HttpStatusCode.Forbidden)]
    public async Task AVerifiedCrawlerPassesAConditionItOtherwiseMeets(string verdict, HttpStatusCode status)
    {
        await using var api = await AgentApi.StartAsync(verdict);
        await using var app = await App(api, allowBots: true);

        var answer = await Send(app, Googlebot);

        Assert.Equal(status, answer.StatusCode);
        Assert.Equal(new string?[] { Googlebot }, api.Agents);
        if (status == HttpStatusCode.OK)
        {
            Assert.Equal($"{PublicIp} verified", await answer.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task OffSendsNoAgentAndRefusesTheCrawler()
    {
        await using var api = await AgentApi.StartAsync("verified");
        await using var app = await App(api, allowBots: false);

        var answer = await Send(app, Googlebot);

        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
        Assert.Equal(new string?[] { null }, api.Agents);
    }

    [Fact]
    public async Task AnAbsentHeaderSendsNone()
    {
        await using var api = await AgentApi.StartAsync("verified");
        await using var app = await App(api, allowBots: true);

        var answer = await Send(app, null);

        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
        Assert.Equal(new string?[] { null }, api.Agents);
    }

    private static async Task<HttpResponseMessage> Send(WebApplication app, string? agent)
    {
        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        // The test client's own User-Agent, if it carries one, is no visitor's.
        client.DefaultRequestHeaders.UserAgent.Clear();
        request.Headers.Remove("User-Agent");
        if (agent is not null)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", agent);
        }
        return await client.SendAsync(request);
    }

    // The README's setup, AllowBots set, refusing every VPN.
    private static async Task<WebApplication> App(AgentApi api, bool allowBots)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(PublicIp);
            return next();
        });
        app.UseVPNDetection(o =>
        {
            o.ApiKey = "vd_example_key";
            o.BaseUrl = api.BaseUrl;
            o.AllowBots = allowBots;
            o.BlockCondition = new[] { new Condition { ["is_vpn"] = true } };
        });
        app.MapGet("/", (HttpContext context) =>
        {
            var result = context.GetVPNDetection()?.Result;
            return $"{result?.Ip} {result?.UserAgent?.Verdict}";
        });
        await app.StartAsync();
        return app;
    }

    private sealed class AgentApi : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly List<string?> agents = new();

        private AgentApi(WebApplication app) => this.app = app;

        internal string BaseUrl => app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        internal IReadOnlyList<string?> Agents
        {
            get
            {
                lock (agents)
                {
                    return agents.ToArray();
                }
            }
        }

        internal static async Task<AgentApi> StartAsync(string verdict)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            var api = new AgentApi(app);
            app.Run(async context =>
            {
                string? agent = context.Request.Query.TryGetValue("user_agent", out var sent) ? sent.ToString() : null;
                lock (api.agents)
                {
                    api.agents.Add(agent);
                }
                var answer = new JsonObject { ["ip"] = context.Request.Path.Value!.TrimStart('/'), ["is_vpn"] = true };
                if (agent is not null)
                {
                    answer["user_agent"] = new JsonObject { ["agent"] = "Googlebot", ["kind"] = "bot", ["verdict"] = verdict };
                }
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(answer.ToJsonString());
            });
            await app.StartAsync();
            return api;
        }

        public ValueTask DisposeAsync() => app.DisposeAsync();
    }
}
