using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportMate.Application.Sports;

namespace SportMate.API.Controllers;

/// <summary>
/// Read-only sport catalog. Sports are seeded; there are no create/update/delete endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/sports")]
[Produces("application/json")]
public sealed class SportsController(SportService sportService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SportResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await sportService.ListActiveAsync(cancellationToken));
}
