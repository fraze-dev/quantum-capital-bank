using BankApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace BankApi.Repositories;

// One-time data changes, run at startup. Each finished migration is recorded
// in the "migrations" collection so later startups skip it with a single
// query. Every step is also safe to repeat, in case two Lambda instances
// start at the same moment and both run it.
public static class DataMigrations
{
    private static readonly (string Name, Func<IMongoDatabase, Task> Run)[] All =
    {
        // Listed first so a fresh database gets demo data before the steps
        // below; on an existing database it's a no-op.
        ("000-seed-demo-data", MongoSeedData.SeedAsync),
        ("001-transaction-indexes", TransactionRepository.EnsureIndexesAsync),
        ("002-opening-balances", AddOpeningBalancesAsync),
        ("003-account-number-counters", AccountRepository.SyncAccountNumberCountersAsync),
        ("004-users-from-customers", CopyCustomersToUsersAsync)
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

    // Logins used to live in a "customers" collection (password hash plus a
    // copied list of account numbers). Copy each into "users" as a Customer.
    // The old collection is left untouched rather than renamed, so a copy of
    // the API still running the previous version keeps working until it's
    // redeployed; the demo-data reset removes it later.
    private static async Task CopyCustomersToUsersAsync(IMongoDatabase database)
    {
        var customers = database.GetCollection<BsonDocument>("customers");
        var users = database.GetCollection<UserDocument>(UserRepository.CollectionName);
        DateTime now = DateTime.UtcNow;

        List<BsonDocument> legacy = await customers.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        foreach (BsonDocument customer in legacy)
        {
            await IgnoreDuplicateAsync(() => users.InsertOneAsync(new UserDocument
            {
                Username = customer["_id"].AsString,
                PasswordHash = customer.GetValue("Password", BsonNull.Value).IsString ? customer["Password"].AsString : null,
                Role = Roles.Customer,
                CreatedAt = now
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
