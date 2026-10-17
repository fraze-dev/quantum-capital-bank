using BankApi.Models;
using MongoDB.Driver;

namespace BankApi.Repositories;

public interface IAuditRepository
{
    Task InsertAsync(IClientSessionHandle session, AuditEntryDocument entry);

    // Newest first. targetIds (account numbers and/or usernames) is optional;
    // when given, only entries about one of those targets are returned.
    Task<List<AuditEntryDocument>> FindAsync(IReadOnlyCollection<string> targetIds, int limit);
}
