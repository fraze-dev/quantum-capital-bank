using System.Security.Claims;
using BankApi.Models;

namespace BankApi.Services;

public static class ClaimsPrincipalExtensions
{
    // The username the caller logged in as, taken from their JWT.
    public static string Username(this ClaimsPrincipal user) => user.Identity?.Name;

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);

    // Customers may only look at their own data; admins may look at anyone's.
    public static bool IsSelfOrAdmin(this ClaimsPrincipal user, string username) =>
        user.IsAdmin() || user.Username() == username;
}
