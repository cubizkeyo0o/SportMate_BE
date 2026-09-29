namespace SportMate.Domain.Enums;

/// <summary>
/// The single application role of a user. Values are persisted in users.role and must not change.
/// CourtOwner and Admin keep all normal user functionality.
/// </summary>
public enum UserRole : byte
{
    User = 1,
    CourtOwner = 2,
    Admin = 3
}
