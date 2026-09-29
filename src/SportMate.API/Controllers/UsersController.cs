using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportMate.API.Authentication;
using SportMate.Application.Sports;
using SportMate.Application.Users;

namespace SportMate.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
[Produces("application/json")]
public sealed class UsersController(UserService userService, UserSportProfileService sportProfileService) : ControllerBase
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

    [HttpGet("me/sports")]
    [ProducesResponseType<IReadOnlyList<UserSportProfileResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UserSportProfileResponse>>> GetMySports(
        CancellationToken cancellationToken) =>
        Ok(await sportProfileService.ListAsync(User.GetUserId(), cancellationToken));

    /// <summary>
    /// Creates or updates the caller's profile for the sport. Always 200 with the resulting profile.
    /// </summary>
    [HttpPut("me/sports/{sportId:guid}")]
    [ProducesResponseType<UserSportProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserSportProfileResponse>> SetMySport(
        Guid sportId,
        SetSportProfileRequest request,
        CancellationToken cancellationToken) =>
        await sportProfileService.SetAsync(User.GetUserId(), sportId, request, cancellationToken);

    /// <summary>
    /// Removes the caller's profile for the sport. Idempotent: 204 even if there was no such profile.
    /// </summary>
    [HttpDelete("me/sports/{sportId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveMySport(Guid sportId, CancellationToken cancellationToken)
    {
        await sportProfileService.RemoveAsync(User.GetUserId(), sportId, cancellationToken);
        return NoContent();
    }
}
