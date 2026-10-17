using BankApi.Models;

namespace BankApi.Services;

// Which account status changes an admin may make:
//   Active -> Frozen, Frozen -> Active, Active/Frozen -> Closed (at $0).
// Closed is final.
public static class AccountStatusRules
{
    // Returns the audit action for a valid change, or an error message.
    public static (string Action, string Error) Check(AccountDocument account, string newStatus)
    {
        string current = account.Status;
        if (current == newStatus)
        {
            return (null, $"Account {account.AccountNumber} is already {newStatus.ToLowerInvariant()}.");
        }

        if (current == AccountStatuses.Closed)
        {
            return (null, $"Account {account.AccountNumber} is closed. Closed accounts can't be changed.");
        }

        switch (newStatus)
        {
            case AccountStatuses.Frozen:
                return (AuditActions.FreezeAccount, null);
            case AccountStatuses.Active:
                return (AuditActions.UnfreezeAccount, null);
            case AccountStatuses.Closed when account.Balance != 0m:
                return (null, $"Account {account.AccountNumber} has a balance of {Money.Format(account.Balance)}. " +
                              "Only accounts with a $0.00 balance can be closed.");
            case AccountStatuses.Closed:
                return (AuditActions.CloseAccount, null);
            default:
                return (null, $"Status must be one of: {string.Join(", ", AccountStatuses.All)}.");
        }
    }
}
