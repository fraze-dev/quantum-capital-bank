namespace BankApi.Dtos;

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
