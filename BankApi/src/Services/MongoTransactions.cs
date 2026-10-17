using MongoDB.Driver;

namespace BankApi.Services;

// Runs a unit of work in a MongoDB transaction: everything it writes is
// committed together or not at all.
public class MongoTransactions
{
    private readonly IMongoClient _client;

    public MongoTransactions(IMongoClient client)
    {
        _client = client;
    }

    // WithTransactionAsync commits when the callback returns and retries it
    // automatically on transient errors such as two transactions touching the
    // same document at once. To undo work partway through, throw
    // TransactionAbortedException: it rolls everything back and comes out of
    // this method as a failed ServiceResult.
    public async Task<ServiceResult<T>> RunAsync<T>(Func<IClientSessionHandle, Task<T>> work)
    {
        using IClientSessionHandle session = await _client.StartSessionAsync();
        try
        {
            T value = await session.WithTransactionAsync((s, _) => work(s));
            return ServiceResult<T>.Ok(value);
        }
        catch (TransactionAbortedException ex)
        {
            return ServiceResult<T>.Fail(ex.Error, ex.Message);
        }
    }
}

public class TransactionAbortedException : Exception
{
    public TransactionAbortedException(ServiceError error, string message) : base(message)
    {
        Error = error;
    }

    public ServiceError Error { get; }
}
