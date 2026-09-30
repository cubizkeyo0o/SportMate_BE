# SportMate Backend

ASP.NET Core Web API foundation targeting .NET 10 LTS, with Clean Architecture projects, EF Core/Pomelo for MySQL, development Swagger, JWT authentication with rotating refresh tokens, user profile endpoints, court management (including court sports and weekly operating hours), sports with user sport profiles, and court bookings with a 15-minute hold and mock payment.

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
| `Booking:HoldDurationMinutes` | `Booking__HoldDurationMinutes` | `15` | 1-1440. How long a new (Pending) booking holds the court. |

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
| GET | `/api/v1/discovery/courts` | Bearer | 200 active courts | 401 |
| GET | `/api/v1/discovery/courts/{courtId}` | Bearer | 200 court | 401, 404 inactive or unknown |
| GET | `/api/v1/discovery/courts/{courtId}/sports` | Bearer | 200 offered sports | 401, 404 inactive or unknown |
| GET | `/api/v1/discovery/courts/{courtId}/operating-hours` | Bearer | 200 weekly periods | 401, 404 inactive or unknown |
| GET | `/api/v1/courts/{courtId}/sports` | Bearer, Admin or assigned CourtOwner | 200 court sports | 401, 403, 404 |
| POST | `/api/v1/courts/{courtId}/sports` | Bearer, Admin or assigned CourtOwner | 201 court sport + `Location` | 400, 401, 403, 404, 409 already added |
| PUT | `/api/v1/courts/{courtId}/sports/{sportId}` | Bearer, Admin or assigned CourtOwner | 200 court sport | 400, 401, 403, 404 |
| DELETE | `/api/v1/courts/{courtId}/sports/{sportId}` | Bearer, Admin or assigned CourtOwner | 204 (deactivates) | 401, 403, 404 court |
| POST | `/api/v1/bookings` | Bearer | 201 booking (Pending) + `Location` | 400, 401, 404 court sport, 409 court already booked |
| GET | `/api/v1/bookings/me` | Bearer | 200 caller's bookings | 401 |
| GET | `/api/v1/bookings/{id}` | Bearer, booker / court's owner / Admin | 200 booking | 401, 404 |
| POST | `/api/v1/bookings/{id}/pay` | Bearer, booker | 200 booking (Confirmed) | 400, 401, 403, 404 |
| POST | `/api/v1/bookings/{id}/cancel` | Bearer, booker / court's owner / Admin | 200 booking (Cancelled) | 400, 401, 404 |
| POST | `/api/v1/bookings/{id}/complete` | Bearer, court's owner / Admin | 200 booking (Completed) | 400, 401, 403, 404 |
| POST | `/api/v1/bookings/{id}/no-show` | Bearer, court's owner / Admin | 200 booking (NoShow) | 400, 401, 403, 404 |
| GET | `/api/v1/courts/{courtId}/bookings` | Bearer, Admin or assigned CourtOwner | 200 court's bookings | 401, 403, 404 |
| GET | `/api/v1/courts/{courtId}/operating-hours` | Bearer, Admin or assigned CourtOwner | 200 periods | 401, 403, 404 |
| POST | `/api/v1/courts/{courtId}/operating-hours` | Bearer, Admin or assigned CourtOwner | 201 period + `Location` | 400, 401, 403, 404, 409 concurrent change |
| PUT | `/api/v1/courts/{courtId}/operating-hours/{id}` | Bearer, Admin or assigned CourtOwner | 200 period | 400, 401, 403, 404, 409 concurrent change |
| DELETE | `/api/v1/courts/{courtId}/operating-hours/{id}` | Bearer, Admin or assigned CourtOwner | 204 | 401, 403, 404 |

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

### Court discovery

Read-only browsing for **every** authenticated user (User, CourtOwner, Admin), separate from the management endpoints above, which keep their own permissions and response shapes. Only `Active` courts exist here: an inactive or unknown court is 404 (the same response for both), even for its owner or an Admin. No filters, search or pagination yet.

`GET /api/v1/discovery/courts` lists active courts ordered by name (`[]` when there are none); `GET /api/v1/discovery/courts/{courtId}` returns one:

```json
{ "id": "…", "name": "Tennis Court 01", "description": "Outdoor hard court", "address": "1 Tran Phu, Ba Dinh", "city": "Ha Noi", "latitude": 21.028511, "longitude": 105.804817, "time_zone_id": "Asia/Ho_Chi_Minh", "status": "active" }
```

No `owner_id` and no timestamps. A court without sports or hours is still listed.

`GET /api/v1/discovery/courts/{courtId}/sports` lists what can be booked there, ordered by sport name: only court sports that are active and whose sport is active. `is_active` is therefore always `true`.

```json
[ { "court_sport_id": "…", "sport": { "id": "…", "name": "Tennis", "slug": "tennis" }, "price_per_hour": 150000.00, "currency": "VND", "is_active": true } ]
```

`court_sport_id` is the court-sport link to send as `court_sport_id` in `POST /api/v1/bookings`.

