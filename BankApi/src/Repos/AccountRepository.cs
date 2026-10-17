using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace BankApi;

public class AccountRepository : IAccountRepository
{
    public const string CountersCollectionName = "counters";

    // Account numbers start after this, e.g. SAV-1001.
    private const int FirstAccountNumberBase = 1000;

    private readonly IMongoCollection<AccountDocument> _accounts;
    private readonly IMongoCollection<CounterDocument> _counters;

    public AccountRepository(IMongoDatabase database)
    {
        _accounts = database.GetCollection<AccountDocument>("accounts");
        _counters = database.GetCollection<CounterDocument>(CountersCollectionName);
    }

    public async Task<List<AccountDocument>> GetAllAsync(decimal? minBalance)
    {
        FilterDefinition<AccountDocument> filter = minBalance.HasValue
            ? Builders<AccountDocument>.Filter.Gte(a => a.Balance, minBalance.Value)
            : FilterDefinition<AccountDocument>.Empty;

        return await _accounts.Find(filter).ToListAsync();
    }

    public async Task<List<AccountDocument>> GetByCustomerUsernameAsync(string username)
    {
        return await _accounts.Find(a => a.OwnerUsername == username).ToListAsync();
    }

    public async Task<AccountDocument> GetByAccountNumberAsync(string accountNumber)
    {
        return await _accounts.Find(a => a.AccountNumber == accountNumber).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(IClientSessionHandle session, AccountDocument account)
    {
        await _accounts.InsertOneAsync(session, account);
    }

    // Adds delta to the balance in a single atomic database operation and
    // returns the updated account. When requiredBalance is set, the update only
    // happens if the current balance is at least that much; otherwise nothing
    // changes and null is returned. Doing the check and the update together
    // means two simultaneous withdrawals can't both pass the check.
    public async Task<AccountDocument> ApplyBalanceChangeAsync(
        IClientSessionHandle session, string accountNumber, decimal delta, decimal? requiredBalance)
    {
        var filter = Builders<AccountDocument>.Filter.Eq(a => a.AccountNumber, accountNumber);
        if (requiredBalance.HasValue)
        {
            filter &= Builders<AccountDocument>.Filter.Gte(a => a.Balance, requiredBalance.Value);
        }

        return await _accounts.FindOneAndUpdateAsync(
            session,
            filter,
            Builders<AccountDocument>.Update.Inc(a => a.Balance, delta),
            new FindOneAndUpdateOptions<AccountDocument> { ReturnDocument = ReturnDocument.After });
    }

    // Each prefix (SAV, CHK) has its own counter that Mongo increments
    // atomically, so two accounts opened at the same moment never collide.
    public async Task<string> GetNextAccountNumberAsync(string prefix)
    {
        CounterDocument counter = await _counters.FindOneAndUpdateAsync(
            Builders<CounterDocument>.Filter.Eq(c => c.Id, prefix),
            Builders<CounterDocument>.Update.Inc(c => c.Value, 1),
            new FindOneAndUpdateOptions<CounterDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After });

        return $"{prefix}-{counter.Value}";
    }

    // Moves each prefix's counter up to the highest account number already in
    // use. $max never lowers a counter, so this is safe to run more than once.
    public static async Task SyncAccountNumberCountersAsync(IMongoDatabase database)
    {
        var accounts = database.GetCollection<AccountDocument>("accounts");
        var counters = database.GetCollection<CounterDocument>(CountersCollectionName);

        List<string> accountNumbers = await accounts.Find(FilterDefinition<AccountDocument>.Empty)
            .Project(a => a.AccountNumber)
            .ToListAsync();

        foreach (string prefix in new[] { "SAV", "CHK" })
        {
            int highest = accountNumbers
                .Where(n => n.StartsWith(prefix + "-"))
                .Select(n => int.TryParse(n.Substring(prefix.Length + 1), out int value) ? value : 0)
                .DefaultIfEmpty(0)
                .Max();

            await counters.UpdateOneAsync(
                Builders<CounterDocument>.Filter.Eq(c => c.Id, prefix),
                Builders<CounterDocument>.Update.Max(c => c.Value, Math.Max(highest, FirstAccountNumberBase)),
                new UpdateOptions { IsUpsert = true });
        }
    }
}

// Next-number counter for one account-number prefix, e.g. { _id: "SAV", Value: 1008 }.
internal class CounterDocument
{
    [BsonId]
    public string Id { get; set; }

    public int Value { get; set; }
}
