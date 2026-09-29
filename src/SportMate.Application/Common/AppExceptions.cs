namespace SportMate.Application.Common;

/// <summary>
/// Request input failed validation. Keys are the snake_case request field names.
/// </summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>
/// The caller could not be authenticated (bad credentials, invalid token). Maps to 401.
/// </summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);

/// <summary>
/// The caller is authenticated but not allowed to proceed. Maps to 403.
/// </summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>
/// The request conflicts with existing state. Maps to 409.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
