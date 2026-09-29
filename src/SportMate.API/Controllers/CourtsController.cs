using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportMate.API.Authentication;
using SportMate.Application.Auth;
using SportMate.Application.Courts;

namespace SportMate.API.Controllers;

/// <summary>
/// Court management. Per-court ownership is checked by <see cref="CourtService"/>.
/// </summary>
[ApiController]
[Route("api/v1/courts")]
[Produces("application/json")]
public sealed class CourtsController(CourtService courtService) : ControllerBase
{
    private const string Managers = Roles.Admin + "," + Roles.CourtOwner;

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ProducesResponseType<CourtResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(CreateCourtRequest request, CancellationToken cancellationToken)
    {
        var court = await courtService.CreateAsync(User.GetUserRole(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = court.Id }, court);
    }

    [Authorize(Roles = Managers)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CourtResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        await courtService.GetAsync(id, User.GetUserId(), User.GetUserRole(), cancellationToken);

    [Authorize(Roles = Managers)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<CourtResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> Update(
        Guid id,
        UpdateCourtRequest request,
        CancellationToken cancellationToken) =>
        await courtService.UpdateAsync(id, User.GetUserId(), User.GetUserRole(), request, cancellationToken);
}
