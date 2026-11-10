using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UclAggregator.Auth;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace UclAggregator.Tests;

public class AuthTests
{
    private const string Key = "unit-test-signing-key-0123456789-abcdef";

    private static TokenService Tokens(string user = "dev", string password = "pw", int expiryMinutes = 30) =>
        new(MsOptions.Create(new JwtOptions { SigningKey = Key, ExpiryMinutes = expiryMinutes }),
            MsOptions.Create(new DevUserOptions { Username = user, Password = password }));

    [Theory]
    [InlineData("dev", "pw", true)]
    [InlineData("dev", "wrong", false)]
    [InlineData("other", "pw", false)]
    [InlineData("", "pw", false)]
    [InlineData("dev", null, false)]
    public void ValidateCredentials_matches_configured_user(string? user, string? password, bool expected) =>
        Assert.Equal(expected, Tokens().ValidateCredentials(user, password));

    [Fact]
    public void ValidateCredentials_rejects_everything_when_dev_user_not_configured() =>
        Assert.False(Tokens(user: "", password: "").ValidateCredentials("", ""));

    [Fact]
    public void CreateToken_contains_name_claim_and_expiry()
    {
        var before = DateTime.UtcNow;
        var response = Tokens(expiryMinutes: 15).CreateToken("dev");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Equal("dev", jwt.Claims.First(c => c.Type == ClaimTypes.Name || c.Type == "unique_name" || c.Type == "name").Value);
        Assert.InRange(response.ExpiresAtUtc, before.AddMinutes(15), DateTime.UtcNow.AddMinutes(15));
    }

    [Fact]
    public void AddJwtAuth_throws_for_short_signing_key()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "too-short" })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddJwtAuth(config));
        Assert.Contains("Jwt:SigningKey", ex.Message);
    }
}
