using BankApi.Models;
using MongoDB.Driver;

namespace BankApi.Repositories;

public class AuditRepository : IAuditRepository
{
    public const string CollectionName = "auditLog";

    private readonly IMongoCollection<AuditEntryDocument> _entries;

    public AuditRepository(IMongoDatabase database)
    {
        _entries = database.GetCollection<AuditEntryDocument>(CollectionName);
    }

    public async Task InsertAsync(IClientSessionHandle session, AuditEntryDocument entry)
    {
        await _entries.InsertOneAsync(session, entry);
    }

    public async Task<List<AuditEntryDocument>> FindAsync(IReadOnlyCollection<string> targetIds, int limit)
    {
        FilterDefinition<AuditEntryDocument> filter = targetIds == null
            ? FilterDefinition<AuditEntryDocument>.Empty
            : Builders<AuditEntryDocument>.Filter.In(e => e.TargetId, targetIds);

        return await _entries.Find(filter)
            .SortByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Limit(limit)
            .ToListAsync();
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<AuditEntryDocument>(CollectionName);
        var keys = Builders<AuditEntryDocument>.IndexKeys;

        await collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<AuditEntryDocument>(keys.Descending(e => e.Timestamp)),
            new CreateIndexModel<AuditEntryDocument>(keys.Ascending(e => e.TargetId).Descending(e => e.Timestamp))
        });
    }
}