`GET /api/v1/discovery/courts/{courtId}/operating-hours` lists the weekly periods exactly as configured, in the court's local time (not converted to UTC), ordered by day then opening time. `day_of_week` is `0` = Sunday … `6` = Saturday.

```json
[ { "day_of_week": 1, "opens_at": "06:00", "closes_at": "12:00" } ]
```

### Court sports

The sports a court offers, each with its own hourly price (rules in [docs/BUSINESS_RULES.md](../docs/BUSINESS_RULES.md) section 9). Same permissions as the court itself: Admin for any court, the assigned CourtOwner for their own courts, 403 for everyone else, checked against the caller's current database role. A relationship is addressed by the court and the sport id (see the sport table above).

```json
POST /api/v1/courts/{courtId}/sports
{ "sport_id": "0b1e7a6e-5c2a-4f4e-9a01-000000000001", "price_per_hour": 150000, "currency": "VND" }
```

```json
PUT /api/v1/courts/{courtId}/sports/{sportId}
{ "price_per_hour": 120000, "currency": "VND", "is_active": true }
```

Court sport object (GET returns a list of these, ordered by sport name, including inactive ones):

```json
{ "sport": { "id": "…", "name": "Tennis", "slug": "tennis", "is_active": true }, "price_per_hour": 150000.00, "currency": "VND", "is_active": true, "created_at_utc": "…", "updated_at_utc": "…" }
```

`sport.is_active` is the sport's own state; `is_active` is whether this court offers it.

- `price_per_hour`: required, `>= 0` (zero allowed), at most 2 decimal places and 99,999,999.99 (`DECIMAL(10,2)`). It is the price for one hour; booking-time price calculation is not implemented yet.
- `currency`: required, exactly 3 letters, stored uppercase.
- POST: unknown sport 404; inactive sport 400 on `sport_id`; the court already has that sport (active or not) 409, so reactivate it with PUT instead. New relationships start active.
- PUT: replaces all three fields. `is_active: true` on an inactive relationship whose sport is inactive is 400 on `is_active`; price changes and deactivation are always allowed. A court without that sport is 404. Unchanged values leave `updated_at_utc` as it is.
- DELETE deactivates the relationship (`is_active = false`) and keeps the row. It returns 204 when already inactive and when the court does not have that sport; only an unknown court is 404.
- Timestamps and any other body fields (`court_id`, `owner_id`, …) are ignored; the court always comes from the path.

### Court operating hours

Recurring weekly opening periods in the court's local time (rules in [docs/BUSINESS_RULES.md](../docs/BUSINESS_RULES.md) section 10). Same permissions as court sports.

```json
POST /api/v1/courts/{courtId}/operating-hours
{ "day_of_week": 1, "opens_at": "06:00", "closes_at": "12:00" }
```

