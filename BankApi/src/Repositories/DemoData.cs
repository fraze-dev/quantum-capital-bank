using BankApi.Models;
using BankApi.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BankApi.Repositories;

// The public demo's data: four customers with ~90 days of believable activity
// (paydays, card spending, monthly savings transfers), arranged to show every
// feature: a frozen account, a closed account and a disabled customer, each
// with its audit-log entry. Plus a read-only "auditor" staff login.
//
// Generated from a fixed random seed, so it's the same every time, with dates
// counted back from when it's loaded, so the history always looks recent.
// Every account's ledger adds up to its balance.
public static class DemoData
{
    public const string AuditorUsername = "auditor";
    public const string AuditorPassword = "auditor123";

    private const int HistoryDays = 90;
    private const string Admin = "admin";

    // First startup: load the demo data into an empty database.
    public static async Task SeedIfEmptyAsync(IMongoDatabase database)
    {
        bool hasData = await database.GetCollection<UserDocument>(UserRepository.CollectionName)
                .Find(u => u.Role != Roles.Admin).AnyAsync()
            || await database.GetCollection<AccountDocument>("accounts")
                .Find(FilterDefinition<AccountDocument>.Empty).AnyAsync();
        if (!hasData)
        {
            await LoadAsync(database);
        }
    }

    // Wipe customers, accounts, history and the audit log (admin logins are
    // kept), drop the legacy "customers" collection, and load fresh demo data.
    public static async Task ResetAsync(IMongoDatabase database)
    {
        await database.GetCollection<UserDocument>(UserRepository.CollectionName).DeleteManyAsync(u => u.Role != Roles.Admin);
        await database.GetCollection<BsonDocument>("accounts").DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await database.GetCollection<BsonDocument>(TransactionRepository.CollectionName).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await database.GetCollection<BsonDocument>(AuditRepository.CollectionName).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await database.GetCollection<BsonDocument>(AccountRepository.CountersCollectionName).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        await database.DropCollectionAsync("customers");

        await LoadAsync(database);
    }

    // `dotnet run -- reset-demo-data --yes` (see Program.cs).
    public static async Task RunResetCommandAsync(IConfiguration configuration, bool confirmed)
    {
        string databaseName = configuration["MongoDb:DatabaseName"] ?? "BankApiDb";
        if (!confirmed)
        {
            Console.WriteLine($"This deletes all customers, accounts, transactions and audit entries in '{databaseName}' " +
                              "and replaces them with demo data. Run again with --yes to proceed.");
            return;
        }

        var database = new MongoClient(configuration["MongoDb:ConnectionString"]).GetDatabase(databaseName);
        Console.WriteLine($"Resetting demo data in '{databaseName}'...");
        await ResetAsync(database);
        Console.WriteLine("Done.");
    }

    private static async Task LoadAsync(IMongoDatabase database)
    {
        DemoSet demo = Build(DateTime.UtcNow);

        await database.GetCollection<UserDocument>(UserRepository.CollectionName).InsertManyAsync(demo.Users);
        await database.GetCollection<AccountDocument>("accounts").InsertManyAsync(demo.Ledger.Accounts.Values);
        await database.GetCollection<TransactionDocument>(TransactionRepository.CollectionName).InsertManyAsync(demo.Ledger.Entries);
        await database.GetCollection<AuditEntryDocument>(AuditRepository.CollectionName).InsertManyAsync(demo.Audit);
        await AccountRepository.SyncAccountNumberCountersAsync(database);
    }

    private record DemoSet(List<UserDocument> Users, Ledger Ledger, List<AuditEntryDocument> Audit);

    // A customer's regular money habits, used to generate their history.
    private record Profile(
        string Checking, decimal CheckingOpening, decimal Payday,
        string Savings, decimal SavingsOpening, decimal MonthlySaving);

