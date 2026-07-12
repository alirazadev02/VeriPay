using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VeriPay.Shared.Models;

[Table("wallets")]
public class Wallet
{
    [Key]
    [Column("client_id"), MaxLength(50)]
    public string ClientId { get; set; } = string.Empty;

    [Column("balance", TypeName = "decimal(15,2)")]
    public decimal Balance { get; set; } = 100000m;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
