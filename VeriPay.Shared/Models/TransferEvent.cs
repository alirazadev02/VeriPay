using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VeriPay.Shared.Models;

[Table("transfer_events")]
public class TransferEvent
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("transfer_id"), MaxLength(50)]
    public string TransferId { get; set; } = string.Empty;

    [Column("status"), MaxLength(100)]
    public string Status { get; set; } = string.Empty;

    [Column("source"), MaxLength(100)]
    public string Source { get; set; } = string.Empty;

    [Column("occurred_at_utc", TypeName = "datetime2")]
    public DateTime OccurredAtUtc { get; set; }

    [Column("details", TypeName = "nvarchar(max)")]
    public string? Details { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(TransferId))]
    public Transfer Transfer { get; set; } = null!;
}
