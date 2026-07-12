using Microsoft.AspNetCore.Mvc;
using VeriPay.Registry.Services;

namespace VeriPay.Registry.Controllers;

[ApiController]
[Route("registry")]
public class RegistryController : ControllerBase
{
    private readonly RegistryService _svc;

    public RegistryController(RegistryService svc)
    {
        _svc = svc;
    }

    /// <summary>Get the full timeline for a transfer.</summary>
    [HttpGet("transfers/{id}/timeline")]
    public async Task<IActionResult> GetTimeline(string id)
    {
        var result = await _svc.GetTimelineAsync(id);
        return result is null
            ? NotFound(new { error = $"Transfer '{id}' not found." })
            : Ok(result);
    }

    /// <summary>List the most recent transfers.</summary>
    [HttpGet("transfers")]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 20)
        => Ok(await _svc.GetRecentAsync(count));

    /// <summary>Full history for a client, optionally all clients.</summary>
    [HttpGet("transfers/history")]
    public async Task<IActionResult> GetHistory([FromQuery] string clientId = "", [FromQuery] int count = 100)
        => Ok(await _svc.GetHistoryAsync(clientId, count));

    /// <summary>Get all failed transfers for a client.</summary>
    [HttpGet("transfers/failed")]
    public async Task<IActionResult> GetFailed([FromQuery] string clientId = "")
        => Ok(await _svc.GetFailedAsync(clientId));

    /// <summary>Get wallet balance for a client. Returns 100,000 default if client has no wallet yet.</summary>
    [HttpGet("wallet/{clientId}")]
    public async Task<IActionResult> GetWallet(string clientId)
        => Ok(await _svc.GetWalletAsync(clientId));

    /// <summary>Liveness probe.</summary>
    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "ok", service = "Registry", port = 7001, time = DateTime.UtcNow });
}
