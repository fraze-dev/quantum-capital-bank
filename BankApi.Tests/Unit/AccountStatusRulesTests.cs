using BankApi.Models;
using BankApi.Services;

namespace BankApi.Tests.Unit;

public class AccountStatusRulesTests
{
    private static AccountDocument Account(string status, decimal balance = 0m) =>
        new() { AccountNumber = "CHK-1001", Status = status, Balance = balance };

    [Theory]
    [InlineData(AccountStatuses.Active, AccountStatuses.Frozen, AuditActions.FreezeAccount)]
    [InlineData(AccountStatuses.Frozen, AccountStatuses.Active, AuditActions.UnfreezeAccount)]
    [InlineData(AccountStatuses.Active, AccountStatuses.Closed, AuditActions.CloseAccount)]
    [InlineData(AccountStatuses.Frozen, AccountStatuses.Closed, AuditActions.CloseAccount)]
    public void AllowedChanges_ReturnTheirAuditAction(string from, string to, string expectedAction)
    {
        (string action, string error) = AccountStatusRules.Check(Account(from), to);

        Assert.Null(error);
        Assert.Equal(expectedAction, action);
    }

    [Theory]
    [InlineData(AccountStatuses.Closed, AccountStatuses.Active, "closed")]
    [InlineData(AccountStatuses.Closed, AccountStatuses.Frozen, "closed")]
    [InlineData(AccountStatuses.Active, AccountStatuses.Active, "already active")]
    [InlineData(AccountStatuses.Frozen, AccountStatuses.Frozen, "already frozen")]
    [InlineData(AccountStatuses.Active, "Sleeping", "Status must be one of")]
    public void DisallowedChanges_ExplainWhy(string from, string to, string expectedMessagePart)
    {
        (string action, string error) = AccountStatusRules.Check(Account(from), to);

        Assert.Null(action);
        Assert.Contains(expectedMessagePart, error);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(-25)] // overdrawn checking accounts can't be closed either
    [InlineData(0.01)]
    public void Closing_RequiresAZeroBalance(decimal balance)
    {
        (string action, string error) = AccountStatusRules.Check(Account(AccountStatuses.Active, balance), AccountStatuses.Closed);

        Assert.Null(action);
        Assert.Contains("Only accounts with a $0.00 balance can be closed", error);
    }
}
