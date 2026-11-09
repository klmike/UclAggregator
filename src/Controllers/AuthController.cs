using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UclAggregator.Auth;
using UclAggregator.Errors;

namespace UclAggregator.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
public class AuthController(TokenService tokens) : ApiControllerBase
{
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        if (!tokens.ValidateCredentials(request.Username, request.Password))
        {
            var failed = new LoginResponse();
            failed.AddError(ErrorCode.InvalidCredentials, "Invalid username or password.");
            return Respond(failed);
        }
        return Respond(tokens.CreateToken(request.Username));
    }
}
