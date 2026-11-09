using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UclAggregator.Options;

namespace UclAggregator.Auth;

public class TokenService(IOptions<JwtOptions> jwt, IOptions<DevUserOptions> devUser)
{
    public static SymmetricSecurityKey CreateKey(JwtOptions o) => new(Encoding.UTF8.GetBytes(o.SigningKey));

    public bool ValidateCredentials(string? username, string? password)
    {
        var dev = devUser.Value;
        if (string.IsNullOrWhiteSpace(dev.Username) || string.IsNullOrWhiteSpace(dev.Password)
            || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        return username == dev.Username && password == dev.Password;
    }

    public LoginResponse CreateToken(string username)
    {
        var o = jwt.Value;
        var expires = DateTime.UtcNow.AddMinutes(o.ExpiryMinutes);
        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.Name, username)],
            expires: expires,
            signingCredentials: new SigningCredentials(CreateKey(o), SecurityAlgorithms.HmacSha256));
        return new LoginResponse { AccessToken = new JwtSecurityTokenHandler().WriteToken(token), ExpiresAtUtc = expires };
    }
}
