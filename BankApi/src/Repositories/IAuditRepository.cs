using BankApi.Models;
using MongoDB.Driver;

namespace BankApi.Repositories;

public interface IAuditRepository
{
    Task InsertAsync(IClientSessionHandle session, AuditEntryDocument entry);

    // Newest first. targetId (an account number or username) is optional.
    Task<List<AuditEntryDocument>> FindAsync(string targetId, int limit);
}
