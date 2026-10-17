using MongoDB.Bson;
using MongoDB.Driver;

namespace BankApi;

// Everything that changes an account's balance goes through here. Each
// operation runs in a MongoDB transaction, so the balance change(s) and the
// matching ledger entries are saved together or not at all.
public class AccountService
{
    public const decimal CheckingOverdraftLimit = 100m;

    private readonly IMongoClient _client;
    private readonly IAccountRepository _accounts;
    private readonly ICustomerRepository _customers;
    private readonly ITransactionRepository _transactions;

    public AccountService(
        IMongoClient client,
        IAccountRepository accounts,
        ICustomerRepository customers,
        ITransactionRepository transactions)
    {
        _client = client;
        _accounts = accounts;
        _customers = customers;
        _transactions = transactions;
    }

    public static decimal OverdraftLimitFor(AccountDocument account) =>
        account.AccountType == "Checking" ? CheckingOverdraftLimit : 0m;

    public async Task<AccountDocument> OpenAccountAsync(string ownerUsername, string accountType, string prefix)
    {
        // Taken outside the transaction: like a database sequence, a number
        // that ends up unused just leaves a gap.
        string accountNumber = await _accounts.GetNextAccountNumberAsync(prefix);

        var account = new AccountDocument
        {
            AccountNumber = accountNumber,
            OwnerUsername = ownerUsername,
            AccountType = accountType,
            Balance = 0m
        };

        return await RunInTransactionAsync(async session =>
        {
            await _accounts.CreateAsync(session, account);
            await _customers.AddAccountNumberAsync(session, ownerUsername, accountNumber);
            return account;
        });
    }

    public Task<AccountDocument> DepositAsync(AccountDocument account, decimal amount, string performedBy)
    {
        return RunInTransactionAsync(async session =>
        {
            AccountDocument updated = await _accounts.ApplyBalanceChangeAsync(session, account.AccountNumber, amount, null);
            await _transactions.InsertAsync(session, new[]
            {
                NewEntry(updated, TransactionTypes.Deposit, amount, performedBy, DateTime.UtcNow)
            });
            return updated;
        });
    }

    // Returns null (and changes nothing) if the withdrawal would take the
    // balance past the account's overdraft limit.
    public Task<AccountDocument> WithdrawAsync(AccountDocument account, decimal amount, string performedBy)
    {
        return RunInTransactionAsync(async session =>
        {
            AccountDocument updated = await DebitAsync(session, account, amount);
            if (updated == null)
            {
                return null;
            }

            await _transactions.InsertAsync(session, new[]
            {
                NewEntry(updated, TransactionTypes.Withdrawal, -amount, performedBy, DateTime.UtcNow)
            });
            return updated;
        });
    }

    // Returns null (and changes nothing) if the source account can't cover it.
    public Task<TransferResult> TransferAsync(AccountDocument from, AccountDocument to, decimal amount, string performedBy)
    {
        return RunInTransactionAsync(async session =>
        {
            AccountDocument updatedFrom = await DebitAsync(session, from, amount);
            if (updatedFrom == null)
            {
                return null;
            }

            AccountDocument updatedTo = await _accounts.ApplyBalanceChangeAsync(session, to.AccountNumber, amount, null)
                ?? throw new InvalidOperationException($"Account {to.AccountNumber} disappeared mid-transfer.");

            string transferId = ObjectId.GenerateNewId().ToString();
            DateTime timestamp = DateTime.UtcNow;

            TransactionDocument outLeg = NewEntry(updatedFrom, TransactionTypes.TransferOut, -amount, performedBy, timestamp);
            outLeg.CounterpartyAccountNumber = to.AccountNumber;
            outLeg.TransferId = transferId;

            TransactionDocument inLeg = NewEntry(updatedTo, TransactionTypes.TransferIn, amount, performedBy, timestamp);
            inLeg.CounterpartyAccountNumber = from.AccountNumber;
            inLeg.TransferId = transferId;

            await _transactions.InsertAsync(session, new[] { outLeg, inLeg });
            return new TransferResult(updatedFrom, updatedTo);
        });
    }

    // Only succeeds if balance - amount stays within the overdraft limit,
    // i.e. balance >= amount - limit, checked atomically by the database.
    private Task<AccountDocument> DebitAsync(IClientSessionHandle session, AccountDocument account, decimal amount)
    {
        return _accounts.ApplyBalanceChangeAsync(
            session, account.AccountNumber, -amount, amount - OverdraftLimitFor(account));
    }

    private static TransactionDocument NewEntry(
        AccountDocument account, string type, decimal amount, string performedBy, DateTime timestamp)
    {
        return new TransactionDocument
        {
            Id = ObjectId.GenerateNewId().ToString(),
            AccountNumber = account.AccountNumber,
            OwnerUsername = account.OwnerUsername,
            Type = type,
            Amount = amount,
            BalanceAfter = account.Balance,
            PerformedBy = performedBy,
            Timestamp = timestamp
        };
    }

    // WithTransactionAsync commits when the callback returns and retries it
    // automatically on transient errors such as two transactions touching the
    // same account at once.
    private async Task<T> RunInTransactionAsync<T>(Func<IClientSessionHandle, Task<T>> work)
    {
        using IClientSessionHandle session = await _client.StartSessionAsync();
        return await session.WithTransactionAsync((s, _) => work(s));
    }
}

public record TransferResult(AccountDocument From, AccountDocument To);