Period object (GET returns all of the court's periods ordered by day, then opening time):

```json
{ "id": "…", "day_of_week": 1, "opens_at": "06:00", "closes_at": "12:00", "created_at_utc": "…", "updated_at_utc": "…" }
```

- `day_of_week`: required, `0` = Sunday … `6` = Saturday.
- `opens_at` / `closes_at`: required, exactly `HH:mm` from `00:00` to `23:59` (seconds are rejected). `opens_at` must be earlier than `closes_at`, so a period cannot cross midnight, and `24:00` is not supported.
- A period that overlaps another period of the same court on the same day is 400 on `opens_at`. Touching periods (`06:00–12:00` and `12:00–14:00`) are fine. A unique index on `(court_id, day_of_week, opens_at)` backs this up; losing a race against a concurrent change returns 409.
- PUT replaces the period (it may move to another day); DELETE removes it permanently. A period id that does not belong to the court in the path is 404.
- Timestamps are set by the server.

### Bookings

Rules in [docs/BUSINESS_RULES.md](../docs/BUSINESS_RULES.md) section 11. Any authenticated user books for themselves.

```json
POST /api/v1/bookings
{ "court_sport_id": "<court sport id>", "starts_at": "2026-10-06T10:00:00+07:00", "ends_at": "2026-10-06T11:30:00+07:00" }
```

Booking object returned by every booking endpoint:

```json
{
  "id": "…", "user_id": "…", "court_id": "…", "court_sport_id": "…",
  "sport": { "id": "…", "name": "Tennis", "slug": "tennis", "is_active": true },
  "starts_at_utc": "2026-10-06T03:00:00Z", "ends_at_utc": "2026-10-06T04:30:00Z",
  "time_zone_id": "Asia/Ho_Chi_Minh", "local_date": "2026-10-06", "local_start_time": "10:00", "local_end_time": "11:30",
  "status": "pending", "total_price": 150000.00, "currency": "VND",
  "hold_expires_at_utc": "…", "cancellation_reason": null, "cancelled_at_utc": null,
  "created_at_utc": "…", "updated_at_utc": "…"
}
```

`status` is `pending`, `confirmed`, `cancelled`, `completed` or `no_show`. `hold_expires_at_utc` (`created_at_utc` + hold duration) is present only while Pending. `local_*` are the court's wall-clock times.

- **Hold:** a new booking is `pending` and blocks the court for `Booking:HoldDurationMinutes` (15). An expired hold is cancelled with reason `Hold expired` the next time bookings of that court or user are read or changed; there is no background job.
- **Pay (mock):** `POST /bookings/{id}/pay` has no body and takes no money; it only turns a Pending booking with a running hold **whose start time has not been reached** into `confirmed`. Booker only (Admin/CourtOwner get 403). 400 on `status` for an expired hold (booking cancelled, reason `Hold expired`), a Pending booking at or after its start (booking cancelled, reason `Booking start time has passed`), or a booking that is not Pending.
- **Cancel:** `POST /bookings/{id}/cancel` with optional `{ "reason": "…" }` (max 500). Pending or Confirmed, before the start. 400 on `status` otherwise.
- **Complete / no-show:** `POST /bookings/{id}/complete` and `/no-show`, no body. Confirmed bookings after their end, by the court's owner or an Admin (the booker gets 403).
- **Visibility:** a booking someone may not see is 404. `GET /bookings/me` and `GET /courts/{courtId}/bookings` are ordered by start time.

Create validation (400 unless noted):

- `court_sport_id` required; unknown: 404. The court, price, currency and status come from the server; any other body fields are ignored.
- `starts_at` / `ends_at`: ISO-8601 with `Z` or `±hh:mm`; no seconds or fractions. Start before end, start in the future.
- Court, court sport and sport must be active (error on `court_sport_id`).
- In the court's local time zone: one local day (no crossing midnight), entirely inside the union of that day's operating periods (touching periods are continuous, gaps are not), not at an ambiguous local time and not across a daylight-saving change.
- `total_price = price_per_hour × minutes / 60`, rounded to 2 decimals away from zero; it must fit `DECIMAL(10,2)`.
- Overlap with a Confirmed booking or a running hold on the **same physical court** (any sport): 409.

Concurrency: every booking write runs in a `READ COMMITTED` transaction that first locks the booking's court row with `SELECT … FOR UPDATE` and re-reads what it changes: creation (re-reads the court, court sport, sport and hours, expires stale holds, checks overlaps), pay, cancel, complete, no-show, and the hold expiry done before reads (which only takes locks, in a fixed court order, when a read-only check finds expired holds). So exactly one transition out of Pending can win for a booking. The conflict query uses the existing `(court_id, starts_at_utc, ends_at_utc)` index.

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

Migrations live in `src/SportMate.Infrastructure/Persistence/Migrations`: `InitialCreate`, then `AddCourtCity` (adds `courts.city VARCHAR(100) NOT NULL`; existing rows get an empty string), then `AddSportSlugAndSeed` (adds `sports.slug` with a unique index and `sports.updated_at_utc`, backfills any existing rows, and inserts the 7 seeded sports), then `CourtSportsAndOperatingHours` (relaxes `ck_court_sports_price_per_hour` to `>= 0`, adds `court_operating_hours.created_at_utc`/`updated_at_utc` backfilled with the migration time, and makes the `(court_id, day_of_week, opens_at)` index unique). Apply them to the configured database from the repository root:

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
- `src/SportMate.Application`: use cases (`AuthService`, `UserService`, `CourtService`, `CourtSportService`, `CourtOperatingHourService`, `CourtDiscoveryService`, `SportService`, `UserSportProfileService`, `BookingService`), request/response DTOs, validation, and the interfaces Infrastructure implements.
- `src/SportMate.Infrastructure`: `SportMateDbContext`, entity configurations, migrations, MySQL registration, password hashing and JWT creation.
- `src/SportMate.API`: controllers, JWT validation, Swagger, and exception-to-problem-details middleware.
- `tests/`: unit and API integration test projects.

## Tests

```powershell
dotnet test backend/SportMate.sln
```

- `tests/SportMate.UnitTests`: domain rules and input validation/token helpers.
- `tests/SportMate.IntegrationTests`: the full HTTP pipeline via `WebApplicationFactory`, against a SQLite in-memory database created from the EF model (unique indexes, foreign keys and check constraints are enforced). No MySQL or secrets are needed; the tests use a test-only signing key.
- `Bookings/MySqlBookingConcurrencyTests`: booking concurrency against **real MySQL**, because SQLite has no row locks. They run only when `SPORTMATE_TEST_MYSQL` holds a MySQL connection string for an account allowed to `CREATE`/`DROP DATABASE`; otherwise they are reported as skipped. Each run creates a throwaway `sm_test_<random>` database with the real migrations and drops it afterwards. Keep the connection string in the environment only:

```powershell
$env:SPORTMATE_TEST_MYSQL = 'Server=localhost;Port=3306;User ID=<user>;Password=<password>;'
dotnet test backend/tests/SportMate.IntegrationTests --filter "FullyQualifiedName~MySqlBookingConcurrencyTests"
```

Run only the authentication and user tests:

```powershell
dotnet test backend/tests/SportMate.IntegrationTests --filter "FullyQualifiedName~SportMate.IntegrationTests.Auth|FullyQualifiedName~SportMate.IntegrationTests.Users"
```