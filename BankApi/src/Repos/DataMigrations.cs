using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace BankApi;

// One-time data changes, run at startup. Each finished migration is recorded
// in the "migrations" collection so later startups skip it with a single
// query. Every step is also safe to repeat, in case two Lambda instances
// start at the same moment and both run it.
public static class DataMigrations
{
    private static readonly (string Name, Func<IMongoDatabase, Task> Run)[] All =
    {
        ("001-transaction-indexes", TransactionRepository.EnsureIndexesAsync),
        ("002-opening-balances", AddOpeningBalancesAsync),
        ("003-account-number-counters", AccountRepository.SyncAccountNumberCountersAsync)
    };

    public static async Task RunAsync(IMongoDatabase database)
    {
        var migrations = database.GetCollection<MigrationDocument>("migrations");
        HashSet<string> applied = (await migrations.Find(FilterDefinition<MigrationDocument>.Empty)
                .Project(m => m.Id)
                .ToListAsync())
            .ToHashSet();

        foreach ((string name, Func<IMongoDatabase, Task> run) in All)
        {
            if (applied.Contains(name))
            {
                continue;
            }

            await run(database);
            await IgnoreDuplicateAsync(() =>
                migrations.InsertOneAsync(new MigrationDocument { Id = name, AppliedAt = DateTime.UtcNow }));
        }
    }

    // Accounts that existed before the ledger have a balance but no history.
    // Give each one an "opening balance" entry so its history adds up. The id
    // is derived from the account number, so a repeat run can't add a second.
    private static async Task AddOpeningBalancesAsync(IMongoDatabase database)
    {
        var accounts = database.GetCollection<AccountDocument>("accounts");
        var transactions = database.GetCollection<TransactionDocument>(TransactionRepository.CollectionName);

        HashSet<string> withHistory = (await transactions
                .Distinct(t => t.AccountNumber, FilterDefinition<TransactionDocument>.Empty)
                .ToListAsync())
            .ToHashSet();

        List<AccountDocument> needOpening = await accounts.Find(a => a.Balance != 0m).ToListAsync();
        DateTime now = DateTime.UtcNow;

        foreach (AccountDocument account in needOpening.Where(a => !withHistory.Contains(a.AccountNumber)))
        {
            await IgnoreDuplicateAsync(() => transactions.InsertOneAsync(new TransactionDocument
            {
                Id = $"opening-{account.AccountNumber}",
                AccountNumber = account.AccountNumber,
                OwnerUsername = account.OwnerUsername,
                Type = TransactionTypes.OpeningBalance,
                Amount = account.Balance,
                BalanceAfter = account.Balance,
                PerformedBy = "system",
                Timestamp = now
            }));
        }
    }

    private static async Task IgnoreDuplicateAsync(Func<Task> insert)
    {
        try
        {
            await insert();
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another instance got there first.
        }
    }
}

internal class MigrationDocument
{
    [BsonId]
    public string Id { get; set; }

    public DateTime AppliedAt { get; set; }
}
