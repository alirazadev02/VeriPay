using Microsoft.AspNetCore.Mvc;
using VeriPay.Orchestrator.DTOs;
using VeriPay.Orchestrator.Services;

namespace VeriPay.Orchestrator.Controllers;

[ApiController]
[Route("transfers")]
public class TransfersController : ControllerBase
{
    private readonly OrchestratorService _svc;

    public TransfersController(OrchestratorService svc)
    {
        _svc = svc;
    }

    /// <summary>Submit a new transfer or reversal.</summary>
    [HttpPost("track")]
    public async Task<IActionResult> Track([FromBody] TrackTransferRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _svc.TrackAsync(req);
        return Ok(response);
    }

    /// <summary>Liveness probe.</summary>
    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "ok", service = "Orchestrator", port = 7002, time = DateTime.UtcNow });
}
