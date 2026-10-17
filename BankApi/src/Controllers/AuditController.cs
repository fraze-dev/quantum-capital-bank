using BankApi.Dtos;
using BankApi.Models;
using BankApi.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AuditController : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    private readonly IAuditRepository _auditRepository;

    public AuditController(IAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    // GET /api/v1/audit?targetId=CHK-1001&limit=50
    // Admin actions, newest first. targetId (account number or username) is optional.
    [HttpGet]
    public async Task<IActionResult> GetAuditLog([FromQuery] string targetId, [FromQuery] int? limit)
    {
        List<AuditEntryDocument> entries = await _auditRepository.FindAsync(
            targetId, Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit));

        return Ok(entries.Select(AuditEntrySummary.From).ToList());
    }
}
