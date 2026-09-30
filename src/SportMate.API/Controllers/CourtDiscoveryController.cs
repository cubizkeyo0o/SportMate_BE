using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportMate.Application.Courts;

namespace SportMate.API.Controllers;

/// <summary>
/// Read-only court discovery for any authenticated user. Only Active courts are visible; inactive or unknown
/// courts are 404. Court management reads stay under <c>/api/v1/courts</c> with their own permissions.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/discovery/courts")]
[Produces("application/json")]
public sealed class CourtDiscoveryController(CourtDiscoveryService discoveryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DiscoveredCourtResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DiscoveredCourtResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await discoveryService.ListAsync(cancellationToken));

    [HttpGet("{courtId:guid}")]
    [ProducesResponseType<DiscoveredCourtResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscoveredCourtResponse>> Get(Guid courtId, CancellationToken cancellationToken) =>
        await discoveryService.GetAsync(courtId, cancellationToken);

    [HttpGet("{courtId:guid}/sports")]
    [ProducesResponseType<IReadOnlyList<DiscoveredCourtSportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DiscoveredCourtSportResponse>>> ListSports(
        Guid courtId,
        CancellationToken cancellationToken) =>
        Ok(await discoveryService.ListSportsAsync(courtId, cancellationToken));

    [HttpGet("{courtId:guid}/operating-hours")]
    [ProducesResponseType<IReadOnlyList<DiscoveredOperatingHourResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DiscoveredOperatingHourResponse>>> ListOperatingHours(
        Guid courtId,
        CancellationToken cancellationToken) =>
        Ok(await discoveryService.ListOperatingHoursAsync(courtId, cancellationToken));
}
