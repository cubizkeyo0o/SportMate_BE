using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SportMate.API.Authentication;
using SportMate.Application.Auth;
using SportMate.Application.Bookings;

namespace SportMate.API.Controllers;

/// <summary>
/// Bookings. Any authenticated user books for themselves; visibility and lifecycle permissions are checked by
/// <see cref="BookingService"/>. Bookings the caller may not see are reported as 404.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/bookings")]
[Produces("application/json")]
public sealed class BookingsController(BookingService bookingService) : ControllerBase
{
    /// <summary>
    /// Creates a Pending booking that holds the court until <c>hold_expires_at_utc</c>.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await bookingService.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = booking.Id }, booking);
    }

    [HttpGet("me")]
    [ProducesResponseType<IReadOnlyList<BookingResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> ListMine(CancellationToken cancellationToken) =>
        Ok(await bookingService.ListMineAsync(User.GetUserId(), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        await bookingService.GetAsync(id, User.GetUserId(), User.GetUserRole(), cancellationToken);

    /// <summary>
    /// Mock payment (no real payment is taken): Pending → Confirmed while the hold is running. Booker only.
    /// </summary>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Pay(Guid id, CancellationToken cancellationToken) =>
        await bookingService.PayAsync(id, User.GetUserId(), User.GetUserRole(), cancellationToken);

    /// <summary>
    /// Cancels a Pending or Confirmed booking before it starts. Booker, the court's owner or Admin.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Cancel(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelBookingRequest? request,
        CancellationToken cancellationToken) =>
        await bookingService.CancelAsync(id, User.GetUserId(), User.GetUserRole(), request, cancellationToken);

    /// <summary>
    /// Confirmed → Completed after the booking ends. The court's owner or Admin.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Complete(Guid id, CancellationToken cancellationToken) =>
        await bookingService.CompleteAsync(id, User.GetUserId(), User.GetUserRole(), cancellationToken);

    /// <summary>
    /// Confirmed → NoShow after the booking ends. The court's owner or Admin.
    /// </summary>
    [HttpPost("{id:guid}/no-show")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> MarkNoShow(Guid id, CancellationToken cancellationToken) =>
        await bookingService.MarkNoShowAsync(id, User.GetUserId(), User.GetUserRole(), cancellationToken);

    /// <summary>
    /// All bookings of a court. Admin, or the court's assigned CourtOwner.
    /// </summary>
    [Authorize(Roles = Roles.Admin + "," + Roles.CourtOwner)]
    [HttpGet("/api/v1/courts/{courtId:guid}/bookings")]
    [ProducesResponseType<IReadOnlyList<BookingResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> ListForCourt(
        Guid courtId,
        CancellationToken cancellationToken) =>
        Ok(await bookingService.ListForCourtAsync(courtId, User.GetUserId(), User.GetUserRole(), cancellationToken));
}
