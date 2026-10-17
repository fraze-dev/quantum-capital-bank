using BankApi.Models;

namespace BankApi.Dtos;

public class TransactionSummary
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    public string Id { get; set; }
    public string AccountNumber { get; set; }
    public string OwnerUsername { get; set; }
    public string Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string CounterpartyAccountNumber { get; set; }
    public string PerformedBy { get; set; }
    public DateTime Timestamp { get; set; }

    public static TransactionSummary From(TransactionDocument transaction)
    {
        return new TransactionSummary
        {
            Id = transaction.Id,
            AccountNumber = transaction.AccountNumber,
            OwnerUsername = transaction.OwnerUsername,
            Type = transaction.Type,
            Amount = transaction.Amount,
            BalanceAfter = transaction.BalanceAfter,
            CounterpartyAccountNumber = transaction.CounterpartyAccountNumber,
            PerformedBy = transaction.PerformedBy,
            Timestamp = DateTime.SpecifyKind(transaction.Timestamp, DateTimeKind.Utc)
        };
    }

    // Page size for transaction lists: 50 by default, never more than 500.
    public static int ClampLimit(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
}
