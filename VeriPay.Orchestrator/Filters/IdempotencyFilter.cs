using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

namespace VeriPay.Orchestrator.Services;

public class IdempotencyFilter : IAsyncActionFilter
{
    private readonly IdempotencyStore _store;
    private readonly ILogger<IdempotencyFilter> _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public IdempotencyFilter(IdempotencyStore store, ILogger<IdempotencyFilter> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var key = ctx.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

        // Header is optional — proceed normally without it
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        var cached = _store.TryGet(key);
        if (cached is not null)
        {
            _logger.LogInformation("Idempotency replay for key={Key}", key);
            ctx.HttpContext.Response.Headers["X-Idempotent-Replayed"] = "true";
            ctx.Result = new ContentResult
            {
                Content     = cached,
                ContentType = "application/json",
                StatusCode  = 200,
            };
            return;
        }

        var executed = await next();

        if (executed.Result is ObjectResult { Value: not null } ok)
        {
            var body = JsonSerializer.Serialize(ok.Value, JsonOpts);
            _store.Save(key, body, TimeSpan.FromHours(24));
        }
    }
}
