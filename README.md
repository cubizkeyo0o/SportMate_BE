# SportMate Backend

ASP.NET Core Web API foundation targeting .NET 10 LTS, with Clean Architecture projects, EF Core/Pomelo for MySQL, development Swagger, JWT authentication with rotating refresh tokens, and user profile endpoints.

## Requirements

- .NET 10 SDK
- MySQL 8 installed and running locally
- EF Core CLI for migrations: `dotnet tool install --global dotnet-ef --version 9.0.0`

No Docker is used. The API does not connect to the database at startup; the configured connection is used when a DbContext operation runs. It does refuse to start if `Jwt:SigningKey` is missing or shorter than 32 bytes.

## MySQL configuration

Follow [database/README.md](database/README.md) to create a local database. Set the connection string as an environment variable rather than storing a password in JSON:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=localhost;Port=3306;Database=sportmate;User ID=sportmate_app;Password=<your-local-password>;'
```

## Configuration

| Setting | Environment variable | Default | Notes |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | localhost, no password | Put the password only in the environment variable. |
| `Jwt:SigningKey` | `Jwt__SigningKey` | none (required) | HMAC-SHA256 key, at least 32 bytes. Never commit it. |
| `Jwt:Issuer` | `Jwt__Issuer` | `SportMate` | |
| `Jwt:Audience` | `Jwt__Audience` | `SportMate` | |
| `Jwt:AccessTokenMinutes` | `Jwt__AccessTokenMinutes` | `15` | 1-1440 |
| `Jwt:RefreshTokenDays` | `Jwt__RefreshTokenDays` | `30` | 1-365 |

Generate a local signing key (PowerShell):

```powershell
$env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

## Build and run

From the repository root:

```powershell
dotnet restore backend/SportMate.sln
dotnet build backend/SportMate.sln
dotnet test backend/SportMate.sln
dotnet run --project backend/src/SportMate.API/SportMate.API.csproj
```

The launch profile binds to `http://localhost:5000`. To expose the development API to a phone over the local network, run:

```powershell
dotnet run --project backend/src/SportMate.API/SportMate.API.csproj --no-launch-profile -- --urls http://0.0.0.0:5000
```

Allow inbound TCP port 5000 in the development computer's firewall when using LAN access.

## API

JSON uses snake_case property names. Errors are RFC 7807 problem details; validation errors (400) list messages per field under `errors`, keyed by the snake_case field name.

