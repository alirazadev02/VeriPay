using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VeriPay.Shared.Data;
using VeriPay.Shared.Models;
using VeriPay.Orchestrator.DTOs;

namespace VeriPay.Orchestrator.Services;

public class OrchestratorService
{
    private readonly VeriPayDbContext _db;
    private readonly ILogger<OrchestratorService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WebhookService _webhooks;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public OrchestratorService(VeriPayDbContext db, ILogger<OrchestratorService> logger,
        IServiceScopeFactory scopeFactory, WebhookService webhooks)
    {
        _db           = db;
        _logger       = logger;
        _scopeFactory = scopeFactory;
        _webhooks     = webhooks;
    }

    // ── Entry point ──────────────────────────────────────────────────────────

    public async Task<TrackTransferResponse> TrackAsync(TrackTransferRequest req)
    {
        var transferId = $"TXN-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-" +
                         $"{Random.Shared.Next(0, 1000):D3}";

        var isReversal = req.Type.Equals("REVERSAL", StringComparison.OrdinalIgnoreCase);

        var transfer = new Transfer
        {
            TransferId         = transferId,
            Rail               = req.Rail,
            RefId              = req.RefId,
            ClientId           = req.ClientId,
            FromBank           = req.FromBank,
            ToBank             = req.ToBank,
            Amount             = req.Amount,
            Currency           = req.Currency,
            CurrentStatus      = 1,
            Type               = isReversal ? "REVERSAL" : "TRANSFER",
            OriginalTransferId = isReversal ? req.OriginalTransferId : null,
            Reason             = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim(),
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow,
        };

        _db.Transfers.Add(transfer);

        var initDetails = new Dictionary<string, object>
        {
            ["refId"]    = req.RefId,
            ["clientId"] = req.ClientId,
            ["rail"]     = req.Rail,
        };
        if (isReversal)
            initDetails["originalTransferId"] = req.OriginalTransferId ?? "N/A";
        else
            initDetails["debitedFrom"] = req.FromBank;

        _db.TransferEvents.Add(new TransferEvent
        {
            TransferId    = transferId,
            Status        = isReversal ? "ReversalInitiated" : "Initiated",
            Source        = "Client",
            OccurredAtUtc = DateTime.UtcNow,
            Details       = JsonSerializer.Serialize(initDetails, JsonOpts),
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("Transfer {Id} created (type={Type})", transferId, transfer.Type);

        // Persist balance change
        var wallet = await _db.Wallets.FindAsync(req.ClientId);
        if (wallet is null)
        {
            _db.Wallets.Add(new VeriPay.Shared.Models.Wallet
            {
                ClientId  = req.ClientId,
                Balance   = 100000m + (isReversal ? req.Amount : -req.Amount),
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            wallet.Balance   += isReversal ? req.Amount : -req.Amount;
            wallet.UpdatedAt  = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        // Fire-and-forget — simulate progression in the background
        _ = Task.Run(() => SimulateAsync(transferId, req, isReversal));

        return new TrackTransferResponse
        {
            TransferId    = transferId,
            CurrentStatus = 1,
            Message       = isReversal
                ? "Reversal submitted successfully."
                : "Transfer submitted successfully.",
            CreatedAt = transfer.CreatedAt,
        };
    }

    // ── Routing ──────────────────────────────────────────────────────────────

    private async Task SimulateAsync(string id, TrackTransferRequest req, bool isReversal)
    {
        var outcome = req.ForceFailure  ? "forcedFail"
                   : req.DelayAtSwitch ? "delayAtSwitch"
                   : req.RefId switch
        {
            "TXN1003" => "failedAtSwitch",
            "TXN1004" => "failedAtBank",
            "TXN1002" => "beneficiary",
            "REV001"  => "reversal",
            _         => "full",
        };

        if (isReversal)
            await SimulateReversalAsync(id, outcome, req);
        else
            await SimulateTransferAsync(id, outcome, req);
    }

    // ── Normal transfer ──────────────────────────────────────────────────────

    private async Task SimulateTransferAsync(string id, string outcome, TrackTransferRequest req)
    {
        var switchName = req.Rail == "RAAST" ? "RAAST Hub" : "1LINK Switch";

        await Task.Delay(1500);
        await PushEventAsync(id, (byte)2, "SentToSwitch", switchName,
            new { switchRef = $"SW{Ts()}" });

        if (outcome == "failedAtSwitch")
        {
            await Task.Delay(2000);
            await PushEventAsync(id, (byte)5, "Failed", switchName,
                new { reason = "Invalid beneficiary IBAN", errorCode = "ERR_IBAN_INVALID" });
            return;
        }

        if (outcome == "forcedFail")
        {
            await Task.Delay(2500);
            await PushEventAsync(id, (byte)5, "Failed", req.ToBank,
                new { reason = $"Transaction failed at {req.ToBank} — payment declined", errorCode = "ERR_TXN_FAILED" });
            return;
        }

        if (outcome == "delayAtSwitch")
        {
            // Simulate SLA breach — switch holds the transaction for 8 seconds
            await Task.Delay(8000);
            await PushEventAsync(id, (byte)2, "SwitchDelayWarning", switchName,
                new { note = "Late ACK received — SLA threshold breached", delaySeconds = 8, switchRef = $"SW{Ts()}" });
            await Task.Delay(2000);
            // Falls through to normal ReceivedByBeneficiaryBank → Credited
        }

        await Task.Delay(2500);
        await PushEventAsync(id, (byte)3, "ReceivedByBeneficiaryBank", req.ToBank,
            new { bankRef = $"BNK{Ts()}" });

        if (outcome == "failedAtBank")
        {
            await Task.Delay(2500);
            await PushEventAsync(id, (byte)5, "Failed", req.ToBank,
                new { reason = "Account frozen by beneficiary bank", errorCode = "ERR_ACCOUNT_FROZEN" });
            return;
        }

        if (outcome == "beneficiary") return;

        await Task.Delay(3000);
        await PushEventAsync(id, (byte)4, "Credited", req.ToBank,
            new { creditRef = $"CR{Ts()}", creditedAt = DateTime.UtcNow });

        if (outcome == "reversal")
        {
            await Task.Delay(3000);
            await PushEventAsync(id, (byte)5, "PaymentFailed", req.ToBank,
                new { reason = "Transaction disputed by sender", errorCode = "ERR_DISPUTE_RAISED", reversalPending = true });

            await Task.Delay(5000);
            await PushEventAsync(id, (byte)7, "Reversed", req.FromBank,
                new { reversedAt = DateTime.UtcNow, refundRef = $"RF{Ts()}", note = "Amount returned to initiator account" });
        }
    }

    // ── Reversal ─────────────────────────────────────────────────────────────

    private async Task SimulateReversalAsync(string id, string outcome, TrackTransferRequest req)
    {
        var switchName = req.Rail == "RAAST" ? "RAAST Hub" : "1LINK Switch";
        var origId     = req.OriginalTransferId ?? "N/A";

        await Task.Delay(1500);
        await PushEventAsync(id, (byte)2, "SentToSwitch", switchName,
            new { switchRef = $"SW{Ts()}" });

        if (outcome == "failedAtSwitch")
        {
            await Task.Delay(2000);
            await PushEventAsync(id, (byte)5, "Failed", switchName,
                new { reason = "Invalid beneficiary IBAN", errorCode = "ERR_IBAN_INVALID" });

            await Task.Delay(2000);
            await PushEventAsync(id, (byte)4, "Credited – Reversal", req.FromBank,
                new { creditedTo = req.FromBank, note = "Auto-recovery: amount returned to initiator", originalTransferId = origId });
            return;
        }

        await Task.Delay(2500);
        await PushEventAsync(id, (byte)3, "ReceivedByBeneficiaryBank", req.ToBank,
            new { bankRef = $"BNK{Ts()}" });

        if (outcome == "failedAtBank")
        {
            await Task.Delay(2500);
            await PushEventAsync(id, (byte)5, "Failed", req.ToBank,
                new { reason = "Account frozen by beneficiary bank", errorCode = "ERR_ACCOUNT_FROZEN" });

            await Task.Delay(2000);
            await PushEventAsync(id, (byte)4, "Credited – Reversal", req.FromBank,
                new { creditedTo = req.FromBank, note = "Auto-recovery: amount returned to initiator", originalTransferId = origId });
            return;
        }

        if (outcome == "beneficiary") return;

        // Successful reversal: Credited → Failed (dispute) → Reversed (5 s gap)
        await Task.Delay(3000);
        await PushEventAsync(id, (byte)4, "Credited", req.ToBank,
            new { creditRef = $"CR{Ts()}", creditedAt = DateTime.UtcNow, originalTransferId = origId });

        await Task.Delay(3000);
        await PushEventAsync(id, (byte)6, "ReversalPending", req.ToBank,
            new { reason = "Funds placed on hold — pending reversal confirmation", errorCode = "ERR_FUNDS_ON_HOLD", originalTransferId = origId });

        await Task.Delay(5000);
        await PushEventAsync(id, (byte)7, "Reversed", req.FromBank,
            new { reversedAt = DateTime.UtcNow, refundRef = $"RF{Ts()}", note = "Amount credited back to initiator account", originalTransferId = origId });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task PushEventAsync(string id, byte status, string statusLabel, string source, object details)
    {
        // Create a fresh scope — the original DbContext is disposed after the HTTP request ends
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VeriPayDbContext>();

        var transfer = await db.Transfers.FirstOrDefaultAsync(t => t.TransferId == id);
        if (transfer is null) return;

        transfer.CurrentStatus = status;
        transfer.UpdatedAt     = DateTime.UtcNow;

        db.TransferEvents.Add(new TransferEvent
        {
            TransferId    = id,
            Status        = statusLabel,
            Source        = source,
            OccurredAtUtc = DateTime.UtcNow,
            Details       = JsonSerializer.Serialize(details, JsonOpts),
        });

        await db.SaveChangesAsync();
        _logger.LogInformation("  [{Id}] status={Status} label={Label}", id, status, statusLabel);

        _webhooks.FireAsync(id, status, statusLabel);
    }

    private static long Ts() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
