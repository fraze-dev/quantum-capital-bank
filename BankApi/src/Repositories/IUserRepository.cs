using BankApi.Models;

namespace BankApi.Repositories;

public interface IUserRepository
{
    Task<UserDocument> GetByUsernameAsync(string username);
    Task<List<UserDocument>> GetByRoleAsync(string role);
    Task<bool> TryCreateAsync(UserDocument user);
    Task UpdatePasswordHashAsync(string username, string passwordHash);
}
