using BankApi.Models;
using BankApi.Repositories;

namespace BankApi.Services;

// Logins for customers and admins, all stored in the "users" collection.
public class UserService
{
    public const string AdminUsername = "admin";
    private const int MinAdminPasswordLength = 8;

    private readonly IUserRepository _users;

    public UserService(IUserRepository users)
    {
        _users = users;
    }

    // Returns the user if the username exists and the password matches, else null.
    public async Task<UserDocument> AuthenticateAsync(string username, string password)
    {
        UserDocument user = await _users.GetByUsernameAsync(username);
        return user != null && PasswordUtil.Verify(user.PasswordHash, password) ? user : null;
    }

    // Returns the new customer, or null if the username is already taken.
    public async Task<UserDocument> RegisterCustomerAsync(string username, string password)
    {
        var user = new UserDocument
        {
            Username = username,
            PasswordHash = PasswordUtil.Hash(password),
            Role = Roles.Customer,
            CreatedAt = DateTime.UtcNow
        };

        return await _users.TryCreateAsync(user) ? user : null;
    }

    // The admin's password comes from configuration (Seed:AdminPassword:
    // user-secrets locally, a stack parameter in AWS), never from source code.
    // Runs at startup: creates the admin on first run, and if the configured
    // password has since changed, updates the stored hash to match. So
    // rotating it is: change the secret, then redeploy.
    public async Task EnsureAdminAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinAdminPasswordLength)
        {
            throw new InvalidOperationException(
                $"Seed:AdminPassword must be configured and at least {MinAdminPasswordLength} characters long.");
        }

        UserDocument admin = await _users.GetByUsernameAsync(AdminUsername);
        if (admin == null)
        {
            await _users.TryCreateAsync(new UserDocument
            {
                Username = AdminUsername,
                PasswordHash = PasswordUtil.Hash(password),
                Role = Roles.Admin,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        if (admin.Role != Roles.Admin)
        {
            throw new InvalidOperationException($"The username '{AdminUsername}' belongs to a non-admin user.");
        }

        if (!PasswordUtil.Verify(admin.PasswordHash, password))
        {
            await _users.UpdatePasswordHashAsync(AdminUsername, PasswordUtil.Hash(password));
        }
    }
}
