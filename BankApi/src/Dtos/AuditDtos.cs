using BankApi.Models;

namespace BankApi.Dtos;

// Body for the admin status endpoints, e.g. { "status": "Frozen", "reason": "Suspected fraud" }.
public class StatusChangeRequest
{
    public const int MaxReasonLength = 500;

    public string Status { get; set; }
    public string Reason { get; set; }
}

public class AuditEntrySummary
{
    public string Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string PerformedBy { get; set; }
    public string Action { get; set; }
    public string TargetType { get; set; }
    public string TargetId { get; set; }
    public string FromStatus { get; set; }
    public string ToStatus { get; set; }
    public string Reason { get; set; }

    public static AuditEntrySummary From(AuditEntryDocument entry)
    {
        return new AuditEntrySummary
        {
            Id = entry.Id,
            Timestamp = DateTime.SpecifyKind(entry.Timestamp, DateTimeKind.Utc),
            PerformedBy = entry.PerformedBy,
            Action = entry.Action,
            TargetType = entry.TargetType,
            TargetId = entry.TargetId,
            FromStatus = entry.FromStatus,
            ToStatus = entry.ToStatus,
            Reason = entry.Reason
        };
    }
}
