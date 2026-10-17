using BankApi.Models;
using BankApi.Services;
using MongoDB.Driver;

namespace BankApi.Repositories;

// Demo customers and accounts for a brand-new, empty database. Runs as the
// first data migration, so the later ones (opening-balance ledger entries,
// account-number counters) build on it. Does nothing if any data exists.
public static class MongoSeedData
{
    public static async Task SeedAsync(IMongoDatabase database)
    {
        var users = database.GetCollection<UserDocument>(UserRepository.CollectionName);
        var accounts = database.GetCollection<AccountDocument>("accounts");

        bool hasData = await users.Find(FilterDefinition<UserDocument>.Empty).AnyAsync()
            || await accounts.Find(FilterDefinition<AccountDocument>.Empty).AnyAsync();
        if (hasData)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        await users.InsertManyAsync(new[]
        {
            new UserDocument { Username = "jsmith", PasswordHash = PasswordUtil.Hash("pass123"), Role = Roles.Customer, CreatedAt = now },
            new UserDocument { Username = "agarcia", PasswordHash = PasswordUtil.Hash("pass456"), Role = Roles.Customer, CreatedAt = now }
        });

        await accounts.InsertManyAsync(new[]
        {
            new AccountDocument { AccountNumber = "SAV-1001", OwnerUsername = "jsmith", AccountType = "Savings", Balance = 500m },
            new AccountDocument { AccountNumber = "CHK-1001", OwnerUsername = "jsmith", AccountType = "Checking", Balance = 200m },
            new AccountDocument { AccountNumber = "CHK-1002", OwnerUsername = "jsmith", AccountType = "Checking", Balance = 700m },
            new AccountDocument { AccountNumber = "CHK-1003", OwnerUsername = "jsmith", AccountType = "Checking", Balance = 0m },
            new AccountDocument { AccountNumber = "SAV-1002", OwnerUsername = "agarcia", AccountType = "Savings", Balance = 1500m }
        });
    }
}
