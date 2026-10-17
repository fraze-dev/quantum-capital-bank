using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly Bank _bank;
    private readonly ICustomerRepository _customerRepository;
    private readonly TokenService _tokenService;

    public AuthController(Bank bank, ICustomerRepository customerRepository, TokenService tokenService)
    {
        _bank = bank;
        _customerRepository = customerRepository;
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Username and password are required.");
        }

        // Admins and the in-memory seeded customers live in Bank; customers
        // registered through POST /api/v1/customers only exist in MongoDB,
        // so fall back to checking there if Bank doesn't recognize them.
        User user = _bank.Authenticate(request.Username, request.Password);
        if (user != null)
        {
            return Ok(CreateLoginResponse(user.Username, user.GetRole()));
        }

        CustomerDocument customer = await _customerRepository.GetByUsernameAsync(request.Username);
        if (customer != null && PasswordUtil.Verify(customer.Password, request.Password))
        {
            return Ok(CreateLoginResponse(customer.Username, "Customer"));
        }

        return Unauthorized("Invalid username or password.");
    }

    private LoginResponse CreateLoginResponse(string username, string role)
    {
        (string token, DateTime expiresAt) = _tokenService.CreateToken(username, role);
        return new LoginResponse { Username = username, Role = role, Token = token, ExpiresAt = expiresAt };
    }
}

public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}

public class LoginResponse
{
    public string Username { get; set; }
    public string Role { get; set; }
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
}