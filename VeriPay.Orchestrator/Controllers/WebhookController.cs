using Microsoft.AspNetCore.Mvc;
using VeriPay.Orchestrator.Services;

namespace VeriPay.Orchestrator.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhookController : ControllerBase
{
    private readonly WebhookService _svc;
    public WebhookController(WebhookService svc) => _svc = svc;

    /// <summary>Register a webhook endpoint.</summary>
    [HttpPost]
    public IActionResult Register([FromBody] RegisterWebhookRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Url))
            return BadRequest(new { error = "url is required." });

        var sub = _svc.Register(req.Url);
        return Ok(sub);
    }

    /// <summary>List all registered webhooks.</summary>
    [HttpGet]
    public IActionResult GetAll() => Ok(_svc.GetAll());

    /// <summary>Delete a webhook by id.</summary>
    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
        => _svc.Delete(id) ? Ok(new { deleted = id }) : NotFound(new { error = $"Webhook '{id}' not found." });

    /// <summary>Get recent webhook fire logs.</summary>
    [HttpGet("logs")]
    public IActionResult Logs([FromQuery] int count = 50)
        => Ok(_svc.GetLogs(count));
}

public record RegisterWebhookRequest(string Url);
