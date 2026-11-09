using UclAggregator.Models;

namespace UclAggregator.Auth;

public sealed record LoginRequest(string Username, string Password);

public class LoginResponse : ApiResponse
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}
