using BankApi.Models;

namespace BankApi.Dtos;

public class AccountSummary
{
    public string AccountNumber { get; set; }
    public string OwnerUsername { get; set; }
    public string AccountType { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; }

    // The status reason is deliberately left out: it can be internal (e.g.
    // "suspected fraud"). Admins see it in the audit log.
    public static AccountSummary From(AccountDocument account)
    {
        return new AccountSummary
        {
            AccountNumber = account.AccountNumber,
            OwnerUsername = account.OwnerUsername,
            AccountType = account.AccountType,
            Balance = account.Balance,
            Status = account.Status
        };
    }
}

public class CreateAccountRequest
{
    public string OwnerUsername { get; set; }
    public string AccountType { get; set; }
}

public class AmountRequest
{
    public decimal Amount { get; set; }
}

public class TransferRequest
{
    public string FromAccountNumber { get; set; }
    public string ToAccountNumber { get; set; }
    public decimal Amount { get; set; }
}

public class TransferResponse
{
    public AccountSummary From { get; set; }
    public AccountSummary To { get; set; }
}
