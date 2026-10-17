using BankApi.Models;
using BankApi.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace BankApi.Services;

// Hooks into JWT validation so a token stops working as soon as its user is
// disabled, instead of staying valid until it expires. Costs one user lookup
// per authenticated request.
public static class JwtEvents
{
    private const string DisabledMessage = "Your account has been disabled.";

    public static JwtBearerEvents Create() => new()
    {
        OnTokenValidated = async context =>
        {
            string username = context.Principal?.Identity?.Name;
            var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            UserDocument user = username == null ? null : await users.GetByUsernameAsync(username);

            if (user == null)
            {
                context.Fail("User no longer exists.");
            }
            else if (user.Status == UserStatuses.Disabled)
            {
                context.Fail(new UserDisabledException());
            }
        },

        // Say why the request was rejected when it's because of a disabled
        // account, so the app can show that instead of "session expired".
        OnChallenge = async context =>
        {
            if (context.AuthenticateFailure is UserDisabledException)
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync(DisabledMessage);
            }
        }
    };

    private class UserDisabledException : Exception
    {
        public UserDisabledException() : base(DisabledMessage)
        {
        }
    }
}
