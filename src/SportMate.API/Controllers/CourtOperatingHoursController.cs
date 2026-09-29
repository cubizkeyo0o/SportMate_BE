using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportMate.API.Authentication;
using SportMate.Application.Auth;
using SportMate.Application.Courts;

namespace SportMate.API.Controllers;

/// <summary>
/// A court's recurring weekly operating hours. Per-court ownership is checked by
/// <see cref="CourtOperatingHourService"/>.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Admin + "," + Roles.CourtOwner)]
[Route("api/v1/courts/{courtId:guid}/operating-hours")]
[Produces("application/json")]
public sealed class CourtOperatingHoursController(CourtOperatingHourService operatingHourService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OperatingHourResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OperatingHourResponse>>> List(
        Guid courtId,
        CancellationToken cancellationToken) =>
        Ok(await operatingHourService.ListAsync(courtId, User.GetUserId(), User.GetUserRole(), cancellationToken));

    [HttpPost]
    [ProducesResponseType<OperatingHourResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(
        Guid courtId,
        OperatingHourRequest request,
        CancellationToken cancellationToken)
    {
        var hour = await operatingHourService.AddAsync(
            courtId, User.GetUserId(), User.GetUserRole(), request, cancellationToken);
        return Created($"/api/v1/courts/{courtId}/operating-hours/{hour.Id}", hour);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<OperatingHourResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OperatingHourResponse>> Update(
        Guid courtId,
        Guid id,
        OperatingHourRequest request,
        CancellationToken cancellationToken) =>
        await operatingHourService.UpdateAsync(
            courtId, id, User.GetUserId(), User.GetUserRole(), request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid courtId, Guid id, CancellationToken cancellationToken)
    {
        await operatingHourService.DeleteAsync(courtId, id, User.GetUserId(), User.GetUserRole(), cancellationToken);
        return NoContent();
    }
}
