using BankApi.Models;
using MongoDB.Driver;

namespace BankApi.Repositories;

public class UserRepository : IUserRepository
{
    public const string CollectionName = "users";

    private readonly IMongoCollection<UserDocument> _users;

    public UserRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<UserDocument>(CollectionName);
    }

    public async Task<UserDocument> GetByUsernameAsync(string username)
    {
        return await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
    }

    public async Task<List<UserDocument>> GetByRoleAsync(string role)
    {
        return await _users.Find(u => u.Role == role).SortBy(u => u.Username).ToListAsync();
    }

    // Usernames are the document id, so the database itself rejects a
    // duplicate. Returns false if the username is already taken.
    public async Task<bool> TryCreateAsync(UserDocument user)
    {
        try
        {
            await _users.InsertOneAsync(user);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task UpdatePasswordHashAsync(string username, string passwordHash)
    {
        await _users.UpdateOneAsync(
            u => u.Username == username,
            Builders<UserDocument>.Update.Set(u => u.PasswordHash, passwordHash));
    }

    // Changes the status only if it is still expectedStatus; returns null if
    // someone else changed it first.
    public async Task<UserDocument> UpdateStatusAsync(
        IClientSessionHandle session, string username, string expectedStatus, StatusChange change)
    {
        return await _users.FindOneAndUpdateAsync(
            session,
            Builders<UserDocument>.Filter.Eq(u => u.Username, username)
                & Builders<UserDocument>.Filter.Eq(u => u.Status, expectedStatus),
            Builders<UserDocument>.Update
                .Set(u => u.Status, change.NewStatus)
                .Set(u => u.StatusReason, change.Reason)
                .Set(u => u.StatusChangedBy, change.ChangedBy)
                .Set(u => u.StatusChangedAt, change.ChangedAt),
            new FindOneAndUpdateOptions<UserDocument> { ReturnDocument = ReturnDocument.After });
    }
}
