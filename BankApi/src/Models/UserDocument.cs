using MongoDB.Bson.Serialization.Attributes;

namespace BankApi.Models;

// Anyone who can log in, customer or admin. Only a salted hash of the
// password is stored (see PasswordUtil), never the password itself.
public class UserDocument
{
    [BsonId]
    public string Username { get; set; }

    public string PasswordHash { get; set; }

    public string Role { get; set; }

    public DateTime CreatedAt { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}
