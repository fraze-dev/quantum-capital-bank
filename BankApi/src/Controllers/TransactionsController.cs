using BankApi.Dtos;
using BankApi.Models;
using BankApi.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = Roles.Staff)]
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
