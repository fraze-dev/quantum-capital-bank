using BankApi.Models;
using BankApi.Repositories;
using MongoDB.Bson;

namespace BankApi.Services;

// Logins for customers and admins, all stored in the "users" collection.
public class UserService
{
    public const string AdminUsername = "admin";
    private const int MinAdminPasswordLength = 8;

    private readonly IUserRepository _users;
    private readonly IAuditRepository _audit;
    private readonly MongoTransactions _transactions;

    public UserService(IUserRepository users, IAuditRepository audit, MongoTransactions transactions)
    {
        _users = users;
        _audit = audit;
        _transactions = transactions;
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

    // Admin action: disable or re-enable a customer's login, recorded in the
    // audit log. Their accounts and history are untouched.
    public async Task<ServiceResult<UserDocument>> ChangeCustomerStatusAsync(
        string username, string newStatus, string reason, string performedBy)
    {
        UserDocument customer = await _users.GetByUsernameAsync(username);
        if (customer == null || customer.Role != Roles.Customer)
        {
            return ServiceResult<UserDocument>.Fail(ServiceError.NotFound, $"No customer found with username '{username}'.");
        }

        string action = newStatus switch
        {
            UserStatuses.Disabled => AuditActions.DisableCustomer,
            UserStatuses.Active => AuditActions.EnableCustomer,
            _ => null
        };
        if (action == null)
        {
            return ServiceResult<UserDocument>.Fail(ServiceError.Invalid, $"Status must be one of: {string.Join(", ", UserStatuses.All)}.");
        }

        if (customer.Status == newStatus)
        {
            return ServiceResult<UserDocument>.Fail(ServiceError.Invalid, $"Customer {username} is already {newStatus.ToLowerInvariant()}.");
        }

        return await _transactions.RunAsync(async session =>
        {
            // Timestamped inside the transaction, as for account status changes.
            var change = new StatusChange(newStatus, reason, performedBy, DateTime.UtcNow);
            UserDocument updated = await _users.UpdateStatusAsync(session, username, customer.Status, change)
                ?? throw new TransactionAbortedException(ServiceError.Conflict,
                    $"Customer {username} changed while this was being applied. Reload and try again.");

            await _audit.InsertAsync(session, new AuditEntryDocument
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Timestamp = change.ChangedAt,
                PerformedBy = performedBy,
                Action = action,
                TargetType = AuditTargetTypes.Customer,
                TargetId = username,
                FromStatus = customer.Status,
                ToStatus = newStatus,
                Reason = reason
            });
            return updated;
        });
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
