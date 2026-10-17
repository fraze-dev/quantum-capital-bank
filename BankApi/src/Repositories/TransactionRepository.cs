using BankApi.Models;
using MongoDB.Driver;

namespace BankApi.Repositories;

public class TransactionRepository : ITransactionRepository
{
    public const string CollectionName = "transactions";

    private readonly IMongoCollection<TransactionDocument> _transactions;

    public TransactionRepository(IMongoDatabase database)
    {
        _transactions = database.GetCollection<TransactionDocument>(CollectionName);
    }

    public async Task InsertAsync(IClientSessionHandle session, IEnumerable<TransactionDocument> transactions)
    {
        await _transactions.InsertManyAsync(session, transactions);
    }

    public async Task<List<TransactionDocument>> FindAsync(TransactionQuery query)
    {
        var builder = Builders<TransactionDocument>.Filter;
        var filters = new List<FilterDefinition<TransactionDocument>>();

        if (!string.IsNullOrWhiteSpace(query.AccountNumber))
        {
            filters.Add(builder.Eq(t => t.AccountNumber, query.AccountNumber));
        }
        if (!string.IsNullOrWhiteSpace(query.OwnerUsername))
        {
            filters.Add(builder.Eq(t => t.OwnerUsername, query.OwnerUsername));
        }
        if (query.From.HasValue)
        {
            filters.Add(builder.Gte(t => t.Timestamp, query.From.Value));
        }
        if (query.To.HasValue)
        {
            filters.Add(builder.Lt(t => t.Timestamp, query.To.Value));
        }

        FilterDefinition<TransactionDocument> filter = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await _transactions.Find(filter)
            .SortByDescending(t => t.Timestamp)
            .ThenByDescending(t => t.Id)
            .Limit(query.Limit)
            .ToListAsync();
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<TransactionDocument>(CollectionName);
        var keys = Builders<TransactionDocument>.IndexKeys;

        await collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TransactionDocument>(keys.Ascending(t => t.AccountNumber).Descending(t => t.Timestamp)),
            new CreateIndexModel<TransactionDocument>(keys.Ascending(t => t.OwnerUsername).Descending(t => t.Timestamp)),
            new CreateIndexModel<TransactionDocument>(keys.Descending(t => t.Timestamp))
        });
    }
}
