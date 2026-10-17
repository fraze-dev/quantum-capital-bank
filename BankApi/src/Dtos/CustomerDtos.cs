namespace BankApi.Dtos;

public class CustomerSummary
{
    public string Username { get; set; }
    public List<string> AccountNumbers { get; set; }
}

public class CreateCustomerRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}
