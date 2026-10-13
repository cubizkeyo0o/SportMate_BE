using SportMate.Domain.Enums;

namespace SportMate.Application.PlayRequests;

public interface IPlayRequestService
{
    Task<PlayRequestPageResponse> DiscoverAsync(Guid userId, PlayRequestQuery query, CancellationToken cancellationToken);

    Task<PlayRequestResponse> GetAsync(Guid playRequestId, Guid userId, CancellationToken cancellationToken);

    Task<PlayRequestPageResponse> ListMineAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);

    Task<PlayRequestPageResponse> ListMyParticipationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PlayRequestResponse> CreateAsync(
        Guid userId,
        CreatePlayRequestRequest request,
        CancellationToken cancellationToken);

    Task<PlayRequestResponse> UpdateAsync(
        Guid playRequestId,
        Guid userId,
        UpdatePlayRequestRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayRequestParticipantResponse>> ListParticipantsAsync(
        Guid playRequestId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<PlayRequestResponse> JoinAsync(Guid playRequestId, Guid userId, CancellationToken cancellationToken);

    Task<PlayRequestResponse> AcceptAsync(
        Guid playRequestId,
        Guid participantId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<PlayRequestResponse> RejectAsync(
        Guid playRequestId,
        Guid participantId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<PlayRequestResponse> WithdrawAsync(Guid playRequestId, Guid userId, CancellationToken cancellationToken);

    Task<PlayRequestResponse> CancelAsync(Guid playRequestId, Guid userId, CancellationToken cancellationToken);

    Task<PlayRequestResponse> CompleteAsync(Guid playRequestId, Guid userId, CancellationToken cancellationToken);
}
