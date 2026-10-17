using MongoDB.Driver;

namespace BankApi;

public class CustomerRepository : ICustomerRepository
{
    private readonly IMongoCollection<CustomerDocument> _customers;

    public CustomerRepository(IMongoDatabase database)
    {
        _customers = database.GetCollection<CustomerDocument>("customers");
    }

    public async Task<List<CustomerDocument>> GetAllAsync()
    {
        return await _customers.Find(FilterDefinition<CustomerDocument>.Empty).ToListAsync();
    }

    public async Task<CustomerDocument> GetByUsernameAsync(string username)
    {
        return await _customers.Find(c => c.Username == username).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(CustomerDocument customer)
    {
        await _customers.InsertOneAsync(customer);
    }

    public async Task AddAccountNumberAsync(IClientSessionHandle session, string username, string accountNumber)
    {
        await _customers.UpdateOneAsync(
            session,
            c => c.Username == username,
            Builders<CustomerDocument>.Update.Push(c => c.AccountNumbers, accountNumber));
    }
}