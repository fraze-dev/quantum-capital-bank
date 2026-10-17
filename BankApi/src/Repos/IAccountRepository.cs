using MongoDB.Driver;

namespace BankApi;

public interface IAccountRepository
{
    Task<List<AccountDocument>> GetAllAsync(decimal? minBalance);
    Task<List<AccountDocument>> GetByCustomerUsernameAsync(string username);
    Task<AccountDocument> GetByAccountNumberAsync(string accountNumber);
    Task CreateAsync(IClientSessionHandle session, AccountDocument account);
    Task<AccountDocument> ApplyBalanceChangeAsync(IClientSessionHandle session, string accountNumber, decimal delta, decimal? requiredBalance);
    Task<string> GetNextAccountNumberAsync(string prefix);
}
