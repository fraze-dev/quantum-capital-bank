using BankApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BankApi.Tests.Unit;

public class TokenServiceTests
{
    private static readonly string Key = new('k', 64);

    private static IConfiguration ConfigWithKey(string key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["Jwt:SigningKey"] = key })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short-for-hs256")]
    public void LoadSigningKey_RejectsMissingOrShortKeys(string key)
    {
        Assert.Throws<InvalidOperationException>(() => TokenService.LoadSigningKey(ConfigWithKey(key)));
    }

    [Fact]
    public async Task CreateToken_ProducesAValidTokenWithNameRoleAndOneHourExpiry()
    {
        SymmetricSecurityKey signingKey = TokenService.LoadSigningKey(ConfigWithKey(Key));
        var service = new TokenService(signingKey);

        (string token, DateTime expiresAt) = service.CreateToken("jsmith", "Customer");

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = TokenService.Issuer,
            ValidAudience = TokenService.Audience,
            IssuerSigningKey = signingKey
        });

        Assert.True(result.IsValid);
        Assert.Equal("jsmith", result.Claims[TokenService.NameClaim]);
        Assert.Equal("Customer", result.Claims[TokenService.RoleClaim]);
        Assert.InRange(expiresAt - DateTime.UtcNow, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61));
    }

    [Fact]
    public async Task Token_SignedWithADifferentKey_IsRejected()
    {
        var service = new TokenService(TokenService.LoadSigningKey(ConfigWithKey(Key)));
        (string token, _) = service.CreateToken("jsmith", "Admin");

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = TokenService.Issuer,
            ValidAudience = TokenService.Audience,
            IssuerSigningKey = TokenService.LoadSigningKey(ConfigWithKey(new string('x', 64)))
        });

        Assert.False(result.IsValid);
    }
}
