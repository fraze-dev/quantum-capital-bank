using BankApi.Dtos;
using BankApi.Models;
using BankApi.Repositories;
using BankApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly UserService _userService;

    public CustomersController(IUserRepository userRepository, IAccountRepository accountRepository, UserService userService)
    {
        _userRepository = userRepository;
        _accountRepository = accountRepository;
        _userService = userService;
    }

    // GET /api/v1/customers
    [Authorize(Roles = Roles.Staff)]
    [HttpGet]
    public async Task<IActionResult> GetCustomers()
    {
        List<UserDocument> customers = await _userRepository.GetByRoleAsync(Roles.Customer);
        ILookup<string, string> accountNumbersByOwner = (await _accountRepository.GetAllAsync(null))
            .OrderBy(a => a.AccountNumber)
            .ToLookup(a => a.OwnerUsername, a => a.AccountNumber);

        List<CustomerSummary> result = customers
            .Select(c => new CustomerSummary
            {
                Username = c.Username,
                Status = c.Status,
                AccountNumbers = accountNumbersByOwner[c.Username].ToList()
            })
            .ToList();

        return Ok(result);
    }

    // GET /api/v1/customers/{username}
    [HttpGet("{username}")]
    public async Task<IActionResult> GetCustomerByUsername(string username)
    {
        if (!User.CanViewCustomer(username))
        {
            return StatusCode(403, "You can only view your own customer record.");
        }

        UserDocument customer = await _userRepository.GetByUsernameAsync(username);
        if (customer == null || customer.Role != Roles.Customer)
        {
            return NotFound($"No customer found with username '{username}'.");
        }

        List<AccountDocument> accounts = await _accountRepository.GetByCustomerUsernameAsync(username);
        return Ok(new CustomerSummary
        {
            Username = customer.Username,
            Status = customer.Status,
            AccountNumbers = accounts.Select(a => a.AccountNumber).Order().ToList()
        });
    }

    // POST /api/v1/customers/{username}/status  { "status": "Disabled", "reason": "..." }
    // Disable or re-enable a customer's login. Recorded in the audit log.
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{username}/status")]
    public async Task<IActionResult> ChangeStatus(string username, [FromBody] StatusChangeRequest request)
    {
        string requestError = ControllerExtensions.ValidateStatusChange(request);
        if (requestError != null)
        {
            return BadRequest(requestError);
        }

        ServiceResult<UserDocument> result = await _userService.ChangeCustomerStatusAsync(
            username, request.Status.Trim(), request.Reason.Trim(), User.Username());
        if (!result.Succeeded)
        {
            return this.ErrorResult(result.Error, result.Message);
        }

        List<AccountDocument> accounts = await _accountRepository.GetByCustomerUsernameAsync(username);
        return Ok(new CustomerSummary
        {
            Username = result.Value.Username,
            Status = result.Value.Status,
            AccountNumbers = accounts.Select(a => a.AccountNumber).Order().ToList()
        });
    }

    // POST /api/v1/customers (registration, so no token yet)
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Username and password are required.");
        }

        UserDocument customer = await _userService.RegisterCustomerAsync(request.Username, request.Password);
        if (customer == null)
        {
            return Conflict($"A user with username '{request.Username}' already exists.");
        }

        var summary = new CustomerSummary { Username = customer.Username, Status = customer.Status, AccountNumbers = new List<string>() };
        return CreatedAtAction(nameof(GetCustomerByUsername), new { username = customer.Username }, summary);
    }
}
