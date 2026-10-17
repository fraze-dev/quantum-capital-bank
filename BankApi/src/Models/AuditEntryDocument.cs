using MongoDB.Bson.Serialization.Attributes;

namespace BankApi.Models;

// One administrative action, e.g. "admin froze CHK-1001 because ...".
// Like ledger entries, these are only ever inserted, never changed.
[BsonIgnoreExtraElements]
public class AuditEntryDocument
{
    [BsonId]
    public string Id { get; set; }

    public DateTime Timestamp { get; set; }

    public string PerformedBy { get; set; }

    public string Action { get; set; }

    public string TargetType { get; set; }

    // Account number or username, depending on TargetType.
    public string TargetId { get; set; }

    public string FromStatus { get; set; }

    public string ToStatus { get; set; }

    public string Reason { get; set; }
}

public static class AuditActions
{
    public const string FreezeAccount = "FreezeAccount";
    public const string UnfreezeAccount = "UnfreezeAccount";
    public const string CloseAccount = "CloseAccount";
    public const string DisableCustomer = "DisableCustomer";
    public const string EnableCustomer = "EnableCustomer";
}

public static class AuditTargetTypes
{
    public const string Account = "Account";
    public const string Customer = "Customer";
}
