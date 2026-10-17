using MongoDB.Driver;

namespace BankApi;

public interface ITransactionRepository
{
    Task InsertAsync(IClientSessionHandle session, IEnumerable<TransactionDocument> transactions);
    Task<List<TransactionDocument>> FindAsync(TransactionQuery query);
}

// All filters are optional. From is inclusive, To is exclusive.
public class TransactionQuery
{
    public string AccountNumber { get; set; }
    public string OwnerUsername { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Limit { get; set; } = 50;
}
