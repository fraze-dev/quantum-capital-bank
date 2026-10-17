using System.Security.Claims;
using BankApi.Models;

namespace BankApi.Services;

public static class ClaimsPrincipalExtensions
{
    // The username the caller logged in as, taken from their JWT.
    public static string Username(this ClaimsPrincipal user) => user.Identity?.Name;

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);

    // Admins and auditors can see every customer's data.
    public static bool IsStaff(this ClaimsPrincipal user) => user.IsAdmin() || user.IsInRole(Roles.Auditor);

    // Viewing: customers see only their own data; staff see anyone's.
    public static bool CanViewCustomer(this ClaimsPrincipal user, string username) =>
        user.IsStaff() || user.Username() == username;

    // Changing: customers act only for themselves; of staff, only admins act for
    // others (auditors are read-only).
    public static bool CanActForCustomer(this ClaimsPrincipal user, string username) =>
        user.IsAdmin() || user.Username() == username;
}
