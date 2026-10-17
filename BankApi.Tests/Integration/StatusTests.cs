using System.Net;
using System.Net.Http.Json;
using BankApi.Dtos;
using static BankApi.Tests.Integration.TestApi;

namespace BankApi.Tests.Integration;

// Admin status changes (freeze / close / disable) and the audit log.
[Collection(ApiCollection.Name)]
public class StatusTests(BankApiFixture api)
{
    [Fact]
    public async Task FrozenAccount_BlocksAllMoneyMovement_AndUnfreezingRestoresIt()
    {
        HttpClient admin = await api.AdminAsync();
        Customer customer = await api.NewCustomerAsync();
        AccountSummary frozen = await customer.OpenAccountAsync("Checking", initialDeposit: 100m);
        AccountSummary other = await customer.OpenAccountAsync("Savings", initialDeposit: 100m);

        Assert.Equal(HttpStatusCode.OK, (await admin.SetAccountStatusAsync(frozen.AccountNumber, "Frozen")).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await customer.Client.DepositAsync(frozen.AccountNumber, 5m)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await customer.Client.WithdrawAsync(frozen.AccountNumber, 5m)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await customer.Client.TransferAsync(frozen.AccountNumber, other.AccountNumber, 5m)).StatusCode);

        // Transfer *into* the frozen account: the money is taken from the
        // other account first, then the deposit side is refused, so the whole
        // transaction must roll back.
        Assert.Equal(HttpStatusCode.Conflict, (await customer.Client.TransferAsync(other.AccountNumber, frozen.AccountNumber, 5m)).StatusCode);
        Assert.Equal(100m, (await customer.GetAccountAsync(other.AccountNumber)).Balance);
        Assert.Single(await customer.Client.GetHistoryAsync(other.AccountNumber)); // just the initial deposit

        Assert.Equal(HttpStatusCode.OK, (await admin.SetAccountStatusAsync(frozen.AccountNumber, "Active")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.Client.DepositAsync(frozen.AccountNumber, 5m)).StatusCode);
    }

    [Fact]
    public async Task Closing_RequiresAZeroBalance_AndIsFinal()
    {
        HttpClient admin = await api.AdminAsync();
        Customer customer = await api.NewCustomerAsync();
        AccountSummary account = await customer.OpenAccountAsync("Checking", initialDeposit: 20m);

        HttpResponseMessage withMoney = await admin.SetAccountStatusAsync(account.AccountNumber, "Closed");
        Assert.Equal(HttpStatusCode.BadRequest, withMoney.StatusCode);
        Assert.Contains("$0.00", await withMoney.Content.ReadAsStringAsync());

        await customer.Client.WithdrawAsync(account.AccountNumber, 20m);
        Assert.Equal(HttpStatusCode.OK, (await admin.SetAccountStatusAsync(account.AccountNumber, "Closed")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SetAccountStatusAsync(account.AccountNumber, "Active")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await customer.Client.DepositAsync(account.AccountNumber, 1m)).StatusCode);

        // Closed accounts are kept, with their history.
        AccountSummary closed = await customer.GetAccountAsync(account.AccountNumber);
        Assert.Equal("Closed", closed.Status);
        Assert.Equal(2, (await customer.Client.GetHistoryAsync(account.AccountNumber)).Count);
    }

    [Fact]
    public async Task StatusChanges_RequireAReason_AndAreAudited()
    {
        HttpClient admin = await api.AdminAsync();
        Customer customer = await api.NewCustomerAsync();
        AccountSummary account = await customer.OpenAccountAsync("Savings");

        HttpResponseMessage noReason = await admin.PostAsJsonAsync(
            $"{V1}/accounts/{account.AccountNumber}/status", new { status = "Frozen", reason = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        await admin.SetAccountStatusAsync(account.AccountNumber, "Frozen", "Suspicious activity");
        await admin.SetCustomerStatusAsync(customer.Username, "Disabled", "Identity check failed");

        List<AuditEntrySummary> log = await admin.GetFromJsonAsync<List<AuditEntrySummary>>(
            $"{V1}/audit?username={customer.Username}");
        Assert.Collection(log, // newest first; covers the customer and their accounts
            e => Assert.Equal(("DisableCustomer", customer.Username, "Active", "Disabled", "Identity check failed", "admin"),
                (e.Action, e.TargetId, e.FromStatus, e.ToStatus, e.Reason, e.PerformedBy)),
            e => Assert.Equal(("FreezeAccount", account.AccountNumber, "Active", "Frozen", "Suspicious activity", "admin"),
                (e.Action, e.TargetId, e.FromStatus, e.ToStatus, e.Reason, e.PerformedBy)));
    }

    [Fact]
    public async Task Customers_CannotChangeStatuses()
    {
        Customer customer = await api.NewCustomerAsync();
        AccountSummary account = await customer.OpenAccountAsync("Savings");

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Client.SetAccountStatusAsync(account.AccountNumber, "Frozen")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Client.SetCustomerStatusAsync(customer.Username, "Disabled")).StatusCode);
    }

    [Fact]
    public async Task DisabledCustomer_IsLockedOutImmediately_AndCanBeReenabled()
    {
        HttpClient admin = await api.AdminAsync();
        Customer customer = await api.NewCustomerAsync();
        string accountsUrl = $"{V1}/accounts/customer/{customer.Username}";
        Assert.Equal(HttpStatusCode.OK, (await customer.Client.GetAsync(accountsUrl)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.SetCustomerStatusAsync(customer.Username, "Disabled")).StatusCode);

        // The token they already hold stops working on the very next request...
        HttpResponseMessage withOldToken = await customer.Client.GetAsync(accountsUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, withOldToken.StatusCode);
        Assert.Equal("Your account has been disabled.", await withOldToken.Content.ReadAsStringAsync());

        // ...and they can't log in again (told why only with the right password).
        Assert.Equal(HttpStatusCode.Forbidden, (await api.LoginRawAsync(customer.Username, customer.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.LoginRawAsync(customer.Username, "wrong")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.SetCustomerStatusAsync(customer.Username, "Active")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.LoginRawAsync(customer.Username, customer.Password)).StatusCode);
    }

    [Fact]
    public async Task TheAdminLogin_CannotBeDisabled()
    {
        HttpClient admin = await api.AdminAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.SetCustomerStatusAsync("admin", "Disabled")).StatusCode);
    }
}
