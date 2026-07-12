using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VeriPay.Shared.Data;
using VeriPay.Registry.DTOs;

namespace VeriPay.Registry.Services;

public class RegistryService
{
    private readonly VeriPayDbContext _db;

    public RegistryService(VeriPayDbContext db)
    {
        _db = db;
    }

    public async Task<TimelineResponse?> GetTimelineAsync(string transferId)
    {
        var transfer = await _db.Transfers
            .Include(t => t.Events.OrderBy(e => e.OccurredAtUtc))
            .FirstOrDefaultAsync(t => t.TransferId == transferId);

        return transfer is null ? null : Map(transfer);
    }

    public async Task<List<TimelineResponse>> GetRecentAsync(int count = 20)
    {
        var transfers = await _db.Transfers
            .Include(t => t.Events.OrderBy(e => e.OccurredAtUtc))
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync();

        return transfers.Select(Map).ToList();
    }

    public async Task<List<TimelineResponse>> GetFailedAsync(string clientId)
    {
        var list = await _db.Transfers
            .Where(t => t.ClientId == clientId && t.CurrentStatus == (byte)5)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return list.Select(t => new TimelineResponse
        {
            TransferId         = t.TransferId,
            Type               = t.Type,
            Rail               = t.Rail,
            FromBank           = t.FromBank,
            ToBank             = t.ToBank,
            Amount             = t.Amount,
            Currency           = t.Currency,
            CurrentStatus      = t.CurrentStatus,
            OriginalTransferId = t.OriginalTransferId,
            Reason             = t.Reason,
            CreatedAt          = t.CreatedAt,
            Events             = new(),
        }).ToList();
    }

    public async Task<List<TimelineResponse>> GetHistoryAsync(string clientId, int count = 100)
    {
        var query = _db.Transfers
            .Include(t => t.Events.OrderBy(e => e.OccurredAtUtc))
            .OrderByDescending(t => t.CreatedAt);

        var transfers = string.IsNullOrEmpty(clientId)
            ? await query.Take(count).ToListAsync()
            : await query.Where(t => t.ClientId == clientId).Take(count).ToListAsync();

        return transfers.Select(Map).ToList();
    }

    public async Task<WalletResponse> GetWalletAsync(string clientId)
    {
        var wallet = await _db.Wallets.FindAsync(clientId);
        return wallet is null
            ? new WalletResponse { ClientId = clientId, Balance = 100000m, UpdatedAt = DateTime.UtcNow }
            : new WalletResponse { ClientId = wallet.ClientId, Balance = wallet.Balance, UpdatedAt = wallet.UpdatedAt };
    }

    private static TimelineResponse Map(VeriPay.Shared.Models.Transfer t) => new()
    {
        TransferId         = t.TransferId,
        Type               = t.Type,
        Rail               = t.Rail,
        RefId              = t.RefId,
        ClientId           = t.ClientId,
        FromBank           = t.FromBank,
        ToBank             = t.ToBank,
        Amount             = t.Amount,
        Currency           = t.Currency,
        CurrentStatus      = t.CurrentStatus,
        OriginalTransferId = t.OriginalTransferId,
        Reason             = t.Reason,
        CreatedAt          = t.CreatedAt,
        Events = t.Events.Select(e => new TimelineEvent
        {
            Status        = e.Status,
            Source        = e.Source,
            OccurredAtUtc = e.OccurredAtUtc,
            Details       = e.Details is null
                ? null
                : JsonSerializer.Deserialize<object>(e.Details),
        }).ToList(),
    };
}
