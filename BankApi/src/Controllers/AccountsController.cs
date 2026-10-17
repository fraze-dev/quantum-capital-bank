using BankApi.Dtos;
using BankApi.Models;
using BankApi.Repositories;
using BankApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly AccountService _accountService;

    public AccountsController(
        IAccountRepository accountRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        AccountService accountService)
    {
        _accountRepository = accountRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _accountService = accountService;
    }

    // GET /api/v1/accounts
    // GET /api/v1/accounts?minBalance=500
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> GetAccounts([FromQuery] decimal? minBalance)
    {
        List<AccountDocument> accounts = await _accountRepository.GetAllAsync(minBalance);
        return Ok(accounts.Select(AccountSummary.From).ToList());
    }

    // GET /api/v1/accounts/customer/{username}
    [HttpGet("customer/{username}")]
    public async Task<IActionResult> GetAccountsByCustomer(string username)
    {
        if (!User.IsSelfOrAdmin(username))
        {
            return StatusCode(403, "You can only view your own accounts.");
        }

        if (!await IsCustomerAsync(username))
        {
            return NotFound($"No customer found with username '{username}'.");
        }

        List<AccountDocument> accounts = await _accountRepository.GetByCustomerUsernameAsync(username);
        return Ok(accounts.Select(AccountSummary.From).ToList());
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

        if (!await IsCustomerAsync(request.OwnerUsername))
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

        return CreatedAtAction(nameof(GetAccountsByCustomer), new { username = request.OwnerUsername }, AccountSummary.From(account));
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

        ServiceResult<AccountDocument> result = await _accountService.DepositAsync(account, request.Amount, User.Username());
        return result.Succeeded ? Ok(AccountSummary.From(result.Value)) : this.ErrorResult(result.Error, result.Message);
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

        ServiceResult<AccountDocument> result = await _accountService.WithdrawAsync(account, request.Amount, User.Username());
        return result.Succeeded ? Ok(AccountSummary.From(result.Value)) : this.ErrorResult(result.Error, result.Message);
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

        ServiceResult<TransferResult> result = await _accountService.TransferAsync(fromAccount, toAccount, request.Amount, username);
        if (!result.Succeeded)
        {
            return this.ErrorResult(result.Error, result.Message);
        }

        return Ok(new TransferResponse { From = AccountSummary.From(result.Value.From), To = AccountSummary.From(result.Value.To) });
    }

    // POST /api/v1/accounts/{accountNumber}/status  { "status": "Frozen", "reason": "..." }
    // Freeze, unfreeze or close an account. Recorded in the audit log.
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{accountNumber}/status")]
    public async Task<IActionResult> ChangeStatus(string accountNumber, [FromBody] StatusChangeRequest request)
    {
        string requestError = ControllerExtensions.ValidateStatusChange(request);
        if (requestError != null)
        {
            return BadRequest(requestError);
        }

        ServiceResult<AccountDocument> result = await _accountService.ChangeStatusAsync(
            accountNumber, request.Status.Trim(), request.Reason.Trim(), User.Username());
        return result.Succeeded ? Ok(AccountSummary.From(result.Value)) : this.ErrorResult(result.Error, result.Message);
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

    private async Task<bool> IsCustomerAsync(string username)
    {
        UserDocument user = await _userRepository.GetByUsernameAsync(username);
        return user?.Role == Roles.Customer;
    }
}
