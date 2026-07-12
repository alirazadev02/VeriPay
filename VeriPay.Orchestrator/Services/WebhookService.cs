using System.Text;
using System.Text.Json;

namespace VeriPay.Orchestrator.Services;

public class WebhookSubscription
{
    public string   Id        { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string   Url       { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WebhookLog
{
    public string   SubscriptionId { get; set; } = string.Empty;
    public string   Url            { get; set; } = string.Empty;
    public string   Payload        { get; set; } = string.Empty;
    public DateTime FiredAt        { get; set; }
    public int      StatusCode     { get; set; }
    public bool     Success        { get; set; }
    public string?  Error          { get; set; }
}

public class WebhookService
{
    private readonly List<WebhookSubscription> _subs = new();
    private readonly List<WebhookLog>          _logs = new();
    private readonly IHttpClientFactory        _http;
    private readonly ILogger<WebhookService>   _logger;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WebhookService(IHttpClientFactory http, ILogger<WebhookService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public WebhookSubscription Register(string url)
    {
        var sub = new WebhookSubscription { Url = url };
        lock (_lock) _subs.Add(sub);
        _logger.LogInformation("Webhook registered: {Id} → {Url}", sub.Id, url);
        return sub;
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            var sub = _subs.FirstOrDefault(s => s.Id == id);
            if (sub is null) return false;
            _subs.Remove(sub);
            return true;
        }
    }

    public IReadOnlyList<WebhookSubscription> GetAll()
    {
        lock (_lock) return _subs.ToList();
    }

    public IReadOnlyList<WebhookLog> GetLogs(int count = 50)
    {
        lock (_lock) return _logs.TakeLast(count).Reverse().ToList();
    }

    // Called from OrchestratorService after every status push
    public void FireAsync(string transferId, byte status, string statusLabel)
    {
        List<WebhookSubscription> targets;
        lock (_lock) targets = _subs.ToList();
        if (targets.Count == 0) return;

        var payload = JsonSerializer.Serialize(new
        {
            transferId,
            status,
            statusLabel,
            firedAt = DateTime.UtcNow,
        }, JsonOpts);

        foreach (var sub in targets)
            _ = Task.Run(() => SendAsync(sub, payload));
    }

    private async Task SendAsync(WebhookSubscription sub, string payload)
    {
        var log = new WebhookLog
        {
            SubscriptionId = sub.Id,
            Url            = sub.Url,
            Payload        = payload,
            FiredAt        = DateTime.UtcNow,
        };

        try
        {
            var client  = _http.CreateClient("webhook");
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var res     = await client.PostAsync(sub.Url, content);
            log.StatusCode = (int)res.StatusCode;
            log.Success    = res.IsSuccessStatusCode;
            _logger.LogInformation("Webhook → {Url} [{Code}]", sub.Url, log.StatusCode);
        }
        catch (Exception ex)
        {
            log.StatusCode = 0;
            log.Success    = false;
            log.Error      = ex.Message;
            _logger.LogWarning("Webhook failed → {Url}: {Err}", sub.Url, ex.Message);
        }

        lock (_lock)
        {
            _logs.Add(log);
            if (_logs.Count > 200) _logs.RemoveAt(0);
        }
    }
}
