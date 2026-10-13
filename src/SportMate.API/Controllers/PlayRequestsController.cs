using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportMate.API.Authentication;
using SportMate.Application.PlayRequests;

namespace SportMate.API.Controllers;

/// <summary>Player discovery and host/participant lifecycle for play requests.</summary>
[ApiController]
[Authorize]
[Route("api/v1/play-requests")]
[Produces("application/json")]
public sealed class PlayRequestsController(IPlayRequestService playRequestService) : ControllerBase
{
    /// <summary>Lists open and full requests that match the caller's sport profile.</summary>
    [HttpGet]
    [ProducesResponseType<PlayRequestPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PlayRequestPageResponse>> Discover(
        [FromQuery(Name = "sport_id")] Guid? sportId,
        [FromQuery(Name = "starts_from_utc")] DateTimeOffset? startsFromUtc,
        [FromQuery(Name = "starts_to_utc")] DateTimeOffset? startsToUtc,
        [FromQuery] decimal? latitude,
        [FromQuery] decimal? longitude,
        [FromQuery(Name = "radius_km")] double? radiusKm,
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await playRequestService.DiscoverAsync(
            User.GetUserId(),
            new PlayRequestQuery(sportId, startsFromUtc, startsToUtc, latitude, longitude, radiusKm, page, pageSize),
            cancellationToken));

    /// <summary>Creates an open request. The authenticated caller is the host.</summary>
    [HttpPost]
    [ProducesResponseType<PlayRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreatePlayRequestRequest request,
        CancellationToken cancellationToken)
    {
        var created = await playRequestService.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Gets a matching request or one owned by the caller.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlayRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayRequestResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        await playRequestService.GetAsync(id, User.GetUserId(), cancellationToken);

    /// <summary>Lists requests created by the caller, including historical statuses.</summary>
    [HttpGet("me")]
    [ProducesResponseType<PlayRequestPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PlayRequestPageResponse>> ListMine(
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await playRequestService.ListMineAsync(User.GetUserId(), page, pageSize, cancellationToken));

    /// <summary>Lists requests for which the caller has a participant record.</summary>
    [HttpGet("me/participations")]
    [ProducesResponseType<PlayRequestPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PlayRequestPageResponse>> ListMyParticipations(
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await playRequestService.ListMyParticipationsAsync(User.GetUserId(), page, pageSize, cancellationToken));

    /// <summary>Updates a host's request. Matching details become immutable after the first join request.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<PlayRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayRequestResponse>> Update(
        Guid id,
        UpdatePlayRequestRequest request,
        CancellationToken cancellationToken) =>
        await playRequestService.UpdateAsync(id, User.GetUserId(), request, cancellationToken);

    /// <summary>Lists participant requests and their current sport skill levels. Host only.</summary>
    [HttpGet("{id:guid}/participants")]
    [ProducesResponseType<IReadOnlyList<PlayRequestParticipantResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PlayRequestParticipantResponse>>> ListParticipants(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await playRequestService.ListParticipantsAsync(id, User.GetUserId(), cancellationToken));

    /// <summary>Requests to join an open matching request. A previous rejected/withdrawn record is reused.</summary>
    [HttpPost("{id:guid}/participants")]
    [ProducesResponseType<PlayRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayRequestResponse>> Join(Guid id, CancellationToken cancellationToken) =>
        await playRequestService.JoinAsync(id, User.GetUserId(), cancellationToken);

    /// <summary>Accepts one pending participant. Host only.</summary>
    [HttpPost("{id:guid}/participants/{participantId:guid}/accept")]
    [ProducesResponseType<PlayRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayRequestResponse>> Accept(
        Guid id,
        Guid participantId,
        CancellationToken cancellationToken) =>
        await playRequestService.AcceptAsync(id, participantId, User.GetUserId(), cancellationToken);

    /// <summary>Rejects one pending participant. Host only.</summary>
    [HttpPost("{id:guid}/participants/{participantId:guid}/reject")]
    [ProducesResponseType<PlayRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayRequestResponse>> Reject(
        Guid id,
        Guid participantId,
        CancellationToken cancellationToken) =>
        await playRequestService.RejectAsync(id, participantId, User.GetUserId(), cancellationToken);

    /// <summary>Withdraws the caller's pending or accepted participation.</summary>
    [HttpPost("{id:guid}/withdraw")]
    [ProducesResponseType<PlayRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayRequestResponse>> Withdraw(Guid id, CancellationToken cancellationToken) =>
        await playRequestService.WithdrawAsync(id, User.GetUserId(), cancellationToken);

    /// <summary>Cancels an open or full request. Host only.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<PlayRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayRequestResponse>> Cancel(Guid id, CancellationToken cancellationToken) =>
        await playRequestService.CancelAsync(id, User.GetUserId(), cancellationToken);

    /// <summary>Marks an open or full request completed at the host's discretion. Host only.</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<PlayRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayRequestResponse>> Complete(Guid id, CancellationToken cancellationToken) =>
        await playRequestService.CompleteAsync(id, User.GetUserId(), cancellationToken);
}
