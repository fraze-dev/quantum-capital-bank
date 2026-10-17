using Amazon.Lambda.AspNetCoreServer.Hosting;
using BankApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddOpenApi();

// AddSingleton ensures ASP.NET Core creates
// it once and reuses that same instance for every request.

builder.Services.AddSingleton<Bank>();

// --- JWT auth ---
// Login hands out a signed token; every other endpoint requires one unless it
// opts out with [AllowAnonymous]. Claims are kept under their short JWT names
// ("name", "role") so User.Identity.Name and [Authorize(Roles = ...)] read them directly.
SymmetricSecurityKey jwtSigningKey = TokenService.LoadSigningKey(builder.Configuration);
builder.Services.AddSingleton(new TokenService(jwtSigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = TokenService.Issuer,
            ValidAudience = TokenService.Audience,
            IssuerSigningKey = jwtSigningKey,
            NameClaimType = TokenService.NameClaim,
            RoleClaimType = TokenService.RoleClaim,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// --- MongoDB wiring ---

builder.Services.AddSingleton<IMongoClient>(_ =>
{
    string connectionString = builder.Configuration["MongoDb:ConnectionString"];
    return new MongoClient(connectionString);
});

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    IMongoClient client = sp.GetRequiredService<IMongoClient>();
    string databaseName = builder.Configuration["MongoDb:DatabaseName"] ?? "BankApiDb";
    return client.GetDatabase(databaseName);
});

builder.Services.AddSingleton<ICustomerRepository, CustomerRepository>();
builder.Services.AddSingleton<IAccountRepository, AccountRepository>();

// Only needed for local dev (Vite on :5173 calling the API on :5167). In AWS the
// site and API share one CloudFront origin, so requests are same-origin.
const string FrontendCorsPolicy = "FrontendCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// --- Lambda wiring ---
// When this app is actually running inside AWS Lambda (behind an API Gateway
// HTTP API), AWS_LAMBDA_FUNCTION_NAME will be set in the environment and this
// switches the app to Lambda's request pipeline instead of Kestrel. Locally,
// that variable doesn't exist, so `dotnet run` behaves exactly as before.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

// Seed the in-memory Bank (for Auth) and MongoDB (for Customers/Accounts)
// once, right after the app is built and before it starts handling requests.
using (var scope = app.Services.CreateScope())
{
    var bank = scope.ServiceProvider.GetRequiredService<Bank>();
    bank.SeedData();

    var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    await MongoSeedData.SeedAsync(database);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// API Gateway already terminates HTTPS before the request ever reaches
// Lambda, so forcing an HTTPS redirect inside the function is unnecessary
// and can misbehave (e.g. turning a POST into a broken redirect). Only
// apply it when running locally under Kestrel.
bool isRunningInLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));
if (!isRunningInLambda)
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();