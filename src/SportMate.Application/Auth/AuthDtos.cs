using SportMate.Application.Users;

namespace SportMate.Application.Auth;

// Request properties are nullable so missing fields reach application validation
// and produce the same error shape as other invalid input.

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName, string? Phone);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshTokenRequest(string? RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    UserResponse User);
