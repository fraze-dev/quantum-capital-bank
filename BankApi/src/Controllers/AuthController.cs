using BankApi.Dtos;
using BankApi.Models;
using BankApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserService _userService;
    private readonly TokenService _tokenService;

    public AuthController(UserService userService, TokenService tokenService)
    {
        _userService = userService;
        _tokenService = tokenService;
    }

    // POST /api/v1/auth/login
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Username and password are required.");
        }

        UserDocument user = await _userService.AuthenticateAsync(request.Username, request.Password);
        if (user == null)
        {
            return Unauthorized("Invalid username or password.");
        }

        (string token, DateTime expiresAt) = _tokenService.CreateToken(user.Username, user.Role);
        return Ok(new LoginResponse { Username = user.Username, Role = user.Role, Token = token, ExpiresAt = expiresAt });
    }
}
