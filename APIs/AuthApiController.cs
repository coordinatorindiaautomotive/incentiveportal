using IncentivePortal.DTOs;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncentivePortal.APIs;

[ApiController]
[Route("api/auth")]
[IgnoreAntiforgeryToken]
public sealed class AuthApiController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.LoginApiAsync(request, cancellationToken);
        return response.Succeeded ? Ok(response) : Unauthorized(response);
    }
}
