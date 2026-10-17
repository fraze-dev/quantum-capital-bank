using Microsoft.AspNetCore.Identity;

namespace BankApi.Services;

// Thin wrapper around ASP.NET Core Identity's PasswordHasher<TUser> so the
// app can hash and verify passwords stored on UserDocument without needing
// an Identity "user" object to do it. The underlying algorithm is PBKDF2
// with a random per-password salt — the same approach flagged as the "real"
// answer in the project's own design-defense notes, just using the
// framework's well-tested implementation instead of hand-rolling the crypto.
public static class PasswordUtil
{
    // PasswordHasher<TUser>'s default implementation never actually reads
    // the "user" argument — it only hashes/verifies the password string
    // itself — so a generic object placeholder is fine here.
    private static readonly PasswordHasher<object> Hasher = new PasswordHasher<object>();

    public static string Hash(string password)
    {
        return Hasher.HashPassword(null!, password);
    }

    public static bool Verify(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || providedPassword == null)
        {
            return false;
        }

        try
        {
            PasswordVerificationResult result = Hasher.VerifyHashedPassword(null!, hashedPassword, providedPassword);
            return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            // The stored value isn't a hash this hasher recognizes (e.g. a
            // leftover plaintext password from before hashing existed).
            // Treat that as "doesn't match" rather than crashing the request.
            return false;
        }
    }
}