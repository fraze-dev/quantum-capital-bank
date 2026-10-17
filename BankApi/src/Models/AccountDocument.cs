using MongoDB.Bson.Serialization.Attributes;

namespace BankApi.Models;

// Ignore fields this version doesn't know about, so records written by a
// newer version (e.g. during a deploy) can still be read.
[BsonIgnoreExtraElements]
public class AccountDocument
{
    [BsonId]
    public string AccountNumber { get; set; }

    public string OwnerUsername { get; set; }

    public string AccountType { get; set; }

    public decimal Balance { get; set; }

    // Accounts are never deleted (their history is needed for reporting and
    // audits); they move between statuses instead. Only Active accounts can
    // send or receive money. The full history of changes is in the audit log.
    [BsonDefaultValue(AccountStatuses.Active)]
    public string Status { get; set; } = AccountStatuses.Active;

    public string StatusReason { get; set; }

    public string StatusChangedBy { get; set; }

    public DateTime? StatusChangedAt { get; set; }
}

public static class AccountStatuses
{
    public const string Active = "Active";

    // Temporarily blocked (e.g. suspected fraud); an admin can unfreeze it.
    public const string Frozen = "Frozen";

    // Permanently closed, only allowed at a zero balance. Kept for records.
    public const string Closed = "Closed";

    public static readonly string[] All = { Active, Frozen, Closed };
}
