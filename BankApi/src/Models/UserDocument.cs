using MongoDB.Bson.Serialization.Attributes;

namespace BankApi.Models;

// Anyone who can log in, customer or admin. Only a salted hash of the
// password is stored (see PasswordUtil), never the password itself.
[BsonIgnoreExtraElements]
public class UserDocument
{
    [BsonId]
    public string Username { get; set; }

    public string PasswordHash { get; set; }

    public string Role { get; set; }

    public DateTime CreatedAt { get; set; }

    // Disabled users can't log in, and tokens they already hold stop working.
    [BsonDefaultValue(UserStatuses.Active)]
    public string Status { get; set; } = UserStatuses.Active;

    public string StatusReason { get; set; }

    public string StatusChangedBy { get; set; }

    public DateTime? StatusChangedAt { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}

public static class UserStatuses
{
    public const string Active = "Active";
    public const string Disabled = "Disabled";

    public static readonly string[] All = { Active, Disabled };
}
