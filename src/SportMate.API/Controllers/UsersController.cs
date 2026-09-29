using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportMate.API.Authentication;
using SportMate.Application.Users;

namespace SportMate.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
[Produces("application/json")]
public sealed class UsersController(UserService userService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponse>> GetMe(CancellationToken cancellationToken) =>
        await userService.GetCurrentAsync(User.GetUserId(), cancellationToken);

    [HttpPut("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponse>> UpdateMe(
        UpdateProfileRequest request,
        CancellationToken cancellationToken) =>
        await userService.UpdateProfileAsync(User.GetUserId(), request, cancellationToken);
}
