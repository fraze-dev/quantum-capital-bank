using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly AccountService _accountService;

    public AccountsController(
        IAccountRepository accountRepository,
        ICustomerRepository customerRepository,
        ITransactionRepository transactionRepository,
        AccountService accountService)
    {
        _accountRepository = accountRepository;
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
        _accountService = accountService;
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

    // GET /api/v1/accounts/{accountNumber}/transactions?limit=20
    [HttpGet("{accountNumber}/transactions")]
    public async Task<IActionResult> GetAccountTransactions(string accountNumber, [FromQuery] int? limit)
    {
        AccountDocument account = await _accountRepository.GetByAccountNumberAsync(accountNumber);
        if (account == null)
        {
            return NotFound($"No account found with number '{accountNumber}'.");
        }

        if (!User.IsSelfOrAdmin(account.OwnerUsername))
        {
            return StatusCode(403, "You do not own this account.");
        }

        List<TransactionDocument> transactions = await _transactionRepository.FindAsync(new TransactionQuery
        {
            AccountNumber = accountNumber,
            Limit = TransactionSummary.ClampLimit(limit)
        });

        return Ok(transactions.Select(TransactionSummary.From).ToList());
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
        AccountDocument account = await _accountService.OpenAccountAsync(request.OwnerUsername, accountType, prefix);

        return CreatedAtAction(nameof(GetAccountsByCustomer), new { username = request.OwnerUsername }, ToSummary(account));
    }

    // POST /api/v1/accounts/{accountNumber}/deposit
    [HttpPost("{accountNumber}/deposit")]
    public async Task<IActionResult> Deposit(string accountNumber, [FromBody] AmountRequest request)
    {
        string amountError = ValidateAmount(request?.Amount, "Deposit");
        if (amountError != null)
        {
            return BadRequest(amountError);
        }

        (AccountDocument account, IActionResult error) = await LoadOwnedAccount(accountNumber);
        if (error != null)
        {
            return error;
        }

        AccountDocument updated = await _accountService.DepositAsync(account, request.Amount, User.Username());
        return Ok(ToSummary(updated));
    }

    // POST /api/v1/accounts/{accountNumber}/withdraw
    [HttpPost("{accountNumber}/withdraw")]
    public async Task<IActionResult> Withdraw(string accountNumber, [FromBody] AmountRequest request)
    {
        string amountError = ValidateAmount(request?.Amount, "Withdrawal");
        if (amountError != null)
        {
            return BadRequest(amountError);
        }

        (AccountDocument account, IActionResult error) = await LoadOwnedAccount(accountNumber);
        if (error != null)
        {
            return error;
        }

        AccountDocument updated = await _accountService.WithdrawAsync(account, request.Amount, User.Username());
        if (updated == null)
        {
            return BadRequest(InsufficientFundsMessage(account));
        }

        return Ok(ToSummary(updated));
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

        string amountError = ValidateAmount(request.Amount, "Transfer");
        if (amountError != null)
        {
            return BadRequest(amountError);
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

        TransferResult result = await _accountService.TransferAsync(fromAccount, toAccount, request.Amount, username);
        if (result == null)
        {
            return BadRequest(InsufficientFundsMessage(fromAccount));
        }

        return Ok(new TransferResponse { From = ToSummary(result.From), To = ToSummary(result.To) });
    }

    private async Task<(AccountDocument Account, IActionResult Error)> LoadOwnedAccount(string accountNumber)
    {
        AccountDocument account = await _accountRepository.GetByAccountNumberAsync(accountNumber);
        if (account == null)
        {
            return (null, NotFound($"No account found with number '{accountNumber}'."));
        }

        if (account.OwnerUsername != User.Username())
        {
            return (null, StatusCode(403, "You do not own this account."));
        }

        return (account, null);
    }

    private static string ValidateAmount(decimal? amount, string action)
    {
        if (amount == null || amount <= 0)
        {
            return $"{action} amount must be positive.";
        }

        if (decimal.Round(amount.Value, 2) != amount.Value)
        {
            return "Amounts can have at most two decimal places.";
        }

        return null;
    }

    private static string InsufficientFundsMessage(AccountDocument account)
    {
        return account.AccountType == "Checking"
            ? $"Insufficient funds. Overdraft limit is {AccountService.CheckingOverdraftLimit:C}."
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
