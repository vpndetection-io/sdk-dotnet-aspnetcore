using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VPNDetection.Middleware;

namespace VPNDetection.AspNetCore;

/// <summary>How the ASP.NET Core middleware behaves.</summary>
/// <remarks>
/// Everything <see cref="MiddlewareOptions{TRequest}"/> has, plus how a blocked request is
/// answered.
/// </remarks>
public sealed class VPNDetectionOptions : MiddlewareOptions<HttpContext>
{
    /// <summary>
    /// Answers a request the condition matched. Defaults to <c>403</c> with a JSON body; the
    /// endpoint never runs either way.
    /// </summary>
    public Func<HttpContext, Lookup, Task>? OnBlocked { get; set; }
}

/// <summary>
/// Classifies the visitor behind each request and optionally refuses it.
/// </summary>
/// <remarks>
/// Without a <c>BlockCondition</c> this only enriches: the answer lands on
/// <c>HttpContext.Items</c>, read with <see cref="HttpContextExtensions.GetVPNDetection"/>, and
/// what it means is your endpoint's decision.
/// </remarks>
public sealed class VPNDetectionMiddleware
{
    /// <summary>The <c>HttpContext.Items</c> key the answer is stored under.</summary>
    public const string ItemKey = "vpndetection";

    private static readonly JsonSerializerOptions Refusal =
        new() { WriteIndented = false };

    private readonly RequestDelegate next;
    private readonly Core<HttpContext> core;
    private readonly Func<HttpContext, Lookup, Task> onBlocked;

    public VPNDetectionMiddleware(RequestDelegate next, VPNDetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.next = next;
        this.core = new Core<HttpContext>(options, IpSelectors.Default);
        this.onBlocked = options.OnBlocked ?? RefuseAsync;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // RequestAborted, so a visitor who hangs up does not hold a lookup open behind them.
        Lookup? found = await core.EvaluateAsync(context, context.RequestAborted)
            .ConfigureAwait(false);
        if (found is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }
        context.Items[ItemKey] = found;
        if (found.Blocked)
        {
            await onBlocked(context, found).ConfigureAwait(false);
            return;
        }
        await next(context).ConfigureAwait(false);
    }

    private static Task RefuseAsync(HttpContext context, Lookup lookup)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new { error = "access denied" }, Refusal));
    }
}
