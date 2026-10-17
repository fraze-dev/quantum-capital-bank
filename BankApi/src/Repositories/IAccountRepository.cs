using BankApi.Models;
using MongoDB.Driver;

namespace BankApi.Repositories;

public interface IAccountRepository
{
    Task<List<AccountDocument>> GetAllAsync(decimal? minBalance);
    Task<List<AccountDocument>> GetByCustomerUsernameAsync(string username);
    Task<AccountDocument> GetByAccountNumberAsync(string accountNumber);
    Task CreateAsync(AccountDocument account);
    Task<AccountDocument> ApplyBalanceChangeAsync(IClientSessionHandle session, string accountNumber, decimal delta, decimal? requiredBalance);
    Task<AccountDocument> UpdateStatusAsync(IClientSessionHandle session, string accountNumber, string expectedStatus, StatusChange change, bool requireZeroBalance);
    Task<string> GetNextAccountNumberAsync(string prefix);
}

public record StatusChange(string NewStatus, string Reason, string ChangedBy, DateTime ChangedAt);
