using BankApi.Models;
using BankApi.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BankApi.Services;

// Everything that changes an account goes through here. Money movements and
// status changes each run in a MongoDB transaction, so the change and its
// record (ledger entries or audit entry) are saved together or not at all.
public class AccountService
{
    public const decimal CheckingOverdraftLimit = 100m;

    private readonly MongoTransactions _transactions;
    private readonly IAccountRepository _accounts;
    private readonly ITransactionRepository _ledger;
    private readonly IAuditRepository _audit;

    public AccountService(
        MongoTransactions transactions,
        IAccountRepository accounts,
        ITransactionRepository ledger,
        IAuditRepository audit)
    {
        _transactions = transactions;
        _accounts = accounts;
        _ledger = ledger;
        _audit = audit;
    }

    public static decimal OverdraftLimitFor(AccountDocument account) =>
        account.AccountType == "Checking" ? CheckingOverdraftLimit : 0m;

    // A new account starts at zero, so there's no ledger entry to write.
    public async Task<AccountDocument> OpenAccountAsync(string ownerUsername, string accountType, string prefix)
    {
        var account = new AccountDocument
        {
            AccountNumber = await _accounts.GetNextAccountNumberAsync(prefix),
            OwnerUsername = ownerUsername,
            AccountType = accountType,
            Balance = 0m,
            Status = AccountStatuses.Active
        };

        await _accounts.CreateAsync(account);
        return account;
    }

    public Task<ServiceResult<AccountDocument>> DepositAsync(AccountDocument account, decimal amount, string performedBy)
    {
        return _transactions.RunAsync(async session =>
        {
            AccountDocument updated = await CreditAsync(session, account, amount);
            await _ledger.InsertAsync(session, new[]
            {
                NewEntry(updated, TransactionTypes.Deposit, amount, performedBy, DateTime.UtcNow)
            });
            return updated;
        });
    }

    public Task<ServiceResult<AccountDocument>> WithdrawAsync(AccountDocument account, decimal amount, string performedBy)
    {
        return _transactions.RunAsync(async session =>
        {
            AccountDocument updated = await DebitAsync(session, account, amount);
            await _ledger.InsertAsync(session, new[]
            {
                NewEntry(updated, TransactionTypes.Withdrawal, -amount, performedBy, DateTime.UtcNow)
            });
            return updated;
        });
    }

    public Task<ServiceResult<TransferResult>> TransferAsync(
        AccountDocument from, AccountDocument to, decimal amount, string performedBy)
    {
        return _transactions.RunAsync(async session =>
        {
            AccountDocument updatedFrom = await DebitAsync(session, from, amount);
            AccountDocument updatedTo = await CreditAsync(session, to, amount);

            string transferId = ObjectId.GenerateNewId().ToString();
            DateTime timestamp = DateTime.UtcNow;

            TransactionDocument outLeg = NewEntry(updatedFrom, TransactionTypes.TransferOut, -amount, performedBy, timestamp);
            outLeg.CounterpartyAccountNumber = to.AccountNumber;
            outLeg.TransferId = transferId;

            TransactionDocument inLeg = NewEntry(updatedTo, TransactionTypes.TransferIn, amount, performedBy, timestamp);
            inLeg.CounterpartyAccountNumber = from.AccountNumber;
            inLeg.TransferId = transferId;

            await _ledger.InsertAsync(session, new[] { outLeg, inLeg });
            return new TransferResult(updatedFrom, updatedTo);
        });
    }

    // Admin action: freeze, unfreeze or close an account, recorded in the
    // audit log. See AccountStatusRules for which changes are allowed.
    public async Task<ServiceResult<AccountDocument>> ChangeStatusAsync(
        string accountNumber, string newStatus, string reason, string performedBy)
    {
        AccountDocument account = await _accounts.GetByAccountNumberAsync(accountNumber);
        if (account == null)
        {
            return ServiceResult<AccountDocument>.Fail(ServiceError.NotFound, $"No account found with number '{accountNumber}'.");
        }

        (string action, string error) = AccountStatusRules.Check(account, newStatus);
        if (error != null)
        {
            return ServiceResult<AccountDocument>.Fail(ServiceError.Invalid, error);
        }

        return await _transactions.RunAsync(async session =>
        {
            // Timestamped inside the transaction (fresh on each retry), so the
            // audit entry orders correctly against ledger entries that were
            // committed just before it.
            var change = new StatusChange(newStatus, reason, performedBy, DateTime.UtcNow);

            // Applies only if nothing changed since the checks above.
            AccountDocument updated = await _accounts.UpdateStatusAsync(
                session, accountNumber, account.Status, change, requireZeroBalance: newStatus == AccountStatuses.Closed)
                ?? throw new TransactionAbortedException(ServiceError.Conflict,
                    $"Account {accountNumber} changed while this was being applied. Reload and try again.");

            await _audit.InsertAsync(session, new AuditEntryDocument
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Timestamp = change.ChangedAt,
                PerformedBy = performedBy,
                Action = action,
                TargetType = AuditTargetTypes.Account,
                TargetId = accountNumber,
                FromStatus = account.Status,
                ToStatus = newStatus,
                Reason = reason
            });
            return updated;
        });
    }

    // Adds money. Aborts the transaction if the account isn't Active.
    private async Task<AccountDocument> CreditAsync(IClientSessionHandle session, AccountDocument account, decimal amount)
    {
        return await _accounts.ApplyBalanceChangeAsync(session, account.AccountNumber, amount, null)
            ?? throw await NotActiveErrorAsync(account.AccountNumber);
    }

    // Removes money if balance - amount stays within the overdraft limit,
    // i.e. balance >= amount - limit, checked atomically by the database.
    // Aborts the transaction if the account isn't Active or can't cover it.
    private async Task<AccountDocument> DebitAsync(IClientSessionHandle session, AccountDocument account, decimal amount)
    {
        AccountDocument updated = await _accounts.ApplyBalanceChangeAsync(
            session, account.AccountNumber, -amount, amount - OverdraftLimitFor(account));
        if (updated != null)
        {
            return updated;
        }

        // The update matched nothing: work out which condition failed.
        AccountDocument current = await _accounts.GetByAccountNumberAsync(account.AccountNumber);
        if (current?.Status != AccountStatuses.Active)
        {
            throw await NotActiveErrorAsync(account.AccountNumber);
        }

        throw new TransactionAbortedException(ServiceError.Invalid, account.AccountType == "Checking"
            ? $"Insufficient funds. Overdraft limit is {Money.Format(CheckingOverdraftLimit)}."
            : "Insufficient funds. Savings accounts do not allow overdraft.");
    }

    private async Task<TransactionAbortedException> NotActiveErrorAsync(string accountNumber)
    {
        AccountDocument current = await _accounts.GetByAccountNumberAsync(accountNumber);
        string status = current?.Status?.ToLowerInvariant() ?? "unavailable";
        return new TransactionAbortedException(ServiceError.Conflict,
            $"Account {accountNumber} is {status}, so money can't be moved in or out of it.");
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
}

public record TransferResult(AccountDocument From, AccountDocument To);
