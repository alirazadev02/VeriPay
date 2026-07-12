using System.ComponentModel.DataAnnotations;

namespace VeriPay.Orchestrator.DTOs;

public class TrackTransferRequest
{
    [Required] public string Rail     { get; set; } = string.Empty;
    [Required] public string RefId    { get; set; } = string.Empty;
    [Required] public string ClientId { get; set; } = string.Empty;
    [Required] public string FromBank { get; set; } = string.Empty;
    [Required] public string ToBank   { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    public string  Currency           { get; set; } = "PKR";
    public string  Type               { get; set; } = "TRANSFER";
    public string? OriginalTransferId { get; set; }
    public string? Reason             { get; set; }
    public bool    ForceFailure       { get; set; } = false;
    public bool    DelayAtSwitch      { get; set; } = false;
}
