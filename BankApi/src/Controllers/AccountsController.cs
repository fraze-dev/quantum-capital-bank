using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AccountsController : ControllerBase
{
    private const decimal CheckingOverdraftLimit = 100m;

    private readonly IAccountRepository _accountRepository;
    private readonly ICustomerRepository _customerRepository;

    public AccountsController(IAccountRepository accountRepository, ICustomerRepository customerRepository)
    {
        _accountRepository = accountRepository;
        _customerRepository = customerRepository;
    }

    // GET /api/v1/accounts
    // GET /api/v1/accounts?minBalance=500
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAccounts([FromQuery] decimal? minBalance)
    {
        List<AccountDocument> accounts = await _accountRepository.GetAllAsync(minBalance);
        return Ok(accounts.Select(ToSummary).ToList());
    }

    // GET /api/v1/accounts/customer/{username}
    [HttpGet("customer/{username}")]
    public async Task<IActionResult> GetAccountsByCustomer(string username)
    {
        if (!User.IsSelfOrAdmin(username))
        {
            return StatusCode(403, "You can only view your own accounts.");
        }

        CustomerDocument customer = await _customerRepository.GetByUsernameAsync(username);
        if (customer == null)
        {
            return NotFound($"No customer found with username '{username}'.");
        }

        List<AccountDocument> accounts = await _accountRepository.GetByCustomerUsernameAsync(username);
        return Ok(accounts.Select(ToSummary).ToList());
    }

    // POST /api/v1/accounts
    [HttpPost]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.OwnerUsername) || string.IsNullOrWhiteSpace(request.AccountType))
        {
            return BadRequest("Owner username and account type are required.");
        }

        if (!User.IsSelfOrAdmin(request.OwnerUsername))
        {
            return StatusCode(403, "You can only open accounts for yourself.");
        }

        CustomerDocument customer = await _customerRepository.GetByUsernameAsync(request.OwnerUsername);
        if (customer == null)
        {
            return NotFound($"No customer found with username '{request.OwnerUsername}'.");
        }

        string prefix = request.AccountType.Trim().ToLowerInvariant() switch
        {
            "savings" => "SAV",
            "checking" => "CHK",
            _ => null
        };

        if (prefix == null)
        {
            return BadRequest("Account type must be 'Savings' or 'Checking'.");
        }

        string accountType = prefix == "SAV" ? "Savings" : "Checking";
        string accountNumber = await _accountRepository.GetNextAccountNumberAsync(prefix);

        var account = new AccountDocument
        {
            AccountNumber = accountNumber,
            OwnerUsername = request.OwnerUsername,
            AccountType = accountType,
            Balance = 0m
        };

        await _accountRepository.CreateAsync(account);
        await _customerRepository.AddAccountNumberAsync(request.OwnerUsername, accountNumber);

        return CreatedAtAction(nameof(GetAccountsByCustomer), new { username = request.OwnerUsername }, ToSummary(account));
    }

    // POST /api/v1/accounts/{accountNumber}/deposit
    [HttpPost("{accountNumber}/deposit")]
    public async Task<IActionResult> Deposit(string accountNumber, [FromBody] AmountRequest request)
    {
        if (request == null)
        {
            return BadRequest("Amount is required.");
        }

        (AccountDocument account, IActionResult error) = await LoadOwnedAccount(accountNumber, User.Username());
        if (error != null)
        {
            return error;
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Deposit amount must be positive.");
        }

        decimal newBalance = account.Balance + request.Amount;
        await _accountRepository.UpdateBalanceAsync(accountNumber, newBalance);
        account.Balance = newBalance;

        return Ok(ToSummary(account));
    }

    // POST /api/v1/accounts/{accountNumber}/withdraw
    [HttpPost("{accountNumber}/withdraw")]
    public async Task<IActionResult> Withdraw(string accountNumber, [FromBody] AmountRequest request)
    {
        if (request == null)
        {
            return BadRequest("Amount is required.");
        }

        (AccountDocument account, IActionResult error) = await LoadOwnedAccount(accountNumber, User.Username());
        if (error != null)
        {
            return error;
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Withdrawal amount must be positive.");
        }

        decimal? newBalance = ComputeBalanceAfterWithdrawal(account, request.Amount);
        if (newBalance == null)
        {
            return BadRequest(InsufficientFundsMessage(account));
        }

        await _accountRepository.UpdateBalanceAsync(accountNumber, newBalance.Value);
        account.Balance = newBalance.Value;

        return Ok(ToSummary(account));
    }

    // POST /api/v1/accounts/transfer
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.FromAccountNumber) || string.IsNullOrWhiteSpace(request.ToAccountNumber))
        {
            return BadRequest("fromAccountNumber and toAccountNumber are required.");
        }

        if (request.FromAccountNumber == request.ToAccountNumber)
        {
            return BadRequest("Cannot transfer to the same account.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Transfer amount must be positive.");
        }

        AccountDocument fromAccount = await _accountRepository.GetByAccountNumberAsync(request.FromAccountNumber);
        if (fromAccount == null)
        {
            return NotFound($"No account found with number '{request.FromAccountNumber}'.");
        }

        AccountDocument toAccount = await _accountRepository.GetByAccountNumberAsync(request.ToAccountNumber);
        if (toAccount == null)
        {
            return NotFound($"No account found with number '{request.ToAccountNumber}'.");
        }

        string username = User.Username();
        if (fromAccount.OwnerUsername != username || toAccount.OwnerUsername != username)
        {
            return StatusCode(403, "You do not own one or both of these accounts.");
        }

        decimal? newFromBalance = ComputeBalanceAfterWithdrawal(fromAccount, request.Amount);
        if (newFromBalance == null)
        {
            return BadRequest(InsufficientFundsMessage(fromAccount));
        }

        decimal newToBalance = toAccount.Balance + request.Amount;

        await _accountRepository.UpdateBalanceAsync(fromAccount.AccountNumber, newFromBalance.Value);
        await _accountRepository.UpdateBalanceAsync(toAccount.AccountNumber, newToBalance);

        fromAccount.Balance = newFromBalance.Value;
        toAccount.Balance = newToBalance;

        return Ok(new TransferResponse { From = ToSummary(fromAccount), To = ToSummary(toAccount) });
    }

    private async Task<(AccountDocument Account, IActionResult Error)> LoadOwnedAccount(string accountNumber, string username)
    {
        AccountDocument account = await _accountRepository.GetByAccountNumberAsync(accountNumber);
        if (account == null)
        {
            return (null, NotFound($"No account found with number '{accountNumber}'."));
        }

        if (account.OwnerUsername != username)
        {
            return (null, StatusCode(403, "You do not own this account."));
        }

        return (account, null);
    }

    private static decimal? ComputeBalanceAfterWithdrawal(AccountDocument account, decimal amount)
    {
        decimal overdraftLimit = account.AccountType == "Checking" ? CheckingOverdraftLimit : 0m;

        if (amount > account.Balance + overdraftLimit)
        {
            return null;
        }

        return account.Balance - amount;
    }

    private static string InsufficientFundsMessage(AccountDocument account)
    {
        return account.AccountType == "Checking"
            ? $"Insufficient funds. Overdraft limit is {CheckingOverdraftLimit:C}."
            : "Insufficient funds. Savings accounts do not allow overdraft.";
    }

    private static AccountSummary ToSummary(AccountDocument account)
    {
        return new AccountSummary
        {
            AccountNumber = account.AccountNumber,
            OwnerUsername = account.OwnerUsername,
            AccountType = account.AccountType,
            Balance = account.Balance
        };
    }
}

public class AccountSummary
{
    public string AccountNumber { get; set; }
    public string OwnerUsername { get; set; }
    public string AccountType { get; set; }
    public decimal Balance { get; set; }
}

public class CreateAccountRequest
{
    public string OwnerUsername { get; set; }
    public string AccountType { get; set; }
}

public class AmountRequest
{
    public decimal Amount { get; set; }
}

public class TransferRequest
{
    public string FromAccountNumber { get; set; }
    public string ToAccountNumber { get; set; }
    public decimal Amount { get; set; }
}

public class TransferResponse
{
    public AccountSummary From { get; set; }
    public AccountSummary To { get; set; }
}
