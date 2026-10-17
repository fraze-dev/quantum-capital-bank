using BankApi.Dtos;
using BankApi.Models;
using BankApi.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class AuditController : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    private readonly IAuditRepository _auditRepository;
    private readonly IAccountRepository _accountRepository;

    public AuditController(IAuditRepository auditRepository, IAccountRepository accountRepository)
    {
        _auditRepository = auditRepository;
        _accountRepository = accountRepository;
    }

    // GET /api/v1/audit?limit=50                   everything
    // GET /api/v1/audit?targetId=CHK-1001          one account (or one username)
    // GET /api/v1/audit?username=jsmith            a customer and all of their accounts
    // Admin actions, newest first.
    [HttpGet]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string targetId, [FromQuery] string username, [FromQuery] int? limit)
    {
        List<string> targetIds = null;
        if (!string.IsNullOrWhiteSpace(username))
        {
            List<AccountDocument> accounts = await _accountRepository.GetByCustomerUsernameAsync(username);
            targetIds = accounts.Select(a => a.AccountNumber).Append(username).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(targetId))
        {
            targetIds = new List<string> { targetId };
        }

        List<AuditEntryDocument> entries = await _auditRepository.FindAsync(
            targetIds, Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit));

        return Ok(entries.Select(AuditEntrySummary.From).ToList());
    }
}
