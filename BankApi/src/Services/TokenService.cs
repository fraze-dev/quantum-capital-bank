using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BankApi.Services;

// Issues the signed JWT handed out at login. Every later request carries it in
// the Authorization header, and the API reads the caller's username and role
// from it instead of trusting anything the client puts in the request body.
public class TokenService
{
    public const string Issuer = "bankapp";
    public const string Audience = "bankapp";
    public const string NameClaim = "name";
    public const string RoleClaim = "role";

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly SigningCredentials _credentials;

    public TokenService(SymmetricSecurityKey signingKey)
    {
        _credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTime ExpiresAt) CreateToken(string username, string role)
    {
        DateTime expiresAt = DateTime.UtcNow.Add(Lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = expiresAt,
            SigningCredentials = _credentials,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(NameClaim, username),
                new Claim(RoleClaim, role)
            })
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }

    // Reads Jwt:SigningKey (user-secrets locally, an environment variable in
    // Lambda). HS256 needs at least 256 bits, so short keys are rejected.
    public static SymmetricSecurityKey LoadSigningKey(IConfiguration configuration)
    {
        string key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured and at least 32 bytes long.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
