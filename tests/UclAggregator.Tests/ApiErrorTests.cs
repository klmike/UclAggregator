using System.Net;
using System.Text.Json;
using UclAggregator.Errors;
using Xunit;

namespace UclAggregator.Tests;

public class ApiErrorTests
{
    [Fact]
    public void From_maps_known_exceptions()
    {
        Assert.Equal(ErrorCode.TeamNotFound, ApiError.From(new AppException(ErrorCode.TeamNotFound, "gone"), "x").Code);
        Assert.Equal(ErrorCode.ProviderRateLimited, ApiError.From(new HttpRequestException("429", null, HttpStatusCode.TooManyRequests), "News").Code);
        Assert.Equal(ErrorCode.ProviderTimeout, ApiError.From(new TimeoutException(), "News").Code);
        Assert.Equal(ErrorCode.ProviderTimeout, ApiError.From(new TaskCanceledException(), "News").Code);
        Assert.Equal(ErrorCode.ProviderBadResponse, ApiError.From(new JsonException(), "News").Code);
        Assert.Equal(ErrorCode.ProviderUnavailable, ApiError.From(new HttpRequestException("down"), "News").Code);
        Assert.Equal(ErrorCode.Unexpected, ApiError.From(new InvalidOperationException(), "News").Code);
    }

    [Fact]
    public void From_does_not_echo_exception_text()
    {
        var error = ApiError.From(new HttpRequestException("secret-host"), "News provider 'google'");
        Assert.DoesNotContain("secret-host", error.Message);
        Assert.Contains("News provider 'google'", error.Message);
    }

    [Theory]
    [InlineData(ErrorCode.InvalidRequest, 400)]
    [InlineData(ErrorCode.Unauthorized, 401)]
    [InlineData(ErrorCode.TeamNotFound, 404)]
    [InlineData(ErrorCode.ProviderUnavailable, 502)]
    [InlineData(ErrorCode.ProviderRateLimited, 503)]
    [InlineData(ErrorCode.ProviderTimeout, 504)]
    [InlineData(ErrorCode.DataNotAvailable, 500)]
    public void ToStatusCode(ErrorCode code, int status) =>
        Assert.Equal(status, code.ToStatusCode());
}
