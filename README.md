# SportMate Backend

ASP.NET Core Web API foundation targeting .NET 10 LTS, with Clean Architecture projects, EF Core/Pomelo for MySQL, development Swagger, JWT authentication with rotating refresh tokens, user profile endpoints, court management, and sports with user sport profiles.

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
| GET | `/api/v1/sports` | Bearer | 200 active sports | 401 |
| GET | `/api/v1/users/me/sports` | Bearer | 200 caller's sport profiles | 401 |
| PUT | `/api/v1/users/me/sports/{sportId}` | Bearer | 200 profile (created or updated) | 400, 401, 404 unknown sport, 409 concurrent create |
| DELETE | `/api/v1/users/me/sports/{sportId}` | Bearer | 204 (also when nothing to delete) | 401 |
| POST | `/api/v1/courts` | Bearer, Admin | 201 court + `Location` | 400, 401, 403 |
| GET | `/api/v1/courts/{id}` | Bearer, Admin or assigned CourtOwner | 200 court | 401, 403, 404 |
| PUT | `/api/v1/courts/{id}` | Bearer, Admin or assigned CourtOwner | 200 court | 400, 401, 403, 404 |

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

### Sports and user sport profiles

Sports are a fixed seeded catalog (rules in [docs/BUSINESS_RULES.md](../docs/BUSINESS_RULES.md) section 3); there is no create/update/delete API. All endpoints below need only a valid access token, so User, CourtOwner and Admin use them alike, and always for the caller's own profiles (the user id comes from the token, never the request).

| Sport | Slug | Id |
|---|---|---|
| Tennis | `tennis` | `0b1e7a6e-5c2a-4f4e-9a01-000000000001` |
| Badminton | `badminton` | `0b1e7a6e-5c2a-4f4e-9a01-000000000002` |
| Basketball | `basketball` | `0b1e7a6e-5c2a-4f4e-9a01-000000000003` |
| Football | `football` | `0b1e7a6e-5c2a-4f4e-9a01-000000000004` |
| Pickleball | `pickleball` | `0b1e7a6e-5c2a-4f4e-9a01-000000000005` |
| Table Tennis | `table-tennis` | `0b1e7a6e-5c2a-4f4e-9a01-000000000006` |
| Volleyball | `volleyball` | `0b1e7a6e-5c2a-4f4e-9a01-000000000007` |

`GET /api/v1/sports` returns the active sports ordered by name:

```json
[ { "id": "0b1e7a6e-5c2a-4f4e-9a01-000000000002", "name": "Badminton", "slug": "badminton" }, … ]
```

Skill levels: `beginner` (1), `elementary` (2), `intermediate` (3), `advanced` (4), `expert` (5). Requests may send the name or the number; responses always use the name.

`PUT /api/v1/users/me/sports/{sportId}` creates the caller's profile for that sport or changes its skill level, and always returns 200 with the resulting profile. Repeating it is harmless.

```json
PUT /api/v1/users/me/sports/0b1e7a6e-5c2a-4f4e-9a01-000000000001
{ "skill_level": "intermediate" }
```

```json
{ "sport": { "id": "0b1e7a6e-5c2a-4f4e-9a01-000000000001", "name": "Tennis", "slug": "tennis", "is_active": true }, "skill_level": "intermediate" }
```

- `skill_level` missing or outside 1-5: 400 with an error on `skill_level`. An unknown name (for example `"pro"`): 400.
- Unknown `sportId`: 404.
- A new profile for an inactive sport: 400 with an error on `sport_id`. An existing profile whose sport was later deactivated can still be updated.
- Any `user_id`, `id` or timestamp fields in the body are ignored.
- Two simultaneous first-time PUTs for the same sport can make one fail with 409; retrying it succeeds.

`GET /api/v1/users/me/sports` returns the caller's profiles (same item shape as the PUT response) ordered by sport name, or `[]`. Profiles of deactivated sports are included with `"is_active": false`.

