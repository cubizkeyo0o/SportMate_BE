# SportMate Backend

ASP.NET Core Web API for the SportMate platform. The backend provides authentication, user profiles, sports, court discovery and management, bookings, and play requests.

## Technology

- C# and ASP.NET Core on .NET 10
- Entity Framework Core and MySQL
- JWT access tokens and rotating refresh tokens
- REST API with Swagger / OpenAPI

## Requirements

- .NET 10 SDK
- MySQL 8
- EF Core CLI (`dotnet-ef`) to apply or create migrations

## Local setup

Create a local MySQL database and account by following [database/README.md](database/README.md). Configure the connection string and JWT signing key through environment variables; do not commit local credentials:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=localhost;Port=3306;Database=sportmate;User ID=<user>;Password=<password>;'
$env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

Apply the database migrations from the `backend` directory:

```powershell
dotnet ef database update --project src/SportMate.Infrastructure --startup-project src/SportMate.API
```

## Build and run

Run these commands from the `backend` directory:

```powershell
dotnet restore SportMate.sln
dotnet build SportMate.sln
dotnet test SportMate.sln
dotnet run --project src/SportMate.API/SportMate.API.csproj
```

The API address is configured by the `SportMate.API` launch profile in `src/SportMate.API/Properties/launchSettings.json`.

## API documentation

When the API is running in Development, use Swagger UI at `/swagger`. The health check is available at `/api/v1/health`. Example requests are in [SportMate.API.http](src/SportMate.API/SportMate.API.http).

## Project structure

- `src/SportMate.Domain` — domain entities and rules
- `src/SportMate.Application` — application services, validation, and API contracts
- `src/SportMate.Infrastructure` — EF Core, MySQL, and persistence
- `src/SportMate.API` — HTTP endpoints, authentication, and API configuration
- `tests` — unit and integration tests

Business behavior and constraints are documented in [docs/BUSINESS_RULES.md](../docs/BUSINESS_RULES.md).
