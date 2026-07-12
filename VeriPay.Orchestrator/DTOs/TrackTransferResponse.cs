namespace VeriPay.Orchestrator.DTOs;

public class TrackTransferResponse
{
    public string   TransferId    { get; set; } = string.Empty;
    public int      CurrentStatus { get; set; }
    public string   Message       { get; set; } = string.Empty;
    public DateTime CreatedAt     { get; set; }
}
