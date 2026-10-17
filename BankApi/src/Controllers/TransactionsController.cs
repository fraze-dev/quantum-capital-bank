using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionRepository _transactionRepository;

    public TransactionsController(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    // GET /api/v1/transactions?username=jsmith&accountNumber=CHK-1001&from=...&to=...&limit=100
    // All filters optional. from is inclusive and to is exclusive, both ISO 8601
    // instants, so the client decides what "a day" means in its own time zone.
    [HttpGet]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] string username,
        [FromQuery] string accountNumber,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? limit)
    {
        List<TransactionDocument> transactions = await _transactionRepository.FindAsync(new TransactionQuery
        {
            OwnerUsername = username,
            AccountNumber = accountNumber,
            From = from?.UtcDateTime,
            To = to?.UtcDateTime,
            Limit = TransactionSummary.ClampLimit(limit)
        });

        return Ok(transactions.Select(TransactionSummary.From).ToList());
    }
}

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

    public static int ClampLimit(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
}
