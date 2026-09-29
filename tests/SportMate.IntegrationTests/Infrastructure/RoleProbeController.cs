using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportMate.Application.Auth;

namespace SportMate.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only endpoints exercising role authorization; registered only by <see cref="SportMateApiFactory"/>.
/// No production endpoint needs a role yet.
/// </summary>
[ApiController]
[Route("api/v1/test/roles")]
public sealed class RoleProbeController : ControllerBase
{
    public const string AdminPath = "/api/v1/test/roles/admin";
    public const string CourtOwnerPath = "/api/v1/test/roles/court-owner";
    public const string AuthenticatedPath = "/api/v1/test/roles/authenticated";

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("admin")]
    public IActionResult Admin() => Ok();

    [Authorize(Roles = Roles.CourtOwner)]
    [HttpGet("court-owner")]
    public IActionResult CourtOwner() => Ok();

    [Authorize]
    [HttpGet("authenticated")]
    public IActionResult Authenticated() => Ok(new { role = User.FindFirst(Roles.ClaimType)?.Value });
}