| Method | Path | Auth | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health` | public | 200 `{"status":"ok"}` | |
| POST | `/api/v1/auth/register` | public | 201 user | 400 validation, 409 email already registered |
| POST | `/api/v1/auth/login` | public | 200 tokens + user | 400, 401 invalid email or password, 403 account not active |
| POST | `/api/v1/auth/refresh` | public | 200 new tokens + user | 400, 401 invalid/expired/revoked token |
| POST | `/api/v1/auth/logout` | Bearer | 204 | 400, 401 |
| GET | `/api/v1/users/me` | Bearer | 200 user | 401 |
| PUT | `/api/v1/users/me` | Bearer | 200 user | 400, 401 |

### Register

```json
POST /api/v1/auth/register
{ "email": "player@example.com", "password": "Str0ngPassw0rd", "display_name": "Minh Anh", "phone": "+84 912 345 678" }
```

- Email is trimmed and lower-cased; uniqueness is enforced by a unique index on `users.email`.
- Password: 8-128 characters with at least one letter and one digit. Stored only as an ASP.NET Core Identity PBKDF2 hash.
- `display_name` is required (max 100). `phone` is optional: 7-15 digits, optional leading `+`, spaces, `-`, `.`, `()` allowed, max 20 characters.
- New accounts get the `User` role; the client cannot choose a role.

The user object returned by register and the user endpoints:

```json
{ "id": "…", "email": "player@example.com", "display_name": "Minh Anh", "phone": "+84 912 345 678", "status": "active", "role": "User" }
```

`role` is one of `User`, `CourtOwner`, `Admin` and always reflects the current database value.

### Login

```json
POST /api/v1/auth/login
{ "email": "player@example.com", "password": "Str0ngPassw0rd" }
```

Response:

```json
{ "access_token": "<jwt>", "refresh_token": "<opaque>", "token_type": "Bearer", "expires_in": 900, "user": { … } }
```

Wrong password and unknown email return the same 401 response. A suspended account gets 403, but only after the correct password is given.

### Refresh

```json
POST /api/v1/auth/refresh
{ "refresh_token": "<opaque>" }
```

Returns a new access token and a new refresh token (same shape as login). The presented refresh token is revoked; each refresh token works once. Presenting a refresh token that was already revoked (a sign it was copied) revokes every active refresh token of that user, forcing a new login.

### Logout

```json
POST /api/v1/auth/logout
Authorization: Bearer <access_token>
{ "refresh_token": "<opaque>" }
```

Revokes the given refresh token if it belongs to the caller. Always returns 204 for a well-formed request. The access token stays valid until it expires (`Jwt:AccessTokenMinutes`), so clients should discard it.

### Current user and profile

- `GET /api/v1/users/me` returns the user object above.
- `PUT /api/v1/users/me` with `{ "display_name": "New Name", "phone": "0912345678" }` replaces the editable fields; omitting `phone` clears it. Any other fields (`email`, `status`, `id`, password) are ignored. Email change is not supported yet.

### Tokens

- Access token: HS256 JWT with `sub` (user id), `email`, `role`, `jti`, `iat`, `nbf`, `exp`, `iss`, `aud`. Validated with 30 seconds clock skew.
- On every request with a valid access token, the API loads the user's current status and role by primary key. Tokens of suspended or deleted users are rejected with 401, and the `role` claim is replaced with the current role, so suspensions and role changes take effect immediately rather than when the token expires.
- Refresh token: 32 cryptographically random bytes, base64url-encoded. Only its SHA-256 hex hash is stored in `refresh_tokens.token_hash`.

### Roles and authorization

Each user has exactly one application role, stored in `users.role` (`1` = `User`, `2` = `CourtOwner`, `3` = `Admin`).

- Normal user functionality uses plain `[Authorize]` and is available to every role; CourtOwner and Admin are still normal users.
- Role-specific endpoints use `[Authorize(Roles = Roles.Admin)]` (constants in `SportMate.Application.Auth.Roles`). Missing or invalid token: 401. Authenticated with another role: 403.
- There is no role-management API. Promote an existing, registered account directly in MySQL (development or admin operation):

```sql
-- Admin
UPDATE users SET role = 3, updated_at_utc = UTC_TIMESTAMP(6) WHERE email = 'admin@example.com';
-- CourtOwner
UPDATE users SET role = 2, updated_at_utc = UTC_TIMESTAMP(6) WHERE email = 'owner@example.com';
-- Back to User
UPDATE users SET role = 1, updated_at_utc = UTC_TIMESTAMP(6) WHERE email = 'someone@example.com';
```

The change applies to the user's next request; no re-login is needed. Being a CourtOwner does not by itself grant access to any court: court-specific ownership is checked per court.

### Swagger

- Swagger UI: `http://localhost:5000/swagger` in the Development environment. Log in, click **Authorize**, and paste the `access_token` (without the `Bearer ` prefix).
- OpenAPI JSON: `http://localhost:5000/swagger/v1/swagger.json`.

## EF Core migrations

The initial migration `InitialCreate` lives in `src/SportMate.Infrastructure/Persistence/Migrations`. Apply it to the configured database from the repository root:

```powershell
dotnet ef database update --project backend/src/SportMate.Infrastructure --startup-project backend/src/SportMate.API
```

Add later migrations with:

```powershell
dotnet ef migrations add <Name> --project backend/src/SportMate.Infrastructure --startup-project backend/src/SportMate.API --output-dir Persistence/Migrations
```

MySQL 8.0.16 or later is required (CHECK constraints are enforced from that version).

## Project layout

- `src/SportMate.Domain`: entities, enums and domain invariants.
- `src/SportMate.Application`: use cases (`AuthService`, `UserService`), request/response DTOs, validation, and the interfaces Infrastructure implements.
- `src/SportMate.Infrastructure`: `SportMateDbContext`, entity configurations, migrations, MySQL registration, password hashing and JWT creation.
- `src/SportMate.API`: controllers, JWT validation, Swagger, and exception-to-problem-details middleware.
- `tests/`: unit and API integration test projects.

## Tests

```powershell
dotnet test backend/SportMate.sln
```

- `tests/SportMate.UnitTests`: domain rules and input validation/token helpers.
- `tests/SportMate.IntegrationTests`: the full HTTP pipeline via `WebApplicationFactory`, against a SQLite in-memory database created from the EF model (unique indexes, foreign keys and check constraints are enforced). No MySQL or secrets are needed; the tests use a test-only signing key.

Run only the authentication and user tests:

```powershell
dotnet test backend/tests/SportMate.IntegrationTests --filter "FullyQualifiedName~SportMate.IntegrationTests.Auth|FullyQualifiedName~SportMate.IntegrationTests.Users"
```