using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BankApi.Dtos;
using static BankApi.Tests.Integration.TestApi;

namespace BankApi.Tests.Integration;

// Who can call what: logins, tokens and role/ownership rules.
[Collection(ApiCollection.Name)]
public class AuthTests(BankApiFixture api)
{
    [Fact]
    public async Task HealthCheck_IsPublic_AndUnknownPathsAre404()
    {
        HttpClient client = api.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{V1}/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{V1}/no-such-thing")).StatusCode);
    }

    [Fact]
    public async Task SeededDemoCustomer_CanLogIn_AndGetsACustomerToken()
    {
        HttpResponseMessage response = await api.LoginRawAsync("jsmith", "pass123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        LoginResponse login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal("Customer", login.Role);
        Assert.False(string.IsNullOrEmpty(login.Token));
    }

    [Fact]
    public async Task WrongPassword_IsRejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.LoginRawAsync("jsmith", "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.LoginRawAsync("nobody", "pass123")).StatusCode);
    }

    [Fact]
    public async Task Admin_UsesTheConfiguredPassword_NotTheOldHardcodedOne()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.LoginRawAsync("admin", "admin123")).StatusCode);

        HttpResponseMessage response = await api.LoginRawAsync("admin", BankApiFixture.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Admin", (await response.Content.ReadFromJsonAsync<LoginResponse>()).Role);
    }

    [Theory]
    [InlineData("/accounts")]
    [InlineData("/customers")]
    [InlineData("/transactions")]
    [InlineData("/audit")]
    [InlineData("/accounts/customer/jsmith")]
    [InlineData("/customers/jsmith")]
    public async Task ProtectedEndpoints_RequireAToken(string path)
    {
        HttpResponseMessage response = await api.CreateClient().GetAsync(V1 + path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedToken_IsRejected()
    {
        HttpResponseMessage login = await api.LoginRawAsync("jsmith", "pass123");
        string token = (await login.Content.ReadFromJsonAsync<LoginResponse>()).Token;

        HttpClient client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^2] + "xx");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{V1}/accounts/customer/jsmith")).StatusCode);
    }

    [Theory]
    [InlineData("/accounts")]
    [InlineData("/customers")]
    [InlineData("/transactions")]
    [InlineData("/audit")]
    public async Task Customers_CannotUseAdminEndpoints(string path)
    {
        Customer customer = await api.NewCustomerAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Client.GetAsync(V1 + path)).StatusCode);
    }

    [Fact]
    public async Task Customers_CannotSeeOrTouchSomeoneElsesAccounts()
    {
        Customer victim = await api.NewCustomerAsync();
        AccountSummary victimAccount = await victim.OpenAccountAsync("Checking", initialDeposit: 100m);
        Customer attacker = await api.NewCustomerAsync();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await attacker.Client.GetAsync($"{V1}/accounts/customer/{victim.Username}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await attacker.Client.GetAsync($"{V1}/accounts/{victimAccount.AccountNumber}/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await attacker.Client.WithdrawAsync(victimAccount.AccountNumber, 10m)).StatusCode);

        // Claiming to be the victim in the request body doesn't help: the API
        // only trusts the identity in the token.
        HttpResponseMessage spoofed = await attacker.Client.PostAsJsonAsync(
            $"{V1}/accounts/{victimAccount.AccountNumber}/withdraw", new { username = victim.Username, amount = 10m });
        Assert.Equal(HttpStatusCode.Forbidden, spoofed.StatusCode);

        Assert.Equal(100m, (await victim.GetAccountAsync(victimAccount.AccountNumber)).Balance);
    }

    [Fact]
    public async Task Admins_CanSeeEveryone()
    {
        HttpClient admin = await api.AdminAsync();

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{V1}/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{V1}/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{V1}/accounts/customer/jsmith")).StatusCode);
    }

    [Theory]
    [InlineData("jsmith")]
    [InlineData("admin")]
    public async Task Registration_RejectsTakenUsernames(string username)
    {
        HttpResponseMessage response = await api.CreateClient()
            .PostAsJsonAsync($"{V1}/customers", new { username, password = "whatever1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
