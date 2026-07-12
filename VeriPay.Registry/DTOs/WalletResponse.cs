namespace VeriPay.Registry.DTOs;

public class WalletResponse
{
    public string   ClientId  { get; set; } = string.Empty;
    public decimal  Balance   { get; set; }
    public DateTime UpdatedAt { get; set; }
}
