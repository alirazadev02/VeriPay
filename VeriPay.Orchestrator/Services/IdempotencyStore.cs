namespace VeriPay.Orchestrator.Services;

public class IdempotencyStore
{
    private readonly record struct Entry(string Body, DateTime ExpiresAt);
    private readonly Dictionary<string, Entry> _cache = new();
    private readonly object _lock = new();

    public string? TryGet(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var e) && e.ExpiresAt > DateTime.UtcNow)
                return e.Body;
            _cache.Remove(key);
            return null;
        }
    }

    public void Save(string key, string body, TimeSpan ttl)
    {
        lock (_lock)
            _cache[key] = new Entry(body, DateTime.UtcNow.Add(ttl));
    }
}
