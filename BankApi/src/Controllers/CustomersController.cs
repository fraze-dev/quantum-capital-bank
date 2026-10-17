using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerRepository _customerRepository;

    public CustomersController(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    // GET /api/v1/customers
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetCustomers()
    {
        List<CustomerDocument> customers = await _customerRepository.GetAllAsync();

        List<CustomerSummary> result = customers
            .Select(c => new CustomerSummary
            {
                Username = c.Username,
                AccountNumbers = c.AccountNumbers
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

        CustomerDocument customer = await _customerRepository.GetByUsernameAsync(username);

        if (customer == null)
        {
            return NotFound($"No customer found with username '{username}'.");
        }

        var summary = new CustomerSummary
        {
            Username = customer.Username,
            AccountNumbers = customer.AccountNumbers
        };

        return Ok(summary);
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

        CustomerDocument existing = await _customerRepository.GetByUsernameAsync(request.Username);
        if (existing != null)
        {
            return Conflict($"A user with username '{request.Username}' already exists.");
        }

        var customer = new CustomerDocument
        {
            Username = request.Username,
            Password = PasswordUtil.Hash(request.Password),
            AccountNumbers = new List<string>()
        };

        await _customerRepository.CreateAsync(customer);

        var summary = new CustomerSummary
        {
            Username = customer.Username,
            AccountNumbers = customer.AccountNumbers
        };

        return CreatedAtAction(nameof(GetCustomerByUsername), new { username = customer.Username }, summary);
    }
}

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