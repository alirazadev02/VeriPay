namespace VeriPay.Registry.DTOs;

public class TimelineResponse
{
    public string              TransferId         { get; set; } = string.Empty;
    public string              Type               { get; set; } = string.Empty;
    public string              Rail               { get; set; } = string.Empty;
    public string              RefId              { get; set; } = string.Empty;
    public string              ClientId           { get; set; } = string.Empty;
    public string              FromBank           { get; set; } = string.Empty;
    public string              ToBank             { get; set; } = string.Empty;
    public decimal             Amount             { get; set; }
    public string              Currency           { get; set; } = string.Empty;
    public int                 CurrentStatus      { get; set; }
    public string?             OriginalTransferId { get; set; }
    public string?             Reason             { get; set; }
    public DateTime            CreatedAt          { get; set; }
    public List<TimelineEvent> Events             { get; set; } = new();
}

public class TimelineEvent
{
    public string   Status        { get; set; } = string.Empty;
    public string   Source        { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public object?  Details       { get; set; }
}
