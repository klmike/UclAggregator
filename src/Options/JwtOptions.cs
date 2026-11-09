namespace UclAggregator.Options;

public sealed class JwtOptions
{
    public string SigningKey { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60;
}
