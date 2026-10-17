using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BankApi.Dtos;

namespace BankApi.Tests.Integration;

// Small helpers so tests read as steps ("log in", "open an account",
// "deposit") instead of raw HTTP plumbing.
public static class TestApi
{
    public const string V1 = "/api/v1";

    public static async Task<HttpResponseMessage> LoginRawAsync(this BankApiFixture api, string username, string password)
    {
        return await api.CreateClient().PostAsJsonAsync($"{V1}/auth/login", new { username, password });
    }

    // An HttpClient that sends this user's token with every request.
    public static async Task<HttpClient> LoginAsync(this BankApiFixture api, string username, string password)
    {
        HttpResponseMessage response = await api.LoginRawAsync(username, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        LoginResponse login = await response.Content.ReadFromJsonAsync<LoginResponse>();

        HttpClient client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return client;
    }

    public static Task<HttpClient> AdminAsync(this BankApiFixture api) =>
        api.LoginAsync("admin", BankApiFixture.AdminPassword);

    // Registers a brand-new customer with a unique name and logs them in, so
    // each test works on its own data.
    public static async Task<Customer> NewCustomerAsync(this BankApiFixture api)
    {
        string username = $"test{Guid.NewGuid():N}"[..16];
        const string password = "Test-password-1";

        HttpResponseMessage response = await api.CreateClient()
            .PostAsJsonAsync($"{V1}/customers", new { username, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return new Customer(username, password, await api.LoginAsync(username, password));
    }

    public static async Task<AccountSummary> OpenAccountAsync(this Customer customer, string accountType, decimal initialDeposit = 0m)
    {
        HttpResponseMessage response = await customer.Client.PostAsJsonAsync(
            $"{V1}/accounts", new { ownerUsername = customer.Username, accountType });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AccountSummary account = await response.Content.ReadFromJsonAsync<AccountSummary>();

        if (initialDeposit > 0m)
        {
            Assert.Equal(HttpStatusCode.OK, (await customer.Client.DepositAsync(account.AccountNumber, initialDeposit)).StatusCode);
        }

        return account;
    }

    public static Task<HttpResponseMessage> DepositAsync(this HttpClient client, string accountNumber, decimal amount) =>
        client.PostAsJsonAsync($"{V1}/accounts/{accountNumber}/deposit", new { amount });

    public static Task<HttpResponseMessage> WithdrawAsync(this HttpClient client, string accountNumber, decimal amount) =>
        client.PostAsJsonAsync($"{V1}/accounts/{accountNumber}/withdraw", new { amount });

    public static Task<HttpResponseMessage> TransferAsync(this HttpClient client, string from, string to, decimal amount) =>
        client.PostAsJsonAsync($"{V1}/accounts/transfer", new { fromAccountNumber = from, toAccountNumber = to, amount });

    public static Task<HttpResponseMessage> SetAccountStatusAsync(this HttpClient client, string accountNumber, string status, string reason = "Integration test") =>
        client.PostAsJsonAsync($"{V1}/accounts/{accountNumber}/status", new { status, reason });

    public static Task<HttpResponseMessage> SetCustomerStatusAsync(this HttpClient client, string username, string status, string reason = "Integration test") =>
        client.PostAsJsonAsync($"{V1}/customers/{username}/status", new { status, reason });

    public static async Task<AccountSummary> GetAccountAsync(this Customer customer, string accountNumber)
    {
        List<AccountSummary> accounts = await customer.Client
            .GetFromJsonAsync<List<AccountSummary>>($"{V1}/accounts/customer/{customer.Username}");
        return Assert.Single(accounts, a => a.AccountNumber == accountNumber);
    }

    public static async Task<List<TransactionSummary>> GetHistoryAsync(this HttpClient client, string accountNumber) =>
        await client.GetFromJsonAsync<List<TransactionSummary>>($"{V1}/accounts/{accountNumber}/transactions?limit=500");
}

public record Customer(string Username, string Password, HttpClient Client);