    private static DemoSet Build(DateTime now)
    {
        var random = new Random(20261017);
        DateTime start = now.Date.AddDays(-HistoryDays);
        DateTime customersSince = start.AddDays(-7);

        var users = new List<UserDocument>
        {
            Customer("jsmith", "pass123", customersSince),
            Customer("agarcia", "pass456", customersSince),
            Customer("mlee", "pass789", customersSince),
            Customer("rjones", "pass000", customersSince),
            new()
            {
                Username = AuditorUsername,
                PasswordHash = PasswordUtil.Hash(AuditorPassword),
                Role = Roles.Auditor,
                Status = UserStatuses.Active,
                CreatedAt = customersSince
            }
        };

        // When activity stops on an account, because of an admin action.
        DateTime closeAt = start.AddDays(60).AddHours(14);
        DateTime freezeAt = now.Date.AddDays(-3).AddHours(11).AddMinutes(20);
        DateTime disableAt = now.Date.AddDays(-10).AddHours(16).AddMinutes(45);
        var activeUntil = new Dictionary<string, DateTime>
        {
            ["CHK-1002"] = closeAt,
            ["CHK-1004"] = freezeAt,
            ["CHK-1005"] = disableAt
        };

        var ledger = new Ledger((account, when) => !activeUntil.TryGetValue(account, out DateTime until) || when < until);
        ledger.Open("CHK-1001", "jsmith", "Checking");
        ledger.Open("SAV-1001", "jsmith", "Savings");
        ledger.Open("CHK-1002", "jsmith", "Checking"); // closed partway through
        ledger.Open("CHK-1003", "agarcia", "Checking");
        ledger.Open("SAV-1002", "agarcia", "Savings");
        ledger.Open("CHK-1004", "mlee", "Checking");   // frozen 3 days ago
        ledger.Open("SAV-1003", "mlee", "Savings");
        ledger.Open("CHK-1005", "rjones", "Checking"); // owner disabled 10 days ago

        var profiles = new[]
        {
            new Profile("CHK-1001", 1250m, 2150m, "SAV-1001", 4200m, 400m),
            new Profile("CHK-1003", 2100m, 2800m, "SAV-1002", 9500m, 600m),
            new Profile("CHK-1004", 640m, 1650m, "SAV-1003", 1200m, 250m),
            new Profile("CHK-1005", 900m, 1900m, null, 0m, 0m)
        };

        for (int day = 0; day <= HistoryDays; day++)
        {
            DateTime date = start.AddDays(day);
            var today = new List<(DateTime When, Action<DateTime> Apply)>();

            foreach (Profile p in profiles)
            {
                string owner = ledger.Accounts[p.Checking].OwnerUsername;

                if (day == 0)
                {
                    today.Add((date.AddHours(9).AddMinutes(random.Next(60)), at => ledger.Deposit(p.Checking, p.CheckingOpening, at, owner)));
                    if (p.Savings != null)
                    {
                        today.Add((date.AddHours(10).AddMinutes(random.Next(60)), at => ledger.Deposit(p.Savings, p.SavingsOpening, at, owner)));
                    }
                }

                if (date.Day is 1 or 15)
                {
                    today.Add((date.AddHours(7).AddMinutes(random.Next(50)), at => ledger.Deposit(p.Checking, p.Payday, at, owner)));
                }

                // Card spending: zero to three purchases a day.
                int purchases = random.Next(10) switch { < 3 => 0, < 7 => 1, < 9 => 2, _ => 3 };
                for (int i = 0; i < purchases; i++)
                {
                    decimal amount = Money(random, 6m, random.Next(10) == 0 ? 420m : 140m);
                    today.Add((date.AddHours(10 + random.Next(11)).AddMinutes(random.Next(60)),
                        at => ledger.TryWithdraw(p.Checking, amount, at, owner)));
                }

                if (p.Savings != null && date.Day == 3)
                {
                    decimal saving = p.MonthlySaving;
                    today.Add((date.AddHours(12).AddMinutes(random.Next(30)),
                        at => ledger.TryTransfer(p.Checking, p.Savings, saving, at, owner)));
                }

                if (p.Savings != null && random.Next(40) == 0)
                {
                    decimal amount = Money(random, 50m, 300m);
                    today.Add((date.AddHours(15).AddMinutes(random.Next(60)), at => ledger.TryWithdraw(p.Savings, amount, at, owner)));
                }
            }

            // jsmith's old checking account: a little use, then emptied and closed.
            if (day == 0)
            {
                today.Add((date.AddHours(9).AddMinutes(30), at => ledger.Deposit("CHK-1002", 800m, at, "jsmith")));
            }
            if (day % 9 == 4)
            {
                decimal amount = Money(random, 20m, 90m);
                today.Add((date.AddHours(18), at => ledger.TryWithdraw("CHK-1002", amount, at, "jsmith")));
            }
            if (day == 60)
            {
                today.Add((closeAt.AddMinutes(-10), at =>
                    ledger.TryTransfer("CHK-1002", "SAV-1001", ledger.Accounts["CHK-1002"].Balance, at, "jsmith")));
            }

            // In time order, and nothing in the future. (The ledger itself skips
            // anything on an account after it was frozen, closed or its owner
            // was disabled.)
            foreach ((DateTime when, Action<DateTime> apply) in today.OrderBy(e => e.When))
            {
                if (when < now.AddMinutes(-5))
                {
                    apply(when);
                }
            }
        }

        var audit = new List<AuditEntryDocument>
        {
            ledger.SetStatus("CHK-1002", AccountStatuses.Closed, AuditActions.CloseAccount,
                "Customer request: consolidating into existing accounts.", closeAt),
            ledger.SetStatus("CHK-1004", AccountStatuses.Frozen, AuditActions.FreezeAccount,
                "Unusual card activity flagged by fraud monitoring.", freezeAt),
            DisableCustomer(users.Single(u => u.Username == "rjones"),
                "Identity documents expired; awaiting updated ID.", disableAt)
        };

        return new DemoSet(users, ledger, audit.OrderBy(e => e.Timestamp).ToList());
    }

