using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MongoDb;

namespace BankApi.Tests.Integration;

// Starts a throwaway MongoDB (a single-node replica set, since the app uses
// transactions) in Docker, then runs the real API in memory against it. The
// app's own startup migrations seed the demo data (jsmith, agarcia) and the
// admin login. One instance is shared by every integration test; tests stay
// independent by creating their own customers (see TestApi).
public sealed class BankApiFixture : IAsyncLifetime
{
    public const string AdminPassword = "Integration-Test-Admin-1";

    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:8.0")
        .WithReplicaSet()
        .Build();

    private WebApplicationFactory<Program> _factory;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        // Environment variables rather than factory configuration, because the
        // app reads some settings (e.g. the signing key) while it's being built.
        // They also override any developer user-secrets.
        Environment.SetEnvironmentVariable("MongoDb__ConnectionString", _mongo.GetConnectionString());
        Environment.SetEnvironmentVariable("MongoDb__DatabaseName", "BankApiTests");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", new string('t', 64));
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

        // Start the app now so its startup migrations run before any test.
        _ = _factory.Server;
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        await _mongo.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<BankApiFixture>
{
    public const string Name = "api";
}
