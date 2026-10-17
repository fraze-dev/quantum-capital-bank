using BankApi.Models;
using MongoDB.Driver;

namespace BankApi.Repositories;

public interface IUserRepository
{
    Task<UserDocument> GetByUsernameAsync(string username);
    Task<List<UserDocument>> GetByRoleAsync(string role);
    Task<bool> TryCreateAsync(UserDocument user);
    Task UpdatePasswordHashAsync(string username, string passwordHash);
    Task<UserDocument> UpdateStatusAsync(IClientSessionHandle session, string username, string expectedStatus, StatusChange change);
}
