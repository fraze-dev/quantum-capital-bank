using System.Net;
using System.Net.Http.Json;
using BankApi.Dtos;
using static BankApi.Tests.Integration.TestApi;

namespace BankApi.Tests.Integration;

// The demo dataset (seeded into the empty test database on startup) and the
// read-only auditor login that the public demo advertises.
[Collection(ApiCollection.Name)]
public class DemoDataTests(BankApiFixture api)
{
    private static readonly string[] DemoAccounts =
        { "CHK-1001", "SAV-1001", "CHK-1002", "CHK-1003", "SAV-1002", "CHK-1004", "SAV-1003", "CHK-1005" };

    [Fact]
    public async Task EveryDemoAccount_HasHistoryThatAddsUpToItsBalance()
    {
        HttpClient auditor = await api.LoginAsync("auditor", "auditor123");
        List<AccountSummary> accounts = await auditor.GetFromJsonAsync<List<AccountSummary>>($"{V1}/accounts");

        foreach (string number in DemoAccounts)
        {
            AccountSummary account = Assert.Single(accounts, a => a.AccountNumber == number);
            List<TransactionSummary> history = await auditor.GetHistoryAsync(number);

            Assert.NotEmpty(history);
            Assert.Equal(account.Balance, history.Sum(t => t.Amount));
            Assert.Equal(account.Balance, history[0].BalanceAfter); // newest entry
            Assert.All(history, t => Assert.True(t.BalanceAfter >= 0m, $"{number} went negative"));
        }
    }

    [Fact]
    public async Task DemoData_ShowsEachStatus_WithItsAuditEntry_AndNoActivityAfterwards()
    {
        HttpClient auditor = await api.LoginAsync("auditor", "auditor123");
        List<AccountSummary> accounts = await auditor.GetFromJsonAsync<List<AccountSummary>>($"{V1}/accounts");
        List<AuditEntrySummary> audit = await auditor.GetFromJsonAsync<List<AuditEntrySummary>>($"{V1}/audit?limit=500");

        Assert.Equal("Closed", accounts.Single(a => a.AccountNumber == "CHK-1002").Status);
        Assert.Equal(0m, accounts.Single(a => a.AccountNumber == "CHK-1002").Balance);
        Assert.Equal("Frozen", accounts.Single(a => a.AccountNumber == "CHK-1004").Status);

        AuditEntrySummary closed = Assert.Single(audit, e => e.TargetId == "CHK-1002" && e.Action == "CloseAccount");
        AuditEntrySummary frozen = Assert.Single(audit, e => e.TargetId == "CHK-1004" && e.Action == "FreezeAccount");
        AuditEntrySummary disabled = Assert.Single(audit, e => e.TargetId == "rjones" && e.Action == "DisableCustomer");

        Assert.All((await auditor.GetHistoryAsync("CHK-1002")), t => Assert.True(t.Timestamp <= closed.Timestamp));
        Assert.All((await auditor.GetHistoryAsync("CHK-1004")), t => Assert.True(t.Timestamp <= frozen.Timestamp));
        Assert.All((await auditor.GetHistoryAsync("CHK-1005")), t => Assert.True(t.Timestamp <= disabled.Timestamp));

        // rjones is disabled, so the right password gets "disabled", not a token.
        Assert.Equal(HttpStatusCode.Forbidden, (await api.LoginRawAsync("rjones", "pass000")).StatusCode);
    }

    [Theory]
    [InlineData("/accounts")]
    [InlineData("/customers")]
    [InlineData("/transactions")]
    [InlineData("/audit")]
    [InlineData("/accounts/customer/jsmith")]
    [InlineData("/customers/jsmith")]
    [InlineData("/accounts/CHK-1001/transactions")]
    public async Task Auditor_CanViewEverything(string path)
    {
        HttpClient auditor = await api.LoginAsync("auditor", "auditor123");

        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync(V1 + path)).StatusCode);
    }

    [Fact]
    public async Task Auditor_CannotChangeAnything()
    {
        HttpClient auditor = await api.LoginAsync("auditor", "auditor123");
        Customer customer = await api.NewCustomerAsync();
        AccountSummary account = await customer.OpenAccountAsync("Checking", initialDeposit: 10m);

        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.SetAccountStatusAsync(account.AccountNumber, "Frozen")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.SetCustomerStatusAsync(customer.Username, "Disabled")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.WithdrawAsync(account.AccountNumber, 5m)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsJsonAsync(
            $"{V1}/accounts", new { ownerUsername = customer.Username, accountType = "Savings" })).StatusCode);

        AccountSummary unchanged = await customer.GetAccountAsync(account.AccountNumber);
        Assert.Equal(("Active", 10m), (unchanged.Status, unchanged.Balance));
    }
}
