using MongoDB.Bson.Serialization.Attributes;

namespace BankApi.Models;

// One ledger entry. Entries are only ever inserted, never updated or deleted,
// so an account's history always adds up to its balance.
public class TransactionDocument
{
    [BsonId]
    public string Id { get; set; }

    public string AccountNumber { get; set; }

    public string OwnerUsername { get; set; }

    public string Type { get; set; }

    // Signed: positive for money in, negative for money out.
    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    // For transfers: the other account, and an id shared by both legs.
    public string CounterpartyAccountNumber { get; set; }

    public string TransferId { get; set; }

    public string PerformedBy { get; set; }

    public DateTime Timestamp { get; set; }
}

public static class TransactionTypes
{
    public const string Deposit = "Deposit";
    public const string Withdrawal = "Withdrawal";
    public const string TransferIn = "TransferIn";
    public const string TransferOut = "TransferOut";
    public const string OpeningBalance = "OpeningBalance";
}
