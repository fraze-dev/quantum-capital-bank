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
    [Authorize(Roles = Roles.Admin)]
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
                AccountNumbers = accountNumbersByOwner[c.Username].ToList()
            })
            .ToList();

        return Ok(result);
    }

    // GET /api/v1/customers/{username}
    [HttpGet("{username}")]
    public async Task<IActionResult> GetCustomerByUsername(string username)
    {
        if (!User.IsSelfOrAdmin(username))
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

        var summary = new CustomerSummary { Username = customer.Username, AccountNumbers = new List<string>() };
        return CreatedAtAction(nameof(GetCustomerByUsername), new { username = customer.Username }, summary);
    }
}