    private static UserDocument Customer(string username, string password, DateTime since) => new()
    {
        Username = username,
        PasswordHash = PasswordUtil.Hash(password),
        Role = Roles.Customer,
        Status = UserStatuses.Active,
        CreatedAt = since
    };

    private static AuditEntryDocument DisableCustomer(UserDocument user, string reason, DateTime at)
    {
        user.Status = UserStatuses.Disabled;
        user.StatusReason = reason;
        user.StatusChangedBy = Admin;
        user.StatusChangedAt = at;

        return new AuditEntryDocument
        {
            Id = ObjectId.GenerateNewId(at).ToString(),
            Timestamp = at,
            PerformedBy = Admin,
            Action = AuditActions.DisableCustomer,
            TargetType = AuditTargetTypes.Customer,
            TargetId = user.Username,
            FromStatus = UserStatuses.Active,
            ToStatus = UserStatuses.Disabled,
            Reason = reason
        };
    }

    private static decimal Money(Random random, decimal min, decimal max) =>
        Math.Round(min + (decimal)random.NextDouble() * (max - min), 2);

    // Applies demo activity in time order, keeping balances and ledger entries
    // in step exactly as the real AccountService does. Never overdraws, and
    // ignores activity on an account once isOpen says it's no longer open.
    private sealed class Ledger
    {
        private readonly Func<string, DateTime, bool> _isOpen;

        public Ledger(Func<string, DateTime, bool> isOpen)
        {
            _isOpen = isOpen;
        }

        public Dictionary<string, AccountDocument> Accounts { get; } = new();
        public List<TransactionDocument> Entries { get; } = new();

        public void Open(string number, string owner, string type)
        {
            Accounts[number] = new AccountDocument
            {
                AccountNumber = number,
                OwnerUsername = owner,
                AccountType = type,
                Balance = 0m,
                Status = AccountStatuses.Active
            };
        }

        public void Deposit(string account, decimal amount, DateTime at, string by)
        {
            if (!_isOpen(account, at)) return;
            Record(account, TransactionTypes.Deposit, amount, at, by);
        }

        public void TryWithdraw(string account, decimal amount, DateTime at, string by)
        {
            if (!_isOpen(account, at) || Accounts[account].Balance < amount) return;
            Record(account, TransactionTypes.Withdrawal, -amount, at, by);
        }

        public void TryTransfer(string from, string to, decimal amount, DateTime at, string by)
        {
            if (amount <= 0m || !_isOpen(from, at) || !_isOpen(to, at) || Accounts[from].Balance < amount) return;

            string transferId = ObjectId.GenerateNewId(at).ToString();
            Record(from, TransactionTypes.TransferOut, -amount, at, by, to, transferId);
            Record(to, TransactionTypes.TransferIn, amount, at, by, from, transferId);
        }

        public AuditEntryDocument SetStatus(string account, string status, string action, string reason, DateTime at)
        {
            AccountDocument doc = Accounts[account];
            string from = doc.Status;
            doc.Status = status;
            doc.StatusReason = reason;
            doc.StatusChangedBy = Admin;
            doc.StatusChangedAt = at;

            return new AuditEntryDocument
            {
                Id = ObjectId.GenerateNewId(at).ToString(),
                Timestamp = at,
                PerformedBy = Admin,
                Action = action,
                TargetType = AuditTargetTypes.Account,
                TargetId = account,
                FromStatus = from,
                ToStatus = status,
                Reason = reason
            };
        }

        private void Record(string account, string type, decimal amount, DateTime at, string by,
            string counterparty = null, string transferId = null)
        {
            AccountDocument doc = Accounts[account];
            doc.Balance += amount;
            Entries.Add(new TransactionDocument
            {
                Id = ObjectId.GenerateNewId(at).ToString(),
                AccountNumber = account,
                OwnerUsername = doc.OwnerUsername,
                Type = type,
                Amount = amount,
                BalanceAfter = doc.Balance,
                CounterpartyAccountNumber = counterparty,
                TransferId = transferId,
                PerformedBy = by,
                Timestamp = at
            });
        }
    }
}
