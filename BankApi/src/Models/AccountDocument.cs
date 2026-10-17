using MongoDB.Bson.Serialization.Attributes;

namespace BankApi.Models;

public class AccountDocument
{
    [BsonId]
    public string AccountNumber { get; set; }

    public string OwnerUsername { get; set; }

    public string AccountType { get; set; }

    public decimal Balance { get; set; }
}