`DELETE /api/v1/users/me/sports/{sportId}` removes the caller's profile for that sport and returns 204. It also returns 204 when the caller has no such profile or the sport does not exist. The sport itself is never deleted.

### Courts

A court is one physical court. Court rules are in [docs/BUSINESS_RULES.md](../docs/BUSINESS_RULES.md) sections 5, 6 and 8.

| Caller | POST `/courts` | GET `/courts/{id}` | PUT `/courts/{id}` |
|---|---|---|---|
| Anonymous | 401 | 401 | 401 |
| User | 403 | 403 | 403 |
| CourtOwner, assigned owner | 403 | 200 | 200 (cannot change `owner_id`) |
| CourtOwner, another owner's court | 403 | 403 | 403 |
| Admin | 201 | 200 | 200 |

Ownership is checked against the stored `courts.owner_id` and the caller's id from the access token, using the caller's current database role. Values in the request body never grant access. A CourtOwner who is demoted to User loses access at once; the court stays as it is until an Admin reassigns it.

Create (Admin only):

```json
POST /api/v1/courts
{ "owner_id": "<CourtOwner user id>", "name": "Tennis Court 01", "description": "Outdoor hard court", "address": "1 Tran Phu, Ba Dinh", "city": "Ha Noi", "latitude": 21.028511, "longitude": 105.804817, "time_zone_id": "Asia/Ho_Chi_Minh" }
```

Update (full replacement; omitting `description` clears it, omitting `owner_id` keeps the owner):

```json
PUT /api/v1/courts/{id}
{ "name": "Tennis Court 01", "description": null, "address": "1 Tran Phu, Ba Dinh", "city": "Ha Noi", "latitude": 21.028511, "longitude": 105.804817, "time_zone_id": "Asia/Ho_Chi_Minh", "status": "inactive", "owner_id": "<optional, Admin only>" }
```

Court object returned by all three endpoints:

```json
{ "id": "…", "owner_id": "…", "name": "Tennis Court 01", "description": "Outdoor hard court", "address": "1 Tran Phu, Ba Dinh", "city": "Ha Noi", "latitude": 21.028511, "longitude": 105.804817, "time_zone_id": "Asia/Ho_Chi_Minh", "status": "active", "created_at_utc": "…", "updated_at_utc": "…" }
```

Validation (400, errors keyed by field):

- `name` required, max 150; `address` required, max 300; `city` required, max 100; `description` optional, max 1000. Text is trimmed.
- `latitude` -90..90 and `longitude` -180..180, required, at most 6 decimal places (`DECIMAL(9,6)`).
- `time_zone_id` required IANA identifier known to the runtime (for example `Asia/Ho_Chi_Minh`); Windows ids are rejected.
- `status` (update only) required, `active` or `inactive`. Both directions are allowed; setting the current status is a no-op. New courts start `active`.
- `owner_id` must be an existing, active user with the `CourtOwner` role (not `User`, not `Admin`). Required on create. On update, a different `owner_id` from a CourtOwner is 403.
- `id`, `created_at_utc` and `updated_at_utc` in the body are ignored. `updated_at_utc` changes only when a value actually changes.

There is no court listing/search and no delete; setting `status` to `inactive` takes a court offline.

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

Migrations live in `src/SportMate.Infrastructure/Persistence/Migrations`: `InitialCreate`, then `AddCourtCity` (adds `courts.city VARCHAR(100) NOT NULL`; existing rows get an empty string), then `AddSportSlugAndSeed` (adds `sports.slug` with a unique index and `sports.updated_at_utc`, backfills any existing rows, and inserts the 7 seeded sports). Apply them to the configured database from the repository root:

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
- `src/SportMate.Application`: use cases (`AuthService`, `UserService`, `CourtService`, `SportService`, `UserSportProfileService`), request/response DTOs, validation, and the interfaces Infrastructure implements.
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