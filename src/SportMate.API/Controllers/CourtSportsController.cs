using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportMate.API.Authentication;
using SportMate.Application.Auth;
using SportMate.Application.Courts;

namespace SportMate.API.Controllers;

/// <summary>
/// The sports a court offers. Per-court ownership is checked by <see cref="CourtSportService"/>.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Admin + "," + Roles.CourtOwner)]
[Route("api/v1/courts/{courtId:guid}/sports")]
[Produces("application/json")]
public sealed class CourtSportsController(CourtSportService courtSportService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CourtSportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CourtSportResponse>>> List(
        Guid courtId,
        CancellationToken cancellationToken) =>
        Ok(await courtSportService.ListAsync(courtId, User.GetUserId(), User.GetUserRole(), cancellationToken));

    [HttpPost]
    [ProducesResponseType<CourtSportResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(
        Guid courtId,
        AddCourtSportRequest request,
        CancellationToken cancellationToken)
    {
        var courtSport = await courtSportService.AddAsync(
            courtId, User.GetUserId(), User.GetUserRole(), request, cancellationToken);
        return Created($"/api/v1/courts/{courtId}/sports/{courtSport.Sport.Id}", courtSport);
    }

    [HttpPut("{sportId:guid}")]
    [ProducesResponseType<CourtSportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtSportResponse>> Update(
        Guid courtId,
        Guid sportId,
        UpdateCourtSportRequest request,
        CancellationToken cancellationToken) =>
        await courtSportService.UpdateAsync(
            courtId, sportId, User.GetUserId(), User.GetUserRole(), request, cancellationToken);

    /// <summary>
    /// Deactivates the court's sport (the record is kept). Idempotent: always 204 for a manageable court.
    /// </summary>
    [HttpDelete("{sportId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid courtId, Guid sportId, CancellationToken cancellationToken)
    {
        await courtSportService.DeactivateAsync(courtId, sportId, User.GetUserId(), User.GetUserRole(), cancellationToken);
        return NoContent();
    }
}
