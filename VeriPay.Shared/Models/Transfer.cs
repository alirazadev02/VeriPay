using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VeriPay.Shared.Models;

[Table("transfers")]
public class Transfer
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("transfer_id"), MaxLength(50)]
    public string TransferId { get; set; } = string.Empty;

    [Column("rail"), MaxLength(10)]
    public string Rail { get; set; } = string.Empty;

    [Column("ref_id"), MaxLength(100)]
    public string RefId { get; set; } = string.Empty;

    [Column("client_id"), MaxLength(50)]
    public string ClientId { get; set; } = string.Empty;

    [Column("from_bank"), MaxLength(50)]
    public string FromBank { get; set; } = string.Empty;

    [Column("to_bank"), MaxLength(50)]
    public string ToBank { get; set; } = string.Empty;

    [Column("amount", TypeName = "decimal(15,2)")]
    public decimal Amount { get; set; }

    [Column("currency"), MaxLength(10)]
    public string Currency { get; set; } = "PKR";

    [Column("current_status")]
    public byte CurrentStatus { get; set; } = 1;

    [Column("type"), MaxLength(10)]
    public string Type { get; set; } = "TRANSFER";

    [Column("original_transfer_id"), MaxLength(50)]
    public string? OriginalTransferId { get; set; }

    [Column("reason"), MaxLength(500)]
    public string? Reason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TransferEvent> Events { get; set; } = new List<TransferEvent>();
}
