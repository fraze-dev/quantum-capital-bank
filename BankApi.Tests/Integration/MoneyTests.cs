using System.Net;
using System.Net.Http.Json;
using BankApi.Dtos;

namespace BankApi.Tests.Integration;

// Deposits, withdrawals and transfers: limits, validation, history, and
// behaviour under concurrent requests.
[Collection(ApiCollection.Name)]
public class MoneyTests(BankApiFixture api)
{
    [Fact]
    public async Task DepositAndWithdraw_UpdateTheBalance_AndAreRecordedInHistory()
    {
        Customer customer = await api.NewCustomerAsync();
        AccountSummary account = await customer.OpenAccountAsync("Savings");

        await customer.Client.DepositAsync(account.AccountNumber, 100m);
        HttpResponseMessage withdraw = await customer.Client.WithdrawAsync(account.AccountNumber, 30.25m);

        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
        Assert.Equal(69.75m, (await withdraw.Content.ReadFromJsonAsync<AccountSummary>()).Balance);

        List<TransactionSummary> history = await customer.Client.GetHistoryAsync(account.AccountNumber);
        Assert.Collection(history, // newest first
            t => { Assert.Equal("Withdrawal", t.Type); Assert.Equal(-30.25m, t.Amount); Assert.Equal(69.75m, t.BalanceAfter); },
            t => { Assert.Equal("Deposit", t.Type); Assert.Equal(100m, t.Amount); Assert.Equal(100m, t.BalanceAfter); });
        Assert.All(history, t => Assert.Equal(customer.Username, t.PerformedBy));
    }

    [Fact]
    public async Task Savings_CannotBeOverdrawn()
    {
        Customer customer = await api.NewCustomerAsync();
        AccountSummary savings = await customer.OpenAccountAsync("Savings", initialDeposit: 50m);

        HttpResponseMessage response = await customer.Client.WithdrawAsync(savings.AccountNumber, 50.01m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Savings accounts do not allow overdraft", await response.Content.ReadAsStringAsync());
        Assert.Equal(50m, (await customer.GetAccountAsync(savings.AccountNumber)).Balance);
    }

    [Fact]
    public async Task Checking_CanGoUpTo100Overdrawn_ButNoFurther()
    {
        Customer customer = await api.NewCustomerAsync();
        AccountSummary checking = await customer.OpenAccountAsync("Checking", initialDeposit: 50m);

        Assert.Equal(HttpStatusCode.OK, (await customer.Client.WithdrawAsync(checking.AccountNumber, 150m)).StatusCode);
        Assert.Equal(-100m, (await customer.GetAccountAsync(checking.AccountNumber)).Balance);

        HttpResponseMessage oneCentMore = await customer.Client.WithdrawAsync(checking.AccountNumber, 0.01m);
        Assert.Equal(HttpStatusCode.BadRequest, oneCentMore.StatusCode);
        Assert.Contains("Overdraft limit is $100.00", await oneCentMore.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1.005)]
    public async Task InvalidAmounts_AreRejected(decimal amount)
    {
        Customer customer = await api.NewCustomerAsync();
        AccountSummary account = await customer.OpenAccountAsync("Checking", initialDeposit: 10m);

        Assert.Equal(HttpStatusCode.BadRequest, (await customer.Client.DepositAsync(account.AccountNumber, amount)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.Client.WithdrawAsync(account.AccountNumber, amount)).StatusCode);
        Assert.Equal(10m, (await customer.GetAccountAsync(account.AccountNumber)).Balance);
    }

    [Fact]
    public async Task Transfer_MovesMoney_AndRecordsBothSides()
    {
        Customer customer = await api.NewCustomerAsync();
        AccountSummary checking = await customer.OpenAccountAsync("Checking", initialDeposit: 200m);
        AccountSummary savings = await customer.OpenAccountAsync("Savings");

        HttpResponseMessage response = await customer.Client.TransferAsync(checking.AccountNumber, savings.AccountNumber, 75m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        TransferResponse result = await response.Content.ReadFromJsonAsync<TransferResponse>();
        Assert.Equal(125m, result.From.Balance);
        Assert.Equal(75m, result.To.Balance);

        TransactionSummary outLeg = (await customer.Client.GetHistoryAsync(checking.AccountNumber))[0];
        TransactionSummary inLeg = (await customer.Client.GetHistoryAsync(savings.AccountNumber))[0];
        Assert.Equal(("TransferOut", -75m, savings.AccountNumber), (outLeg.Type, outLeg.Amount, outLeg.CounterpartyAccountNumber));
        Assert.Equal(("TransferIn", 75m, checking.AccountNumber), (inLeg.Type, inLeg.Amount, inLeg.CounterpartyAccountNumber));
    }

    [Fact]
    public async Task Transfer_ToAnotherCustomersAccount_IsForbidden()
    {
        Customer sender = await api.NewCustomerAsync();
        AccountSummary from = await sender.OpenAccountAsync("Checking", initialDeposit: 100m);
        AccountSummary to = await (await api.NewCustomerAsync()).OpenAccountAsync("Savings");

        HttpResponseMessage response = await sender.Client.TransferAsync(from.AccountNumber, to.AccountNumber, 10m);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(100m, (await sender.GetAccountAsync(from.AccountNumber)).Balance);
    }

    // The core safety property: many simultaneous withdrawals can't push an
    // account past its limit, and every one that succeeded is in the history.
    [Fact]
    public async Task ConcurrentWithdrawals_NeverExceedTheOverdraftLimit()
    {
        Customer customer = await api.NewCustomerAsync();
        AccountSummary checking = await customer.OpenAccountAsync("Checking", initialDeposit: 50m);

        // $50 balance + $100 overdraft = room for exactly 15 withdrawals of $10.
        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => customer.Client.WithdrawAsync(checking.AccountNumber, 10m)));

        Assert.Equal(15, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));

        decimal balance = (await customer.GetAccountAsync(checking.AccountNumber)).Balance;
        List<TransactionSummary> history = await customer.Client.GetHistoryAsync(checking.AccountNumber);
        Assert.Equal(-100m, balance);
        Assert.Equal(balance, history.Sum(t => t.Amount));
    }

    [Fact]
    public async Task AccountsOpenedAtTheSameTime_GetDistinctNumbers()
    {
        Customer customer = await api.NewCustomerAsync();

        AccountSummary[] accounts = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => customer.OpenAccountAsync("Savings")));

        Assert.Equal(5, accounts.Select(a => a.AccountNumber).Distinct().Count());
    }
}
